using System;
using UnityEngine;

namespace ARObjectReplacement.Geometry
{
    [Serializable]
    public struct WorldPoint
    {
        public bool IsValid;
        public Vector3 Position;
        public Vector2 Pixel;
        public Vector3 CameraPoint;
        public float DepthMeters;
        public double Timestamp;

        public static WorldPoint Invalid(Vector2 pixel, float depthMeters, double timestamp)
        {
            return new WorldPoint
            {
                IsValid = false,
                Position = Vector3.zero,
                Pixel = pixel,
                CameraPoint = Vector3.zero,
                DepthMeters = depthMeters,
                Timestamp = timestamp
            };
        }
    }
}

