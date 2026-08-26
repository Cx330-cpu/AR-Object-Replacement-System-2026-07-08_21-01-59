using UnityEngine;

namespace ARObjectReplacement.Rendering
{
    [CreateAssetMenu(
        fileName = "ReplacementModelProfile",
        menuName = "AR Object Replacement/Replacement Model Profile")]
    public sealed class ReplacementModelProfileAsset : ScriptableObject
    {
        public string ResourceName;
        public Vector3 RotationOffsetEuler;
        public Vector3 PivotOffsetMeters;
        public float VerticalOffsetMeters;
        public float ScaleMultiplier = 1f;
        public ReplacementFitMode FitMode = ReplacementFitMode.MaxExtent;
        public float MinimumScale = 0.01f;
        public float MaximumScale = 3f;

        public ReplacementModelProfile ToProfile(string fallbackResourceName)
        {
            return new ReplacementModelProfile
            {
                ResourceName = string.IsNullOrWhiteSpace(ResourceName) ? fallbackResourceName : ResourceName,
                RotationOffsetEuler = RotationOffsetEuler,
                PivotOffsetMeters = PivotOffsetMeters,
                VerticalOffsetMeters = VerticalOffsetMeters,
                ScaleMultiplier = ScaleMultiplier,
                FitMode = FitMode,
                MinimumScale = MinimumScale,
                MaximumScale = MaximumScale
            };
        }

        public void SetFromProfile(ReplacementModelProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            ResourceName = profile.ResourceName;
            RotationOffsetEuler = profile.RotationOffsetEuler;
            PivotOffsetMeters = profile.PivotOffsetMeters;
            VerticalOffsetMeters = profile.VerticalOffsetMeters;
            ScaleMultiplier = profile.ScaleMultiplier;
            FitMode = profile.FitMode;
            MinimumScale = profile.MinimumScale;
            MaximumScale = profile.MaximumScale;
        }
    }
}
