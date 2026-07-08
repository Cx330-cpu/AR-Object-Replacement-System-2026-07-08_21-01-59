from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml


@dataclass(frozen=True)
class DetectionConfig:
    model_path: str
    model_download_url: str
    confidence_threshold: float
    nms_iou_threshold: float
    max_detections: int
    device: str
    image_size: int
    use_half_precision: bool
    class_names: dict[int, str]
    log_level: str
    log_file: str
    center_only: bool
    center_region_ratio: float
    center_crosshair_size_px: int
    center_crosshair_color_bgr: tuple[int, int, int]
    center_crosshair_thickness: int
    require_center_inside_bbox: bool

    @classmethod
    def from_yaml(cls, path: str | Path) -> "DetectionConfig":
        config_path = Path(path)
        with config_path.open("r", encoding="utf-8") as file:
            data: dict[str, Any] = yaml.safe_load(file) or {}

        yolo = data.get("yolo", {})
        center = data.get("center_detection", {})
        logging = data.get("logging", {})
        visualization = data.get("visualization", {})

        class_names = {
            int(class_id): str(name)
            for class_id, name in (yolo.get("class_names") or {}).items()
        }

        color = visualization.get("crosshair_color_bgr", [0, 255, 0])
        return cls(
            model_path=str(yolo.get("model_path", "Assets/models/yolov8n.pt")),
            model_download_url=str(yolo.get("model_download_url", "")),
            confidence_threshold=float(yolo.get("confidence_threshold", 0.25)),
            nms_iou_threshold=float(yolo.get("nms_iou_threshold", 0.45)),
            max_detections=int(yolo.get("max_detections", 20)),
            device=str(yolo.get("device", "auto")),
            image_size=int(yolo.get("image_size", 640)),
            use_half_precision=bool(yolo.get("use_half_precision", False)),
            class_names=class_names,
            log_level=str(logging.get("level", "INFO")),
            log_file=str(logging.get("file", "logs/detection.log")),
            center_only=bool(center.get("enabled", True)),
            center_region_ratio=float(center.get("region_ratio", 0.25)),
            center_crosshair_size_px=int(visualization.get("crosshair_size_px", 24)),
            center_crosshair_color_bgr=tuple(int(v) for v in color[:3]),
            center_crosshair_thickness=int(visualization.get("crosshair_thickness", 2)),
            require_center_inside_bbox=bool(center.get("require_center_inside_bbox", True)),
        )
