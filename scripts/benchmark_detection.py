from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from detection.benchmark import run_benchmark
from detection.mock import CenterObjectMockPredictor
from detection.service import DetectionService


def main() -> int:
    service = DetectionService(predictor=CenterObjectMockPredictor())
    metrics = run_benchmark(service, frame_count=100)
    print(f"frames={metrics.frame_count}")
    print(f"fps={metrics.fps:.2f}")
    print(f"average_latency_ms={metrics.average_latency_ms:.2f}")
    print(f"max_latency_ms={metrics.max_latency_ms:.2f}")
    print(f"memory_mb={metrics.memory_mb:.2f}")
    print(f"fps_target_pass={metrics.fps >= 20}")
    print(f"latency_target_pass={metrics.average_latency_ms <= 30}")
    return 0 if metrics.fps >= 20 and metrics.average_latency_ms <= 30 else 1


if __name__ == "__main__":
    raise SystemExit(main())

