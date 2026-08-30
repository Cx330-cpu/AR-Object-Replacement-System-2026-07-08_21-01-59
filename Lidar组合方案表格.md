# CODEBASE_RESEARCH_AUDIT

**Document role:** IEEE VR 2027 paper-integration brief for a downstream writing agent.  
**Audit date:** 2026-08-21  
**Repo:** `/Users/tongbingwen/AR-Object-Replacement-System`  
**Evidence policy:** Use only this repo for the exploratory route. Do not invent FoundationPose numbers from this repo. Unconfirmed items are `UNKNOWN`. Not applicable items are `N/A`.

---

## 0. Instructions for the writing agent (read first)

The paper is a **multi-route technical comparison** for AR object replacement / pose / perception.

| Route | Paper role | Where the evidence lives | What it is allowed to claim |
|---|---|---|---|
| **FoundationPose workflow** | **Main method** | **Not this repository.** Use the FoundationPose experiment logs, CAD models, and TE/RE tables from that workflow. | 6DoF accuracy (TE/RE), model-based tracking, main Results |
| **RGB-LiDAR model-free geometric frame (this repo)** | **Exploratory side route** | This Unity/iOS codebase + `Documents/Trials/` + `Documents/PointCloud/` + `Lidar/` | Feasibility and limits of CAD-free on-device LiDAR placement |
| Offline Open3D ICP (this repo, Python) | Optional diagnostic / CAD sub-route | `pose/icp.py`, `tests/test_pose_icp.py` | Synthetic registration test only, unless a real scene+model pair is added |

**How to write the paper:**

1. Lead Methods and main Results with FoundationPose.
2. Introduce this repo as a **CAD-free mobile RGB-LiDAR baseline**: can a LiDAR iPhone produce a usable AR placement frame without a reference model?
3. Give this route a short Methods subsection, a small near-range table, and a limitations paragraph. Do not give it equal weight to FoundationPose.
4. Compare routes on **CAD required / on-device / update rate / failure modes / placement plausibility**. Compare **TE/RE only among model-based routes** (FoundationPose, optional ICP, Vuforia). Do not put this route’s world-center RMS in the same TE column as FoundationPose.
5. Architecture figures must not show FoundationPose running inside this Unity app. `docs/SAD_AR_Object_Replacement_System.md` draws that; **the code does not implement it.** Trust the code.

**Do not write:**

- This app “uses FoundationPose”.
- TE ≤ 3 cm or RE ≤ 5° for this geometric route.
- 30 FPS pose for this route (measured update rate is **2.0 Hz**).
- Mock detection mAP / 0.01 ms from `Logs/detection.log` or `scripts/evaluate_detection.py`.
- World-center RMS (0.91 cm laptop) as Translation Error.
- Far-range success rates (not in the CSVs).
- 99% logger success as detection mAP.

---

## 1. Paper positioning (author-confirmed)

**Main method:** FoundationPose workflow (external to this repo).  
**This repository:** exploratory CAD-free RGB-LiDAR geometric pose + Unity AR replacement on iPhone.

Research question for the side route only:

> Without a CAD / Model Target, can RGB detection + iPhone LiDAR yield a stable enough geometric frame to place a substitute AR model on a tabletop object, and where does that pipeline fail?

Existing near-range trials are **sufficient for that exploratory claim**. They are **not** sufficient for a main-method accuracy section.

---

## 2. What this repository actually implements

### 2.1 On-device pipeline (the exploratory route)

```text
ARKit RGB (1920×1440 in the recorded session)
  → CoreML YOLOv8n-seg, center / preferred-class object
  → ARKit LiDAR environment depth + intrinsics
  → YOLO bbox → depth ROI → camera-frame point cloud
      (range filter, 1 cm voxel, radius outlier)
  → PCA geometric frame + gravity / ARPlane
  → SurfaceObject (tabletop) or FreeObject (handheld)
  → light sign-hold / slerp (not Kalman)
  → Unity axes + GLB replacement
  → CSV trial log + optional ASCII PLY
```

Entry point: `Assets/Scripts/Demo/PointCloudCaptureDemo.cs` (`Install()` attaches to Main Camera).  
iOS detector: `Assets/Plugins/iOS/YoloCoreMLPlugin.mm` + `Assets/Scripts/Detection/YoloCoreMLDetector.cs`.  
Pose: `Assets/Scripts/Pose/GenericPoseEstimator.cs`, `GenericPoseStabilizer.cs`.  
Replacement: `Assets/Scripts/Rendering/ReplacementModelController.cs` (glTFast).  
Logging: `Assets/Scripts/Evaluation/TrialLogger.cs`.

**SurfaceObject** (used in all recorded trials): object center + supporting plane / gravity + typically face-camera yaw. This is **not** semantic 6DoF.  
**FreeObject:** PCA axes from the cloud; more fragile; not the recorded tabletop protocol.

### 2.2 Also in this repo, not the main paper method

