# 软件架构设计文档（Software Architecture Design）

Version：1.0

---

# 1. 系统概述

## 1.1 系统目标

系统需要实现：

真实物体

↓

识别

↓

三维定位

↓

姿态估计

↓

AR模型替换

↓

持续跟踪

整个流程要求实时完成。

系统主要面向：

- AR认知训练
- 科研验证
- 后续论文实验

---

## 1.2 总体架构

```text
                   ┌──────────────────────────┐
                   │      RGB Camera          │
                   └────────────┬─────────────┘
                                │
                                ▼
                   ┌──────────────────────────┐
                   │     YOLO Detection       │
                   └────────────┬─────────────┘
                                │
                    Bounding Box / Class
                                │
                                ▼
                   ┌──────────────────────────┐
                   │      LiDAR Depth         │
                   └────────────┬─────────────┘
                                │
                  Depth + Camera Intrinsics
                                │
                                ▼
                   ┌──────────────────────────┐
                   │ Coordinate Recovery      │
                   └────────────┬─────────────┘
                                │
                                ▼
                   ┌──────────────────────────┐
                   │ Point Cloud Builder      │
                   └────────────┬─────────────┘
                                │
                                ▼
                   ┌──────────────────────────┐
                   │ Pose Estimation          │
                   │ ICP / FoundationPose     │
                   └────────────┬─────────────┘
                                │
                                ▼
                   ┌──────────────────────────┐
                   │ Tracking Module          │
                   └────────────┬─────────────┘
                                │
                                ▼
                   ┌──────────────────────────┐
                   │ Unity Renderer           │
                   └──────────────────────────┘
```

---

# 2. 模块划分

系统采用模块化架构。

模块之间禁止直接调用。

统一通过 Interface 通信。

```
Detection
↓

Geometry

↓

Pose

↓

Tracking

↓

Rendering
```

所有模块必须支持独立调试。

---

# 3. Detection Layer

## 功能

负责目标检测。

输入：

RGB Frame

输出：

```python
BoundingBox

class_id

confidence
```

接口：

```python
detect(frame)->DetectionResult
```

数据结构：

```python
DetectionResult

class_id

confidence

bbox

timestamp
```

错误：

```
NO_OBJECT

LOW_CONFIDENCE
```

日志：

```
Frame

Class

Confidence

Latency
```

---

# 4. Depth Layer

负责获取LiDAR数据。

输入：

ARKit

输出：

DepthMap

ConfidenceMap

CameraIntrinsics

接口：

```
get_depth()
```

输出：

```python
DepthResult

depth_map

intrinsics

timestamp
```

异常：

```
DepthUnavailable

LowConfidence
```

---

# 5. Geometry Layer

职责：

恢复三维坐标。

流程：

```
Pixel

↓

Depth

↓

Camera Matrix

↓

World Coordinate
```

接口：

```
pixel_to_world()
```

输出：

```
WorldPoint

x

y

z
```

第二部分：

PointCloud

接口：

```
build_point_cloud()
```

输出：

```
PointCloud
```

需要：

ROI Crop

Voxel DownSample

Outlier Removal

Normal Estimation

---

# 6. Pose Layer

职责：

计算6DoF Pose。

支持两种实现。

## ICP

输入：

PointCloud

CAD Model

输出：

```
Pose

Position

Rotation
```

接口：

```
estimate_pose_icp()
```

---

## FoundationPose

输入：

RGB

Depth

Mask

输出：

Pose

接口：

```
estimate_pose_network()
```

统一输出：

```
PoseResult

position

rotation

confidence

latency
```

---

# 7. Tracking Layer

输入：

上一帧Pose

当前Pose

输出：

Smooth Pose

算法：

Kalman Filter

支持：

Anchor更新

重新定位

Occlusion Recovery

接口：

```
update_pose()
```

状态：

```
TRACKING

LOST

RELOCALIZING
```

---

# 8. Rendering Layer

Unity负责渲染。

输入：

Pose

ModelID

输出：

AR Scene

模块：

Model Loader

Animation

Occlusion

Shadow

Model Manager

接口：

```
update_model_pose()
```

支持：

Position

Rotation

Scale

Animation

---

# 9. 数据流

RGB

↓

Detection

↓

Bounding Box

↓

Depth ROI

↓

Point Cloud

↓

Pose

↓

Tracking

↓

Unity

整个流程不得存在循环依赖。

---

# 10. 配置系统

所有参数必须配置化。

config.yaml

包括：

YOLO

Threshold

ICP

Voxel Size

Kalman

Noise

Tracking

Timeout

Unity

Scale

Model Path

禁止硬编码。

---

# 11. 日志系统

统一Logger。

级别：

INFO

WARNING

ERROR

DEBUG

日志包括：

FPS

Latency

Detection

Pose

Tracking

Memory

GPU

---

# 12. 错误处理

统一ErrorCode。

例如：

```
1001

YOLO_ERROR

1002

DEPTH_ERROR

1003

POSE_ERROR

1004

TRACKING_ERROR

1005

RENDER_ERROR
```

所有异常必须可恢复。

---

# 13. 性能要求

整体：

FPS≥30

YOLO

≤25ms

Depth

≤10ms

Geometry

≤15ms

Pose

≤35ms

Tracking

≤5ms

Unity

≤15ms

总延迟：

≤100ms

---

# 14. 测试接口

每个模块必须支持：

Mock Input

Replay

Unit Test

Performance Test

接口全部可独立测试。

---

# 15. 部署架构

Python

↓

Inference Server

↓

Unity

↓

iPhone

支持：

Debug

Release

Research

三种模式。