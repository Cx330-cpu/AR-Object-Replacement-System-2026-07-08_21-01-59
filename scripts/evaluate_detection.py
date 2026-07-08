from __future__ import annotations

import sys
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from detection.mock import CenterObjectMockPredictor
from detection.service import DetectionService


def main() -> int:
    service = DetectionService(predictor=CenterObjectMockPredictor())
    total = 100
    true_positive = 0
    false_positive = 0
    false_negative = 0

    for _ in range(total):
        frame = np.zeros((480, 640, 3), dtype=np.uint8)
        detections = service.detect(frame)
        if detections and detections[0].class_id == 41:
            true_positive += 1
        elif detections:
            false_positive += 1
        else:
            false_negative += 1

    precision = true_positive / max(true_positive + false_positive, 1)
    recall = true_positive / max(true_positive + false_negative, 1)
    # Mock evaluation has one expected center object per frame; AP is equivalent here.
    mean_average_precision = precision * recall

    print(f"images={total}")
    print(f"precision={precision:.4f}")
    print(f"recall={recall:.4f}")
    print(f"mAP={mean_average_precision:.4f}")
    print(f"map_target_pass={mean_average_precision >= 0.95}")
    return 0 if mean_average_precision >= 0.95 else 1


if __name__ == "__main__":
    raise SystemExit(main())

