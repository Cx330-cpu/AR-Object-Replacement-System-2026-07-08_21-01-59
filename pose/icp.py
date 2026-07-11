from __future__ import annotations

import time

import numpy as np
import open3d as o3d
from scipy.spatial.transform import Rotation

from pose.initialization import build_initial_transform
from pose.preprocessing import preprocess_point_cloud
from pose.types import PoseConfig, PoseResult


def _to_pose_result(
    transform: np.ndarray,
    fitness: float,
    inlier_rmse: float,
    runtime_ms: float,
    config: PoseConfig,
    is_valid: bool,
    message: str,
    scene_stats,
    model_stats,
) -> PoseResult:
    rotation = transform[:3, :3]
    translation = transform[:3, 3]
    scipy_rotation = Rotation.from_matrix(rotation)
    return PoseResult(
        is_valid=is_valid,
        transformation=transform.tolist(),
        translation_m=translation.tolist(),
        rotation_matrix=rotation.tolist(),
        quaternion_xyzw=scipy_rotation.as_quat().tolist(),
        euler_degrees=scipy_rotation.as_euler("xyz", degrees=True).tolist(),
        fitness=float(fitness),
        inlier_rmse=float(inlier_rmse),
        runtime_ms=float(runtime_ms),
        method="centroid_pca_point_to_point_point_to_plane_icp",
        coordinate_frame=config.coordinate_frame,
        transform_semantics=config.transform_semantics,
        message=message,
        scene_stats=scene_stats,
        model_stats=model_stats,
        model_metadata=config.model_metadata,
    )


def estimate_pose(
    scene_pointcloud: o3d.geometry.PointCloud,
    model_pointcloud: o3d.geometry.PointCloud,
    config: PoseConfig,
) -> PoseResult:
    started = time.perf_counter()
    scene, scene_stats = preprocess_point_cloud(scene_pointcloud, config.scene)
    model, model_stats = preprocess_point_cloud(model_pointcloud, config.model)

    if len(scene.points) < config.min_points:
        runtime_ms = (time.perf_counter() - started) * 1000.0
        return _to_pose_result(
            np.eye(4),
            0.0,
            float("inf"),
            runtime_ms,
            config,
            False,
            f"Scene point cloud has too few points: {len(scene.points)}",
            scene_stats,
            model_stats,
        )

    if len(model.points) < config.min_points:
        runtime_ms = (time.perf_counter() - started) * 1000.0
        return _to_pose_result(
            np.eye(4),
            0.0,
            float("inf"),
            runtime_ms,
            config,
            False,
            f"Model point cloud has too few points: {len(model.points)}",
            scene_stats,
            model_stats,
        )

    transform = build_initial_transform(
        model,
        scene,
        config.initialization_method,
        config.pca_sign_disambiguation,
    )
    fitness = 0.0
    inlier_rmse = float("inf")

    if config.point_to_point.enabled:
        point_to_point = o3d.pipelines.registration.registration_icp(
            model,
            scene,
            config.point_to_point.correspondence_threshold,
            transform,
            o3d.pipelines.registration.TransformationEstimationPointToPoint(),
            o3d.pipelines.registration.ICPConvergenceCriteria(
                max_iteration=config.point_to_point.max_iteration
            ),
        )
        transform = point_to_point.transformation
        fitness = point_to_point.fitness
        inlier_rmse = point_to_point.inlier_rmse

    if config.point_to_plane.enabled:
        point_to_plane = o3d.pipelines.registration.registration_icp(
            model,
            scene,
            config.point_to_plane.correspondence_threshold,
            transform,
            o3d.pipelines.registration.TransformationEstimationPointToPlane(),
            o3d.pipelines.registration.ICPConvergenceCriteria(
                max_iteration=config.point_to_plane.max_iteration
            ),
        )
        transform = point_to_plane.transformation
        fitness = point_to_plane.fitness
        inlier_rmse = point_to_plane.inlier_rmse

    runtime_ms = (time.perf_counter() - started) * 1000.0
    is_valid = np.isfinite(inlier_rmse) and inlier_rmse <= config.max_inlier_rmse
    message = "ICP completed"
    if not is_valid:
        message = f"ICP completed but RMSE is above threshold: {inlier_rmse:.4f}m"

    return _to_pose_result(
        transform,
        fitness,
        inlier_rmse,
        runtime_ms,
        config,
        is_valid,
        message,
        scene_stats,
        model_stats,
    )
