"""
Detection result schema returned by the inference API.
Matches the C# DetectionResultDto record.
"""

from pydantic import BaseModel, Field


class DetectionResult(BaseModel):
    """Single detection bounding box with label and confidence score."""

    label: str = Field(..., description="Seafood class label (e.g. 'salmon', 'tuna')")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Detection confidence [0, 1]")
    x: float = Field(..., description="Bounding box left edge (normalised 0-1)")
    y: float = Field(..., description="Bounding box top edge (normalised 0-1)")
    width: float = Field(..., description="Bounding box width (normalised 0-1)")
    height: float = Field(..., description="Bounding box height (normalised 0-1)")
