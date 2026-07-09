from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from pointcloud.open3d_viewer import estimate_normals, load_point_cloud


def main() -> int:
    parser = argparse.ArgumentParser(description="Evaluate a M4 PLY point cloud.")
    parser.add_argument("path", help="Path to .ply point cloud")
    parser.add_argument("--min-points", type=int, default=50)
    parser.add_argument("--estimate-normals", action="store_true")
    args = parser.parse_args()

    point_cloud = load_point_cloud(args.path)
    if args.estimate_normals:
        estimate_normals(point_cloud)

    point_count = len(point_cloud.points)
    bounds_min = point_cloud.get_min_bound()
    bounds_max = point_cloud.get_max_bound()
    extent = point_cloud.get_axis_aligned_bounding_box().get_extent()

    print(f"points={point_count}")
    print(f"has_normals={point_cloud.has_normals()}")
    print(f"bounds_min={bounds_min}")
    print(f"bounds_max={bounds_max}")
    print(f"extent={extent}")
    print(f"point_count_pass={point_count >= args.min_points}")
    return 0 if point_count >= args.min_points else 1


if __name__ == "__main__":
    raise SystemExit(main())

