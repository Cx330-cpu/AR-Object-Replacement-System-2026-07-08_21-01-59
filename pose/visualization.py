from __future__ import annotations

import copy

import numpy as np
import open3d as o3d


def transformed_model(
    model: o3d.geometry.PointCloud,
    transform_model_to_camera: np.ndarray,
) -> o3d.geometry.PointCloud:
    output = copy.deepcopy(model)
    output.transform(transform_model_to_camera)
    return output


def build_registration_geometries(
    scene: o3d.geometry.PointCloud,
    model: o3d.geometry.PointCloud,
    transform_model_to_camera: np.ndarray,
    pose_axis_size: float = 0.08,
) -> list[o3d.geometry.Geometry]:
    scene_vis = copy.deepcopy(scene)
    model_initial = copy.deepcopy(model)
    model_aligned = transformed_model(model, transform_model_to_camera)

    scene_vis.paint_uniform_color([0.1, 0.55, 1.0])
    model_initial.paint_uniform_color([0.8, 0.8, 0.8])
    model_aligned.paint_uniform_color([1.0, 0.25, 0.15])

    camera_axis = o3d.geometry.TriangleMesh.create_coordinate_frame(size=0.1)
    model_pose_axis = o3d.geometry.TriangleMesh.create_coordinate_frame(size=pose_axis_size)
    model_pose_axis.transform(transform_model_to_camera)
    return [scene_vis, model_initial, model_aligned, camera_axis, model_pose_axis]


def visualize_registration(
    scene: o3d.geometry.PointCloud,
    model: o3d.geometry.PointCloud,
    transform_model_to_camera: np.ndarray,
    pose_axis_size: float = 0.08,
) -> None:
    o3d.visualization.draw_geometries(
        build_registration_geometries(scene, model, transform_model_to_camera, pose_axis_size),
        window_name="M5 ICP Registration",
    )