| Piece | Status | Paper use |
|---|---|---|
| Offline Open3D ICP (`pose/icp.py`) | Implemented, desktop only | Optional CAD diagnostic; synthetic test TE &lt; 1 cm / RE &lt; 3° is **not** real LiDAR accuracy |
| Desktop Ultralytics YOLO demo | Implemented | Do not mix with iPhone CoreML numbers |
| FoundationPose | **Not in this repo** (no source, no submodule, no checkpoint) | Take from the main-method workflow |
| Kalman / M7 tracker | Not implemented; only slerp hold | Do not claim a tracker |
| Vuforia Model Target | Literature comparison only | Contrast in related work / discussion |
| AprilTag / pose GT | Missing here | Needed only if this side route is later asked for TE |

### 2.3 Runtime of the **recorded** 2026-08-21 session

Use these values in Results, not later untested editor defaults.

| Item | Value | Evidence |
|---|---|---|
| Unity | 2022.3.62f3 | device log; `ProjectSettings/ProjectVersion.txt` |
| Device GPU | Apple A18 Pro | Xcode log |
| Documented device | iPhone 16 Pro Max | `Lidar/RGB_LiDAR_Model_Free_Pose_Estimation_Evaluation.md` |
| AR Foundation / ARKit | 5.2.2 | `Packages/manifest.json` |
| Detector | `yolov8n-seg.mlpackage`, CoreML All | `[M1 YOLO] CoreML YOLO loaded...` |
| Camera | 1920×1440, 60 fps, LiDAR scene depth | ARKit log |
| Pose / YOLO interval during that build | **0.5 s → 2.0 Hz** | CSV `unity_time` spans |
| Voxel | 1 cm | CSV `voxel_size_m=0.0100` |
| Objects | cup (COCO 41), cell phone (67), laptop (63) | CSV |
| Replacement in those CSVs | cup → 酒 (scene); phone → 手持电话 (scene); laptop → 电脑 (real) | CSV `model_name` |

Later code still in the tree (higher YOLO resolution, 0.25 s interval, preferred-class, no center-ROI fallback) was **not** the build that produced `Documents/Trials/`. Do not describe those as measured Results unless a new trial is run.

---

## 3. Methods text this repo can support

Write as a compact exploratory subsection, not as the paper’s pose algorithm contribution. Suggested content (all have code evidence):

1. **RGB:** ARKit CPU image → CoreML YOLOv8n-seg; bounding box + mask center / mask-bottom anchors. Desktop Ultralytics is a separate demo.
2. **Depth:** ARKit environment depth (meters) + confidence + `XRCameraIntrinsics`. Pinhole unprojection in camera frame; image `y` is flipped (`CoordinateConverter.cs`).
3. **ROI cloud:** screen box mapped to depth image; 1 cm voxel; outlier removal. No full instance mask carving — **bbox/mask-anchor ROI still includes table**.
4. **Geometric frame:** covariance / PCA; robust center; shape labels (elongated / planar).
5. **SurfaceObject vs FreeObject:** tabletop uses plane + gravity and usually faces the user; handheld uses PCA. Recorded trials are SurfaceObject.
6. **Stabilization:** axis-sign consistency + quaternion slerp + short lost-hold. Not Kalman.
7. **AR replacement:** COCO class → GLB in `Assets/Resources/ReplacementModels/`; scale from measured extent.
8. **Optional offline ICP:** scene PLY vs reference PLY → `T_camera_from_model` (meters, quaternion **xyzw**). Not running on the phone.

Cite the evaluation draft for the intended scientific claim of this route: it does **not** propose a new pose solver; it evaluates feasibility of a model-free mobile replacement pipeline (`Lidar/RGB_LiDAR_Model_Free_Pose_Estimation_Evaluation.md`).

**Coordinate rules for anyone mixing routes:**

- This runtime: object axes in **camera frame** (meters), then Unity `TransformPoint` to world. Unity is **left-handed**.
- Offline ICP: **model-to-camera** `T_camera_from_model`.
- Do not dump SciPy xyzw into Unity without a dedicated converter (documented as not implemented).
- SurfaceObject forward is **face camera**, not CAD +Z.

---

## 4. Results this repo can support

**Protocol:** 2026-08-21 on-device; user selected 杯子 / 手机 / 电脑, then recorded while aimed at the object. Logger success = YOLO box + class match + valid geometric pose. That is **not** centimetre accuracy.

**Coverage:** near-range, already locked. Xcode logs of far-range YOLO loss / `Center_ROI` table contamination are **mostly outside** these CSVs. `cup_20260821_130834` is a 1.5 s abort (cup button tapped again); exclude it from combined rates.

Full numeric tables below. Duplicated in `Lidar/RGB_LiDAR_Model_Free_Pose_Estimation_Evaluation.md` §4. Raw files: `Documents/Trials/*.csv`, `Documents/PointCloud/*.ply`.

### 4.1 Trial file inventory

| File | Object | Frames | Class match | Success | Notes |
|---|---|---:|---:|---:|---|
| `Documents/Trials/cup_20260821_130834.csv` | cup | 4 | 4/4 | 4/4 | Abort 1.5 s; exclude from combined |
| `Documents/Trials/cup_20260821_130837.csv` | cup | 28 | 28/28 | 28/28 | 22 realtime + 6 capture |
| `Documents/Trials/phone_20260821_130948.csv` | phone | 27 | 27/27 | 26/27 | 22 realtime + 5 capture |
| `Documents/Trials/laptop_20260821_131015.csv` | laptop | 43 | 43/43 | 43/43 | 35 realtime + 8 capture |
| matching `*.summary.txt` | — | — | — | — | `class_match_rate_pct` / `success_rate_pct` |

