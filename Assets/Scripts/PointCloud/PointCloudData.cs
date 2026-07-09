using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARObjectReplacement.PointCloud
{
    [Serializable]
    public sealed class PointCloudData
    {
        public readonly List<Point3D> Points;
        public readonly RectInt Roi;
        public readonly double Timestamp;
        public int RawPointCount;
        public int FilteredPointCount;
        public float VoxelSizeMeters;
        public float ExportTimeMs;

        public PointCloudData(List<Point3D> points, RectInt roi, double timestamp)
        {
            Points = points;
            Roi = roi;
            Timestamp = timestamp;
            RawPointCount = points != null ? points.Count : 0;
            FilteredPointCount = RawPointCount;
        }
    }
}

