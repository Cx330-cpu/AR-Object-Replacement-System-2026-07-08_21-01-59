using System;
using UnityEngine;

namespace ARObjectReplacement.Pose
{
    [Serializable]
    public struct GenericPoseFrame
    {
        public bool IsValid;
        public Vector3 CenterCamera;
        public Vector3 AxisMajorCamera;
        public Vector3 AxisMiddleCamera;
        public Vector3 AxisNormalCamera;
        public Vector3 RightCamera;
        public Vector3 UpCamera;
        public Vector3 ForwardCamera;
        public Vector3 ExtentMeters;
        public float EigenValue1;
        public float EigenValue2;
        public float EigenValue3;
        public float Linearity;
        public float Planarity;
        public float Scattering;
        public float GeometryConfidence;
        public float OrientationConfidence;
        public float TrackingConfidence;
        public float OverallConfidence;
        public GenericShapeType ShapeType;
        public GenericPoseStability Stability;
        public int PointCount;
        public double Timestamp;
        public string Message;

        public static GenericPoseFrame Invalid(string message, int pointCount = 0, double timestamp = 0.0)
        {
            return new GenericPoseFrame
            {
                IsValid = false,
                Stability = GenericPoseStability.Invalid,
                ShapeType = GenericShapeType.Unknown,
                PointCount = pointCount,
                Timestamp = timestamp,
                Message = message,
                TrackingConfidence = 0f,
                GeometryConfidence = 0f,
                OrientationConfidence = 0f,
                OverallConfidence = 0f
            };
        }
    }
}