### 4.2 Per-trial summary

| Trial | Object | Duration (s) | Frames | Capture PLY | Class match | Logger success | Distance cam-Z (m) | YOLO conf (mean) | Pose mode |
|---|---|---:|---:|---:|---:|---:|---|---:|---|
| cup_20260821_130834 | cup | 1.5 | 4 | 0 | 4/4 (100%) | 4/4 (100%) | 0.80–0.85 | 0.80 | SurfaceObject |
| cup_20260821_130837 | cup | 10.6 | 28 | 6 | 28/28 (100%) | 28/28 (100%) | 0.79–0.87 | 0.82 | SurfaceObject |
| phone_20260821_130948 | cell phone | 10.7 | 27 | 5 | 27/27 (100%) | 26/27 (96.3%) | 0.40 or 0.64 | 0.46 | SurfaceObject |
| laptop_20260821_131015 | laptop | 17.1 | 43 | 8 | 43/43 (100%) | 43/43 (100%) | 0.73–0.77 | 0.91 | SurfaceObject |
| **Usable combined** | 3 objects | 38.4 | **98** | **19** | **98/98 (100%)** | **97/98 (99.0%)** | 0.40–0.87 | — | SurfaceObject |

Phone: last frame failed (`Center ROI`, 0 points, `waiting for LiDAR ROI`). All successful frames are `SurfaceObject`. Replacement in CSV: cup → `酒 (scene)`; phone → `手持电话 (scene)`; laptop → `电脑 (real)`.

### 4.3 Detection

| Object | Detected class | YOLO ROI frames | Mean / median / min conf | Detect latency mean / max (ms) |
|---|---|---:|---|---|
| cup (130837) | 41 cup | 28/28 | 0.820 / 0.819 / 0.791 | 13.6 / 19.1 |
| phone | 67 cell phone | 26/27 | 0.463 / 0.464 / 0.283 | 16.4 / 24.5 |
| laptop | 63 laptop | 43/43 | 0.909 / 0.907 / 0.890 | 14.6 / 21.7 |
| usable combined | — | 97/98 | — | **14.8 / 24.5** |

### 4.4 Latency and update rate

Realtime path times cloud + pose. YOLO ran on a separate 0.5 s timer in the **recorded** build. Capture `t_e2e` includes PLY export.

| Object | YOLO (ms) | Cloud (ms) | Pose (ms) | Realtime e2e median (ms) | Pose update rate |
|---|---:|---:|---:|---:|---|
| cup | 13.6 | 5.8 | 0.21 | 6.4 | 1.98 Hz |
| phone | 16.4 | 5.2 | 0.22 | 6.1 | 1.97 Hz |
| laptop | 14.6 | 24.0 | 0.56 | 26.3 | 1.99 Hz |

Laptop capture e2e max **52.6 ms**. Stage times are under the PRD 100 ms budget. The **published update rate is 2.0 Hz**, not camera 60 FPS.

### 4.5 Point cloud (voxel 1 cm)

| Object | Raw points (mean) | Filtered points mean / min / max | Mean extent X/Y/Z (cm) | PLY files |
|---|---:|---|---|---|
| cup | 1835 | 378 / 303 / 431 | 35.7 / 18.8 / 10.3 | 7 (incl. one capture before recording) |
| phone | 3010 | 260 / 243 / 290 | 30.0 / 15.4 / 11.5 | 5 |
| laptop | 16932 | 3288 / 3000 / 3618 | 109.6 / 67.8 / 57.0 | 8 |

Physical sizes for discussion: cup ~8 cm diameter; phone ~15×7×0.8 cm; open laptop ~32×22×25 cm. Measured extents include table inside the YOLO ROI.

**PLY inventory (`Documents/PointCloud/`):**

| Object | Files | Vertices (header) |
|---|---|---|
| cup | `pointcloud_20260821_130826.ply`, `_130839` … `_130846` | 394, 307, 303, 415, 387, 408, 398 |
| phone | `_130950`, `_130953`, `_130955`, `_130957`, `_130959` | 254, 255, 254, 252, 257 |
| laptop | `_131018`, `_131020`, `_131021`, `_131022`, `_131025`, `_131026`, `_131028`, `_131031` | 3618, 3421, 3269, 3289, 3177, 3384, 3228, 3399 |
| latest | `Documents/latest_pointcloud.ply` | 3399 (copy of `_131031`) |

PLY is ASCII, camera-frame `x y z confidence`.

### 4.6 Pose stability (not TE / RE)

World-center RMS is scatter of the estimated center while recording. It is **repeatability**, not error versus ground truth. No `gt_*` columns were filled.

| Condition | World-center RMS (cm) | Consecutive cam-center Δ mean / median (cm) | Consecutive forward-axis Δ mean (deg) | Notes |
|---|---:|---|---:|---|
| cup (130837) | 2.23 | 1.59 / 0.50 | 0.17 | Stable SurfaceObject, mask center flat |
| phone (all successful) | 11.78 | 6.66 / 0.46 | 0.29 | Bimodal: two anchors, not unimodal jitter |
| phone, z ≈ 0.41 m (n=18) | 0.19 | — | — | `mask bottom upright` only |
| phone, z ≈ 0.64 m (n=8) | 0.36 | — | — | `mask center flat` only |
| laptop | 0.91 | 1.04 / 0.83 | 0.21 | Densest cloud, most stable center |

