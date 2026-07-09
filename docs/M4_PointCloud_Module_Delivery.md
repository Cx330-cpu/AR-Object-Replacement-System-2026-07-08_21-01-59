# M4 PointCloud Module Delivery

Version: 1.0

## Scope

M4 generates a target point cloud from ARKit LiDAR depth.

The primary point cloud coordinate system is camera coordinate, not Unity world coordinate. This keeps M4 compatible with M5 pose estimation pipelines such as ICP and FoundationPose.

```text
YOLO BoundingBox / Center ROI fallback
↓
DepthFrame + ROI + Camera Intrinsics
↓
CameraPoint / Point3D
↓
PointCloud(Camera Coordinate)
↓
PLY Export
↓
Open3D Visualization
```

## Implemented Tasks

| Task | Status | Files |
|------|--------|-------|
| Task 4.1 ROI Crop | Done | `Assets/Scripts/PointCloud/PointCloudBuilder.cs` |
| Task 4.2 Voxel DownSample | Done | `Assets/Scripts/PointCloud/PointCloudDownSampler.cs` |
| Task 4.3 Outlier Removal | Done | `Assets/Scripts/PointCloud/PointCloudCleaner.cs` |
| Task 4.4 Normal Estimation | Open3D side | `pointcloud/open3d_viewer.py` |
| Task 4.5 Visualization | Done | `scripts/view_pointcloud.py` |
| M1+M4 YOLO ROI Integration | Done | `Assets/Scripts/Detection/`, `Assets/Plugins/iOS/YoloCoreMLPlugin.mm` |

## Architecture

Core point cloud classes are pure C#:

- `DepthFrame`
- `Point3D`
- `PointCloudData`
- `PointCloudBuilder`
- `PointCloudDownSampler`
- `PointCloudCleaner`
- `PointCloudExporter`

Unity integration is isolated in:

- `PointCloudCaptureDemo`

`PointCloudBuilder` does not read AR Foundation APIs directly. It consumes `DepthFrame`, `RectInt roi`, and camera intrinsics.

YOLO runs on iPhone through CoreML/Vision. The native plugin loads `yolov8n.mlpackage` with `MLComputeUnitsAll`, allowing iOS to schedule inference on Neural Engine/GPU/CPU as appropriate.

## Demo

On iPhone:

1. Aim the center crosshair at a target.
2. Tap `捕获并显示点云`.
3. The app runs YOLO on the current camera frame.
4. If YOLO detects the center target, the detection bounding box becomes the point-cloud ROI.
5. If YOLO is unavailable or no object is detected, the demo falls back to the center ROI.
6. The app captures the ROI from LiDAR depth.
7. The app displays the captured point cloud directly in the AR scene.
8. It also exports a camera-coordinate `.ply` file to:

```text
Application.persistentDataPath/PointCloud/
```

For easier extraction from the iPhone container, it also writes:

```text
Application.persistentDataPath/latest_pointcloud.ply
Application.persistentDataPath/latest_pointcloud_path.txt
```

The iOS build postprocessor enables app document sharing, so these files should appear in the downloaded app container's `AppData/Documents/` folder.

The console logs:

- ROI
- raw point count
- filtered point count
- voxel size
- export time
- capture time / FPS
- output path
- latest output path
- YOLO class/confidence or center-ROI fallback reason

The on-device visualization is for immediate acceptance and debugging. The exported PLY remains the research artifact for Open3D, normal estimation, and later M5 pose estimation.

## Open3D Validation

After copying the `.ply` file to the Mac:

```bash
conda run -n Object_detection_system python scripts/view_pointcloud.py path/to/pointcloud.ply
```

For non-visual validation:

```bash
conda run -n Object_detection_system python scripts/evaluate_pointcloud.py path/to/pointcloud.ply
```

Open3D estimates normals in Python rather than Unity.

## Acceptance

Milestone acceptance:

```text
Open3D displays the captured point cloud normally.
```

Additional research logs:

- raw_points
- filtered_points
- voxel_size
- export_time_ms
- fps
