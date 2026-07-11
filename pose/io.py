from __future__ import annotations

import json
from pathlib import Path

import open3d as o3d

from pose.types import ModelMetadata, PointCloudStats, PoseResult


def load_point_cloud(path: str | Path) -> o3d.geometry.PointCloud:
    point_cloud_path = Path(path)
    if not point_cloud_path.exists():
        raise FileNotFoundError(point_cloud_path)

    point_cloud = o3d.io.read_point_cloud(str(point_cloud_path))
    if point_cloud.is_empty():
        raise ValueError(f"Point cloud is empty: {point_cloud_path}")
    return point_cloud


def save_point_cloud(point_cloud: o3d.geometry.PointCloud, path: str | Path) -> None:
    output_path = Path(path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    if not o3d.io.write_point_cloud(str(output_path), point_cloud):
        raise IOError(f"Failed to write point cloud: {output_path}")


def save_pose_result(result: PoseResult, path: str | Path) -> None:
    output_path = Path(path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(result.to_dict(), handle, indent=2)
        handle.write("\n")


def save_model_metadata(
    metadata: ModelMetadata,
    path: str | Path,
    origin_offset_m: list[float] | None = None,
    stats: PointCloudStats | None = None,
) -> None:
    output_path = Path(path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    data = {
        "unit": metadata.unit,
        "origin": metadata.origin,
        "forward_axis": metadata.forward_axis,
        "up_axis": metadata.up_axis,
        "scale": metadata.scale,
    }
    if origin_offset_m is not None:
        data["origin_offset_m"] = origin_offset_m
    if stats is not None:
        data["points_raw"] = stats.raw
        data["points_final"] = stats.final
        data["aabb_extent_m"] = stats.aabb_extent

    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(data, handle, indent=2)
        handle.write("\n")
