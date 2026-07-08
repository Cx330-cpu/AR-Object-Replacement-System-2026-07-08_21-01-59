"""M1 YOLO detection module."""

from .service import DetectionService
from .types import BoundingBox, DetectionResult

__all__ = ["BoundingBox", "DetectionResult", "DetectionService"]

