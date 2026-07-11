using UnityEngine;

namespace ARObjectReplacement.Pose
{
    public static class GenericPoseMath
    {
        private const float Epsilon = 1e-6f;

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value) || value.sqrMagnitude < Epsilon)
            {
                return fallback.normalized;
            }

            return value.normalized;
        }

        public static float Clamp01Safe(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        public static bool BuildOrthonormalFrame(
            Vector3 forwardCandidate,
            Vector3 upCandidate,
            Vector3 fallbackAxis,
            float parallelThreshold,
            out Vector3 right,
            out Vector3 up,
            out Vector3 forward)
        {
            up = SafeNormalize(upCandidate, Vector3.up);
            forward = Vector3.ProjectOnPlane(forwardCandidate, up);

            if (!IsFinite(forward) || forward.sqrMagnitude < Epsilon ||
                Mathf.Abs(Vector3.Dot(SafeNormalize(forwardCandidate, Vector3.forward), up)) > parallelThreshold)
            {
                forward = Vector3.ProjectOnPlane(fallbackAxis, up);
            }

            if (!IsFinite(forward) || forward.sqrMagnitude < Epsilon)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (!IsFinite(forward) || forward.sqrMagnitude < Epsilon)
            {
                forward = Vector3.right;
            }

            forward.Normalize();
            right = Vector3.Cross(up, forward);
            if (!IsFinite(right) || right.sqrMagnitude < Epsilon)
            {
                right = Vector3.Cross(Vector3.up, forward);
            }

            right = SafeNormalize(right, Vector3.right);
            forward = SafeNormalize(Vector3.Cross(right, up), Vector3.forward);
            up = SafeNormalize(Vector3.Cross(forward, right), Vector3.up);
            return IsFinite(right) && IsFinite(up) && IsFinite(forward);
        }

        public static Quaternion RotationFromFrame(Vector3 right, Vector3 up, Vector3 forward)
        {
            if (!IsFinite(right) || !IsFinite(up) || !IsFinite(forward) ||
                forward.sqrMagnitude < Epsilon || up.sqrMagnitude < Epsilon)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        public static float DirectionAngleDegrees(Vector3 a, Vector3 b)
        {
            if (!IsFinite(a) || !IsFinite(b) || a.sqrMagnitude < Epsilon || b.sqrMagnitude < Epsilon)
            {
                return 180f;
            }

            return Vector3.Angle(a.normalized, b.normalized);
        }
    }
}
