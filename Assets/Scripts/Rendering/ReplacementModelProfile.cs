using UnityEngine;

namespace ARObjectReplacement.Rendering
{
    public sealed class ReplacementModelProfile
    {
        public string ResourceName;
        public Vector3 RotationOffsetEuler;
        public Vector3 PivotOffsetMeters;
        public float VerticalOffsetMeters;
        public float ScaleMultiplier;
        public ReplacementFitMode FitMode;
        public float MinimumScale;
        public float MaximumScale;

        public static ReplacementModelProfile Default(string resourceName)
        {
            return new ReplacementModelProfile
            {
                ResourceName = resourceName,
                RotationOffsetEuler = Vector3.zero,
                PivotOffsetMeters = Vector3.zero,
                VerticalOffsetMeters = 0f,
                ScaleMultiplier = 1f,
                FitMode = ReplacementFitMode.MaxExtent,
                MinimumScale = 0.01f,
                MaximumScale = 3f
            };
        }
    }
}
