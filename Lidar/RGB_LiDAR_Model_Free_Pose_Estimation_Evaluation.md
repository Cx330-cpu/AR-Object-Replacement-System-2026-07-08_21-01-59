# RGB-LiDAR Based Model-Free Object Pose Estimation for AR Object Replacement

## 1. Method Overview

This method investigates a model-free AR object replacement pipeline
based on RGB camera information and LiDAR depth sensing.

Unlike Vuforia Model Target tracking, which requires a predefined CAD
model, this approach aims to estimate object position and geometry
directly from sensor data.

The workflow is:

    RGB Camera
    ↓
    YOLO Object Detection
    ↓
    Object ROI Extraction
    ↓
    LiDAR Depth Acquisition
    ↓
    Depth to Point Cloud Reconstruction
    ↓
    Point Cloud Processing
    ↓
    Generic Geometric Pose Estimation
    ↓
    Virtual Object Replacement

Implementation environment:

-   Unity 2022.3 LTS
-   iPhone 16 Pro Max
-   AR Foundation / ARKit
-   CoreML YOLO detection
-   iPhone LiDAR sensor

# 2. System Architecture

## 2.1 RGB Object Detection

The system uses YOLO through CoreML to detect objects and obtain:

-   Object category
-   Bounding box
-   Confidence score
-   Segmentation information

The detected ROI is mapped to LiDAR depth data.

## 2.2 Depth Acquisition and Point Cloud Reconstruction

ARKit LiDAR depth and camera intrinsics are used to reconstruct 3D
points.

Pipeline:

    Depth pixel
    ↓
    Camera intrinsics
    ↓
    3D coordinate reconstruction
    ↓
    Point cloud generation

The point cloud is processed with filtering and downsampling.

## 2.3 Generic Pose Estimation

The system explores model-free geometric pose estimation.

Estimated information:

-   Object center
-   Principal direction
-   Surface normal
-   Shape information
-   Confidence

Methods include:

-   Point cloud covariance analysis
-   PCA-based axis estimation
-   Pose stabilization


# 3. SurfaceObject and FreeObject Modes

## SurfaceObject Mode

For objects placed on flat surfaces, the system simplifies the problem.

Instead of estimating full 6DoF pose, it estimates:

    Object center
    +
    Surface constraint

This works because:

-   The plane provides a stable reference.
-   Gravity constrains orientation.
-   Full rotation estimation is unnecessary.

## FreeObject Mode

For hand-held objects, the system attempts:

    Point cloud
    ↓
    Geometric analysis
    ↓
    Principal axis estimation
    ↓
    Pose frame generation
    ↓
    AR replacement

# 4. Experimental Results

On-device trial logs from 2026-08-21 on iPhone (ARKit LiDAR, Unity 2022.3.62f3, YOLOv8n-seg CoreML). CSV and PLY live in `Documents/Trials/` and `Documents/PointCloud/`. Pose update was throttled to **2.0 Hz**. **No ground-truth pose** was recorded, so Translation Error and Rotation Error cannot be computed.

Recorded sessions are **near-range, already locked on**. Far-range dropouts seen in the Xcode console were mostly **not** inside these CSVs.

Logger success = YOLO box present + class matches the selected object + a valid geometric pose. That is **not** centimetre pose accuracy.

`cup_20260821_130834` lasted 1.5 s (4 frames) after the cup button was tapped again and is excluded from combined rates. Usable trials: cup `130837`, phone `130948`, laptop `131015`.

## 4.1 Per-trial summary

| Trial | Object | Duration (s) | Frames | Capture PLY | Class match | Logger success | Distance cam-Z (m) | YOLO conf (mean) | Pose mode |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- |
| cup_20260821_130834 | cup | 1.5 | 4 | 0 | 4/4 (100%) | 4/4 (100%) | 0.80–0.85 | 0.80 | SurfaceObject |
| cup_20260821_130837 | cup | 10.6 | 28 | 6 | 28/28 (100%) | 28/28 (100%) | 0.79–0.87 | 0.82 | SurfaceObject |
| phone_20260821_130948 | cell phone | 10.7 | 27 | 5 | 27/27 (100%) | 26/27 (96.3%) | 0.40 or 0.64 | 0.46 | SurfaceObject |
| laptop_20260821_131015 | laptop | 17.1 | 43 | 8 | 43/43 (100%) | 43/43 (100%) | 0.73–0.77 | 0.91 | SurfaceObject |
| **Usable combined** | 3 objects | 38.4 | **98** | **19** | **98/98 (100%)** | **97/98 (99.0%)** | 0.40–0.87 | — | SurfaceObject |

