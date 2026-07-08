from __future__ import annotations

import argparse
import sys
from pathlib import Path

import cv2

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from detection.service import DetectionService
from detection.visualization import draw_detections


def main() -> int:
    parser = argparse.ArgumentParser(description="M1 realtime center-object YOLO demo.")
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--config", default="config/detection.yaml")
    parser.add_argument("--device", default=None, help="auto, mps, cuda, cpu, or a torch device id")
    parser.add_argument("--download-model", action="store_true")
    args = parser.parse_args()

    service = DetectionService(
        config_path=args.config,
        allow_model_download=args.download_model,
        device_override=args.device,
    )
    predictor_device = getattr(service.predictor, "device", "mock")
    print(f"YOLO device: {predictor_device}")

    capture = cv2.VideoCapture(args.camera)
    if not capture.isOpened():
        raise RuntimeError(f"Unable to open camera index {args.camera}")

    try:
        while True:
            ok, frame = capture.read()
            if not ok:
                break
            detections = service.detect(frame)
            output = draw_detections(frame, detections, service.config)
            cv2.imshow("M1 Center Object Detection", output)
            if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                break
    finally:
        capture.release()
        cv2.destroyAllWindows()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
