# M1 Detection Module Delivery

Version: 1.0

## Scope

This delivery implements the M1 Detection Module defined in the milestone plan.

The module is isolated under `detection/` and exposes the required interface:

```python
detect(frame) -> list[DetectionResult]
```

This implementation follows the current product interaction rule:

- The visualization always draws a crosshair at the screen center.
- Detection post-processing returns only the object at the screen center.
- Non-center objects are ignored even if their confidence is higher.

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
- Device (`auto` selects CUDA, then Apple MPS, then CPU)
- Inference image size
- Half precision toggle for CUDA
- Class names
- Center-only detection switch
- Center region ratio
- Crosshair style
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

Check the active detection environment and GPU backend:

```bash
conda run -n Object_detection_system python scripts/check_detection_environment.py
```

Run 100-image metric evaluation with mock annotations:

```bash
python3 scripts/evaluate_detection.py
```

Run realtime camera demo after placing YOLO weights at `Assets/models/yolov8n.pt` or the CoreML export at `Assets/models/yolov8n.mlpackage`:

```bash
python3 app/detection_demo.py
```

When using the Anaconda environment prepared for this project:

```bash
bash scripts/run_detection_demo.sh
```

On Apple Silicon Macs, PyTorch MPS may be unavailable on newer macOS builds. In that case export and use the CoreML model:

```bash
YOLO_CONFIG_DIR=.ultralytics conda run -n Object_detection_system yolo export model=Assets/models/yolov8n.pt format=coreml imgsz=640
```

Force a specific device when needed:

```bash
python3 app/detection_demo.py --device mps
python3 app/detection_demo.py --device cuda
python3 app/detection_demo.py --device cpu
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

## iPhone Runtime Integration

M1 is also integrated into the Unity iOS runtime:

- CoreML model: `Assets/models/yolov8n.mlpackage`
- Native plugin: `Assets/Plugins/iOS/YoloCoreMLPlugin.mm`
- Unity wrapper: `Assets/Scripts/Detection/YoloCoreMLDetector.cs`
- ROI integration: `Assets/Scripts/Demo/PointCloudCaptureDemo.cs`

The native plugin loads YOLO through CoreML/Vision with `MLComputeUnitsAll`, allowing iOS to use device acceleration such as Neural Engine and GPU instead of the Python CPU pipeline.

The runtime behavior is:

```text
ARCamera CPU Image
↓
CoreML/Vision YOLO on iPhone
↓
Center target BoundingBox
↓
M4 PointCloud ROI
```

## Remaining Real-World Validation

The milestone requires real model and dataset validation before research acceptance:

- Place trained YOLO weights in `Assets/models/`
- Prepare 100 real labeled images
- Run the same evaluation pipeline against real annotations
- Confirm FPS >= 20
- Confirm latency <= 30 ms
- Confirm mAP >= 95%
