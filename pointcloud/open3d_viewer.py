from __future__ import annotations

from pathlib import Path

import open3d as o3d


def load_point_cloud(path: str | Path) -> o3d.geometry.PointCloud:
    point_cloud_path = Path(path)
    if not point_cloud_path.exists():
        raise FileNotFoundError(point_cloud_path)

    point_cloud = o3d.io.read_point_cloud(str(point_cloud_path))
    if point_cloud.is_empty():
        raise ValueError(f"Point cloud is empty: {point_cloud_path}")
    return point_cloud


def estimate_normals(point_cloud: o3d.geometry.PointCloud, radius: float = 0.04, max_nn: int = 30) -> None:
    point_cloud.estimate_normals(
        search_param=o3d.geometry.KDTreeSearchParamHybrid(radius=radius, max_nn=max_nn)
    )
    point_cloud.orient_normals_consistent_tangent_plane(k=10)


def view_point_cloud(path: str | Path, estimate_normal_vectors: bool = True) -> None:
    point_cloud = load_point_cloud(path)
    if estimate_normal_vectors:
        estimate_normals(point_cloud)

    print(f"points={len(point_cloud.points)}")
    print(f"has_normals={point_cloud.has_normals()}")
    print(f"bounds_min={point_cloud.get_min_bound()}")
    print(f"bounds_max={point_cloud.get_max_bound()}")
    o3d.visualization.draw_geometries([point_cloud], window_name="M4 PointCloud Viewer")

