from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class BoundingBox:
    x1: float
    y1: float
    x2: float
    y2: float

    @property
    def width(self) -> float:
        return max(0.0, self.x2 - self.x1)

    @property
    def height(self) -> float:
        return max(0.0, self.y2 - self.y1)

    @property
    def area(self) -> float:
        return self.width * self.height

    @property
    def center(self) -> tuple[float, float]:
        return ((self.x1 + self.x2) * 0.5, (self.y1 + self.y2) * 0.5)

    def contains(self, point: tuple[float, float]) -> bool:
        x, y = point
        return self.x1 <= x <= self.x2 and self.y1 <= y <= self.y2

    def clipped(self, image_shape: tuple[int, int] | tuple[int, int, int]) -> "BoundingBox":
        height, width = image_shape[:2]
        return BoundingBox(
            x1=min(max(self.x1, 0.0), float(width - 1)),
            y1=min(max(self.y1, 0.0), float(height - 1)),
            x2=min(max(self.x2, 0.0), float(width - 1)),
            y2=min(max(self.y2, 0.0), float(height - 1)),
        )


@dataclass(frozen=True)
class DetectionResult:
    class_id: int
    class_name: str
    confidence: float
    bbox: BoundingBox
    timestamp: float
    latency_ms: float
    center_score: float = 0.0


@dataclass(frozen=True)
class RawPrediction:
    class_id: int
    confidence: float
    bbox: BoundingBox


@dataclass(frozen=True)
class DetectionMetrics:
    frame_count: int
    fps: float
    average_latency_ms: float
    max_latency_ms: float
    memory_mb: float
