from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Any

import numpy as np


@dataclass
class FilterConfig:
    enabled: bool = False
    nb_neighbors: int = 20
    std_ratio: float = 2.0
    nb_points: int = 8
    radius: float = 0.03


@dataclass
class PointCloudPreprocessConfig:
    voxel_size: float = 0.005
    normal_radius: float = 0.03
    normal_max_nn: int = 30
    statistical_filter: FilterConfig = field(default_factory=lambda: FilterConfig(enabled=True))
    radius_filter: FilterConfig = field(default_factory=FilterConfig)


@dataclass
class ModelMetadata:
    unit: str = "meter"
    origin: str = "centroid"
    forward_axis: str = "+Z"
    up_axis: str = "+Y"
    scale: float = 1.0


@dataclass
class IcpStageConfig:
    enabled: bool = True
    correspondence_threshold: float = 0.04
    max_iteration: int = 80


@dataclass
class PoseConfig:
    coordinate_frame: str = "camera"
    transform_semantics: str = "T_camera_from_model"
    scene: PointCloudPreprocessConfig = field(default_factory=PointCloudPreprocessConfig)
    model: PointCloudPreprocessConfig = field(default_factory=PointCloudPreprocessConfig)
    model_metadata: ModelMetadata = field(default_factory=ModelMetadata)
    initialization_method: str = "centroid_pca"
    pca_sign_disambiguation: bool = True
    point_to_point: IcpStageConfig = field(default_factory=IcpStageConfig)
    point_to_plane: IcpStageConfig = field(
        default_factory=lambda: IcpStageConfig(
            enabled=True,
            correspondence_threshold=0.025,
            max_iteration=50,
        )
    )
    min_points: int = 30
    max_inlier_rmse: float = 0.04


@dataclass
class PointCloudStats:
    raw: int = 0
    finite: int = 0
    voxel: int = 0
    statistical: int = 0
    radius: int = 0
    final: int = 0
    aabb_extent: list[float] = field(default_factory=list)


@dataclass
class PoseResult:
    is_valid: bool
    transformation: list[list[float]]
    translation_m: list[float]
    rotation_matrix: list[list[float]]
    quaternion_xyzw: list[float]
    euler_degrees: list[float]
    fitness: float
    inlier_rmse: float
    runtime_ms: float
    method: str
    coordinate_frame: str
    transform_semantics: str
    message: str = ""
    scene_stats: PointCloudStats | None = None
    model_stats: PointCloudStats | None = None
    model_metadata: ModelMetadata | None = None

    def to_dict(self) -> dict[str, Any]:
        data = asdict(self)
        data["transform_model_to_camera"] = data["transformation"]
        return data


def identity_transform() -> np.ndarray:
    return np.eye(4, dtype=float)
