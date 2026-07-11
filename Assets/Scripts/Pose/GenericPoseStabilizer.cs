using UnityEngine;

namespace ARObjectReplacement.Pose
{
    public sealed class GenericPoseStabilizer
    {
        private bool hasPrevious;
        private GenericPoseFrame previous;
        private Quaternion previousRotation = Quaternion.identity;
        private double lastValidTimestamp;

        public GenericPoseFrame Update(GenericPoseFrame current, GenericPoseConfig config, double timestamp)
        {
            config = config ?? new GenericPoseConfig();
            if (!current.IsValid)
            {
                return BuildTrackingLostFrame(config, timestamp, current.Message);
            }

            if (hasPrevious && config.UseTemporalSignStabilization)
            {
                current = StabilizeSigns(current, previous, config);
            }

            if (hasPrevious)
            {
                var centerDelta = Vector3.Distance(previous.CenterCamera, current.CenterCamera);
                var forwardAngle = GenericPoseMath.DirectionAngleDegrees(previous.ForwardCamera, current.ForwardCamera);
                var upAngle = GenericPoseMath.DirectionAngleDegrees(previous.UpCamera, current.UpCamera);
                current.TrackingConfidence = ComputeTrackingConfidence(centerDelta, Mathf.Max(forwardAngle, upAngle));
            }
            else
            {
                current.TrackingConfidence = 0.5f;
            }

            current.OverallConfidence = GenericPoseEstimator.ComputeOverallConfidence(
                current.GeometryConfidence,
                current.OrientationConfidence,
                current.TrackingConfidence);
            current.Stability = GenericPoseEstimator.StabilityFromConfidence(
                current.OverallConfidence,
                current.ShapeType,
                config);

            if (hasPrevious && config.UseTemporalSmoothing)
            {
                current = SmoothFrame(previous, current, previousRotation, config);
            }

            previousRotation = GenericPoseMath.RotationFromFrame(
                current.RightCamera,
                current.UpCamera,
                current.ForwardCamera);
            previous = current;
            hasPrevious = true;
            lastValidTimestamp = timestamp;
            return current;
        }

        private GenericPoseFrame BuildTrackingLostFrame(GenericPoseConfig config, double timestamp, string message)
        {
            if (!hasPrevious || timestamp - lastValidTimestamp > config.LostTrackingHoldSeconds)
            {
                return GenericPoseFrame.Invalid(message, 0, timestamp);
            }

            var held = previous;
            var age = Mathf.Clamp01((float)((timestamp - lastValidTimestamp) / Mathf.Max(0.01f, config.LostTrackingHoldSeconds)));
            held.Timestamp = timestamp;
            held.Stability = GenericPoseStability.TrackingLost;
            held.TrackingConfidence = Mathf.Lerp(previous.TrackingConfidence, 0f, age);
            held.OverallConfidence = GenericPoseEstimator.ComputeOverallConfidence(
                held.GeometryConfidence,
                held.OrientationConfidence,
                held.TrackingConfidence);
            held.Message = "GenericPose: tracking lost, holding previous frame";
            return held;
        }

        private static GenericPoseFrame StabilizeSigns(
            GenericPoseFrame current,
            GenericPoseFrame previousFrame,
            GenericPoseConfig config)
        {
            if (Vector3.Dot(current.UpCamera, previousFrame.UpCamera) < 0f)
            {
                current.UpCamera = -current.UpCamera;
            }

            if (Vector3.Dot(current.ForwardCamera, previousFrame.ForwardCamera) < 0f)
            {
                current.ForwardCamera = -current.ForwardCamera;
            }

            if (Vector3.Dot(current.AxisMajorCamera, previousFrame.AxisMajorCamera) < 0f)
            {
                current.AxisMajorCamera = -current.AxisMajorCamera;
            }

            if (Vector3.Dot(current.AxisMiddleCamera, previousFrame.AxisMiddleCamera) < 0f)
            {
                current.AxisMiddleCamera = -current.AxisMiddleCamera;
            }

            if (Vector3.Dot(current.AxisNormalCamera, previousFrame.AxisNormalCamera) < 0f)
            {
                current.AxisNormalCamera = -current.AxisNormalCamera;
            }

            GenericPoseMath.BuildOrthonormalFrame(
                current.ForwardCamera,
                current.UpCamera,
                current.AxisMajorCamera,
                config.ParallelDirectionThreshold,
                out current.RightCamera,
                out current.UpCamera,
                out current.ForwardCamera);
            return current;
        }

        private static GenericPoseFrame SmoothFrame(
            GenericPoseFrame previousFrame,
            GenericPoseFrame current,
            Quaternion previousRotation,
            GenericPoseConfig config)
        {
            var alpha = Mathf.Clamp01(config.SmoothingAlpha);
            current.CenterCamera = Vector3.Lerp(previousFrame.CenterCamera, current.CenterCamera, alpha);

            var currentRotation = GenericPoseMath.RotationFromFrame(
                current.RightCamera,
                current.UpCamera,
                current.ForwardCamera);
            var smoothedRotation = Quaternion.Slerp(previousRotation, currentRotation, alpha);
            current.RightCamera = GenericPoseMath.SafeNormalize(smoothedRotation * Vector3.right, Vector3.right);
            current.UpCamera = GenericPoseMath.SafeNormalize(smoothedRotation * Vector3.up, Vector3.up);
            current.ForwardCamera = GenericPoseMath.SafeNormalize(smoothedRotation * Vector3.forward, Vector3.forward);

            GenericPoseMath.BuildOrthonormalFrame(
                current.ForwardCamera,
                current.UpCamera,
                current.AxisMajorCamera,
                config.ParallelDirectionThreshold,
                out current.RightCamera,
                out current.UpCamera,
                out current.ForwardCamera);
            return current;
        }

        private static float ComputeTrackingConfidence(float centerDeltaMeters, float angleDeltaDegrees)
        {
            var centerScore = Mathf.Clamp01(1f - centerDeltaMeters / 0.12f);
            var angleScore = Mathf.Clamp01(1f - angleDeltaDegrees / 60f);
            return GenericPoseMath.Clamp01Safe(0.45f * centerScore + 0.55f * angleScore);
        }
    }
}
