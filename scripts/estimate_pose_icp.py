from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from pose.config import load_pose_config
from pose.icp import estimate_pose
from pose.io import load_point_cloud, save_point_cloud, save_pose_result
from pose.visualization import transformed_model, visualize_registration


def main() -> int:
    parser = argparse.ArgumentParser(description="Estimate M5 6DoF pose with centroid/PCA + ICP.")
    parser.add_argument("--scene", required=True, help="M4 scene point cloud .ply path")
    parser.add_argument("--model", required=True, help="Reference model point cloud .ply path")
    parser.add_argument("--config", default="config/pose_icp.yaml", help="Pose ICP config path")
    parser.add_argument("--output", default="outputs/pose/latest_pose.json", help="Output pose JSON path")
    parser.add_argument(
        "--registration-output",
        default="outputs/pose/registration_result.ply",
        help="Output transformed model point cloud path",
    )
    parser.add_argument("--visualize", action="store_true", help="Open Open3D before/after visualization")
    parser.add_argument(
        "--pose-axis-size",
        type=float,
        default=0.08,
        help="Open3D pose axis size in meters for visual direction inspection",
    )
    args = parser.parse_args()

    config = load_pose_config(args.config)
    scene = load_point_cloud(args.scene)
    model = load_point_cloud(args.model)
    result = estimate_pose(scene, model, config)

    save_pose_result(result, args.output)
    aligned_model = transformed_model(model, np.asarray(result.transformation))
    save_point_cloud(aligned_model, args.registration_output)

    print(f"is_valid={result.is_valid}")
    print(f"message={result.message}")
    print(f"coordinate_frame={result.coordinate_frame}")
    print(f"transform_semantics={result.transform_semantics}")
    print(f"translation_m={result.translation_m}")
    print(f"quaternion_xyzw={result.quaternion_xyzw}")
    transform = np.asarray(result.transformation)
    print(f"model_right_plus_x_camera={transform[:3, 0].tolist()}")
    print(f"model_up_plus_y_camera={transform[:3, 1].tolist()}")
    print(f"model_forward_plus_z_camera={transform[:3, 2].tolist()}")
    print(f"fitness={result.fitness:.6f}")
    print(f"inlier_rmse={result.inlier_rmse:.6f}")
    print(f"runtime_ms={result.runtime_ms:.2f}")
    print(f"scene_points={result.scene_stats.final if result.scene_stats else 0}")
    print(f"model_points={result.model_stats.final if result.model_stats else 0}")
    print(f"pose_output={args.output}")
    print(f"registration_output={args.registration_output}")

    if args.visualize:
        visualize_registration(scene, model, transform, args.pose_axis_size)

    return 0 if result.is_valid else 2


if __name__ == "__main__":
    raise SystemExit(main())
