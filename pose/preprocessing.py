from __future__ import annotations

import copy

import numpy as np
import open3d as o3d

from pose.types import PointCloudPreprocessConfig, PointCloudStats


def clone_point_cloud(point_cloud: o3d.geometry.PointCloud) -> o3d.geometry.PointCloud:
    return copy.deepcopy(point_cloud)


def remove_invalid_points(point_cloud: o3d.geometry.PointCloud) -> o3d.geometry.PointCloud:
    points = np.asarray(point_cloud.points)
    if points.size == 0:
        return point_cloud

    mask = np.isfinite(points).all(axis=1)
    if point_cloud.has_colors():
        colors = np.asarray(point_cloud.colors)[mask]
    else:
        colors = None
    if point_cloud.has_normals():
        normals = np.asarray(point_cloud.normals)[mask]
    else:
        normals = None

    cleaned = o3d.geometry.PointCloud()
    cleaned.points = o3d.utility.Vector3dVector(points[mask])
    if colors is not None:
        cleaned.colors = o3d.utility.Vector3dVector(colors)
    if normals is not None:
        cleaned.normals = o3d.utility.Vector3dVector(normals)
    return cleaned


def estimate_normals(
    point_cloud: o3d.geometry.PointCloud,
    radius: float,
    max_nn: int,
) -> o3d.geometry.PointCloud:
    if len(point_cloud.points) < 3:
        return point_cloud
    point_cloud.estimate_normals(
        search_param=o3d.geometry.KDTreeSearchParamHybrid(radius=radius, max_nn=max_nn)
    )
    return point_cloud


def normalize_model_origin(
    point_cloud: o3d.geometry.PointCloud,
    origin: str,
) -> tuple[o3d.geometry.PointCloud, np.ndarray]:
    normalized = clone_point_cloud(point_cloud)
    if origin != "centroid":
        return normalized, np.zeros(3, dtype=float)

    centroid = normalized.get_center()
    normalized.translate(-centroid)
    return normalized, np.asarray(centroid, dtype=float)


def preprocess_point_cloud(
    point_cloud: o3d.geometry.PointCloud,
    config: PointCloudPreprocessConfig,
) -> tuple[o3d.geometry.PointCloud, PointCloudStats]:
    working = clone_point_cloud(point_cloud)
    stats = PointCloudStats(raw=len(working.points))

    working = remove_invalid_points(working)
    stats.finite = len(working.points)

    if config.voxel_size > 0:
        working = working.voxel_down_sample(config.voxel_size)
    stats.voxel = len(working.points)

    if config.statistical_filter.enabled and len(working.points) > config.statistical_filter.nb_neighbors:
        working, _ = working.remove_statistical_outlier(
            nb_neighbors=config.statistical_filter.nb_neighbors,
            std_ratio=config.statistical_filter.std_ratio,
        )
    stats.statistical = len(working.points)

    if config.radius_filter.enabled and len(working.points) > config.radius_filter.nb_points:
        working, _ = working.remove_radius_outlier(
            nb_points=config.radius_filter.nb_points,
            radius=config.radius_filter.radius,
        )
    stats.radius = len(working.points)

    estimate_normals(working, config.normal_radius, config.normal_max_nn)
    stats.final = len(working.points)
    if stats.final > 0:
        stats.aabb_extent = working.get_axis_aligned_bounding_box().get_extent().tolist()
    return working, stats
