from __future__ import annotations

import numpy as np

try:
    import cv2
except ImportError:  # pragma: no cover - exercised when demo dependencies are absent.
    cv2 = None

from .config import DetectionConfig
from .types import DetectionResult


def draw_center_crosshair(
    frame: np.ndarray,
    size_px: int = 24,
    color_bgr: tuple[int, int, int] = (0, 255, 0),
    thickness: int = 2,
) -> np.ndarray:
    output = frame.copy()
    height, width = output.shape[:2]
    cx = width // 2
    cy = height // 2
    half_thickness = max(0, thickness // 2)
    x1 = max(0, cx - size_px)
    x2 = min(width, cx + size_px + 1)
    y1 = max(0, cy - size_px)
    y2 = min(height, cy + size_px + 1)
    output[max(0, cy - half_thickness) : min(height, cy + half_thickness + 1), x1:x2] = color_bgr
    output[y1:y2, max(0, cx - half_thickness) : min(width, cx + half_thickness + 1)] = color_bgr
    return output


def draw_detections(
    frame: np.ndarray,
    detections: list[DetectionResult],
    config: DetectionConfig,
) -> np.ndarray:
    if cv2 is None:
        raise ImportError("opencv-python is required for drawing detection boxes.")

    output = draw_center_crosshair(
        frame,
        size_px=config.center_crosshair_size_px,
        color_bgr=config.center_crosshair_color_bgr,
        thickness=config.center_crosshair_thickness,
    )

    for detection in detections:
        bbox = detection.bbox
        p1 = (int(bbox.x1), int(bbox.y1))
        p2 = (int(bbox.x2), int(bbox.y2))
        cv2.rectangle(output, p1, p2, (0, 180, 255), 2)
        label = f"{detection.class_name} {detection.confidence:.2f}"
        cv2.putText(
            output,
            label,
            (p1[0], max(20, p1[1] - 8)),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            (0, 180, 255),
            2,
            cv2.LINE_AA,
        )
    return output
