# M5 Pose Module Delivery

Version: 0.1

## Scope

This delivery starts M5 with an offline ICP baseline. The current input is the M4 camera-coordinate target point cloud plus a reference model point cloud:

```text
M4 target point cloud
+
Reference model point cloud
↓
Preprocessing
↓
Centroid + PCA rough initialization
↓
Point-to-point ICP
↓
Point-to-plane ICP refinement
↓
PoseResult JSON
```

M5 output transform semantics are fixed as:

```text
T_camera_from_model

p_camera = T_camera_from_model * p_model
```

The coordinate frame is `camera`. Axis conversion into Unity world space must be handled by a dedicated Unity-side converter in a later integration step.

## Implemented Tasks

| Task | Status | Files |
|------|--------|-------|
| M5.1 Pose data structures | Done | `pose/types.py` |
| M5.1 Config loading | Done | `pose/config.py`, `config/pose_icp.yaml` |
| M5.2 Reference point cloud loading | Done | `pose/io.py`, `scripts/prepare_reference_model.py` |
| M5.2 Model metadata | Done | `pose/types.py`, `scripts/prepare_reference_model.py` |
| M5.3 Scene point cloud preprocessing | Done | `pose/preprocessing.py` |
| M5.4 Centroid initialization | Done | `pose/initialization.py` |
| M5.4 PCA rotation initialization | Done | `pose/initialization.py` |
| M5.4 ICP baseline | Done | `pose/icp.py`, `scripts/estimate_pose_icp.py` |
| M5.4 Synthetic transform test | Done | `tests/test_pose_icp.py` |

## Model Metadata

Reference model assumptions are explicit:

```json
{
  "unit": "meter",
  "origin": "centroid",
  "forward_axis": "+Z",
  "up_axis": "+Y"
}
```

`scripts/prepare_reference_model.py` prints and saves the processed point count, origin offset, and axis-aligned bounding box extent so unit mistakes such as millimeters being treated as meters can be caught early.

## Commands

Prepare a reference model point cloud:

```bash
conda run -n Object_detection_system python scripts/prepare_reference_model.py \
  --model path/to/reference.ply \
  --config config/pose_icp.yaml \
  --output outputs/pose/reference_processed.ply
```

Estimate pose from an M4 point cloud:

```bash
conda run -n Object_detection_system python scripts/estimate_pose_icp.py \
  --scene path/to/latest_pointcloud.ply \
  --model outputs/pose/reference_processed.ply \
  --config config/pose_icp.yaml \
  --visualize \
  --output outputs/pose/latest_pose.json \
  --registration-output outputs/pose/registration_result.ply
```

The Open3D visualization draws:

- Blue: M4 scene point cloud.
- Gray: original reference model point cloud.
- Red: ICP-aligned reference model point cloud.
- Camera coordinate frame at the camera origin.
- Model pose coordinate frame transformed by `T_camera_from_model`.

Use the transformed model pose frame for manual direction inspection:

```text
Red axis: model +X / right
Green axis: model +Y / up
Blue axis: model +Z / forward
```

The script also prints:

```text
model_right_plus_x_camera
model_up_plus_y_camera
model_forward_plus_z_camera
```

These vectors are the program-estimated model axes expressed in camera coordinates.

Run tests:

```bash
conda run -n Object_detection_system python -m unittest discover -s tests
```

## PoseResult JSON

The output JSON includes:

```json
{
  "coordinate_frame": "camera",
  "transform_semantics": "T_camera_from_model",
  "transform_model_to_camera": [
    [1, 0, 0, 0],
    [0, 1, 0, 0],
    [0, 0, 1, 0],
    [0, 0, 0, 1]
  ],
  "translation_m": [0, 0, 0],
  "quaternion_xyzw": [0, 0, 0, 1],
  "fitness": 0.0,
  "inlier_rmse": 0.0,
  "runtime_ms": 0.0
}
```

Unity should not directly apply the Python quaternion. It should read the matrix and convert from camera-local coordinates to Unity world coordinates in one dedicated conversion component.

## Known Limitations

M4 scene clouds are partial, single-view LiDAR surfaces. Reference models are usually complete CAD or scan models. This means the first ICP baseline is a partial-to-complete registration problem, not a complete-to-complete registration problem.

Current baseline limitations:

- No visible-surface rendering or hidden-face clipping for the reference model yet.
- ICP fitness and RMSE are diagnostic signals only; they do not prove pose accuracy.
- Symmetric and near-planar objects can still produce plausible but wrong poses.
- Real translation and rotation accuracy require ground truth.

## On-Device Generic Geometric Frame Overlay

The Unity runtime also includes a lightweight phone-side generic geometric frame overlay for live inspection before full on-device ICP is available.

Files:

- `Assets/Scripts/Pose/GenericPoseFrame.cs`
- `Assets/Scripts/Pose/GenericPoseConfig.cs`
- `Assets/Scripts/Pose/GenericPoseEstimator.cs`
- `Assets/Scripts/Pose/GenericPoseStabilizer.cs`
- `Assets/Scripts/Demo/PointCloudCaptureDemo.cs`

Runtime behavior:

```text
Current YOLO ROI / Center ROI
↓
LiDAR target point cloud
↓
PCA + gravity + temporal stabilization
↓
GenericPoseFrame UI text
↓
AR scene RGB direction axes
```

The overlay displays:

```text
shape
stability
geometry / orientation / tracking / overall confidence
right / up / forward
extent
```

The AR scene line colors are:

```text
Red: Right
Green: Up
Blue: Forward
```

This is a model-free generic geometric frame, not semantic object pose. It is intended for live manual checks on iPhone:

- whether direction axes are stable while aiming at the same object;
- whether axes flip when the phone moves slightly;
- whether YOLO ROI gives better direction than center ROI;
- whether the captured LiDAR surface has enough geometry to infer direction.

The overlay cannot resolve object semantic forward direction for symmetric or near-planar objects. Full object pose still requires model-based registration such as ICP or FoundationPose.

## Acceptance

Engineering acceptance for this M5 baseline:

- Reads M4 scene `.ply`.
- Reads reference model `.ply`.
- Preprocesses both point clouds with configurable filtering.
- Uses centroid + PCA initialization before ICP.
- Runs point-to-point ICP followed by point-to-plane ICP.
- Writes `latest_pose.json`.
- Writes `registration_result.ply`.
- Opens Open3D visualization when `--visualize` is passed.
- Passes synthetic known-transform test.

Research accuracy acceptance remains:

```text
Translation <= 3 cm
Rotation <= 5 deg
```

This requires ground truth from a fixed jig, AprilTag/calibration target, synthetic transform test, or other calibrated capture process.
