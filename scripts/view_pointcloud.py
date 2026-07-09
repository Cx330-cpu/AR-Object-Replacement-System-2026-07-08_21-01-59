from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from pointcloud.open3d_viewer import view_point_cloud


def main() -> int:
    parser = argparse.ArgumentParser(description="View a M4 PLY point cloud with Open3D.")
    parser.add_argument("path", help="Path to .ply point cloud")
    parser.add_argument("--no-normals", action="store_true", help="Skip Open3D normal estimation")
    args = parser.parse_args()

    view_point_cloud(args.path, estimate_normal_vectors=not args.no_normals)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

