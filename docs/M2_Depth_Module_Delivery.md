# M2 Depth Module Delivery

Version: 1.0

## Scope

This delivery implements the M2 Depth Module as a Unity / ARKit LiDAR feature.

The interaction is adjusted from click-to-measure to center-crosshair measurement:

- A crosshair is drawn at the screen center.
- The system continuously samples LiDAR depth at the crosshair.
- The distance text updates in real time for the object aligned with the crosshair.

## Implemented Tasks

| Task | Status | Files |
|------|--------|-------|
| Task 2.1 ARKit initialization | Done | `Assets/Scripts/Depth/ARDepthCrosshairBootstrap.cs` |
| Task 2.2 DepthMap acquisition | Done | `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs` |
| Task 2.3 Confidence acquisition | Done | `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs` |
| Task 2.4 Camera Intrinsics acquisition | Done | `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs`, `Assets/Scripts/Depth/DepthResult.cs` |
| Task 2.5 Depth heatmap visualization | Done | `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs` |
| Task 2.6 Center-crosshair distance output | Done | `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs` |

## Runtime Behavior

The bootstrapper installs required components at runtime:

- `ARCameraManager`
- `AROcclusionManager`
- `ARDepthCrosshairMeasure`

`AROcclusionManager` requests `EnvironmentDepthMode.Fastest` and reads LiDAR depth through:

```csharp
TryAcquireEnvironmentDepthCpuImage()
TryAcquireEnvironmentDepthConfidenceCpuImage()
```

The displayed distance uses the median valid depth in a small window around the depth image center. Low-confidence samples are filtered out.

## Acceptance

M2 requires iPhone / iPad hardware with LiDAR.

Run the Unity app on a LiDAR-capable iPhone, aim the center crosshair at a measured target, and confirm:

- LiDAR depth starts updating.
- The screen center crosshair is visible.
- Distance is displayed without tapping.
- Heatmap is visible.
- Measured distance error is <= 2 cm against a physical ruler or tape measure.

## Notes

Unity Editor simulation cannot validate LiDAR accuracy. Final M2 acceptance must be performed on device.

