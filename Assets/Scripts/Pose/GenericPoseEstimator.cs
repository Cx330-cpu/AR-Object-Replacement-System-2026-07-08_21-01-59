using ARObjectReplacement.PointCloud;
using UnityEngine;

namespace ARObjectReplacement.Pose
{
    public sealed class GenericPoseEstimator
    {
        private const float Epsilon = 1e-6f;

        public GenericPoseFrame Estimate(
            PointCloudData pointCloud,
            GenericPoseConfig config,
            Vector3 worldUpCamera,
            double timestamp)
        {
            config = config ?? new GenericPoseConfig();
            if (pointCloud == null || pointCloud.Points == null)
            {
                return GenericPoseFrame.Invalid("GenericPose: null point cloud", 0, timestamp);
            }

            if (pointCloud.Points.Count < config.MinimumPointCount)
            {
                return GenericPoseFrame.Invalid("GenericPose: too few points", pointCloud.Points.Count, timestamp);
            }

            if (!TryComputeRobustCenter(pointCloud, out var center, out var validCount) ||
                validCount < config.MinimumPointCount)
            {
                return GenericPoseFrame.Invalid("GenericPose: no valid finite points", validCount, timestamp);
            }

            var covariance = BuildCovariance(pointCloud, center, validCount);
            JacobiEigenDecomposition(covariance, out var values, out var vectors);
            SortEigenPairs(values, vectors);

            var lambda1 = Mathf.Max(0f, values[0]);
            var lambda2 = Mathf.Max(0f, values[1]);
            var lambda3 = Mathf.Max(0f, values[2]);
            if (lambda1 <= Epsilon)
            {
                return GenericPoseFrame.Invalid("GenericPose: degenerate covariance", validCount, timestamp);
            }

            var major = GenericPoseMath.SafeNormalize(vectors[0], Vector3.right);
            var middle = GenericPoseMath.SafeNormalize(vectors[1], Vector3.up);
            var normal = GenericPoseMath.SafeNormalize(Vector3.Cross(major, middle), vectors[2]);
            middle = GenericPoseMath.SafeNormalize(Vector3.Cross(normal, major), Vector3.up);
            normal = GenericPoseMath.SafeNormalize(Vector3.Cross(major, middle), Vector3.forward);

            var extent = EstimateExtent(pointCloud, center, major, middle, normal);
            var linearity = GenericPoseMath.Clamp01Safe((lambda1 - lambda2) / Mathf.Max(lambda1, Epsilon));
            var planarity = GenericPoseMath.Clamp01Safe((lambda2 - lambda3) / Mathf.Max(lambda1, Epsilon));
            var scattering = GenericPoseMath.Clamp01Safe(lambda3 / Mathf.Max(lambda1, Epsilon));
            var shape = ClassifyShape(linearity, planarity, scattering, config);

            var upCandidate = config.UseGravityAlignment
                ? SelectGravityAlignedUp(major, middle, normal, worldUpCamera)
                : middle;

            var forwardCandidate = SelectForwardCandidate(shape, center, major, middle, normal, upCandidate, config);
            var fallbackAxis = SelectFallbackAxis(forwardCandidate, upCandidate, major, middle, normal, config);
            if (!GenericPoseMath.BuildOrthonormalFrame(
                    forwardCandidate,
                    upCandidate,
                    fallbackAxis,
                    config.ParallelDirectionThreshold,
                    out var right,
                    out var up,
                    out var forward))
            {
                return GenericPoseFrame.Invalid("GenericPose: failed to build orthonormal frame", validCount, timestamp);
            }

            var geometryConfidence = ComputeGeometryConfidence(validCount, extent, lambda1, config);
            var orientationConfidence = ComputeOrientationConfidence(shape, linearity, planarity);
            var trackingConfidence = 1f;
            var overall = ComputeOverallConfidence(geometryConfidence, orientationConfidence, trackingConfidence);
            var stability = StabilityFromConfidence(overall, shape, config);

            return new GenericPoseFrame
            {
                IsValid = true,
                CenterCamera = center,
                AxisMajorCamera = major,
                AxisMiddleCamera = middle,
                AxisNormalCamera = normal,
                RightCamera = right,
                UpCamera = up,
                ForwardCamera = forward,
                ExtentMeters = extent,
                EigenValue1 = lambda1,
                EigenValue2 = lambda2,
                EigenValue3 = lambda3,
                Linearity = linearity,
                Planarity = planarity,
                Scattering = scattering,
                GeometryConfidence = geometryConfidence,
                OrientationConfidence = orientationConfidence,
                TrackingConfidence = trackingConfidence,
                OverallConfidence = overall,
                ShapeType = shape,
                Stability = stability,
                PointCount = validCount,
                Timestamp = timestamp,
                Message = shape == GenericShapeType.Ambiguous
                    ? "GenericPose: ambiguous geometry"
                    : "GenericPose: geometric frame estimate"
            };
        }

