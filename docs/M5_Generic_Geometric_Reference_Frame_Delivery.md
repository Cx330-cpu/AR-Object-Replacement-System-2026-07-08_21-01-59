# M5 Generic Geometric Reference Frame Delivery

Version: 0.1

## Scope

This delivery implements a no-CAD, no-reference-model geometric pose frame for M4 target point clouds.

```text
YOLO ROI / Center ROI
↓
ARKit LiDAR camera-coordinate point cloud
↓
PCA geometry analysis
↓
Robust center estimation
↓
Gravity alignment
↓
Temporal sign stabilization and smoothing
↓
GenericPoseFrame
↓
Runtime pose mode selection
↓
iPhone UI + AR RGB axes
```

`GenericPoseFrame` is not semantic object pose. It is a stable local geometric reference frame for the current ROI point cloud.

## Research Goal

The goal is to provide a model-free local coordinate system that can support AR attachment experiments before CAD/ICP/FoundationPose is available on device.

It estimates:

- center
- right / up / forward
- PCA major / middle / normal
- shape type
- geometry, orientation, tracking, and overall confidence
- point count
- extent
- timestamp

## Why No CAD Or Reference Model Is Used

This stage deliberately avoids CAD models, reference point clouds, ICP, FoundationPose, and Vuforia Model Target. Without a reference model, the system cannot infer the true semantic front/top of an unknown object. It can only infer the stable geometric frame of the observed LiDAR ROI.

## Architecture

Pure algorithm classes:

- `Assets/Scripts/Pose/GenericPoseFrame.cs`
- `Assets/Scripts/Pose/GenericPoseConfig.cs`
- `Assets/Scripts/Pose/GenericPoseEstimator.cs`
- `Assets/Scripts/Pose/GenericPoseStabilizer.cs`
- `Assets/Scripts/Pose/GenericPoseMath.cs`
- `Assets/Scripts/Pose/GenericShapeType.cs`
- `Assets/Scripts/Pose/GenericPoseStability.cs`

Runtime integration:

- `Assets/Scripts/Demo/PointCloudCaptureDemo.cs`
- `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs` keeps center-depth logic but hides the visual crosshair by default.
- `Assets/Scripts/Geometry/GeometrySphereDemo.cs` no longer auto-installs the old M3 crosshair-follow sphere.

Existing M4 point cloud generation is reused. The estimator does not read AR managers, YOLO, depth images, UI, or scene GameObjects.

The runtime YOLO ROI expansion is configurable through `yoloRoiExpandRatio` in `PointCloudCaptureDemo` and defaults to `0.04` to reduce background contamination.

`PointCloudCaptureDemo` supports three runtime pose modes:

```text
Auto
SurfaceObject
FreeObject
```

`Auto` is conservative. It first checks ARFoundation horizontal planes. If a supporting `ARPlane` is found under the target center, it selects `SurfaceObject`. If no supporting plane is found, it falls back to the generic geometric frame confidence rules.

## Data Flow

```text
PointCloudCaptureDemo
↓
current YOLO ROI or center ROI
↓
PointCloudBuilder.BuildPointCloud
↓
PointCloudCleaner
↓
PointCloudDownSampler
↓
GenericPoseEstimator.Estimate
↓
GenericPoseStabilizer.Update
↓
UI text and AR RGB axes
↓
Yellow target-center sphere
```

## Coordinate System

M4 point clouds are in camera coordinates.

The frame stores:

```text
CenterCamera
RightCamera
UpCamera
ForwardCamera
AxisMajorCamera
AxisMiddleCamera
AxisNormalCamera
```

For AR display:

```csharp
centerWorld = cameraTransform.TransformPoint(frame.CenterCamera);
rightWorld = cameraTransform.TransformDirection(frame.RightCamera);
upWorld = cameraTransform.TransformDirection(frame.UpCamera);
forwardWorld = cameraTransform.TransformDirection(frame.ForwardCamera);
```

Display colors:

```text
Red   = Right
Green = Up
Blue  = Forward
Yellow sphere = estimated target point-cloud center
```

The screen center is still used internally for center-target detection and center ROI fallback, but the visual crosshair is hidden by default.

## Runtime Pose Modes

### SurfaceObject

This is intended for objects placed on a table or another horizontal support.

```text
center  = robust ROI point-cloud center
up      = world up, expressed in camera coordinates
forward = camera-facing horizontal direction
right   = up/forward orthonormal cross axis
```

This mode ignores noisy PCA orientation for tabletop replacement. The replacement model can be placed at the yellow center sphere, kept vertical to the table, and faced toward the camera/user.

### FreeObject

This is intended for objects held in the hand or otherwise not constrained by a table. It uses the full generic geometric frame:

```text
PCA axes
gravity alignment
temporal sign stabilization
quaternion smoothing
```

### Auto

The default `Auto` mode first looks for a supporting ARFoundation horizontal plane:

```text
centerWorld = cameraTransform.TransformPoint(frame.CenterCamera)
height = distance from centerWorld to horizontal ARPlane
```

It selects `SurfaceObject` when:

- an `ARPlaneManager` exists;
- a tracked `HorizontalUp` plane is found;
- the target center is above that plane;
- the height is within `SurfacePlaneMinimumHeightMeters..SurfacePlaneMaximumHeightMeters`;
- the projected center is near the plane bounds.

If no supporting plane is found, `Auto` selects `FreeObject` only when:

- shape is `Elongated` or `BoxLike`;
- overall confidence is above `AutoFreeObjectConfidenceThreshold`;
- scattering is above `AutoFreeObjectMinimumScattering`;
- planarity is below `AutoFreeObjectMaximumPlanarity`.

Otherwise it selects `SurfaceObject`.

The on-device UI and Xcode logs include the reason:

```text
ARPlane support height=...
world-up fallback
```

## PCA Formulas

`CenterCamera` uses a robust centroid. Finite points are sorted by camera-space depth `z`, the nearest and farthest 20% are trimmed, and the remaining points are averaged. This reduces center drift when the ROI includes foreground or background outliers.

For sorted eigenvalues:

```text
lambda1 >= lambda2 >= lambda3 >= 0
```

The geometric descriptors are:

```text
linearity  = (lambda1 - lambda2) / max(lambda1, epsilon)
planarity  = (lambda2 - lambda3) / max(lambda1, epsilon)
scattering = lambda3 / max(lambda1, epsilon)
```

All descriptors are clamped to `0..1`.

## Shape Classification

The first heuristic is configurable:

```text
if scattering > AmbiguousScatteringThreshold:
    Ambiguous
else if linearity > ElongatedLinearityThreshold:
    Elongated
else if planarity > PlanarPlanarityThreshold:
    Planar
else:
    BoxLike
```

Default thresholds live in `GenericPoseConfig`.

## Gravity Alignment

World up is converted to camera coordinates by the demo:

```csharp
worldUpCamera = cameraTransform.InverseTransformDirection(Vector3.up).normalized;
```

The estimator chooses the PCA axis with the highest absolute dot product against this vector as `UpCamera`, then flips it to point toward world up.

## Forward Rule

Because there is no semantic model, `ForwardCamera` is geometric:

- `Elongated`: uses the major axis unless it is nearly parallel to up.
- `Planar`: uses the surface normal and flips it toward the camera.
- `BoxLike`: uses a non-up PCA axis.
- `Ambiguous`: still builds a frame for display but confidence remains low.

Final axes are always orthonormalized.

## Temporal Stabilization

`GenericPoseStabilizer` stores the previous valid frame.

It applies:

- 180 degree sign stabilization for up and forward.
- quaternion smoothing via `Quaternion.Slerp`.
- center smoothing via `Vector3.Lerp`.
- short tracking-lost hold using `LostTrackingHoldSeconds`.

Expired invalid frames hide the AR axes.

## Confidence Definition

The frame reports:

```text
GeometryConfidence
OrientationConfidence
TrackingConfidence
OverallConfidence
```

Current overall confidence:

```text
overall =
    0.30 * geometry +
    0.45 * orientation +
    0.25 * tracking
```

Stability:

```text
overall >= StableConfidenceThreshold -> Stable
overall >= WeakConfidenceThreshold   -> Weak
otherwise                            -> Unreliable
invalid                              -> Invalid
short lost state                     -> TrackingLost
```

Ambiguous shape is prevented from becoming `Stable`.

## Files Added

- `Assets/Scripts/Pose/GenericPoseFrame.cs`
- `Assets/Scripts/Pose/GenericPoseConfig.cs`
- `Assets/Scripts/Pose/GenericPoseEstimator.cs`
- `Assets/Scripts/Pose/GenericPoseStabilizer.cs`
- `Assets/Scripts/Pose/GenericPoseMath.cs`
- `Assets/Scripts/Pose/GenericShapeType.cs`
- `Assets/Scripts/Pose/GenericPoseStability.cs`
- `docs/M5_Generic_Geometric_Reference_Frame_Delivery.md`

## Files Modified

- `Assets/Scripts/Demo/PointCloudCaptureDemo.cs`
- `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs`
- `Assets/Scripts/Geometry/GeometrySphereDemo.cs`

## Build And Test Instructions

Python tests for the existing offline modules:

```bash
conda run -n Object_detection_system python -m unittest discover -s tests
```

Unity build:

1. Open the project in Unity 2022.3.62f3.
2. Build and run on a LiDAR-capable iPhone.
3. Watch the on-device UI and Xcode logs.

This repository currently does not include `com.unity.test-framework` in `Packages/manifest.json`, so EditMode tests are not added in this delivery to avoid changing package dependencies without approval.

## iPhone Acceptance Checklist

Test objects:

- remote control or pencil case: expected `Elongated`
- book or box face: expected `Planar`
- cardboard box: expected `BoxLike`
- cup, ball, or cylinder: expected `Ambiguous` or low confidence

Acceptance:

- YOLO detection still creates an ROI.
- Center ROI fallback still works.
- RGB axes appear near the target point cloud.
- Red/right, green/up, and blue/forward do not flip frequently during small phone motion.
- Planar normal is roughly perpendicular to the visible surface.
- Elongated major direction follows the long side.
- Ambiguous objects do not show high stable confidence.
- Short detection loss reports `TrackingLost` before hiding axes.
- M1-M4 point cloud capture and PLY export still work.

## Known Limitations

- This is not semantic object pose.
- The true front of an unknown object cannot be inferred reliably.
- PCA axes have sign ambiguity.
- Symmetric objects have orientation ambiguity.
- Single-view LiDAR only captures partial geometry.
- YOLO ROI may include background points.
- Shape thresholds are heuristic.
- Fast object motion and occlusion may reduce stability.
- Full CAD alignment still requires ICP, FoundationPose, or another model-based pose method.
