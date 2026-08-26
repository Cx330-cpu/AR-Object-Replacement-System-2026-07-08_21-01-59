# M6 Rendering Model Replacement Delivery

Version: 0.1

## Scope

This delivery connects the current M5 runtime pose frame to a replacement model renderer.

```text
YOLO class
↓
RuntimePoseFrame center / right / up / forward
↓
ReplacementModelController
↓
Prefab or GLB from Resources/ReplacementModels
↓
AR model placed at target center
```

If no matching prefab exists yet, the controller loads the `.glb` with glTFast. While the GLB is loading or unavailable, it shows a fallback cube with a forward marker.

## Available Source Model Files

The repository contains GLB files in the root-level `模型/` directory:

```text
radio.glb
retro_computer.glb
retro_table_lamp(1).glb
tv__old_tv__retro_tv(1).glb
wash basin.glb
帆布包.glb
座机.glb
怀表.glb
手持电话.glb
手提箱.glb
收音机.glb
暖壶.glb
煤油灯.glb
玻璃罐.glb
电脑.glb
相机.glb
缝纫机.glb
酒.glb
```

These files are visible locally, but they are not currently under `Assets/` and the project does not currently include a GLB runtime/import package such as glTFast. Unity will not automatically include or load these root-level GLB files in the iPhone build.

## Runtime Mapping

`ReplacementModelMapper` maps detected COCO classes to model resource names:

```text
handbag    -> 帆布包
suitcase   -> 手提箱
bottle     -> 酒
tv         -> tv__old_tv__retro_tv(1)
laptop     -> retro_computer
cell phone -> 手持电话
clock      -> 怀表
vase       -> 玻璃罐
```

Unknown classes use `DefaultReplacement`.

## Placement Calibration

`ReplacementModelController` applies per-model placement profiles:

```text
fit mode
scale multiplier
rotation offset
pivot offset
vertical offset
min/max scale
```

The controller calculates renderer bounds after the model loads and computes scale from:

```text
target point-cloud extent / model renderer bounds extent
```

Initial profiles:

```text
酒:
  FitMode = Height
  ScaleMultiplier = 1.15
  RotationOffsetEuler = (0, 180, 0)
  VerticalOffsetMeters = 0.02

手提箱 / 帆布包:
  FitMode = Width
  ScaleMultiplier = 1.05
  RotationOffsetEuler = (0, 180, 0)
```

The on-device UI reports:

```text
model=<name> (loading|real|fallback)
```

## Files Added

- `Assets/Scripts/Rendering/ReplacementModelMapper.cs`
- `Assets/Scripts/Rendering/ReplacementModelController.cs`
- `Assets/Scripts/Rendering/ReplacementModelProfileAsset.cs`
- `Assets/Scripts/Rendering/ReplacementModelRegistry.cs`
- `Assets/Editor/ReplacementProfileAssetCreator.cs`
- `Assets/Editor/ReplacementModelRegistryCreator.cs`

## Files Modified

- `Assets/Scripts/Demo/PointCloudCaptureDemo.cs`

## How To Use Real Models

To replace the fallback cube with real models, place prefabs or GLB files under:

```text
Assets/Resources/ReplacementModels/
```

The file names must match the mapping names, for example:

```text
Assets/Resources/ReplacementModels/手提箱.prefab
Assets/Resources/ReplacementModels/酒.prefab
Assets/Resources/ReplacementModels/retro_computer.prefab
Assets/Resources/ReplacementModels/酒.glb
```

Runtime loading order:

1. Try the scene `ReplacementModelRegistry`.
2. Try `Resources.Load<GameObject>("ReplacementModels/<name>")` for prefabs.
3. If no prefab exists, load `<name>.glb` through glTFast.

## Scene Preloaded Model Workflow

Create a scene registry from the Unity menu:

```text
AR Object Replacement > Create Scene Replacement Registry
```

This creates a hierarchy similar to:

```text
XR Origin
└── Replacement Model Registry
    ├── Replacement_酒
    │   └── Visual
    ├── Replacement_手提箱
    │   └── Visual
    └── Replacement_帆布包
        └── Visual
```

If Unity can load a same-name prefab or model asset from `Assets/Resources/ReplacementModels/`, the menu places it under the matching `Visual` child. Otherwise, drag the model into the `Visual` child manually.

Runtime behavior:

- YOLO detects a class.
- `ReplacementModelMapper` maps the class to a resource name.
- `ReplacementModelController` enables the matching preloaded scene model.
- Other registered scene models are hidden.
- The outer `Replacement_<name>` object is moved to the detected object pose. For ARPlane-supported tabletop objects, the anchor is the detected bottom-center point on the support plane.
- The inner `Visual` child keeps your manual direction, scale, and offset adjustments.

Do not manually adjust the outer `Replacement_<name>` object for final tuning, because runtime pose updates overwrite it. Adjust the child object under `Visual` instead.

For model preparation, use:

```text
AR Object Replacement > Fit Selected Replacement Model For Editing
```

This scales the selected model to an editable preview size and aligns the model bounding-box bottom center to the `Visual` origin. The `Visual` origin is therefore the model placement anchor.

## How To Adjust Model Direction, Size, And Offset In Unity

Create editable model profiles from the Unity menu:

```text
AR Object Replacement > Create Missing Replacement Profiles
```

This creates assets under:

```text
Assets/Resources/ReplacementProfiles/
```

Select a profile asset such as `酒.asset` in the Project window and adjust it in the Inspector:

- `Rotation Offset Euler`: fixes model facing direction.
- `Pivot Offset Meters`: moves the model relative to the detected placement anchor.
- `Vertical Offset Meters`: moves the model along the detected up axis.
- `Scale Multiplier`: makes the fitted model larger or smaller.
- `Fit Mode`: chooses whether scaling uses max extent, height, or width.
- `Minimum Scale` / `Maximum Scale`: clamps abnormal scale results.

At runtime, `ReplacementModelController` first loads:

```text
Resources/ReplacementProfiles/<model-name>
```

If that profile does not exist, it falls back to built-in defaults.

## Current Limitations

- GLB runtime loading requires glTFast.
- Model scale and pivot may need per-model adjustment after import.
- The current replacement transform uses M5 `SurfaceObject` / `FreeObject` axes and robust center, not CAD-accurate ICP pose.