        private static bool TryComputeRobustCenter(PointCloudData pointCloud, out Vector3 center, out int validCount)
        {
            center = Vector3.zero;
            validCount = 0;
            var finitePoints = new System.Collections.Generic.List<Vector3>(pointCloud.Points.Count);
            for (var i = 0; i < pointCloud.Points.Count; i++)
            {
                var point = pointCloud.Points[i].Position;
                if (!GenericPoseMath.IsFinite(point))
                {
                    continue;
                }

                finitePoints.Add(point);
            }

            validCount = finitePoints.Count;
            if (validCount <= 0)
            {
                return false;
            }

            finitePoints.Sort((a, b) => a.z.CompareTo(b.z));
            var trimCount = Mathf.FloorToInt(validCount * 0.20f);
            var start = Mathf.Clamp(trimCount, 0, validCount - 1);
            var end = Mathf.Clamp(validCount - trimCount, start + 1, validCount);
            var robustCount = 0;
            for (var i = start; i < end; i++)
            {
                center += finitePoints[i];
                robustCount++;
            }

            if (robustCount <= 0)
            {
                return false;
            }

            center /= robustCount;
            return GenericPoseMath.IsFinite(center);
        }

        private static float[,] BuildCovariance(PointCloudData pointCloud, Vector3 center, int validCount)
        {
            var covariance = new float[3, 3];
            for (var i = 0; i < pointCloud.Points.Count; i++)
            {
                var point = pointCloud.Points[i].Position;
                if (!GenericPoseMath.IsFinite(point))
                {
                    continue;
                }

                var p = point - center;
                covariance[0, 0] += p.x * p.x;
                covariance[0, 1] += p.x * p.y;
                covariance[0, 2] += p.x * p.z;
                covariance[1, 1] += p.y * p.y;
                covariance[1, 2] += p.y * p.z;
                covariance[2, 2] += p.z * p.z;
            }

            var count = Mathf.Max(1, validCount);
            covariance[0, 0] /= count;
            covariance[0, 1] /= count;
            covariance[0, 2] /= count;
            covariance[1, 1] /= count;
            covariance[1, 2] /= count;
            covariance[2, 2] /= count;
            covariance[1, 0] = covariance[0, 1];
            covariance[2, 0] = covariance[0, 2];
            covariance[2, 1] = covariance[1, 2];
            return covariance;
        }