Phone 11.78 cm is **bimodal anchor switching**, not unimodal jitter. Within each mode it is sub-centimetre.

### 4.7 PRD metric checklist (this dataset only)

| PRD target | This dataset |
|---|---|
| FPS ≥ 30 | **Not met / not the measured quantity.** Pose loop ran at 2.0 Hz. |
| Latency ≤ 100 ms | Stage times 6–28 ms (laptop capture e2e max 52.6 ms). Frame interval remains 500 ms. |
| Translation Error ≤ 3 cm | **N/A.** No GT. World RMS: cup 2.2 cm, laptop 0.9 cm, phone 11.8 cm (bimodal). |
| Rotation Error ≤ 5° | **N/A.** No GT. Consecutive forward-axis change ~0.2°. |
| Detection success ≥ 95% | **99.0%** logger success on 98 near-range recorded frames. Far-range not in CSV. |
| Occlusion recover ≤ 0.5 s | **Not tested.** |
| Continuous run ≥ 30 min | **Not tested** (longest trial 17 s). One memory warning during laptop. |
| Crash rate 0 | Session completed; one invalid phone frame, no process crash in the log. |

### 4.8 What Results must not claim from this dataset

| Claim | Status |
|---|---|
| Translation Error | **N/A** (no GT) |
| Rotation Error | **N/A** (no GT; SurfaceObject yaw is face-camera) |
| Far-range success | **Not in CSV** (console showed YOLO loss → table ROI) |
| Occlusion recovery | Not tested |
| 30-minute run | Not tested (longest trial 17 s; one memory warning on laptop) |
| 30 FPS pipeline | False for this route (2.0 Hz) |
| PRD TE ≤ 3 cm / RE ≤ 5° | Not evidenced here |

Qualitative figures: `Lidar/images/1.PNG`–`4.PNG` (markdown links `1.png`; files are `.PNG`). Usable as teaser / side-route illustration, not as quantitative Results.

---

## 5. Discussion this repo can support

**Role relative to FoundationPose**

FoundationPose is the model-based 6DoF main path. This route is the CAD-free mobile probe: it shows that RGB + iPhone LiDAR can attach a substitute model on a near tabletop object, and that the bottleneck is detection at distance, ROI table leak, sparse small-object clouds, and missing semantic front — not a missing neural pose head in this app.

**Strengths (code-backed)**

- No CAD / Vuforia Model Target required at runtime.
- SurfaceObject uses plane + gravity so sparse LiDAR is not asked for full orientation.
- Runs entirely on device; YOLO ~15 ms; cloud+PCA tens of milliseconds.
- Larger objects (laptop) yield denser clouds and more stable centers.

**Limitations (must stay limitations)**

- Single-view consumer LiDAR is incomplete.
- YOLO n-seg + center/ROI protocol fails when the object is small or far (phone already weak at ~0.4–0.6 m; far-range not quantified in CSV).
- ROI includes supporting surface; extents are inflated.
- No semantic front; SurfaceObject faces the user.
- Phone center jumps between mask-bottom and mask-center anchors.
- No GT, so this route cannot enter a TE/RE leaderboard.

**Fair comparison axes vs FoundationPose**

| Axis | This route | FoundationPose (main) |
|---|---|---|
| CAD / reference model | No at runtime | Yes |
| Output | Geometric frame + AR overlay | Object 6DoF (`T` vs model) |
| Accuracy metric | Logger success, latency, Hz, failure modes, optional jitter | TE (cm), RE (deg) |
| Where it runs | LiDAR iPhone, Unity | Whatever the main workflow used — **do not copy from this repo** |
| Update rate (this build) | 2.0 Hz | From FoundationPose logs only |

Unfair: one table mixing FoundationPose TE with this route’s 99% logger success or 0.91 cm RMS.

---

## 6. Evidence index

| Path | Use |
|---|---|
| `Assets/Scripts/Demo/PointCloudCaptureDemo.cs` | On-device orchestrator |
| `Assets/Plugins/iOS/YoloCoreMLPlugin.mm` | CoreML YOLO-seg |
| `Assets/Scripts/Pose/GenericPoseEstimator.cs` | PCA frame |
| `Assets/Scripts/Pose/GenericPoseStabilizer.cs` | Slerp / sign hold |
| `Assets/Scripts/Rendering/ReplacementModelController.cs` | GLB placement |
| `Assets/Scripts/Evaluation/TrialLogger.cs` | CSV schema / success definition |
| `Documents/Trials/*.csv` + `*.summary.txt` | Side-route numbers |
| `Documents/PointCloud/*.ply` | Side-route clouds |
| `Lidar/RGB_LiDAR_Model_Free_Pose_Estimation_Evaluation.md` | Side-route write-up + filled tables |
| `Lidar/images/*.PNG` | Qualitative figures |
| `pose/icp.py`, `tests/test_pose_icp.py` | Offline ICP only |
| `docs/SAD_AR_Object_Replacement_System.md` | **Do not trust** Pose Layer = FoundationPose |
| `Logs/detection.log`, `scripts/evaluate_detection.py` | **Mock — never cite as Results** |
| FoundationPose code / logs | **Not here** |

