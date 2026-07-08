using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARObjectReplacement.Depth
{
    public static class ARDepthCrosshairBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[M2 Depth] Main Camera was not found. Depth crosshair was not installed.");
                return;
            }

            var cameraManager = camera.GetComponent<ARCameraManager>();
            if (cameraManager == null)
            {
                cameraManager = camera.gameObject.AddComponent<ARCameraManager>();
            }

            var occlusionManager = camera.GetComponent<AROcclusionManager>();
            if (occlusionManager == null)
            {
                occlusionManager = camera.gameObject.AddComponent<AROcclusionManager>();
            }

            occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;

            var crosshair = camera.GetComponent<ARDepthCrosshairMeasure>();
            if (crosshair == null)
            {
                crosshair = camera.gameObject.AddComponent<ARDepthCrosshairMeasure>();
            }

            crosshair.CameraManager = cameraManager;
            crosshair.OcclusionManager = occlusionManager;
        }
    }
}