        private static void JacobiEigenDecomposition(float[,] input, out float[] values, out Vector3[] vectors)
        {
            var a = (float[,])input.Clone();
            var v = new float[3, 3];
            v[0, 0] = 1f;
            v[1, 1] = 1f;
            v[2, 2] = 1f;

            for (var iteration = 0; iteration < 32; iteration++)
            {
                var p = 0;
                var q = 1;
                var max = Mathf.Abs(a[0, 1]);
                if (Mathf.Abs(a[0, 2]) > max)
                {
                    p = 0;
                    q = 2;
                    max = Mathf.Abs(a[0, 2]);
                }

                if (Mathf.Abs(a[1, 2]) > max)
                {
                    p = 1;
                    q = 2;
                    max = Mathf.Abs(a[1, 2]);
                }

                if (max < 1e-8f)
                {
                    break;
                }

                var theta = 0.5f * Mathf.Atan2(2f * a[p, q], a[q, q] - a[p, p]);
                var c = Mathf.Cos(theta);
                var s = Mathf.Sin(theta);
                Rotate(a, v, p, q, c, s);
            }

            values = new[] { a[0, 0], a[1, 1], a[2, 2] };
            vectors = new[]
            {
                GenericPoseMath.SafeNormalize(new Vector3(v[0, 0], v[1, 0], v[2, 0]), Vector3.right),
                GenericPoseMath.SafeNormalize(new Vector3(v[0, 1], v[1, 1], v[2, 1]), Vector3.up),
                GenericPoseMath.SafeNormalize(new Vector3(v[0, 2], v[1, 2], v[2, 2]), Vector3.forward)
            };
        }

        private static void Rotate(float[,] a, float[,] v, int p, int q, float c, float s)
        {
            var app = a[p, p];
            var aqq = a[q, q];
            var apq = a[p, q];
            a[p, p] = c * c * app - 2f * s * c * apq + s * s * aqq;
            a[q, q] = s * s * app + 2f * s * c * apq + c * c * aqq;
            a[p, q] = 0f;
            a[q, p] = 0f;

            for (var i = 0; i < 3; i++)
            {
                if (i == p || i == q)
                {
                    continue;
                }

                var aip = a[i, p];
                var aiq = a[i, q];
                a[i, p] = c * aip - s * aiq;
                a[p, i] = a[i, p];
                a[i, q] = s * aip + c * aiq;
                a[q, i] = a[i, q];
            }

            for (var i = 0; i < 3; i++)
            {
                var vip = v[i, p];
                var viq = v[i, q];
                v[i, p] = c * vip - s * viq;
                v[i, q] = s * vip + c * viq;
            }
        }

        private static void SortEigenPairs(float[] values, Vector3[] vectors)
        {
            for (var i = 0; i < values.Length - 1; i++)
            {
                for (var j = i + 1; j < values.Length; j++)
                {
                    if (values[j] <= values[i])
                    {
                        continue;
                    }

                    var value = values[i];
                    values[i] = values[j];
                    values[j] = value;

                    var vector = vectors[i];
                    vectors[i] = vectors[j];
                    vectors[j] = vector;
                }
            }
        }

        private static Vector3 EstimateExtent(PointCloudData pointCloud, Vector3 center, Vector3 major, Vector3 middle, Vector3 normal)
        {
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (var i = 0; i < pointCloud.Points.Count; i++)
            {
                var point = pointCloud.Points[i].Position;
                if (!GenericPoseMath.IsFinite(point))
                {
                    continue;
                }

                var p = point - center;
                var projected = new Vector3(
                    Vector3.Dot(p, major),
                    Vector3.Dot(p, middle),
                    Vector3.Dot(p, normal));
                min = Vector3.Min(min, projected);
                max = Vector3.Max(max, projected);
            }

            var extent = max - min;
            return GenericPoseMath.IsFinite(extent) ? extent : Vector3.zero;
        }

        private static GenericShapeType ClassifyShape(float linearity, float planarity, float scattering, GenericPoseConfig config)
        {
            if (scattering > config.AmbiguousScatteringThreshold)
            {
                return GenericShapeType.Ambiguous;
            }

            if (linearity > config.ElongatedLinearityThreshold)
            {
                return GenericShapeType.Elongated;
            }

            if (planarity > config.PlanarPlanarityThreshold)
            {
                return GenericShapeType.Planar;
            }

            return GenericShapeType.BoxLike;
        }

        private static Vector3 SelectGravityAlignedUp(Vector3 major, Vector3 middle, Vector3 normal, Vector3 worldUpCamera)
        {
            worldUpCamera = GenericPoseMath.SafeNormalize(worldUpCamera, Vector3.up);
            var candidate = major;
            var best = Mathf.Abs(Vector3.Dot(candidate, worldUpCamera));
            var middleScore = Mathf.Abs(Vector3.Dot(middle, worldUpCamera));
            if (middleScore > best)
            {
                candidate = middle;
                best = middleScore;
            }

            var normalScore = Mathf.Abs(Vector3.Dot(normal, worldUpCamera));
            if (normalScore > best)
            {
                candidate = normal;
            }

            return Vector3.Dot(candidate, worldUpCamera) < 0f ? -candidate : candidate;
        }

