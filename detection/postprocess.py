from __future__ import annotations

from math import hypot

from .types import BoundingBox, RawPrediction


def bbox_iou(left: BoundingBox, right: BoundingBox) -> float:
    ix1 = max(left.x1, right.x1)
    iy1 = max(left.y1, right.y1)
    ix2 = min(left.x2, right.x2)
    iy2 = min(left.y2, right.y2)
    iw = max(0.0, ix2 - ix1)
    ih = max(0.0, iy2 - iy1)
    intersection = iw * ih
    union = left.area + right.area - intersection
    if union <= 0:
        return 0.0
    return intersection / union


def nms(
    predictions: list[RawPrediction],
    confidence_threshold: float,
    iou_threshold: float,
    max_detections: int,
) -> list[RawPrediction]:
    candidates = [p for p in predictions if p.confidence >= confidence_threshold and p.bbox.area > 0]
    candidates.sort(key=lambda p: p.confidence, reverse=True)

    selected: list[RawPrediction] = []
    for prediction in candidates:
        if len(selected) >= max_detections:
            break
        if all(bbox_iou(prediction.bbox, kept.bbox) <= iou_threshold for kept in selected):
            selected.append(prediction)
    return selected


def center_score(
    bbox: BoundingBox,
    frame_shape: tuple[int, int] | tuple[int, int, int],
    require_center_inside_bbox: bool,
    center_region_ratio: float,
) -> float:
    height, width = frame_shape[:2]
    frame_center = (width * 0.5, height * 0.5)

    if require_center_inside_bbox and bbox.contains(frame_center):
        return 1.0

    bbox_center = bbox.center
    distance = hypot(bbox_center[0] - frame_center[0], bbox_center[1] - frame_center[1])
    max_distance = max(1.0, min(width, height) * center_region_ratio)
    if distance > max_distance:
        return 0.0
    return 1.0 - (distance / max_distance)


def filter_center_object(
    predictions: list[RawPrediction],
    frame_shape: tuple[int, int] | tuple[int, int, int],
    center_region_ratio: float,
    require_center_inside_bbox: bool,
) -> list[tuple[RawPrediction, float]]:
    scored: list[tuple[RawPrediction, float]] = []
    for prediction in predictions:
        score = center_score(
            prediction.bbox,
            frame_shape,
            require_center_inside_bbox=require_center_inside_bbox,
            center_region_ratio=center_region_ratio,
        )
        if score > 0:
            scored.append((prediction, score))

    scored.sort(key=lambda item: (item[1], item[0].confidence), reverse=True)
    return scored[:1]

