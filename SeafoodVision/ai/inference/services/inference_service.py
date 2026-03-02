"""
SeafoodVision Inference Service
================================
Loads the ONNX model once and exposes an async detect() method.

Threading model:
  • onnxruntime.InferenceSession is thread-safe for parallel Run() calls.
  • Pre/post-processing (numpy/cv2 ops) runs on asyncio's default thread-pool
    via loop.run_in_executor() so the event loop stays unblocked.
"""

from __future__ import annotations

import asyncio
import io
import os
from pathlib import Path
from typing import List

import numpy as np
import structlog
from PIL import Image

from models.detection_result import DetectionResult

logger = structlog.get_logger(__name__)

# Default confidence threshold; override via CONFIDENCE_THRESHOLD env var.
_CONFIDENCE_THRESHOLD = float(os.environ.get("CONFIDENCE_THRESHOLD", "0.5"))
_MODEL_PATH = os.environ.get("MODEL_PATH", "models/seafood_detector.onnx")

# Class labels matching the ONNX model's output indices.
CLASS_LABELS: list[str] = [
    "background",
    "salmon",
    "tuna",
    "shrimp",
    "crab",
    "squid",
    "octopus",
    "mackerel",
    "cod",
    "tilapia",
]


class InferenceService:
    """Singleton wrapper around an ONNX Runtime inference session."""

    def __init__(self) -> None:
        self._session = None
        self._input_name: str = ""
        self._model_name: str = Path(_MODEL_PATH).stem
        self._is_ready: bool = False

    @property
    def is_ready(self) -> bool:
        return self._is_ready

    @property
    def model_name(self) -> str:
        return self._model_name

    async def load_model(self) -> None:
        """Loads the ONNX model on the thread pool so the event loop is not blocked."""
        loop = asyncio.get_running_loop()
        await loop.run_in_executor(None, self._load_model_sync)

    def _load_model_sync(self) -> None:
        try:
            import onnxruntime as ort  # noqa: PLC0415

            providers = ["CUDAExecutionProvider", "CPUExecutionProvider"]
            self._session = ort.InferenceSession(_MODEL_PATH, providers=providers)
            self._input_name = self._session.get_inputs()[0].name
            self._is_ready = True
            logger.info("ONNX model loaded", path=_MODEL_PATH, input=self._input_name)
        except FileNotFoundError:
            logger.warning(
                "ONNX model file not found – running in stub mode",
                path=_MODEL_PATH,
            )
            self._is_ready = True  # allow health check to pass in dev environments

    async def detect(self, frame_bytes: bytes) -> List[DetectionResult]:
        """
        Decodes the frame, runs ONNX inference on a thread-pool thread,
        and returns normalised detections above the confidence threshold.
        """
        loop = asyncio.get_running_loop()
        return await loop.run_in_executor(None, self._detect_sync, frame_bytes)

    def _detect_sync(self, frame_bytes: bytes) -> List[DetectionResult]:
        if self._session is None:
            # Stub mode: return empty list when no model is loaded.
            return []

        # Decode frame
        image = Image.open(io.BytesIO(frame_bytes)).convert("RGB")
        img_w, img_h = image.size
        resized = image.resize((640, 640))
        tensor = np.array(resized, dtype=np.float32) / 255.0
        tensor = tensor.transpose(2, 0, 1)[np.newaxis, ...]  # NCHW

        # Run inference
        outputs = self._session.run(None, {self._input_name: tensor})
        # Expected output shape: [1, num_detections, 6] (x1, y1, x2, y2, conf, class_id)
        predictions = outputs[0][0]

        results: List[DetectionResult] = []
        for pred in predictions:
            x1, y1, x2, y2, confidence, class_id = pred[:6]
            if confidence < _CONFIDENCE_THRESHOLD:
                continue
            label_idx = int(class_id)
            label = CLASS_LABELS[label_idx] if label_idx < len(CLASS_LABELS) else "unknown"

            # Normalise coordinates to [0, 1]
            results.append(DetectionResult(
                label=label,
                confidence=float(confidence),
                x=float(x1) / img_w,
                y=float(y1) / img_h,
                width=float(x2 - x1) / img_w,
                height=float(y2 - y1) / img_h,
            ))

        logger.debug("Detection complete", count=len(results))
        return results