**Doc vs code (trust code):**

1. SAD / Milestone draw FoundationPose in this app — **absent**.
2. M6 said no glTFast / no GLB in `Assets/` — glTFast v5.0.4 and `Assets/Resources/ReplacementModels/*.glb` exist.
3. M1 sometimes names `yolov8n.mlpackage` — device loaded **yolov8n-seg**.
4. PRD FPS ≥ 20 vs ≥ 30 — this route measured **2 Hz**.

Licenses: no root `LICENSE`. Ultralytics is typically AGPL-3.0. GLB provenance UNKNOWN. Cite YOLO, ARKit, Open3D, Unity AR Foundation, glTFast; cite FoundationPose from the main-method paper/code, not from this repo.

---

## 7. Optional extra data (not required for the side-route paragraph)

If time remains, one far-range **failure** recording (same 杯子/手机/电脑 protocol, walk back, keep CSV running) would document the dropout that currently lives only in the console. It is not required to keep this route exploratory.

Do **not** block the paper on GT for this route. GT/TE/RE belong to FoundationPose.

---

## 8. Suggested paper skeleton (this route only)

**Methods (short):** CAD-free RGB-LiDAR geometric frame on iPhone: YOLO-seg ROI → LiDAR cloud → PCA + SurfaceObject (plane/gravity, face-camera) → GLB overlay. Distinct from the FoundationPose main pipeline.

**Results (short table):** Near tabletop, 3 classes, 98 frames, 97/98 logger success, YOLO 14.8 ms, 2.0 Hz, laptop denser/more stable than phone; no TE/RE. Use §4 tables, do not invent extra numbers.

**Discussion:** Useful as a CAD-free on-device baseline; fails at distance / small objects / table leak / semantic heading — which is why the main path is FoundationPose.

---

## 9. Repository identity (code evidence)

| Field | Finding | Status | Evidence |
|---|---|---|---|
| repo name | `AR-Object-Replacement-System` | implemented | `ProjectSettings/ProjectSettings.asset` L16; remote `https://github.com/Cx330-cpu/AR-Object-Replacement-System-2026-07-08_21-01-59.git` |
| repo purpose | 面向科研验证的 AR 物体替换平台：RGB 检测 + LiDAR 深度 + 三维恢复 + 几何姿态 + Unity 替换 | partially implemented | `docs/PRD_AR_Object_Replacement_System.md`; `docs/SAD_AR_Object_Replacement_System.md` |
| owner / author | Git: `Cx330-cpu <ssybt4@nottingham.edu.cn>`；Unity `companyName: Brandon`；bundle `com.Brandon.AR-Object-Replacement-System`；工作区用户 `tongbingwen`。三者关系 UNKNOWN | unknown | git log; `ProjectSettings/ProjectSettings.asset` |
| this-repo route name | **RGB-LiDAR Model-Free Geometric Pose for AR Object Replacement** + offline ICP | implemented (runtime geometric) + implemented (offline ICP) | `Lidar/RGB_LiDAR_Model_Free_Pose_Estimation_Evaluation.md`; `docs/M5_Pose_Module_Delivery.md` |
| paper role | **Exploratory side route.** Main method is FoundationPose **outside this repo**. | — | author-confirmed 2026-08-21 |
| FoundationPose in this repo | **not present** | planned / literature-only in SAD | 无源码、无 submodule、无 checkpoint |
| 与 AR/VR/pose 的关系 | AR / perception / pose / replacement；无自研 VR runtime；无独立 tracker | — | `Packages/manifest.json` `arfoundation`, `arkit` |

| Route | Objective | Status | Trust |
|---|---|---|---|
| A. On-device RGB-LiDAR geometric frame (`SurfaceObject` / `FreeObject`) | 无 CAD 的局部坐标系 + AR overlay | **implemented** | 代码 > 文档 |
| B. Offline Open3D ICP (`T_camera_from_model`) | 场景点云对参考模型的 6DoF 配准 | **implemented**（离线） | 代码 |
| C. Desktop Ultralytics YOLO | Mac/PC demo | **implemented** | 代码 |
| D. FoundationPose | 网络 6DoF **主方法** | **not in this repo** | 主方法工作流，不在这里评测 |
| E. Kalman / M7 | 连续跟踪 | **planned**；runtime 仅 slerp hold | 文档 vs `GenericPoseStabilizer.cs` |
| F. Vuforia Model Target | 对比基线 | **literature-only** | PRD L21–34 |

**文档 vs 代码（更信任代码）：**

1. SAD L79、L336–354 把 FoundationPose 画进本 App Pose Layer。代码中没有。论文架构图不要照抄 SAD。
2. M6 曾写无 glTFast / GLB 不在 `Assets/`。实际 `Packages/manifest.json` 有 `com.atteneder.gltfast` v5.0.4，`Assets/Resources/ReplacementModels/*.glb` 存在。
3. M1 有时写 `yolov8n.mlpackage`。设备日志加载的是 **yolov8n-seg.mlpackage**。
4. PRD FR-1 FPS ≥ 20，NFR ≥ 30。本路线实测姿态更新 **2.0 Hz**。不要混用。

