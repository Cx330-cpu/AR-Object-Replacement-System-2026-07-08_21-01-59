using System.Collections.Generic;
using UnityEngine;

namespace ARObjectReplacement.PointCloud
{
    public sealed class PointCloudCleaner
    {
        public PointCloudData RemoveInvalidAndOutOfRange(
            PointCloudData input,
            float minDepthMeters,
            float maxDepthMeters,
            float minConfidence)
        {
            if (input == null || input.Points == null)
            {
                return input;
            }

            var outputPoints = new List<Point3D>(input.Points.Count);
            foreach (var point in input.Points)
            {
                if (point.DepthMeters < minDepthMeters ||
                    point.DepthMeters > maxDepthMeters ||
                    point.Confidence < minConfidence ||
                    float.IsNaN(point.Position.x) ||
                    float.IsNaN(point.Position.y) ||
                    float.IsNaN(point.Position.z))
                {
                    continue;
                }

                outputPoints.Add(point);
            }

            return CopyWithPoints(input, outputPoints);
        }

        public PointCloudData RadiusOutlierRemoval(
            PointCloudData input,
            float radiusMeters,
            int minimumNeighbors)
        {
            if (input == null ||
                input.Points == null ||
                input.Points.Count == 0 ||
                radiusMeters <= 0f ||
                minimumNeighbors <= 0)
            {
                return input;
            }

            var radiusSquared = radiusMeters * radiusMeters;
            var outputPoints = new List<Point3D>(input.Points.Count);
            for (var i = 0; i < input.Points.Count; i++)
            {
                var neighbors = 0;
                var position = input.Points[i].Position;
                for (var j = 0; j < input.Points.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    if ((input.Points[j].Position - position).sqrMagnitude <= radiusSquared)
                    {
                        neighbors++;
                    }

                    if (neighbors >= minimumNeighbors)
                    {
                        outputPoints.Add(input.Points[i]);
                        break;
                    }
                }
            }

            return CopyWithPoints(input, outputPoints);
        }

        private static PointCloudData CopyWithPoints(PointCloudData input, List<Point3D> outputPoints)
        {
            return new PointCloudData(outputPoints, input.Roi, input.Timestamp)
            {
                RawPointCount = input.RawPointCount,
                FilteredPointCount = outputPoints.Count,
                VoxelSizeMeters = input.VoxelSizeMeters,
                ExportTimeMs = input.ExportTimeMs
            };
        }
    }
}

