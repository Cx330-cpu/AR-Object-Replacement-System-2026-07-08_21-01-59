using System;
using UnityEngine;

namespace ARObjectReplacement.PointCloud
{
    [Serializable]
    public struct DepthFrame
    {
        public float[] DepthMeters;
        public byte[] Confidence;
        public int Width;
        public int Height;
        public double Timestamp;

        public bool HasConfidence => Confidence != null && Confidence.Length == Width * Height;
        public bool IsValid => DepthMeters != null && DepthMeters.Length == Width * Height && Width > 0 && Height > 0;

        public int Index(int x, int y)
        {
            return y * Width + x;
        }

        public float GetDepth(int x, int y)
        {
            return DepthMeters[Index(x, y)];
        }

        public float GetConfidence01(int x, int y)
        {
            if (!HasConfidence)
            {
                return 1f;
            }

            return Mathf.Clamp01(Confidence[Index(x, y)] / 2f);
        }
    }
}