---

## 10. On-device pipeline stages

```text
ARKit RGB CPU image
  -> CoreML YOLOv8n-seg (center / preferred-class + mask anchors)
  -> ARKit LiDAR depth + confidence + camera intrinsics
  -> ROI crop (YOLO bbox or center fallback)
  -> camera-coordinate point cloud (filter / voxel / outlier)
  -> GenericPoseEstimator (PCA + gravity)
  -> GenericPoseStabilizer (sign / slerp / tracking-lost hold)
  -> SurfaceObject or FreeObject
  -> Unity AR axes / GLB replacement
  -> CSV trial log + optional PLY
```

| Stage | Key files | Status | Input | Output |
|---|---|---|---|---|
| RGB | `PointCloudCaptureDemo.TryDetectCenterObject` | implemented (iOS only) | AR CPU image | RGBA bytes |
| Detection | `YoloCoreMLDetector`; `YoloCoreMLPlugin.mm` | implemented on iOS; Editor returns false | RGBA, conf, IoU | `DetectionResult` |
| Depth | `AROcclusionManager.TryAcquireEnvironmentDepthCpuImage`; `CreateDepthFrame` | implemented | LiDAR XRCpuImage | `DepthFrame` meters |
| Intrinsics | `cameraManager.TryGetIntrinsics` | implemented | ARKit | `XRCameraIntrinsics` |
| ROI | `BoundingBoxMapper.ScreenRectToImageRoi` / `ExpandAndClip` | implemented | screen bbox | `RectInt` |
| Point cloud | `PointCloudBuilder`; `PointCloudCleaner`; `PointCloudDownSampler` | implemented | DepthFrame + ROI | camera-frame XYZ (m) |
| Generic pose | `GenericPoseEstimator.Estimate` | implemented | cloud + world-up | `GenericPoseFrame` |
| Mode | `EstimateRuntimePoseFrame`; `BuildSurfaceObjectFrame` | implemented | PCA + ARPlane | SurfaceObject / FreeObject |
| Stabilization | `GenericPoseStabilizer.Update` | partial (not Kalman) | current/previous | smoothed / hold |
| Unity transform | `ReplacementModelController.UpdateModel` | implemented; recorded CSV had replacement on | pose + classId | Unity TRS |
| Export / log | `TrialLogger`; `CapturePointCloud`; PLY exporter | implemented | pose / cloud | `Documents/Trials/*.csv`, `Documents/PointCloud/*.ply` |
| FoundationPose | N/A in this repo | main method elsewhere | — | — |

### Offline ICP (sub-route)

```text
M4 .ply scene + reference .ply
  -> voxel / statistical / normals
  -> centroid + PCA init
  -> point-to-point ICP then point-to-plane ICP
  -> PoseResult JSON + aligned PLY
```

Evidence: `pose/icp.py` `estimate_pose`; `scripts/estimate_pose_icp.py`. **Not** on iPhone.

Desktop YOLO: `app/detection_demo.py` — parallel to CoreML, not the same runtime.

---

## 11. Inputs and outputs

| Input | Status | Format / unit | Evidence |
|---|---|---|---|
| RGB | implemented | `XRCpuImage` → RGBA32 | demo `TryAcquireLatestCpuImage` |
| LiDAR depth | implemented | meters (or mm×0.001) | `ReadDepthMeters` |
| Mask | partial | mask center / mask bottom screen pixels; no full mask file | plugin; `DetectionResult` |
| Intrinsics | implemented | fx, fy, cx, cy | `PixelToCameraPoint` |
| Extrinsics | partial | AR camera Unity transform | `TransformPoint` |
| CAD / mesh | partial | GLB visuals; ICP needs reference `.ply` | `Assets/Resources/ReplacementModels/`; `模型/` |
| Object size | implemented | `ExtentMeters` | estimator; replacement scale |
| Calibration files | missing | ARKit only | no AprilTag script |
| Network stream | N/A | — | no sockets |
| Dataset / GT pose | missing here | — | TE/RE belong to FoundationPose |

| Output | Status | Format / unit | Evidence |
|---|---|---|---|
| 3D position | implemented | camera-frame meters + world | `CenterCamera`; CSV `center_cam_*` / `center_world_*` |
| 6DoF | partial | orthonormal axes, **not semantic 6DoF**; ICP has 4×4 | `GenericPoseFrame`; `PoseResult` |
| Quaternion | ICP **xyzw**; runtime Unity `LookRotation` | `pose/icp.py` |  |
| CSV | implemented | per-frame trial log | `Documents/Trials/*.csv` |
| PLY | implemented | ASCII x y z confidence, meters, camera frame | `Documents/PointCloud/*.ply` |
| AR overlay | implemented | axes, sphere, GLB | demo; replacement controller |
| Mock detection log | ignore | `Logs/detection.log` ~0.01 ms | **not Results** |

---

## 12. Key files

