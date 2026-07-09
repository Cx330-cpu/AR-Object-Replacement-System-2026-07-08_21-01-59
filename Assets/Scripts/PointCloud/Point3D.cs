using System;
using UnityEngine;

namespace ARObjectReplacement.PointCloud
{
    [Serializable]
    public struct Point3D
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 Pixel;
        public float DepthMeters;
        public float Confidence;
        public bool HasNormal;
    }
}

