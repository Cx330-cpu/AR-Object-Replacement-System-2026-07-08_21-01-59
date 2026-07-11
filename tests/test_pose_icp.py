from __future__ import annotations

import unittest

import numpy as np
import open3d as o3d
from scipy.spatial.transform import Rotation

from pose.icp import estimate_pose
from pose.metrics import rotation_angle_error_degrees, translation_error_m
from pose.types import FilterConfig, IcpStageConfig, PointCloudPreprocessConfig, PoseConfig


def _point_cloud(points: np.ndarray) -> o3d.geometry.PointCloud:
    point_cloud = o3d.geometry.PointCloud()
    point_cloud.points = o3d.utility.Vector3dVector(points)
    return point_cloud


def _asymmetric_model_points() -> np.ndarray:
    rng = np.random.default_rng(7)
    body = rng.uniform(low=[-0.05, -0.03, -0.02], high=[0.05, 0.03, 0.02], size=(700, 3))
    handle = rng.uniform(low=[0.035, -0.012, 0.015], high=[0.085, 0.012, 0.05], size=(220, 3))
    marker = rng.uniform(low=[-0.045, 0.02, -0.018], high=[-0.02, 0.045, 0.005], size=(120, 3))
    return np.vstack([body, handle, marker])


class PoseIcpTest(unittest.TestCase):
    def test_synthetic_transform_is_recovered(self) -> None:
        model_points = _asymmetric_model_points()
        expected = np.eye(4)
        expected[:3, :3] = Rotation.from_euler("xyz", [12.0, -18.0, 28.0], degrees=True).as_matrix()
        expected[:3, 3] = [0.18, -0.04, 0.62]

        scene_points = model_points @ expected[:3, :3].T + expected[:3, 3]
        rng = np.random.default_rng(11)
        scene_points = scene_points + rng.normal(scale=0.0008, size=scene_points.shape)

        config = PoseConfig(
            scene=PointCloudPreprocessConfig(
                voxel_size=0.002,
                normal_radius=0.025,
                statistical_filter=FilterConfig(enabled=False),
            ),
            model=PointCloudPreprocessConfig(
                voxel_size=0.002,
                normal_radius=0.025,
                statistical_filter=FilterConfig(enabled=False),
            ),
            point_to_point=IcpStageConfig(
                enabled=True,
                correspondence_threshold=0.08,
                max_iteration=100,
            ),
            point_to_plane=IcpStageConfig(
                enabled=True,
                correspondence_threshold=0.04,
                max_iteration=60,
            ),
            min_points=50,
            max_inlier_rmse=0.01,
        )

        result = estimate_pose(_point_cloud(scene_points), _point_cloud(model_points), config)
        actual = np.asarray(result.transformation)

        self.assertTrue(result.is_valid, result.message)
        self.assertLess(translation_error_m(actual, expected), 0.01)
        self.assertLess(rotation_angle_error_degrees(actual, expected), 3.0)
        self.assertEqual(result.transform_semantics, "T_camera_from_model")
        self.assertEqual(result.coordinate_frame, "camera")


if __name__ == "__main__":
    unittest.main()
