from __future__ import annotations

import logging
import os
import time
from pathlib import Path
from typing import Protocol

import numpy as np

from .config import DetectionConfig
from .device import resolve_torch_device
from .model_manager import ModelManager
from .postprocess import filter_center_object, nms
from .types import BoundingBox, DetectionResult, RawPrediction


class Predictor(Protocol):
    def predict(self, frame: np.ndarray) -> list[RawPrediction]:
        ...


class UltralyticsPredictor:
    def __init__(
        self,
        model_path: Path,
        device: str,
        image_size: int = 640,
        use_half_precision: bool = False,
    ) -> None:
        os.environ.setdefault("PYTORCH_ENABLE_MPS_FALLBACK", "1")
        os.environ.setdefault("TMPDIR", "/private/tmp")
        os.environ.setdefault("MPLCONFIGDIR", str(Path(".cache/matplotlib").resolve()))
        os.environ.setdefault("YOLO_CONFIG_DIR", str(Path(".ultralytics").resolve()))
        Path(os.environ["MPLCONFIGDIR"]).mkdir(parents=True, exist_ok=True)
        Path(os.environ["YOLO_CONFIG_DIR"]).mkdir(parents=True, exist_ok=True)
        try:
            from ultralytics import YOLO
        except ImportError as exc:
            raise ImportError(
                "ultralytics is required for real YOLO inference. "
                "Install dependencies with: pip install -r requirements.txt"
            ) from exc

        self.model = YOLO(str(model_path), task="detect")
        self.device = resolve_torch_device(device)
        self.image_size = image_size
        self.use_half_precision = use_half_precision and self.device == "cuda"

    def predict(self, frame: np.ndarray) -> list[RawPrediction]:
        results = self.model.predict(
            frame,
            verbose=False,
            device=self.device,
            imgsz=self.image_size,
            half=self.use_half_precision,
        )
        predictions: list[RawPrediction] = []
        for result in results:
            boxes = getattr(result, "boxes", None)
            if boxes is None:
                continue
            for box in boxes:
                xyxy = box.xyxy[0].detach().cpu().numpy().tolist()
                confidence = float(box.conf[0].detach().cpu().item())
                class_id = int(box.cls[0].detach().cpu().item())
                predictions.append(
                    RawPrediction(
                        class_id=class_id,
                        confidence=confidence,
                        bbox=BoundingBox(
                            x1=float(xyxy[0]),
                            y1=float(xyxy[1]),
                            x2=float(xyxy[2]),
                            y2=float(xyxy[3]),
                        ),
                    )
                )
        return predictions


class DetectionService:
    def __init__(
        self,
        config_path: str | Path = "config/detection.yaml",
        predictor: Predictor | None = None,
        allow_model_download: bool = False,
        device_override: str | None = None,
    ) -> None:
        self.config = DetectionConfig.from_yaml(config_path)
        self.logger = self._create_logger()
        self.model_manager = ModelManager(
            self.config.model_path,
            self.config.model_download_url,
        )

        if predictor is None:
            model_path = self.model_manager.resolve(allow_download=allow_model_download)
            self.predictor: Predictor = UltralyticsPredictor(
                model_path,
                device_override or self.config.device,
                image_size=self.config.image_size,
                use_half_precision=self.config.use_half_precision,
            )
        else:
            self.predictor = predictor

    def detect(self, frame: np.ndarray) -> list[DetectionResult]:
        if frame is None or not isinstance(frame, np.ndarray) or frame.size == 0:
            raise ValueError("frame must be a non-empty numpy array")

        started_at = time.perf_counter()
        timestamp = time.time()

        raw_predictions = [
            RawPrediction(
                class_id=prediction.class_id,
                confidence=prediction.confidence,
                bbox=prediction.bbox.clipped(frame.shape),
            )
            for prediction in self.predictor.predict(frame)
        ]

        selected = nms(
            raw_predictions,
            confidence_threshold=self.config.confidence_threshold,
            iou_threshold=self.config.nms_iou_threshold,
            max_detections=self.config.max_detections,
        )

        if self.config.center_only:
            scored = filter_center_object(
                selected,
                frame.shape,
                center_region_ratio=self.config.center_region_ratio,
                require_center_inside_bbox=self.config.require_center_inside_bbox,
            )
        else:
            scored = [(prediction, 0.0) for prediction in selected]

        latency_ms = (time.perf_counter() - started_at) * 1000.0
        results = [
            DetectionResult(
                class_id=prediction.class_id,
                class_name=self.config.class_names.get(prediction.class_id, str(prediction.class_id)),
                confidence=prediction.confidence,
                bbox=prediction.bbox,
                timestamp=timestamp,
                latency_ms=latency_ms,
                center_score=score,
            )
            for prediction, score in scored
        ]

        self.logger.info(
            "Frame shape=%s detections=%d latency_ms=%.2f",
            frame.shape,
            len(results),
            latency_ms,
        )
        return results

    def _create_logger(self) -> logging.Logger:
        logger = logging.getLogger("detection")
        logger.setLevel(getattr(logging, self.config.log_level.upper(), logging.INFO))
        logger.propagate = False

        if not logger.handlers:
            log_path = Path(self.config.log_file)
            log_path.parent.mkdir(parents=True, exist_ok=True)
            handler = logging.FileHandler(log_path, encoding="utf-8")
            handler.setFormatter(
                logging.Formatter("%(asctime)s %(levelname)s %(name)s %(message)s")
            )
            logger.addHandler(handler)
        return logger
