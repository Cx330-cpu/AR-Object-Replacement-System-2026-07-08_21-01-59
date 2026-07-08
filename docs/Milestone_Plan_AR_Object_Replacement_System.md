# Milestone Development Plan

Version：1.0

---

# 项目开发原则

本项目采用：

**Incremental Development（增量开发）**

每一个 Milestone 必须满足：

- 可以独立运行
- 可以单独测试
- 可以演示
- 可以回滚
- 可以持续集成（CI）

禁止跨 Milestone 开发。

必须上一阶段验收通过后进入下一阶段。

---

# 项目目录结构

```text
AR-Object-Replacement/

├── app/
│
├── detection/
│
├── depth/
│
├── geometry/
│
├── pointcloud/
│
├── pose/
│
├── tracking/
│
├── rendering/
│
├── config/
│
├── assets/
│
├── tests/
│
├── docs/
│
└── scripts/
```

---

# M0 环境搭建

预计：

3 Days

目标：

所有开发环境统一。

---

## Task 0.1

创建Git仓库

输出：

```
Git Repository
```

验收：

能够：

```
git clone
```

---

## Task 0.2

建立Python工程

安装：

```
Python3.11

PyTorch

OpenCV

Open3D

Ultralytics

NumPy

SciPy
```

输出：

```
requirements.txt
```

验收：

```
pip install -r requirements.txt
```

无错误。

---

## Task 0.3

建立Unity工程

版本：

Unity2022LTS

安装：

AR Foundation

ARKit Plugin

输出：

Unity Project

---

## Task 0.4

iPhone部署

要求：

手机能够：

运行Unity。

验收：

AR Camera正常。

---

## M0 完成交付

- Git
- Unity
- Python
- iPhone

Demo：

AR Camera正常显示。

---

# M1 Detection Module

预计：

1 Week

负责人：

Detection Team

模块：

YOLO

---

## Task 1.1

建立：

```
DetectionService
```

负责：

加载模型。

---

## Task 1.2

建立：

```
ModelManager
```

负责：

模型下载

缓存

热更新

---

## Task1.3

推理接口

统一：

```
detect(frame)
```

返回：

```
DetectionResult
```

---

## Task1.4

后处理

包括：

NMS

Confidence

Threshold

---

## Task1.5

Benchmark

测试：

FPS

Latency

Memory

---

## Task1.6

Visualization

绘制：

BoundingBox

Confidence

ClassName

---

## Task1.7

Unit Test

包括：

100张图片。

自动输出：

Precision

Recall

mAP

---

## 验收标准

FPS：

≥20

Latency：

≤30ms

mAP：

≥95%

Demo：

实时检测。

---

# M2 Depth Module

预计：

1 Week

---

## Task2.1

ARKit初始化

---

## Task2.2

获取：

DepthMap

---

## Task2.3

获取：

Confidence

---

## Task2.4

获取：

Camera Intrinsics

---

## Task2.5

Depth可视化

HeatMap

---

## Task2.6

点击屏幕

输出：

Distance

---

## 验收

误差：

≤2cm

Demo：

点击目标。

显示：

距离。

---

# M3 Geometry Module

目标：

恢复世界坐标。

---

## Task3.1

Pixel

↓

Camera

转换

---

## Task3.2

Depth

↓

World

转换

---

## Task3.3

BoundingBox

↓

Center

---

## Task3.4

World Coordinate

输出

---

## Task3.5

Unity

Sphere

验证

---

验收：

Sphere稳定跟随。

---

# M4 PointCloud Module

任务：

生成目标点云。

---

Task4.1

ROI Crop

---

Task4.2

Voxel DownSample

---

Task4.3

Outlier Removal

---

Task4.4

Normal Estimation

---

Task4.5

Visualization

---

验收：

Open3D

正常显示。

---

# M5 Pose Module

整个项目核心。

拆成：

ICP

FoundationPose

两条开发线。

---

ICP

Task5.1

CAD读取

---

Task5.2

ICP

Registration

---

Task5.3

Pose输出

---

FoundationPose

Task5.4

模型部署

---

Task5.5

推理

---

Task5.6

Pose输出

---

Task5.7

Benchmark

比较：

ICP

VS

FoundationPose

---

验收：

Translation

≤3cm

Rotation

≤5°

---

# M6 Rendering Module

Task6.1

Model Loader

---

Task6.2

Pose Update

---

Task6.3

Scale

---

Task6.4

Animation

---

Task6.5

Occlusion

---

Task6.6

Shadow

---

Demo：

模型替换。

---

# M7 Tracking Module

Task7.1

Kalman

---

Task7.2

Anchor

---

Task7.3

Occlusion Recovery

---

Task7.4

Pose Smooth

---

Task7.5

Stress Test

---

验收：

恢复：

≤0.5s

---

# M8 Experiment

Task8.1

Detection

Task8.2

Pose

Task8.3

Tracking

Task8.4

FPS

Task8.5

Latency

Task8.6

Memory

Task8.7

GPU

Task8.8

论文图表生成

---

# 最终交付

源码

Unity

iOS

Demo

论文

实验数据

技术文档

测试报告

API文档