Phone failure: last frame `Center ROI`, 0 points, `waiting for LiDAR ROI`.

## 4.2 Detection

| Object | Detected class | YOLO ROI frames | Mean / median / min conf | Detect latency mean / max (ms) |
| --- | --- | ---: | --- | --- |
| cup | 41 cup | 28/28 | 0.820 / 0.819 / 0.791 | 13.6 / 19.1 |
| phone | 67 cell phone | 26/27 | 0.463 / 0.464 / 0.283 | 16.4 / 24.5 |
| laptop | 63 laptop | 43/43 | 0.909 / 0.907 / 0.890 | 14.6 / 21.7 |
| combined | — | 97/98 | — | 14.8 / 24.5 |

Replacement models actually loaded: cup → `酒 (scene)`; phone → `手持电话 (scene)`; laptop → `电脑 (real)`.

## 4.3 Point cloud

Voxel size 1 cm. Extent is the PCA bounding box of the **ROI cloud**, so it includes table points inside the YOLO box.

| Object | Raw points (mean) | Filtered points mean / min / max | Mean extent X/Y/Z (cm) | PLY files |
| --- | ---: | --- | --- | --- |
| cup | 1835 | 378 / 303 / 431 | 35.7 / 18.8 / 10.3 | 7 (incl. one capture before recording) |
| phone | 3010 | 260 / 243 / 290 | 30.0 / 15.4 / 11.5 | 5 |
| laptop | 16932 | 3288 / 3000 / 3618 | 109.6 / 67.8 / 57.0 | 8 |

A physical cup is closer to ~8 cm diameter; a phone ~15×7×0.8 cm; an open laptop ~32×22×25 cm. Measured extents are larger because the ROI still contains supporting surface.

## 4.4 Pose stability (not TE / RE)

World-center RMS is the scatter of the estimated center while recording. It is **repeatability**, not error versus ground truth.

| Object | World-center RMS (cm) | Consecutive cam-center Δ mean / median (cm) | Consecutive forward-axis Δ mean (deg) | Notes |
| --- | ---: | --- | ---: | --- |
| cup | 2.23 | 1.59 / 0.50 | 0.17 | Stable SurfaceObject, mask center flat |
| phone (all successful) | 11.78 | 6.66 / 0.46 | 0.29 | Bimodal: two anchors, not unimodal jitter |
| phone, z ≈ 0.41 m (n=18) | 0.19 | — | — | `mask bottom upright` only |
| phone, z ≈ 0.64 m (n=8) | 0.36 | — | — | `mask center flat` only |
| laptop | 0.91 | 1.04 / 0.83 | 0.21 | Densest cloud, most stable center |

## 4.5 Latency and rate

Realtime path times cloud + pose. YOLO runs on a separate 0.5 s timer. Capture `t_e2e` includes PLY export.

| Object | YOLO (ms) | Cloud (ms) | Pose (ms) | Realtime e2e median (ms) | Pose update rate |
| --- | ---: | ---: | ---: | ---: | --- |
| cup | 13.6 | 5.8 | 0.21 | 6.4 | 1.98 Hz |
| phone | 16.4 | 5.2 | 0.22 | 6.1 | 1.97 Hz |
| laptop | 14.6 | 24.0 | 0.56 | 26.3 | 1.99 Hz |

Stage times are well under the PRD 100 ms budget. The **published update rate is 2 Hz**, not 30 FPS, because of the demo throttle.

## 4.6 PRD metric checklist (this dataset only)

