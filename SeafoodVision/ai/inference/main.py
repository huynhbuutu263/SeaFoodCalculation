"""
SeafoodVision – Python FastAPI Inference Service
================================================
Provides real-time object detection for seafood items using an ONNX model.

Endpoints:
  POST /detect   – multipart frame upload → list of DetectionResult
  GET  /health   – liveness probe

Threading model:
  • FastAPI runs on Uvicorn with a thread-pool executor for CPU-bound ONNX inference.
  • The InferenceService is a singleton loaded once at startup.
  • Each request deserializes the frame, runs inference on a thread pool thread,
    and returns JSON immediately. No shared mutable state is accessed concurrently.
"""

from contextlib import asynccontextmanager

import structlog
import uvicorn
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.responses import JSONResponse

from services.inference_service import InferenceService

logger = structlog.get_logger(__name__)

_inference_service: InferenceService | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global _inference_service
    logger.info("Loading ONNX model…")
    _inference_service = InferenceService()
    await _inference_service.load_model()
    logger.info("Model loaded. Service ready.")
    yield
    logger.info("Shutting down inference service.")


app = FastAPI(
    title="SeafoodVision Inference API",
    version="1.0.0",
    description="Real-time seafood detection using ONNX Runtime.",
    lifespan=lifespan,
)


@app.get("/health", summary="Liveness probe")
async def health() -> dict:
    if _inference_service is None or not _inference_service.is_ready:
        raise HTTPException(status_code=503, detail="Model not loaded")
    return {"status": "ok", "model": _inference_service.model_name}


@app.post("/detect", summary="Run object detection on a single frame")
async def detect(file: UploadFile = File(..., description="JPEG/PNG frame bytes")):
    if _inference_service is None or not _inference_service.is_ready:
        raise HTTPException(status_code=503, detail="Model not loaded")

    frame_bytes = await file.read()
    if not frame_bytes:
        raise HTTPException(status_code=400, detail="Empty frame data")

    try:
        results = await _inference_service.detect(frame_bytes)
        return JSONResponse(content=[r.model_dump() for r in results])
    except Exception as exc:
        logger.error("Detection failed", error=str(exc))
        raise HTTPException(status_code=500, detail="Inference error") from exc


if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, workers=1, log_level="info")
