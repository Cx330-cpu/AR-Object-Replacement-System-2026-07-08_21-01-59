using System;
using UnityEngine;

namespace ARObjectReplacement.Geometry
{
    [Serializable]
    public struct CameraPoint
    {
        public bool IsValid;
        public Vector3 Position;
        public Vector2 Pixel;
        public float DepthMeters;
        public float Confidence;
        public double Timestamp;
    }
}

