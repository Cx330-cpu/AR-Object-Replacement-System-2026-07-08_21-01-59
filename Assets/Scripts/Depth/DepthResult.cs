using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ARObjectReplacement.Depth
{
    [Serializable]
    public struct DepthResult
    {
        public bool IsValid;
        public float DistanceMeters;
        public int Confidence;
        public Vector2Int DepthPixel;
        public Vector2Int DepthResolution;
        public XRCameraIntrinsics Intrinsics;
        public bool HasIntrinsics;
        public double Timestamp;
    }
}

