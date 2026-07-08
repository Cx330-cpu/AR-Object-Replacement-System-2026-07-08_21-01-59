# M1 Detection Module Delivery

Version: 1.0

## Scope

This delivery implements the M1 Detection Module defined in the milestone plan.

The module is isolated under `detection/` and exposes the required interface:

```python
detect(frame) -> list[DetectionResult]
```

## Implemented Tasks

| Task | Status | Files |
|------|--------|-------|
| Task 1.1 DetectionService | Done | `detection/service.py` |
| Task 1.2 ModelManager | Done | `detection/model_manager.py` |
| Task 1.3 detect(frame) interface | Done | `detection/service.py`, `detection/types.py` |
| Task 1.4 NMS / Confidence / Threshold | Done | `detection/postprocess.py` |
| Task 1.5 Benchmark | Done | `detection/benchmark.py`, `scripts/benchmark_detection.py` |
| Task 1.6 Visualization | Done | `detection/visualization.py`, `app/detection_demo.py` |
| Task 1.7 Unit Test with 100 images | Done with mock data | `tests/test_detection.py`, `scripts/evaluate_detection.py` |

## Configuration

Detection parameters are configured in:

```text
config/detection.yaml
```

Configurable fields include:

- YOLO model path
- Model download URL
- Confidence threshold
- NMS IoU threshold
- Max detections
- Device
- Class names
- Log level
- Log file

## Run Commands

Install dependencies:

```bash
pip install -r requirements.txt
```

Run unit tests:

```bash
python3 -m unittest discover -s tests
```

Run benchmark with mock frames:

```bash
python3 scripts/benchmark_detection.py
```

Run 100-image metric evaluation with mock annotations:

```bash
python3 scripts/evaluate_detection.py
```

Run realtime camera demo after placing YOLO weights at `Assets/models/yolov8n.pt`:

```bash
python3 app/detection_demo.py
```

## Current Acceptance Result

The module passes local mock-based M1 verification:

- Unit tests: pass
- Benchmark frames: 100
- FPS target: pass
- Latency target: pass
- Precision / Recall / mAP output: pass on mock annotations
- Visualization entrypoint: implemented
- Realtime YOLO demo: implemented, requires dependencies, camera, and local model weights

## Remaining Real-World Validation

The milestone requires real model and dataset validation before research acceptance:

- Place trained YOLO weights in `Assets/models/`
- Prepare 100 real labeled images
- Run the same evaluation pipeline against real annotations
- Confirm FPS >= 20
- Confirm latency <= 30 ms
- Confirm mAP >= 95%
