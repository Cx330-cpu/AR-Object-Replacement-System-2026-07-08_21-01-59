from __future__ import annotations

import itertools

import numpy as np
import open3d as o3d
from scipy.spatial import cKDTree


def centroid_transform(
    source_model: o3d.geometry.PointCloud,
    target_scene: o3d.geometry.PointCloud,
) -> np.ndarray:
    transform = np.eye(4, dtype=float)
    transform[:3, 3] = np.asarray(target_scene.get_center()) - np.asarray(source_model.get_center())
    return transform


def _pca_axes(point_cloud: o3d.geometry.PointCloud) -> np.ndarray:
    points = np.asarray(point_cloud.points)
    centered = points - points.mean(axis=0)
    covariance = np.cov(centered.T)
    _, vectors = np.linalg.eigh(covariance)
    axes = vectors[:, ::-1]
    if np.linalg.det(axes) < 0:
        axes[:, -1] *= -1.0
    return axes


def _mean_nearest_distance(source_points: np.ndarray, target_points: np.ndarray) -> float:
    tree = cKDTree(target_points)
    distances, _ = tree.query(source_points, k=1)
    return float(np.mean(distances))


def pca_alignment_transform(
    source_model: o3d.geometry.PointCloud,
    target_scene: o3d.geometry.PointCloud,
    sign_disambiguation: bool = True,
) -> np.ndarray:
    source_points = np.asarray(source_model.points)
    target_points = np.asarray(target_scene.points)
    source_center = source_points.mean(axis=0)
    target_center = target_points.mean(axis=0)

    source_axes = _pca_axes(source_model)
    target_axes = _pca_axes(target_scene)

    sign_options = [np.diag(signs) for signs in itertools.product((-1.0, 1.0), repeat=3)]
    if not sign_disambiguation:
        sign_options = [np.eye(3)]

    best_transform = np.eye(4, dtype=float)
    best_score = float("inf")
    centered_source = source_points - source_center

    for sign_matrix in sign_options:
        rotation = target_axes @ sign_matrix @ source_axes.T
        if np.linalg.det(rotation) < 0:
            continue
        transformed = centered_source @ rotation.T + target_center
        score = _mean_nearest_distance(transformed, target_points)
        if score < best_score:
            best_score = score
            best_transform[:3, :3] = rotation
            best_transform[:3, 3] = target_center - rotation @ source_center

    return best_transform


def build_initial_transform(
    source_model: o3d.geometry.PointCloud,
    target_scene: o3d.geometry.PointCloud,
    method: str,
    sign_disambiguation: bool = True,
) -> np.ndarray:
    if method == "identity":
        return np.eye(4, dtype=float)
    if method == "centroid":
        return centroid_transform(source_model, target_scene)
    if method == "centroid_pca":
        return pca_alignment_transform(source_model, target_scene, sign_disambiguation)
    raise ValueError(f"Unsupported initialization method: {method}")
