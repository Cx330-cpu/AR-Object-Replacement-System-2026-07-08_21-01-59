using System.Collections.Generic;
using UnityEngine;

namespace ARObjectReplacement.PointCloud
{
    public sealed class PointCloudDownSampler
    {
        public PointCloudData VoxelDownSample(PointCloudData input, float voxelSizeMeters)
        {
            if (input == null || input.Points == null || input.Points.Count == 0 || voxelSizeMeters <= 0f)
            {
                return input;
            }

            var buckets = new Dictionary<Vector3Int, Accumulator>();
            foreach (var point in input.Points)
            {
                var key = new Vector3Int(
                    Mathf.FloorToInt(point.Position.x / voxelSizeMeters),
                    Mathf.FloorToInt(point.Position.y / voxelSizeMeters),
                    Mathf.FloorToInt(point.Position.z / voxelSizeMeters));

                if (!buckets.TryGetValue(key, out var accumulator))
                {
                    accumulator = new Accumulator();
                    buckets[key] = accumulator;
                }

                accumulator.Add(point);
            }

            var outputPoints = new List<Point3D>(buckets.Count);
            foreach (var bucket in buckets.Values)
            {
                outputPoints.Add(bucket.ToPoint());
            }

            return new PointCloudData(outputPoints, input.Roi, input.Timestamp)
            {
                RawPointCount = input.RawPointCount,
                FilteredPointCount = outputPoints.Count,
                VoxelSizeMeters = voxelSizeMeters
            };
        }

        private sealed class Accumulator
        {
            private Vector3 positionSum;
            private Vector2 pixelSum;
            private float depthSum;
            private float confidenceSum;
            private int count;

            public void Add(Point3D point)
            {
                positionSum += point.Position;
                pixelSum += point.Pixel;
                depthSum += point.DepthMeters;
                confidenceSum += point.Confidence;
                count++;
            }

            public Point3D ToPoint()
            {
                var safeCount = Mathf.Max(1, count);
                return new Point3D
                {
                    Position = positionSum / safeCount,
                    Normal = Vector3.zero,
                    Pixel = pixelSum / safeCount,
                    DepthMeters = depthSum / safeCount,
                    Confidence = confidenceSum / safeCount,
                    HasNormal = false
                };
            }
        }
    }
}

