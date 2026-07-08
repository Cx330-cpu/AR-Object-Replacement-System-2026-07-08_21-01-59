from __future__ import annotations

import resource
import time

import numpy as np

from .service import DetectionService
from .types import DetectionMetrics


def current_memory_mb() -> float:
    usage = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    if usage > 10_000_000:
        return usage / (1024 * 1024)
    return usage / 1024


def run_benchmark(
    service: DetectionService,
    frame_count: int = 100,
    frame_shape: tuple[int, int, int] = (480, 640, 3),
) -> DetectionMetrics:
    latencies: list[float] = []
    started_at = time.perf_counter()

    for index in range(frame_count):
        frame = np.zeros(frame_shape, dtype=np.uint8)
        frame[:, :, 1] = index % 255
        call_started = time.perf_counter()
        service.detect(frame)
        latencies.append((time.perf_counter() - call_started) * 1000.0)

    elapsed = max(time.perf_counter() - started_at, 1e-9)
    return DetectionMetrics(
        frame_count=frame_count,
        fps=frame_count / elapsed,
        average_latency_ms=sum(latencies) / len(latencies),
        max_latency_ms=max(latencies),
        memory_mb=current_memory_mb(),
    )

