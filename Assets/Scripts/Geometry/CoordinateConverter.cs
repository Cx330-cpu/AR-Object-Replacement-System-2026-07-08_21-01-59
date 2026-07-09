using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ARObjectReplacement.Geometry
{
    public static class CoordinateConverter
    {
        public static Vector2 ScreenPixelToCameraPixel(
            Vector2 screenPixel,
            Vector2Int screenResolution,
            Vector2Int cameraResolution)
        {
            var screenWidth = Mathf.Max(1, screenResolution.x);
            var screenHeight = Mathf.Max(1, screenResolution.y);

            return new Vector2(
                screenPixel.x * cameraResolution.x / screenWidth,
                (screenHeight - screenPixel.y) * cameraResolution.y / screenHeight);
        }

        public static Vector3 PixelToCameraPoint(
            Vector2 cameraPixel,
            float depthMeters,
            XRCameraIntrinsics intrinsics)
        {
            var focalLength = intrinsics.focalLength;
            var principalPoint = intrinsics.principalPoint;

            if (depthMeters <= 0f ||
                focalLength.x <= 0f ||
                focalLength.y <= 0f)
            {
                return Vector3.zero;
            }

            var x = (cameraPixel.x - principalPoint.x) * depthMeters / focalLength.x;
            var y = -(cameraPixel.y - principalPoint.y) * depthMeters / focalLength.y;
            var z = depthMeters;

            return new Vector3(x, y, z);
        }

        public static Vector3 CameraPointToWorldPoint(Vector3 cameraPoint, Transform cameraTransform)
        {
            return cameraTransform.TransformPoint(cameraPoint);
        }
    }
}