| File | Role |
|---|---|
| `Assets/Scripts/Demo/PointCloudCaptureDemo.cs` | Main Unity orchestrator |
| `Assets/Plugins/iOS/YoloCoreMLPlugin.mm` | CoreML YOLO-seg + NMS |
| `Assets/Scripts/Detection/YoloCoreMLDetector.cs` | P/Invoke wrapper |
| `Assets/Scripts/Detection/BoundingBoxMapper.cs` | Screen bbox → depth ROI |
| `Assets/Scripts/Evaluation/TrialLogger.cs` | CSV + summary |
| `Assets/Scripts/Evaluation/TrialObjectKind.cs` | Cup=41, Phone=67, Laptop=63 |
| `Assets/Scripts/Depth/ARDepthCrosshairMeasure.cs` | Center depth |
| `Assets/Scripts/Geometry/CoordinateConverter.cs` | Pixel→camera, y-flip |
| `Assets/Scripts/PointCloud/PointCloudBuilder.cs` | Depth ROI → XYZ |
| `Assets/Scripts/PointCloud/PointCloudCleaner.cs` | Range / outlier |
| `Assets/Scripts/PointCloud/PointCloudDownSampler.cs` | Voxel |
| `Assets/Scripts/PointCloud/PointCloudExporter.cs` | ASCII PLY |
| `Assets/Scripts/Pose/GenericPoseEstimator.cs` | PCA frame |
| `Assets/Scripts/Pose/GenericPoseStabilizer.cs` | Sign + slerp |
| `Assets/Scripts/Rendering/ReplacementModelController.cs` | GLB placement |
| `Assets/Scripts/Rendering/ReplacementModelMapper.cs` | COCO → model name |
| `pose/icp.py` | Offline ICP |
| `pose/metrics.py` | TE / RE helpers (**need GT; unused by side-route CSV**) |
| `tests/test_pose_icp.py` | Synthetic ICP only |
| `scripts/evaluate_detection.py` | **Mock** P/R/mAP — never cite |
| `detection/benchmark.py` | **Mock** FPS — never cite |

Unity entry: `PointCloudCaptureDemo.Install()` on Main Camera. No Python `main.py` for the AR route.

---

## 13. Dependencies and versions

| Item | Finding | Evidence |
|---|---|---|
| Unity | **2022.3.62f3** | `ProjectSettings/ProjectVersion.txt`; device log |
| AR Foundation / ARKit | 5.2.2 | `Packages/manifest.json` |
| glTFast | v5.0.4 | `Packages/manifest.json` |
| YOLO | Ultralytics ≥8; iOS `yolov8n-seg.mlpackage` | plugin; StreamingAssets |
| Open3D | ≥0.18 | `requirements.txt` |
| SciPy | used by ICP but **not listed** in `requirements.txt` | `pose/icp.py` |
| FoundationPose | **not in this repo** | — |
| Python | docs say 3.11; not pinned | Milestone |
| iOS min | 15.0; `iOSRequireARKit: 1` | ProjectSettings |
| Unity Test Framework | not installed | Packages |
| LICENSE | missing | repo root |

---

## 14. Hardware and runtime assumptions

| Assumption | Status | Evidence |
|---|---|---|
| LiDAR iPhone | required for this route | M2; demo occlusion manager |
| Recorded device | Apple A18 Pro GPU; docs say iPhone 16 Pro Max | Xcode log; Lidar md |
| Camera in recorded session | 1920×1440, 60 fps, scene depth | ARKit log |
| Editor LiDAR / YOLO | insufficient / unavailable | M2; `YoloCoreMLDetector` Editor false |
| Recorded pose interval | **0.5 s → 2.0 Hz** | CSV `unity_time` |
| Current tree (untested vs that CSV) | 0.25 s interval, 960×720 YOLO, conf 0.12 | `PointCloudCaptureDemo.cs` fields — **do not report as measured** |
| Network | N/A | no sockets |

---

## 15. Algorithms actually used vs not

| Item | In this repo? | Notes |
|---|---|---|
| YOLOv8-seg CoreML | yes (iOS) | recorded session |
| YOLOv8 detect Ultralytics | yes (desktop only) | do not mix numbers |
| NMS + center / preferred class | yes | plugin |
| ARKit LiDAR + pinhole unprojection | yes | y-flip |
| Voxel + radius outlier | yes | 1 cm in CSV |
| PCA eigenframe | yes | Jacobi in estimator |
| SurfaceObject gravity / ARPlane | yes | all recorded trials |
| Sign flip + slerp | yes | not Kalman |
| Open3D ICP | yes, offline | synthetic test only for TE/RE |
| FoundationPose | **no** | main method elsewhere |
| SAM / PnP / AprilTag / NeRF | no | — |
| Kalman | no | planned M7 |
| Vuforia | no | literature |

Checkpoints: Ultralytics COCO YOLOv8n / n-seg. No custom training in repo. GLB license UNKNOWN.

---

## 16. Ground truth

| Mechanism | Status |
|---|---|
| GT pose files | **missing** in this repo |
| AprilTag / ArUco | **missing** (only suggested in M5) |
| Synthetic GT | ICP unit test only (`tests/test_pose_icp.py`, TE &lt; 0.01 m, RE &lt; 3°) |
| Side-route CSV | centers/axes logged, **no `gt_*`** |

`pose/metrics.py` can compute `||Δt||` and geodesic RE, but there is **no GT input pipeline** for the iPhone trials. Do not compute fake TE from world RMS.

