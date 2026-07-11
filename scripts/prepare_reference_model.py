from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from pose.config import load_pose_config
from pose.io import load_point_cloud, save_model_metadata, save_point_cloud
from pose.preprocessing import normalize_model_origin, preprocess_point_cloud


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare an M5 reference model point cloud.")
    parser.add_argument("--model", required=True, help="Input reference model point cloud path")
    parser.add_argument("--config", default="config/pose_icp.yaml", help="Pose ICP config path")
    parser.add_argument("--output", required=True, help="Output processed reference .ply path")
    parser.add_argument("--metadata-output", help="Output model metadata .json path")
    args = parser.parse_args()

    config = load_pose_config(args.config)
    point_cloud = load_point_cloud(args.model)
    normalized, origin_offset = normalize_model_origin(point_cloud, config.model_metadata.origin)
    processed, stats = preprocess_point_cloud(normalized, config.model)

    save_point_cloud(processed, args.output)
    metadata_output = args.metadata_output or str(Path(args.output).with_suffix(".metadata.json"))
    save_model_metadata(
        config.model_metadata,
        metadata_output,
        origin_offset_m=origin_offset.tolist(),
        stats=stats,
    )

    print(f"input={args.model}")
    print(f"output={args.output}")
    print(f"metadata={metadata_output}")
    print(f"origin={config.model_metadata.origin}")
    print(f"origin_offset_m={origin_offset.tolist()}")
    print(f"points_raw={stats.raw}")
    print(f"points_final={stats.final}")
    print(f"aabb_extent_m={stats.aabb_extent}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
