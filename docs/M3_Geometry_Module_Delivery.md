# M3 Geometry Module Delivery

Version: 1.0

## Scope

This delivery implements M3 Geometry without making YOLO a prerequisite.

The core module converts an arbitrary screen or camera pixel plus LiDAR depth into a Unity world coordinate:

```text
Pixel + Depth + Camera Intrinsics + Camera Transform
↓
WorldPoint
```

M1 bounding boxes are a future input source only. M3 first validates generic geometry using the screen center.

## Implemented Tasks

| Task | Status | Files |
|------|--------|-------|
| Task 3.1 Pixel -> Camera conversion | Done | `Assets/Scripts/Geometry/CoordinateConverter.cs` |
| Task 3.2 Depth -> World conversion | Done | `Assets/Scripts/Geometry/GeometryService.cs` |
| Task 3.3 BoundingBox -> Center | Deferred integration | Interface supports any pixel; YOLO center can be passed later |
| Task 3.4 World Coordinate output | Done | `Assets/Scripts/Geometry/WorldPoint.cs` |
| Task 3.5 Unity Sphere verification | Done | `Assets/Scripts/Geometry/GeometrySphereDemo.cs` |

## Architecture

Only `GeometrySphereDemo` inherits from `MonoBehaviour`.

Core geometry is implemented as pure C# service classes:

```text
GeometrySphereDemo
↓
GeometryService
↓
CoordinateConverter
```

This keeps M3 reusable by M4 PointCloud and M5 Pose without coupling those modules to Unity scene objects.

## Public Interface

```csharp
WorldPoint PixelToWorld(
    Vector2 cameraPixel,
    float depthMeters,
    XRCameraIntrinsics intrinsics,
    Transform cameraTransform,
    double timestamp)
```

For Unity screen pixels:

```csharp
WorldPoint ScreenPixelToWorld(
    Vector2 screenPixel,
    Vector2Int screenResolution,
    float depthMeters,
    XRCameraIntrinsics intrinsics,
    Transform cameraTransform,
    double timestamp)
```

## Demo

The demo uses:

- screen center pixel
- M2 LiDAR center depth
- M2 camera intrinsics
- AR camera transform

It outputs:

- a green sphere at the recovered world coordinate
- console logs containing `WorldPoint x/y/z`, depth, and pixel

## Acceptance

Run on LiDAR-capable iPhone:

1. Aim the center crosshair at a stable real-world surface.
2. Confirm M2 distance remains accurate.
3. Confirm the green sphere appears at the target world coordinate.
4. Move the phone slightly.
5. Confirm the sphere remains stable in the real world instead of sliding with the screen.

Milestone acceptance:

```text
Sphere stable follow
```