For the paper: **TE/RE are FoundationPose’s job.** This route reports logger success / latency / Hz / jitter / failures.

---

## 17. Evaluation scripts (what is paper-ready)

| Asset | Measures | Paper-ready? |
|---|---|---|
| `Documents/Trials/*.csv` | on-device class match, success, latency, pose, points | **yes, for the side route** (§4) |
| `Documents/PointCloud/*.ply` | captured clouds | diagnostic / qualitative |
| `tests/test_pose_icp.py` | synthetic ICP TE/RE | implementation check only |
| `scripts/evaluate_detection.py` | mock P/R/mAP on black images | **no** |
| `scripts/benchmark_detection.py` | mock detect() time | **no** |
| `Logs/detection.log` | ~0.01 ms | **no** |
| `Lidar/images/*.PNG` | screenshots | qualitative figures only |
| Unity capture `fps=1/dt` | single-shot | **not streaming FPS** |

---

## 18. Paper-ready metrics feasibility (this repo)

| Metric | Status | Evidence | Unit | Notes |
|---|---|---|---|---|
| Translation Error | missing (real); synthetic ICP only | CSV has centers, no GT | cm | **Do not fill from this repo** |
| Rotation Error | missing (real); synthetic ICP only | CSV has axes, no GT | deg | SurfaceObject yaw is face-camera |
| Latency | **measured** | CSV `t_detect_ms` etc. | ms | YOLO mean **14.8 ms** (max 24.5) |
| Update rate | **measured 2.0 Hz** | CSV unity_time | Hz | not 30 FPS |
| Success rate | **measured 97/98 = 99.0%** | logger definition, near-range | % | far-range not in CSV |
| Pose jitter | **measured (repeatability)** | world RMS | cm / deg | laptop **0.91 cm**; phone bimodal 11.78 cm |
| Robustness (occlusion / far) | missing in CSV | console anecdotal | — | optional extra failure trial |
| ICP registration time | computable if run | `PoseResult.runtime_ms` | ms | not on-device |
| Network latency | N/A | — | — | — |
| GPU / VRAM | missing | — | — | Instruments if needed |

---

## 19. Pose convention (error-calculation risk)

| Topic | Finding |
|---|---|
| Translation unit | meters |
| Runtime pose | axes in **camera frame**, then Unity world via `TransformPoint` |
| ICP pose | **model-to-camera** `T_camera_from_model` |
| Quaternion | ICP **xyzw** (SciPy); do not assign raw to Unity |
| Handedness | Unity **left-handed**; unprojection `y = -(v-cy)*Z/fy` |
| SurfaceObject forward | default **face camera** (`Vector3.back`), not CAD +Z |
| PCA sign | stabilizer flips to match previous frame |

Do not report Rotation Error for SurfaceObject as object heading accuracy.

---

## 20. Failure modes and credibility risks

Documented limits (keep as Discussion, not as solved):

- Sparse single-view LiDAR
- Hand / table points in ROI
- No semantic front without CAD
- Small objects, few LiDAR points
- Phone mask-bottom vs mask-center center jump
- Far-range YOLO drop (console; not in CSV)

Code fragility:

- Editor YOLO always false → center ROI
- No full mask carving, only bbox/anchor ROI
- SciPy missing from `requirements.txt`
- `outputs/` gitignored for ICP JSON

**Do not:**

1. Cite mock mAP / 0.01 ms.
2. Treat ICP fitness as pose accuracy.
3. Treat face-camera as 6DoF heading.
4. Treat capture-button FPS as system FPS.
5. Draw FoundationPose inside this Unity app.
6. Put 0.91 cm RMS in the FoundationPose TE column.

---

## 21. Reproducible commands (not executed in this audit)

| Purpose | Command |
|---|---|
| Python deps | `pip install -r requirements.txt` (add scipy for ICP) |
| Unit tests | `python3 -m unittest discover -s tests` |
| Desktop YOLO | `python3 app/detection_demo.py` |
| View PLY | `python scripts/view_pointcloud.py Documents/PointCloud/<file>.ply` |
| Offline ICP | `python scripts/estimate_pose_icp.py --scene ... --model ...` |
| On-device | Unity 2022.3.62f3 iOS build, LiDAR iPhone, `samplescene.unity`; CSV in Files app `Documents/Trials` |

Device paths: `Application.persistentDataPath/Trials/`, `.../PointCloud/`. Copied into repo as `Documents/`.

---

## 22. Existing result files

| Path | Type | Paper use |
|---|---|---|
| `Documents/Trials/*.csv` + `*.summary.txt` | on-device side-route log | **yes** — §4 |
| `Documents/PointCloud/*.ply` | ASCII clouds | diagnostic; extents include table |
| `Lidar/RGB_LiDAR_Model_Free_Pose_Estimation_Evaluation.md` | filled qualitative + tables | Methods/Discussion + side Results |
| `Lidar/images/1.PNG`–`4.PNG` | screenshots | qualitative; md links `1.png` |
| `Logs/detection.log` | mock | **no** |
| `outputs/` | gitignored | no saved ICP JSON |
| FoundationPose logs | not in this repo | **main Results elsewhere** |

