using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ARObjectReplacement.Geometry
{
    public sealed class GeometryService
    {
        public WorldPoint PixelToWorld(
            Vector2 cameraPixel,
            float depthMeters,
            XRCameraIntrinsics intrinsics,
            Transform cameraTransform,
            double timestamp)
        {
            if (cameraTransform == null ||
                !IsValidDepth(depthMeters) ||
                intrinsics.resolution.x <= 0 ||
                intrinsics.resolution.y <= 0)
            {
                return WorldPoint.Invalid(cameraPixel, depthMeters, timestamp);
            }

            var cameraPoint = CoordinateConverter.PixelToCameraPoint(cameraPixel, depthMeters, intrinsics);
            if (cameraPoint == Vector3.zero)
            {
                return WorldPoint.Invalid(cameraPixel, depthMeters, timestamp);
            }

            var worldPosition = CoordinateConverter.CameraPointToWorldPoint(cameraPoint, cameraTransform);
            return new WorldPoint
            {
                IsValid = true,
                Position = worldPosition,
                Pixel = cameraPixel,
                CameraPoint = cameraPoint,
                DepthMeters = depthMeters,
                Timestamp = timestamp
            };
        }

        public WorldPoint ScreenPixelToWorld(
            Vector2 screenPixel,
            Vector2Int screenResolution,
            float depthMeters,
            XRCameraIntrinsics intrinsics,
            Transform cameraTransform,
            double timestamp)
        {
            var cameraPixel = CoordinateConverter.ScreenPixelToCameraPixel(
                screenPixel,
                screenResolution,
                intrinsics.resolution);

            return PixelToWorld(cameraPixel, depthMeters, intrinsics, cameraTransform, timestamp);
        }

        private static bool IsValidDepth(float depthMeters)
        {
            return depthMeters > 0f &&
                   !float.IsNaN(depthMeters) &&
                   !float.IsInfinity(depthMeters);
        }
    }
}

