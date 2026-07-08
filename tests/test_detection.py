from __future__ import annotations

import unittest

import numpy as np

from detection.benchmark import run_benchmark
from detection.device import resolve_torch_device
from detection.mock import CenterObjectMockPredictor, StaticMockPredictor
from detection.postprocess import bbox_iou, nms
from detection.service import DetectionService
from detection.types import BoundingBox, RawPrediction
from detection.visualization import draw_center_crosshair


class DetectionModuleTest(unittest.TestCase):
    def test_detect_returns_only_center_object(self) -> None:
        frame = np.zeros((480, 640, 3), dtype=np.uint8)
        service = DetectionService(predictor=CenterObjectMockPredictor())

        detections = service.detect(frame)

        self.assertEqual(len(detections), 1)
        self.assertEqual(detections[0].class_id, 41)
        self.assertGreaterEqual(detections[0].confidence, 0.95)
        self.assertGreater(detections[0].center_score, 0)

    def test_non_center_object_is_ignored(self) -> None:
        frame = np.zeros((480, 640, 3), dtype=np.uint8)
        predictor = StaticMockPredictor(
            [
                RawPrediction(
                    class_id=56,
                    confidence=0.99,
                    bbox=BoundingBox(10, 10, 120, 120),
                )
            ]
        )
        service = DetectionService(predictor=predictor)

        detections = service.detect(frame)

        self.assertEqual(detections, [])

    def test_center_object_wins_over_higher_confidence_edge_object(self) -> None:
        frame = np.zeros((480, 640, 3), dtype=np.uint8)
        predictor = StaticMockPredictor(
            [
                RawPrediction(
                    class_id=56,
                    confidence=0.99,
                    bbox=BoundingBox(10, 10, 150, 150),
                ),
                RawPrediction(
                    class_id=41,
                    confidence=0.80,
                    bbox=BoundingBox(250, 180, 390, 310),
                ),
            ]
        )
        service = DetectionService(predictor=predictor)

        detections = service.detect(frame)

        self.assertEqual(len(detections), 1)
        self.assertEqual(detections[0].class_id, 41)

    def test_nms_suppresses_overlapping_boxes(self) -> None:
        predictions = [
            RawPrediction(1, 0.90, BoundingBox(10, 10, 100, 100)),
            RawPrediction(1, 0.80, BoundingBox(12, 12, 98, 98)),
            RawPrediction(2, 0.70, BoundingBox(220, 220, 300, 300)),
        ]

        selected = nms(predictions, confidence_threshold=0.25, iou_threshold=0.45, max_detections=20)

        self.assertEqual(len(selected), 2)
        self.assertEqual(selected[0].confidence, 0.90)
        self.assertGreater(bbox_iou(predictions[0].bbox, predictions[1].bbox), 0.45)

    def test_crosshair_draws_at_frame_center(self) -> None:
        frame = np.zeros((100, 100, 3), dtype=np.uint8)

        output = draw_center_crosshair(frame, size_px=10, color_bgr=(0, 255, 0), thickness=1)

        self.assertEqual(output[50, 50, 1], 255)
        self.assertEqual(output[50, 40, 1], 255)
        self.assertEqual(output[40, 50, 1], 255)

    def test_100_mock_images_meet_detection_target(self) -> None:
        service = DetectionService(predictor=CenterObjectMockPredictor())
        correct = 0
        total = 100

        for _ in range(total):
            frame = np.zeros((480, 640, 3), dtype=np.uint8)
            detections = service.detect(frame)
            correct += int(bool(detections and detections[0].class_id == 41))

        self.assertGreaterEqual(correct / total, 0.95)

    def test_benchmark_meets_m1_mock_targets(self) -> None:
        service = DetectionService(predictor=CenterObjectMockPredictor())

        metrics = run_benchmark(service, frame_count=100)

        self.assertGreaterEqual(metrics.fps, 20)
        self.assertLessEqual(metrics.average_latency_ms, 30)

    def test_explicit_device_is_preserved(self) -> None:
        self.assertEqual(resolve_torch_device("mps"), "mps")
        self.assertEqual(resolve_torch_device("cuda"), "cuda")
        self.assertEqual(resolve_torch_device("cpu"), "cpu")


if __name__ == "__main__":
    unittest.main()
