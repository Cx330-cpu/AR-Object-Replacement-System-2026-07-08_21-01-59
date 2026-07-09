using System.Collections.Generic;
using ARObjectReplacement.Geometry;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ARObjectReplacement.PointCloud
{
    public sealed class PointCloudBuilder
    {
        public PointCloudData BuildPointCloud(
            DepthFrame depthFrame,
            RectInt roi,
            XRCameraIntrinsics intrinsics,
            float minDepthMeters = 0.05f,
            float maxDepthMeters = 8f,
            float minimumConfidence = 0f)
        {
            var points = new List<Point3D>();
            if (!depthFrame.IsValid || intrinsics.resolution.x <= 0 || intrinsics.resolution.y <= 0)
            {
                return new PointCloudData(points, roi, depthFrame.Timestamp);
            }

            var clippedRoi = ClipRoi(roi, depthFrame.Width, depthFrame.Height);
            for (var y = clippedRoi.yMin; y < clippedRoi.yMax; y++)
            {
                for (var x = clippedRoi.xMin; x < clippedRoi.xMax; x++)
                {
                    var depth = depthFrame.GetDepth(x, y);
                    if (!IsValidDepth(depth, minDepthMeters, maxDepthMeters))
                    {
                        continue;
                    }

                    var confidence = depthFrame.GetConfidence01(x, y);
                    if (confidence < minimumConfidence)
                    {
                        continue;
                    }

                    var depthPixel = new Vector2(x, y);
                    var cameraPixel = CoordinateConverter.ImagePixelToCameraPixel(
                        depthPixel,
                        new Vector2Int(depthFrame.Width, depthFrame.Height),
                        intrinsics.resolution);
                    var cameraPoint = CoordinateConverter.PixelToCameraPoint(cameraPixel, depth, intrinsics);
                    if (cameraPoint == Vector3.zero)
                    {
                        continue;
                    }

                    points.Add(new Point3D
                    {
                        Position = cameraPoint,
                        Normal = Vector3.zero,
                        Pixel = cameraPixel,
                        DepthMeters = depth,
                        Confidence = confidence,
                        HasNormal = false
                    });
                }
            }

            return new PointCloudData(points, clippedRoi, depthFrame.Timestamp)
            {
                RawPointCount = points.Count,
                FilteredPointCount = points.Count
            };
        }

        private static RectInt ClipRoi(RectInt roi, int width, int height)
        {
            var xMin = Mathf.Clamp(roi.xMin, 0, width);
            var yMin = Mathf.Clamp(roi.yMin, 0, height);
            var xMax = Mathf.Clamp(roi.xMax, xMin, width);
            var yMax = Mathf.Clamp(roi.yMax, yMin, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static bool IsValidDepth(float depth, float minDepthMeters, float maxDepthMeters)
        {
            return depth >= minDepthMeters &&
                   depth <= maxDepthMeters &&
                   !float.IsNaN(depth) &&
                   !float.IsInfinity(depth);
        }
    }
}
