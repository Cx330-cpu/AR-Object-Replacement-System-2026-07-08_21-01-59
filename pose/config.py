from __future__ import annotations

from pathlib import Path
from typing import Any

import yaml

from pose.types import (
    FilterConfig,
    IcpStageConfig,
    ModelMetadata,
    PointCloudPreprocessConfig,
    PoseConfig,
)


def _filter_config(data: dict[str, Any] | None, default_enabled: bool = False) -> FilterConfig:
    data = data or {}
    return FilterConfig(
        enabled=bool(data.get("enabled", default_enabled)),
        nb_neighbors=int(data.get("nb_neighbors", 20)),
        std_ratio=float(data.get("std_ratio", 2.0)),
        nb_points=int(data.get("nb_points", 8)),
        radius=float(data.get("radius", 0.03)),
    )


def _preprocess_config(data: dict[str, Any] | None) -> PointCloudPreprocessConfig:
    data = data or {}
    return PointCloudPreprocessConfig(
        voxel_size=float(data.get("voxel_size", 0.005)),
        normal_radius=float(data.get("normal_radius", 0.03)),
        normal_max_nn=int(data.get("normal_max_nn", 30)),
        statistical_filter=_filter_config(data.get("statistical_filter")),
        radius_filter=_filter_config(data.get("radius_filter")),
    )


def _icp_stage(data: dict[str, Any] | None, threshold: float, iterations: int) -> IcpStageConfig:
    data = data or {}
    return IcpStageConfig(
        enabled=bool(data.get("enabled", True)),
        correspondence_threshold=float(data.get("correspondence_threshold", threshold)),
        max_iteration=int(data.get("max_iteration", iterations)),
    )


def load_pose_config(path: str | Path) -> PoseConfig:
    config_path = Path(path)
    with config_path.open("r", encoding="utf-8") as handle:
        data = yaml.safe_load(handle) or {}

    model_data = data.get("model", {})
    init_data = data.get("initialization", {})
    icp_data = data.get("icp", {})
    acceptance = data.get("acceptance", {})

    return PoseConfig(
        coordinate_frame=str(data.get("coordinate_frame", "camera")),
        transform_semantics=str(data.get("transform_semantics", "T_camera_from_model")),
        scene=_preprocess_config(data.get("scene")),
        model=_preprocess_config(model_data),
        model_metadata=ModelMetadata(
            unit=str(model_data.get("unit", "meter")),
            origin=str(model_data.get("origin", "centroid")),
            forward_axis=str(model_data.get("forward_axis", "+Z")),
            up_axis=str(model_data.get("up_axis", "+Y")),
            scale=float(model_data.get("scale", 1.0)),
        ),
        initialization_method=str(init_data.get("method", "centroid_pca")),
        pca_sign_disambiguation=bool(init_data.get("pca_sign_disambiguation", True)),
        point_to_point=_icp_stage(icp_data.get("point_to_point"), 0.04, 80),
        point_to_plane=_icp_stage(icp_data.get("point_to_plane"), 0.025, 50),
        min_points=int(acceptance.get("min_points", 30)),
        max_inlier_rmse=float(acceptance.get("max_inlier_rmse", 0.04)),
    )