        private static Vector3 SelectForwardCandidate(
            GenericShapeType shape,
            Vector3 center,
            Vector3 major,
            Vector3 middle,
            Vector3 normal,
            Vector3 up,
            GenericPoseConfig config)
        {
            if (shape == GenericShapeType.Planar)
            {
                var toCamera = center.sqrMagnitude > Epsilon ? -center.normalized : Vector3.back;
                return Vector3.Dot(normal, toCamera) < 0f ? -normal : normal;
            }

            if (shape == GenericShapeType.Elongated)
            {
                return Mathf.Abs(Vector3.Dot(major, up)) > config.ParallelDirectionThreshold ? middle : major;
            }

            if (Mathf.Abs(Vector3.Dot(major, up)) < Mathf.Abs(Vector3.Dot(middle, up)))
            {
                return major;
            }

            return middle;
        }

        private static Vector3 SelectFallbackAxis(
            Vector3 forwardCandidate,
            Vector3 upCandidate,
            Vector3 major,
            Vector3 middle,
            Vector3 normal,
            GenericPoseConfig config)
        {
            if (Mathf.Abs(Vector3.Dot(major, upCandidate)) < config.ParallelDirectionThreshold &&
                Mathf.Abs(Vector3.Dot(major, forwardCandidate)) < config.ParallelDirectionThreshold)
            {
                return major;
            }

            if (Mathf.Abs(Vector3.Dot(middle, upCandidate)) < config.ParallelDirectionThreshold)
            {
                return middle;
            }

            return normal;
        }

        private static float ComputeGeometryConfidence(int pointCount, Vector3 extent, float lambda1, GenericPoseConfig config)
        {
            var pointScore = Mathf.Clamp01(pointCount / 120f);
            var maxExtent = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            var minExtent = Mathf.Min(extent.x, Mathf.Min(extent.y, extent.z));
            var extentScore = maxExtent >= config.MinimumExtentMeters &&
                              maxExtent <= config.MaximumExtentMeters &&
                              minExtent >= 0f
                ? 1f
                : 0.25f;
            var eigenScore = lambda1 > Epsilon ? 1f : 0f;
            return GenericPoseMath.Clamp01Safe(0.45f * pointScore + 0.35f * extentScore + 0.20f * eigenScore);
        }

        private static float ComputeOrientationConfidence(GenericShapeType shape, float linearity, float planarity)
        {
            switch (shape)
            {
                case GenericShapeType.Elongated:
                    return GenericPoseMath.Clamp01Safe(linearity);
                case GenericShapeType.Planar:
                    return GenericPoseMath.Clamp01Safe(planarity);
                case GenericShapeType.BoxLike:
                    return GenericPoseMath.Clamp01Safe(Mathf.Max(linearity, planarity) * 0.75f);
                case GenericShapeType.Ambiguous:
                    return 0.2f;
                default:
                    return 0f;
            }
        }

        public static float ComputeOverallConfidence(float geometry, float orientation, float tracking)
        {
            return GenericPoseMath.Clamp01Safe(0.30f * geometry + 0.45f * orientation + 0.25f * tracking);
        }

        public static GenericPoseStability StabilityFromConfidence(
            float confidence,
            GenericShapeType shape,
            GenericPoseConfig config)
        {
            if (shape == GenericShapeType.Ambiguous && confidence >= config.StableConfidenceThreshold)
            {
                return GenericPoseStability.Weak;
            }

            if (confidence >= config.StableConfidenceThreshold)
            {
                return GenericPoseStability.Stable;
            }

            return confidence >= config.WeakConfidenceThreshold
                ? GenericPoseStability.Weak
                : GenericPoseStability.Unreliable;
        }
    }
}
