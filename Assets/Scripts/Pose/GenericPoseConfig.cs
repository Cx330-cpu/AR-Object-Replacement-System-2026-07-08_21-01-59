using System;

namespace ARObjectReplacement.Pose
{
    [Serializable]
    public sealed class GenericPoseConfig
    {
        public int MinimumPointCount = 30;
        public float ElongatedLinearityThreshold = 0.45f;
        public float PlanarPlanarityThreshold = 0.35f;
        public float AmbiguousScatteringThreshold = 0.25f;
        public float StableConfidenceThreshold = 0.65f;
        public float WeakConfidenceThreshold = 0.35f;
        public float SmoothingAlpha = 0.35f;
        public float LostTrackingHoldSeconds = 0.75f;
        public float ParallelDirectionThreshold = 0.92f;
        public float MinimumExtentMeters = 0.01f;
        public float MaximumExtentMeters = 2.0f;
        public float MinimumAxisLengthMeters = 0.03f;
        public float MaximumAxisLengthMeters = 0.20f;
        public float LogIntervalSeconds = 0.75f;
        public float AutoFreeObjectConfidenceThreshold = 0.68f;
        public float AutoFreeObjectMinimumScattering = 0.04f;
        public float AutoFreeObjectMaximumPlanarity = 0.72f;
        public float SurfacePlaneMinimumHeightMeters = 0.005f;
        public float SurfacePlaneMaximumHeightMeters = 0.35f;
        public float SurfacePlaneNormalUpDotThreshold = 0.85f;
        public float SurfacePlaneHorizontalMarginMeters = 0.25f;
        public bool UseGravityAlignment = true;
        public bool UseTemporalSignStabilization = true;
        public bool UseTemporalSmoothing = true;
        public bool ShowDebugPcaValues = true;
    }
}
