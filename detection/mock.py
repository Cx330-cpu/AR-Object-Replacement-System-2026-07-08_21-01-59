from __future__ import annotations

import numpy as np

from .types import BoundingBox, RawPrediction


class StaticMockPredictor:
    def __init__(self, predictions: list[RawPrediction]) -> None:
        self.predictions = predictions

    def predict(self, frame: np.ndarray) -> list[RawPrediction]:
        return self.predictions


class CenterObjectMockPredictor:
    def __init__(self, class_id: int = 41, confidence: float = 0.99) -> None:
        self.class_id = class_id
        self.confidence = confidence

    def predict(self, frame: np.ndarray) -> list[RawPrediction]:
        height, width = frame.shape[:2]
        return [
            RawPrediction(
                class_id=self.class_id,
                confidence=self.confidence,
                bbox=BoundingBox(
                    x1=width * 0.35,
                    y1=height * 0.35,
                    x2=width * 0.65,
                    y2=height * 0.65,
                ),
            ),
            RawPrediction(
                class_id=56,
                confidence=0.98,
                bbox=BoundingBox(
                    x1=width * 0.02,
                    y1=height * 0.02,
                    x2=width * 0.25,
                    y2=height * 0.25,
                ),
            ),
        ]