| PRD target | This dataset |
| --- | --- |
| FPS ≥ 30 | **Not met / not the measured quantity.** Pose loop ran at 2.0 Hz. |
| Latency ≤ 100 ms | Stage times 6–28 ms (laptop capture e2e max 52.6 ms). Frame interval remains 500 ms. |
| Translation Error ≤ 3 cm | **N/A.** No GT. World RMS: cup 2.2 cm, laptop 0.9 cm, phone 11.8 cm (bimodal). |
| Rotation Error ≤ 5° | **N/A.** No GT. Consecutive forward-axis change ~0.2°. |
| Detection success ≥ 95% | **99.0%** logger success on 98 near-range recorded frames. Far-range not in CSV. |
| Occlusion recover ≤ 0.5 s | **Not tested.** |
| Continuous run ≥ 30 min | **Not tested** (longest trial 17 s). One memory warning during laptop. |
| Crash rate 0 | Session completed; one invalid phone frame, no process crash in the log. |

## 4.7 Planar object screenshots


### Figure 1. Laptop detection and center estimation

![Planar Object](images/1.png)

Observation:

The system successfully performs:

-   Object detection
-   ROI extraction
-   Depth acquisition
-   Center estimation

Large objects provide more LiDAR points and more stable estimation.


### Figure 2. Smartphone detection and center estimation

![Planar Object](images/2.png)

Observation:

The smartphone can be detected and its approximate spatial position can
be obtained.

However, smaller objects generate fewer LiDAR points, increasing
uncertainty.


# 5. Hand-held Object Evaluation

## Figure 3. Hand-held smartphone detection

![Hand-held object](images/3.png)


## Figure 4. Hand-held smartphone point cloud visualization

![Hand-held object](images/4.png)

Observation:

The system can successfully obtain:

-   RGB detection
-   ROI extraction
-   LiDAR depth
-   Point cloud generation

However, the point cloud is insufficient for reliable pose estimation.


# 6. Limitations

## 6.1 Limited LiDAR Accuracy

The iPhone LiDAR sensor produces sparse and incomplete point clouds.

Problems:

-   Limited surface coverage
-   Missing geometry
-   Depth noise

Therefore, geometric methods such as PCA cannot reliably estimate object
orientation.

## 6.2 Hand Interference

Hand-held scenarios introduce additional points:

    Object points
    +
    Hand points
    +
    Background points

These points affect:

-   Center estimation
-   Axis estimation
-   Surface normal calculation

## 6.3 Object Size Influence

Large objects provide more depth samples.

Small objects such as smartphones are more challenging because fewer
LiDAR points are captured.


# 7. Comparison with Vuforia

| Aspect | Vuforia Model Target | RGB-LiDAR |
| --- | --- | --- |
| CAD model required | Yes | No |
| Object knowledge | Known | Potentially unknown |
| Pose estimation | Model matching | Geometry estimation |
| Generalization | Limited | Higher potential |
| Current accuracy | High | Limited by mobile LiDAR |


# 8. Strengths and Limitations

## Strengths

-   No CAD model requirement
-   Potential support for unknown objects
-   Uses mobile RGB-D hardware
-   More general AR replacement framework

## Limitations

-   Sparse LiDAR data
-   Difficult hand-object separation
-   Limited pose accuracy
-   Challenging model-free 6DoF estimation



# 9. Existing Work and Contribution

Existing technologies:

-   YOLO object detection
-   CoreML inference
-   ARKit LiDAR depth
-   Point cloud processing
-   PCA geometric analysis
-   ICP offline validation

Self-developed contribution:

-   RGB-LiDAR fusion pipeline
-   ROI-based depth extraction
-   SurfaceObject / FreeObject strategy
-   Generic pose estimation framework
-   Unity AR replacement workflow

This work does not propose a new pose estimation algorithm, but
evaluates the feasibility and limitations of a model-free AR replacement
pipeline.


# 10. Future Improvements

Possible directions:

-   Higher-resolution RGB-D sensors
-   Better object-hand segmentation
-   RGB-D neural pose estimation
-   FoundationPose investigation
-   Real-time ICP refinement
