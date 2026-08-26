using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ARObjectReplacement.Detection;
using ARObjectReplacement.Evaluation;
using ARObjectReplacement.PointCloud;
using ARObjectReplacement.Pose;
using ARObjectReplacement.Rendering;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;

namespace ARObjectReplacement.Demo
{
    public sealed class PointCloudCaptureDemo : MonoBehaviour
    {
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField] private AROcclusionManager occlusionManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private int roiWidth = 120;
        [SerializeField] private int roiHeight = 120;
        [SerializeField] private float voxelSizeMeters = 0.01f;
        [SerializeField] private float minDepthMeters = 0.05f;
        [SerializeField] private float maxDepthMeters = 8f;
        [SerializeField] private float minimumConfidence = 0.0f;
        [SerializeField] private float outlierRadiusMeters = 0.05f;
        [SerializeField] private int outlierMinimumNeighbors = 1;
        [SerializeField] private float yoloRoiExpandRatio = 0.12f;
        [SerializeField] private int yoloRoiMinExpandPixels = 16;
        [SerializeField] private float yoloConfidenceThreshold = 0.12f;
        [SerializeField] private float yoloDetectionHoldSeconds = 5.0f;
        [SerializeField] private int maxVisualPoints = 2500;
        [SerializeField] private float pointVisualSizeMeters = 0.006f;
        [SerializeField] private bool realtimeYoloEnabled = true;
        [SerializeField] private float realtimeYoloIntervalSeconds = 0.25f;
        [SerializeField] private bool realtimeDirectionEnabled = true;
        [SerializeField] private float realtimeDirectionIntervalSeconds = 0.25f;
        [SerializeField] private float directionAxisLengthMeters = 0.12f;
        [SerializeField] private float directionAxisThicknessMeters = 0.008f;
        [SerializeField] private float targetCenterSphereDiameterMeters = 0.045f;
        [SerializeField] private bool replacementModelEnabled = true;
        [SerializeField] private ReplacementModelRegistry replacementModelRegistry;
        [SerializeField] private float replacementDefaultScaleMeters = 0.18f;
        [SerializeField] private int yoloInputWidth = 960;
        [SerializeField] private int yoloInputHeight = 720;
        [SerializeField] private RuntimePoseMode runtimePoseMode = RuntimePoseMode.Auto;
        [SerializeField] private bool surfaceForwardFacesCamera = true;
        [SerializeField] private float flatObjectHeightThresholdMeters = 0.08f;
        [SerializeField] private GenericPoseConfig genericPoseConfig = new GenericPoseConfig();

        private readonly PointCloudBuilder builder = new PointCloudBuilder();
        private readonly PointCloudDownSampler downSampler = new PointCloudDownSampler();
        private readonly PointCloudCleaner cleaner = new PointCloudCleaner();
        private readonly PointCloudExporter exporter = new PointCloudExporter();
        private readonly YoloCoreMLDetector detector = new YoloCoreMLDetector();
        private readonly GenericPoseEstimator genericPoseEstimator = new GenericPoseEstimator();
        private readonly GenericPoseStabilizer genericPoseStabilizer = new GenericPoseStabilizer();
        private ReplacementModelController replacementModelController;
        private Text statusText;
        private Text directionText;
        private GameObject pointCloudVisual;
        private MeshFilter pointCloudMeshFilter;
        private MeshRenderer pointCloudRenderer;
        private GameObject directionAxisRoot;
        private LineRenderer directionAxisMajor;
        private LineRenderer directionAxisMiddle;
        private LineRenderer directionAxisNormal;
        private GameObject targetCenterSphere;
        private MeshRenderer targetCenterSphereRenderer;
        private RectTransform detectionBox;
        private RectTransform detectionAnchorMarker;
        private Text detectionText;
        private bool hasLatestDetection;
        private DetectionResult latestDetection;
        private float lastRealtimeYoloTime;
        private float lastRealtimeDirectionTime;
        private float lastGenericPoseLogTime;
        private readonly List<ARRaycastHit> planeRaycastHits = new List<ARRaycastHit>();
        private readonly TrialLogger trialLogger = new TrialLogger();
        private PoseAnchorSelection latestAnchorSelection;
        private TrialObjectKind selectedTrialObject = TrialObjectKind.None;
        private Button cupButton;
        private Button phoneButton;
        private Button laptopButton;
        private Button recordButton;
        private Text recordButtonLabel;
        private Text trialStatusText;
        private float lastDetectMs;

        private struct PoseAnchorSelection
        {
            public Vector2 Pixel;
            public string Label;
            public bool IsValid;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[M4 PointCloud] Main Camera was not found. Point cloud capture demo was not installed.");
                return;
            }

            if (camera.GetComponent<PointCloudCaptureDemo>() == null)
            {
                camera.gameObject.AddComponent<PointCloudCaptureDemo>();
            }
        }

        private void Awake()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                cameraManager = cameraManager != null ? cameraManager : camera.GetComponent<ARCameraManager>();
                occlusionManager = occlusionManager != null ? occlusionManager : camera.GetComponent<AROcclusionManager>();

                if (cameraManager == null)
                {
                    cameraManager = camera.gameObject.AddComponent<ARCameraManager>();
                }

                if (occlusionManager == null)
                {
                    occlusionManager = camera.gameObject.AddComponent<AROcclusionManager>();
                }
            }

            if (occlusionManager != null)
            {
                occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                occlusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
            }

            replacementModelRegistry = replacementModelRegistry != null
                ? replacementModelRegistry
                : FindObjectOfType<ReplacementModelRegistry>();
            replacementModelController = new ReplacementModelController(
                "ReplacementModels",
                replacementDefaultScaleMeters,
                replacementModelRegistry);
            EnsurePlaneManagers();
            CreateControls();
            RefreshTrialUi();
        }

        private void OnDestroy()
        {
            trialLogger?.Stop();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                trialLogger?.Flush();
            }
        }

        private void EnsurePlaneManagers()
        {
            if (planeManager == null)
            {
                planeManager = FindObjectOfType<ARPlaneManager>();
            }

            if (raycastManager == null)
            {
                raycastManager = FindObjectOfType<ARRaycastManager>();
            }

            if (planeManager == null)
            {
                var xrOrigin = GameObject.Find("XR Origin");
                if (xrOrigin != null)
                {
                    planeManager = xrOrigin.GetComponent<ARPlaneManager>();
                    if (planeManager == null)
                    {
                        planeManager = xrOrigin.AddComponent<ARPlaneManager>();
                    }
                }
            }

            if (raycastManager == null)
            {
                var xrOrigin = GameObject.Find("XR Origin");
                if (xrOrigin != null)
                {
                    raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
                    if (raycastManager == null)
                    {
                        raycastManager = xrOrigin.AddComponent<ARRaycastManager>();
                    }
                }
            }

            if (planeManager != null)
            {
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                planeManager.enabled = true;
            }

            if (raycastManager != null)
            {
                raycastManager.enabled = true;
            }
        }

        private void Update()
        {
            if (realtimeYoloEnabled && Time.time - lastRealtimeYoloTime >= realtimeYoloIntervalSeconds)
            {
                lastRealtimeYoloTime = Time.time;
                var detectWatch = Stopwatch.StartNew();
                hasLatestDetection = TryDetectCenterObject(out latestDetection, true);
                lastDetectMs = (float)detectWatch.Elapsed.TotalMilliseconds;
            }

            if (realtimeDirectionEnabled && Time.time - lastRealtimeDirectionTime >= realtimeDirectionIntervalSeconds)
            {
                lastRealtimeDirectionTime = Time.time;
                UpdateRealtimeDirection();
            }
        }

        private void CapturePointCloud()
        {
            var stopwatch = Stopwatch.StartNew();

            if (occlusionManager == null || cameraManager == null)
            {
                SetStatus("PointCloud: missing AR managers");
                return;
            }

            if (!cameraManager.TryGetIntrinsics(out var intrinsics))
            {
                SetStatus("PointCloud: camera intrinsics unavailable");
                return;
            }

            var hasDetection = HasFreshYoloDetection();
            var detection = latestDetection;
            if (!hasDetection)
            {
                hasDetection = TryDetectCenterObject(out detection, true);
                if (hasDetection)
                {
                    latestDetection = detection;
                    hasLatestDetection = true;
                }
            }

            if (!hasDetection && selectedTrialObject != TrialObjectKind.None)
            {
                SetStatus("PointCloud: YOLO 未检出，把物体对准屏幕中心后再试");
                return;
            }

            if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out var depthImage))
            {
                SetStatus("PointCloud: LiDAR depth unavailable");
                return;
            }

            PointCloudData filteredCloud;
            RectInt roi;
            using (depthImage)
            {
                using var confidenceImage = TryAcquireConfidenceImage();
                var depthFrame = CreateDepthFrame(depthImage, confidenceImage);
                roi = hasDetection
                    ? CreateYoloRoi(detection.PixelRect, depthFrame.Width, depthFrame.Height)
                    : CreateCenterRoi(depthFrame.Width, depthFrame.Height);

                var rawCloud = builder.BuildPointCloud(
                    depthFrame,
                    roi,
                    intrinsics,
                    minDepthMeters,
                    maxDepthMeters,
                    minimumConfidence);

                var cleaned = cleaner.RemoveInvalidAndOutOfRange(
                    rawCloud,
                    minDepthMeters,
                    maxDepthMeters,
                    minimumConfidence);

                var downsampled = downSampler.VoxelDownSample(cleaned, voxelSizeMeters);
                filteredCloud = cleaner.RadiusOutlierRemoval(
                    downsampled,
                    outlierRadiusMeters,
                    outlierMinimumNeighbors);
                filteredCloud.VoxelSizeMeters = voxelSizeMeters;
            }

            var exportStopwatch = Stopwatch.StartNew();
            var path = BuildOutputPath();
            exporter.ExportPLY(filteredCloud, path);
            var latestPath = BuildLatestOutputPath();
            File.Copy(path, latestPath, true);
            WriteLatestManifest(path, latestPath, filteredCloud);
            exportStopwatch.Stop();
            stopwatch.Stop();
            filteredCloud.ExportTimeMs = (float)exportStopwatch.Elapsed.TotalMilliseconds;
            var displayed = DisplayPointCloud(filteredCloud, cameraManager.transform);
            var frame = EstimateRuntimePoseFrame(
                filteredCloud,
                cameraManager.transform,
                filteredCloud.Timestamp,
                hasDetection ? detection : default,
                hasDetection,
                out var activeMode);
            UpdateGenericPoseDisplay(frame, cameraManager.transform, hasDetection ? "YOLO ROI" : "Center ROI", activeMode, hasDetection ? detection.ClassId : -1);

            var fps = stopwatch.Elapsed.TotalSeconds > 0 ? 1.0 / stopwatch.Elapsed.TotalSeconds : 0.0;
            var message =
                $"{(displayed ? "PointCloud displayed" : "PointCloud saved, no visible points")}\n" +
                $"{(hasDetection ? $"YOLO {CocoClassNames.GetName(detection.ClassId)}, conf={detection.Confidence:F2}, anchor={latestAnchorSelection.Label}" : "YOLO unavailable: center ROI fallback")}\n" +
                $"raw={filteredCloud.RawPointCount}, filtered={filteredCloud.FilteredPointCount}\n" +
                $"voxel={voxelSizeMeters:F3}m, export={filteredCloud.ExportTimeMs:F1}ms, fps={fps:F1}\n" +
                $"latest={latestPath}";

            SetStatus(message);
            Debug.Log($"[M4 PointCloud] ROI={roi} raw_points={filteredCloud.RawPointCount} " +
                      $"filtered_points={filteredCloud.FilteredPointCount} voxel_size={voxelSizeMeters:F3}m " +
                      $"yolo={(hasDetection ? $"class={detection.ClassId}, confidence={detection.Confidence:F3}, bbox={detection.PixelRect}" : "fallback_center_roi")} " +
                      $"export_time_ms={filteredCloud.ExportTimeMs:F1} capture_time_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
                      $"fps={fps:F1} path={path} latest_path={latestPath}");
            LogTrialFrame(
                "capture",
                frame,
                filteredCloud,
                hasDetection ? detection : default,
                hasDetection,
                hasDetection ? "YOLO ROI" : "Center ROI",
                activeMode,
                lastDetectMs,
                0f,
                0f,
                filteredCloud.ExportTimeMs,
                (float)stopwatch.Elapsed.TotalMilliseconds,
                path);
        }

        private void UpdateRealtimeDirection()
        {
            var e2eWatch = Stopwatch.StartNew();
            var cloudWatch = Stopwatch.StartNew();
            var hasCloud = TryBuildCurrentDirectionCloud(out var pointCloud, out var roiSource);
            var cloudMs = (float)cloudWatch.Elapsed.TotalMilliseconds;
            if (!hasCloud)
            {
                HideDirectionAxis();
                HideTargetCenterSphere();
                SetDirectionText("Direction: waiting for LiDAR ROI");
                if (trialLogger.IsRecording)
                {
                    LogTrialFrame(
                        "realtime",
                        GenericPoseFrame.Invalid("waiting for LiDAR ROI"),
                        pointCloud,
                        hasLatestDetection ? latestDetection : default,
                        HasFreshYoloDetection(),
                        roiSource,
                        runtimePoseMode,
                        lastDetectMs,
                        cloudMs,
                        0f,
                        0f,
                        (float)e2eWatch.Elapsed.TotalMilliseconds,
                        string.Empty);
                }

                return;
            }

            var hasRecentDetection = HasFreshYoloDetection();
            var poseWatch = Stopwatch.StartNew();
            var frame = EstimateRuntimePoseFrame(
                pointCloud,
                cameraManager != null ? cameraManager.transform : null,
                pointCloud != null ? pointCloud.Timestamp : Time.timeAsDouble,
                hasRecentDetection ? latestDetection : default,
                hasRecentDetection,
                out var activeMode);
            var poseMs = (float)poseWatch.Elapsed.TotalMilliseconds;
            UpdateGenericPoseDisplay(
                frame,
                cameraManager != null ? cameraManager.transform : null,
                roiSource,
                activeMode,
                hasRecentDetection ? latestDetection.ClassId : -1);
            LogTrialFrame(
                "realtime",
                frame,
                pointCloud,
                hasRecentDetection ? latestDetection : default,
                hasRecentDetection,
                roiSource,
                activeMode,
                lastDetectMs,
                cloudMs,
                poseMs,
                0f,
                (float)e2eWatch.Elapsed.TotalMilliseconds,
                string.Empty);
        }

        private bool TryBuildCurrentDirectionCloud(out PointCloudData filteredCloud, out string roiSource)
        {
            filteredCloud = null;
            roiSource = "Center ROI";

            if (occlusionManager == null || cameraManager == null)
            {
                return false;
            }

            if (!cameraManager.TryGetIntrinsics(out var intrinsics))
            {
                return false;
            }

            if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out var depthImage))
            {
                return false;
            }

            using (depthImage)
            {
                var hasDetection = HasFreshYoloDetection();
                if (!hasDetection && selectedTrialObject != TrialObjectKind.None)
                {
                    roiSource = "YOLO lost";
                    return false;
                }

                using var confidenceImage = TryAcquireConfidenceImage();
                var depthFrame = CreateDepthFrame(depthImage, confidenceImage);
                var roi = hasDetection
                    ? CreateYoloRoi(latestDetection.PixelRect, depthFrame.Width, depthFrame.Height)
                    : CreateCenterRoi(depthFrame.Width, depthFrame.Height);

                roiSource = hasDetection ? "YOLO ROI" : "Center ROI";
                var rawCloud = builder.BuildPointCloud(
                    depthFrame,
                    roi,
                    intrinsics,
                    minDepthMeters,
                    maxDepthMeters,
                    minimumConfidence);

                var cleaned = cleaner.RemoveInvalidAndOutOfRange(
                    rawCloud,
                    minDepthMeters,
                    maxDepthMeters,
                    minimumConfidence);

                var downsampled = downSampler.VoxelDownSample(cleaned, voxelSizeMeters);
                filteredCloud = cleaner.RadiusOutlierRemoval(
                    downsampled,
                    outlierRadiusMeters,
                    outlierMinimumNeighbors);
                filteredCloud.VoxelSizeMeters = voxelSizeMeters;
                return filteredCloud.Points != null && filteredCloud.Points.Count > 0;
            }
        }

        private GenericPoseFrame EstimateRuntimePoseFrame(
            PointCloudData pointCloud,
            Transform cameraTransform,
            double timestamp,
            DetectionResult detection,
            bool hasDetection,
            out RuntimePoseMode activeMode)
        {
            var worldUpCamera = cameraTransform != null
                ? cameraTransform.InverseTransformDirection(Vector3.up).normalized
                : Vector3.up;
            var rawFrame = genericPoseEstimator.Estimate(pointCloud, genericPoseConfig, worldUpCamera, timestamp);
            var freeFrame = genericPoseStabilizer.Update(rawFrame, genericPoseConfig, timestamp);
            var hasSupportPlane = TryFindSupportingHorizontalPlane(
                freeFrame,
                cameraTransform,
                out var supportPlaneUpCamera,
                out var supportPlaneHeight,
                out var supportPlaneId);
            activeMode = ResolveActivePoseMode(freeFrame, hasSupportPlane);
            var anchorSelection = SelectPoseAnchor(detection, hasDetection, freeFrame, hasSupportPlane, supportPlaneHeight);
            latestAnchorSelection = anchorSelection;
            return activeMode == RuntimePoseMode.SurfaceObject
                ? BuildSurfaceObjectFrame(
                    freeFrame,
                    hasSupportPlane ? supportPlaneUpCamera : worldUpCamera,
                    hasSupportPlane,
                    supportPlaneHeight,
                    supportPlaneId,
                    anchorSelection.IsValid ? DetectionPixelToUnityScreenPoint(anchorSelection.Pixel) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                    anchorSelection.IsValid,
                    cameraTransform)
                : freeFrame;
        }

        private RuntimePoseMode ResolveActivePoseMode(GenericPoseFrame frame, bool hasSupportPlane)
        {
            if (runtimePoseMode == RuntimePoseMode.SurfaceObject ||
                runtimePoseMode == RuntimePoseMode.FreeObject)
            {
                return runtimePoseMode;
            }

            if (hasSupportPlane)
            {
                return RuntimePoseMode.SurfaceObject;
            }

            if (!frame.IsValid || frame.Stability == GenericPoseStability.TrackingLost)
            {
                return RuntimePoseMode.SurfaceObject;
            }

            var freeShape = frame.ShapeType == GenericShapeType.Elongated ||
                            frame.ShapeType == GenericShapeType.BoxLike;
            var freeConfidence = frame.OverallConfidence >= genericPoseConfig.AutoFreeObjectConfidenceThreshold;
            var enoughThickness = frame.Scattering >= genericPoseConfig.AutoFreeObjectMinimumScattering;
            var notDominatedByPlane = frame.Planarity <= genericPoseConfig.AutoFreeObjectMaximumPlanarity;
            return freeShape && freeConfidence && enoughThickness && notDominatedByPlane
                ? RuntimePoseMode.FreeObject
                : RuntimePoseMode.SurfaceObject;
        }

        private PoseAnchorSelection SelectPoseAnchor(
            DetectionResult detection,
            bool hasDetection,
            GenericPoseFrame frame,
            bool hasSupportPlane,
            float supportPlaneHeightMeters)
        {
            if (!hasDetection || !detection.IsValid)
            {
                return default;
            }

            var isFlatObject = IsFlatSurfaceObject(frame, hasSupportPlane, supportPlaneHeightMeters);
            if (isFlatObject)
            {
                return new PoseAnchorSelection
                {
                    Pixel = detection.HasMaskCenter ? detection.MaskCenter : detection.Center,
                    Label = detection.HasMaskCenter ? "mask center flat" : "bbox center flat",
                    IsValid = true
                };
            }

            return new PoseAnchorSelection
            {
                Pixel = detection.HasMaskBottomCenter ? detection.MaskBottomCenter : detection.BottomCenter,
                Label = detection.HasMaskBottomCenter ? "mask bottom upright" : "bbox bottom upright",
                IsValid = true
            };
        }

        private bool IsFlatSurfaceObject(GenericPoseFrame frame, bool hasSupportPlane, float supportPlaneHeightMeters)
        {
            var threshold = Mathf.Max(0.005f, flatObjectHeightThresholdMeters);
            if (hasSupportPlane && supportPlaneHeightMeters > 0f && supportPlaneHeightMeters <= threshold)
            {
                return true;
            }

            var minExtent = Mathf.Min(frame.ExtentMeters.x, Mathf.Min(frame.ExtentMeters.y, frame.ExtentMeters.z));
            if (minExtent > 0f && minExtent <= threshold)
            {
                return true;
            }

            var verticalExtent = Mathf.Abs(Vector3.Dot(frame.AxisMajorCamera, frame.UpCamera)) * frame.ExtentMeters.x +
                                 Mathf.Abs(Vector3.Dot(frame.AxisMiddleCamera, frame.UpCamera)) * frame.ExtentMeters.y +
                                 Mathf.Abs(Vector3.Dot(frame.AxisNormalCamera, frame.UpCamera)) * frame.ExtentMeters.z;
            return verticalExtent > 0f && verticalExtent <= threshold;
        }

        private bool TryFindSupportingHorizontalPlane(
            GenericPoseFrame frame,
            Transform cameraTransform,
            out Vector3 planeUpCamera,
            out float heightMeters,
            out TrackableId planeId)
        {
            planeUpCamera = Vector3.up;
            heightMeters = 0f;
            planeId = TrackableId.invalidId;
            if (planeManager == null ||
                cameraTransform == null ||
                (!frame.IsValid && frame.Stability != GenericPoseStability.TrackingLost))
            {
                return false;
            }

            var centerWorld = cameraTransform.TransformPoint(frame.CenterCamera);
            var bestScore = float.PositiveInfinity;
            var bestNormalWorld = Vector3.up;
            var bestHeight = 0f;
            var bestPlaneId = TrackableId.invalidId;

            foreach (var plane in planeManager.trackables)
            {
                if (plane == null ||
                    plane.trackingState != TrackingState.Tracking ||
                    plane.alignment != PlaneAlignment.HorizontalUp)
                {
                    continue;
                }

                var normalWorld = plane.transform.up.normalized;
                if (Vector3.Dot(normalWorld, Vector3.up) < genericPoseConfig.SurfacePlaneNormalUpDotThreshold)
                {
                    continue;
                }

                var planeCenter = plane.transform.position;
                var height = Vector3.Dot(centerWorld - planeCenter, normalWorld);
                if (height < genericPoseConfig.SurfacePlaneMinimumHeightMeters ||
                    height > genericPoseConfig.SurfacePlaneMaximumHeightMeters)
                {
                    continue;
                }

                var horizontalOffset = Vector3.ProjectOnPlane(centerWorld - planeCenter, normalWorld).magnitude;
                var size = plane.size;
                var allowedOffset = Mathf.Max(size.x, size.y) * 0.5f + genericPoseConfig.SurfacePlaneHorizontalMarginMeters;
                if (horizontalOffset > allowedOffset)
                {
                    continue;
                }

                var score = height + horizontalOffset * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestNormalWorld = normalWorld;
                    bestHeight = height;
                    bestPlaneId = plane.trackableId;
                }
            }

            if (float.IsPositiveInfinity(bestScore))
            {
                return false;
            }

            planeUpCamera = cameraTransform.InverseTransformDirection(bestNormalWorld).normalized;
            heightMeters = bestHeight;
            planeId = bestPlaneId;
            return true;
        }

        private GenericPoseFrame BuildSurfaceObjectFrame(
            GenericPoseFrame source,
            Vector3 surfaceUpCamera,
            bool hasSupportPlane,
            float supportPlaneHeightMeters,
            TrackableId supportPlaneId,
            Vector2 objectScreenPoint,
            bool hasObjectScreenPoint,
            Transform cameraTransform)
        {
            if (!source.IsValid && source.Stability != GenericPoseStability.TrackingLost)
            {
                return source;
            }

            var up = GenericPoseMath.SafeNormalize(surfaceUpCamera, Vector3.up);
            var faceCandidate = surfaceForwardFacesCamera ? Vector3.back : Vector3.forward;
            var fallback = Mathf.Abs(Vector3.Dot(source.AxisMajorCamera, up)) < genericPoseConfig.ParallelDirectionThreshold
                ? source.AxisMajorCamera
                : source.AxisMiddleCamera;
            GenericPoseMath.BuildOrthonormalFrame(
                faceCandidate,
                up,
                fallback,
                genericPoseConfig.ParallelDirectionThreshold,
                out var right,
                out up,
                out var forward);

            source.RightCamera = right;
            source.UpCamera = up;
            source.ForwardCamera = forward;
            var usedPlaneRaycastOrigin = false;
            if (hasObjectScreenPoint &&
                TryGetPlaneOriginCamera(
                    objectScreenPoint,
                    cameraTransform,
                    hasSupportPlane ? supportPlaneId : TrackableId.invalidId,
                    out var planeOriginCamera))
            {
                source.CenterCamera = planeOriginCamera;
                usedPlaneRaycastOrigin = true;
            }
            else if (hasSupportPlane)
            {
                source.CenterCamera -= up * supportPlaneHeightMeters;
            }
            source.OrientationConfidence = 1f;
            source.OverallConfidence = GenericPoseMath.Clamp01Safe(
                0.55f * source.GeometryConfidence + 0.45f * source.TrackingConfidence);
            source.Stability = source.OverallConfidence >= genericPoseConfig.StableConfidenceThreshold
                ? GenericPoseStability.Stable
                : source.OverallConfidence >= genericPoseConfig.WeakConfidenceThreshold
                    ? GenericPoseStability.Weak
                    : GenericPoseStability.Unreliable;
            if (source.ShapeType == GenericShapeType.Unknown)
            {
                source.ShapeType = GenericShapeType.Planar;
            }
            if (hasSupportPlane)
            {
                var originLabel = usedPlaneRaycastOrigin ? "ARRaycast origin" : "height fallback";
                source.Message = surfaceForwardFacesCamera
                    ? $"SurfaceObject: {originLabel} + ARPlane height={supportPlaneHeightMeters:F2}m + face camera"
                    : $"SurfaceObject: {originLabel} + ARPlane height={supportPlaneHeightMeters:F2}m + camera direction";
            }
            else if (usedPlaneRaycastOrigin)
            {
                source.Message = surfaceForwardFacesCamera
                    ? "SurfaceObject: anchor ARRaycast origin + face camera"
                    : "SurfaceObject: anchor ARRaycast origin + camera direction";
            }
            else
            {
                source.Message = surfaceForwardFacesCamera
                    ? "SurfaceObject: center + world-up fallback + face camera"
                    : "SurfaceObject: center + world-up fallback + camera direction";
            }
            return source;
        }

        private bool TryGetPlaneOriginCamera(
            Vector2 screenPoint,
            Transform cameraTransform,
            TrackableId requiredPlaneId,
            out Vector3 originCamera)
        {
            originCamera = Vector3.zero;
            if (raycastManager == null || cameraTransform == null)
            {
                return false;
            }

            var sampleRadii = new[] { 0f, 16f, 32f, 56f };
            for (var radiusIndex = 0; radiusIndex < sampleRadii.Length; radiusIndex++)
            {
                var radius = sampleRadii[radiusIndex];
                if (radius <= 0f)
                {
                    if (TryGetPlaneOriginCameraAtScreenPoint(screenPoint, cameraTransform, requiredPlaneId, out originCamera))
                    {
                        return true;
                    }
                    continue;
                }

                var offsets = new[]
                {
                    new Vector2(0f, -radius),
                    new Vector2(-radius, 0f),
                    new Vector2(radius, 0f),
                    new Vector2(0f, radius),
                    new Vector2(-radius, -radius),
                    new Vector2(radius, -radius),
                    new Vector2(-radius, radius),
                    new Vector2(radius, radius)
                };

                for (var i = 0; i < offsets.Length; i++)
                {
                    var candidate = screenPoint + offsets[i];
                    candidate.x = Mathf.Clamp(candidate.x, 0f, Screen.width);
                    candidate.y = Mathf.Clamp(candidate.y, 0f, Screen.height);
                    if (TryGetPlaneOriginCameraAtScreenPoint(candidate, cameraTransform, requiredPlaneId, out originCamera))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetPlaneOriginCameraAtScreenPoint(
            Vector2 screenPoint,
            Transform cameraTransform,
            TrackableId requiredPlaneId,
            out Vector3 originCamera)
        {
            originCamera = Vector3.zero;
            planeRaycastHits.Clear();
            var trackableTypes =
                TrackableType.PlaneWithinPolygon |
                TrackableType.PlaneWithinBounds |
                TrackableType.PlaneEstimated;
            if (!raycastManager.Raycast(screenPoint, planeRaycastHits, trackableTypes))
            {
                return false;
            }

            for (var i = 0; i < planeRaycastHits.Count; i++)
            {
                var hit = planeRaycastHits[i];
                if (requiredPlaneId != TrackableId.invalidId && hit.trackableId != requiredPlaneId)
                {
                    continue;
                }

                var plane = planeManager != null ? planeManager.GetPlane(hit.trackableId) : null;
                if (plane != null)
                {
                    if (plane.trackingState != TrackingState.Tracking ||
                        plane.alignment != PlaneAlignment.HorizontalUp)
                    {
                        continue;
                    }
                }
                else if (Vector3.Dot(hit.pose.up.normalized, Vector3.up) < genericPoseConfig.SurfacePlaneNormalUpDotThreshold)
                {
                    continue;
                }

                originCamera = cameraTransform.InverseTransformPoint(hit.pose.position);
                return GenericPoseMath.IsFinite(originCamera);
            }

            return false;
        }

        private static Vector2 DetectionPixelToUnityScreenPoint(Vector2 detectionPixel)
        {
            return new Vector2(
                detectionPixel.x,
                Screen.height - detectionPixel.y);
        }

        private void UpdateGenericPoseDisplay(
            GenericPoseFrame frame,
            Transform cameraTransform,
            string roiSource,
            RuntimePoseMode activeMode,
            int classId)
        {
            if ((!frame.IsValid && frame.Stability != GenericPoseStability.TrackingLost) || cameraTransform == null)
            {
                HideDirectionAxis();
                HideTargetCenterSphere();
                replacementModelController?.Hide();
                SetDirectionText($"{frame.Message}\nsource={roiSource}, points={frame.PointCount}");
                return;
            }

            DisplayTargetCenterSphere(frame, cameraTransform);
            DisplayGenericPoseAxis(frame, cameraTransform);
            var replacementClassId = ResolveReplacementClassId(classId);
            if (replacementModelEnabled)
            {
                replacementModelController?.UpdateModel(frame, cameraTransform, replacementClassId, activeMode);
            }
            else
            {
                replacementModelController?.Hide();
            }
            var originWorld = cameraTransform.TransformPoint(frame.CenterCamera);
            SetDirectionText(
                $"Pose {activeMode} {roiSource}\n" +
                $"{frame.Message}\n" +
                $"shape={frame.ShapeType}, status={frame.Stability}, overall={frame.OverallConfidence:F2}\n" +
                $"geo={frame.GeometryConfidence:F2}, orient={frame.OrientationConfidence:F2}, track={frame.TrackingConfidence:F2}, points={frame.PointCount}\n" +
                $"origin_world={FormatVector(originWorld)}m\n" +
                $"origin_camera={FormatVector(frame.CenterCamera)}m\n" +
                $"anchor={(latestAnchorSelection.IsValid ? latestAnchorSelection.Label : "none")}\n" +
                $"extent={frame.ExtentMeters.x:F2}/{frame.ExtentMeters.y:F2}/{frame.ExtentMeters.z:F2}m\n" +
                $"right={FormatVector(frame.RightCamera)}\n" +
                $"up={FormatVector(frame.UpCamera)}\n" +
                $"forward={FormatVector(frame.ForwardCamera)}\n" +
                $"model={GetReplacementModelLabel()}" +
                (genericPoseConfig.ShowDebugPcaValues
                    ? $"\nlin/plan/scat={frame.Linearity:F2}/{frame.Planarity:F2}/{frame.Scattering:F2}"
                    : string.Empty));
            LogGenericPoseFrame(frame, roiSource, activeMode);
        }

        private bool TryDetectCenterObject(out DetectionResult detection, bool updateOverlay)
        {
            detection = default;
            if (!detector.IsAvailable || cameraManager == null)
            {
                if (updateOverlay)
                {
                    HideDetectionBox();
                    SetDetectionLabel("YOLO unavailable");
                }
                return false;
            }

            if (!cameraManager.TryAcquireLatestCpuImage(out var cameraImage))
            {
                if (updateOverlay)
                {
                    HideDetectionBox();
                    SetDetectionLabel("YOLO waiting for camera");
                }
                return false;
            }

            using (cameraImage)
            {
                var outputWidth = Mathf.Clamp(yoloInputWidth, 64, cameraImage.width);
                var outputHeight = Mathf.Clamp(yoloInputHeight, 64, cameraImage.height);
                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, cameraImage.width, cameraImage.height),
                    outputDimensions = new Vector2Int(outputWidth, outputHeight),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = XRCpuImage.Transformation.None
                };

                var byteCount = cameraImage.GetConvertedDataSize(conversionParams);
                var rgbaBytes = new NativeArray<byte>(byteCount, Allocator.Temp);
                try
                {
                    cameraImage.Convert(conversionParams, rgbaBytes);
                    var success = detector.TryDetectCenterObject(
                        rgbaBytes.ToArray(),
                        outputWidth,
                        outputHeight,
                        yoloConfidenceThreshold,
                        0.45f,
                        TrialObjectCatalog.GetForcedClassId(selectedTrialObject),
                        out detection);

                    if (success)
                    {
                        if (updateOverlay)
                        {
                            ShowDetectionBox(detection);
                        }
                        return true;
                    }
                }
                finally
                {
                    rgbaBytes.Dispose();
                }
            }

            if (updateOverlay)
            {
                HideDetectionBox();
                SetDetectionLabel("YOLO scanning...");
            }
            return false;
        }

        private bool DisplayPointCloud(PointCloudData pointCloud, Transform cameraTransform)
        {
            if (pointCloud == null || pointCloud.Points == null || pointCloud.Points.Count == 0 || cameraTransform == null)
            {
                return false;
            }

            EnsurePointCloudVisual();

            var pointCount = Mathf.Min(pointCloud.Points.Count, Mathf.Max(1, maxVisualPoints));
            var step = Mathf.Max(1, pointCloud.Points.Count / pointCount);
            var visualCount = Mathf.CeilToInt(pointCloud.Points.Count / (float)step);
            visualCount = Mathf.Min(visualCount, pointCount);

            var vertices = new Vector3[visualCount * 4];
            var triangles = new int[visualCount * 12];
            var colors = new Color[vertices.Length];
            var halfSize = Mathf.Max(0.001f, pointVisualSizeMeters) * 0.5f;
            var right = cameraTransform.right * halfSize;
            var up = cameraTransform.up * halfSize;

            var visualIndex = 0;
            for (var sourceIndex = 0; sourceIndex < pointCloud.Points.Count && visualIndex < visualCount; sourceIndex += step)
            {
                var worldPosition = cameraTransform.TransformPoint(pointCloud.Points[sourceIndex].Position);
                var vertexIndex = visualIndex * 4;
                vertices[vertexIndex] = worldPosition - right - up;
                vertices[vertexIndex + 1] = worldPosition - right + up;
                vertices[vertexIndex + 2] = worldPosition + right + up;
                vertices[vertexIndex + 3] = worldPosition + right - up;

                var color = Color.Lerp(new Color(0.1f, 0.45f, 1f, 0.95f), new Color(0f, 1f, 0.75f, 0.95f), pointCloud.Points[sourceIndex].Confidence);
                colors[vertexIndex] = color;
                colors[vertexIndex + 1] = color;
                colors[vertexIndex + 2] = color;
                colors[vertexIndex + 3] = color;

                var triangleIndex = visualIndex * 12;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex + 2;
                triangles[triangleIndex + 3] = vertexIndex;
                triangles[triangleIndex + 4] = vertexIndex + 2;
                triangles[triangleIndex + 5] = vertexIndex + 3;
                triangles[triangleIndex + 6] = vertexIndex + 2;
                triangles[triangleIndex + 7] = vertexIndex + 1;
                triangles[triangleIndex + 8] = vertexIndex;
                triangles[triangleIndex + 9] = vertexIndex + 3;
                triangles[triangleIndex + 10] = vertexIndex + 2;
                triangles[triangleIndex + 11] = vertexIndex;

                visualIndex++;
            }

            var mesh = new Mesh { name = "M4 Captured PointCloud Mesh" };
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.RecalculateBounds();

            if (pointCloudMeshFilter.sharedMesh != null)
            {
                Destroy(pointCloudMeshFilter.sharedMesh);
            }

            pointCloudMeshFilter.sharedMesh = mesh;
            pointCloudVisual.SetActive(true);
            return true;
        }

        private void EnsurePointCloudVisual()
        {
            if (pointCloudVisual != null)
            {
                return;
            }

            pointCloudVisual = new GameObject("M4 Visible PointCloud");
            pointCloudMeshFilter = pointCloudVisual.AddComponent<MeshFilter>();
            pointCloudRenderer = pointCloudVisual.AddComponent<MeshRenderer>();
            pointCloudRenderer.sharedMaterial = CreatePointCloudMaterial();
            pointCloudVisual.SetActive(false);
            DontDestroyOnLoad(pointCloudVisual);
        }

        private static Material CreatePointCloudMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogWarning("[M4 PointCloud] No point cloud shader was found. Mesh was created without a material.");
                return null;
            }

            var material = new Material(shader);
            material.color = new Color(0f, 0.9f, 1f, 0.95f);
            material.SetInt("_Cull", (int)CullMode.Off);
            return material;
        }

        private void DisplayGenericPoseAxis(GenericPoseFrame frame, Transform cameraTransform)
        {
            EnsureDirectionAxisVisual();

            var center = cameraTransform.TransformPoint(frame.CenterCamera);
            var length = ComputeAxisLength(frame);
            var rightEnd = center + cameraTransform.TransformDirection(frame.RightCamera).normalized * length;
            var upEnd = center + cameraTransform.TransformDirection(frame.UpCamera).normalized * length;
            var forwardEnd = center + cameraTransform.TransformDirection(frame.ForwardCamera).normalized * length;

            SetAxisLine(directionAxisMajor, center, rightEnd);
            SetAxisLine(directionAxisMiddle, center, upEnd);
            SetAxisLine(directionAxisNormal, center, forwardEnd);
            directionAxisRoot.SetActive(true);
        }

        private void EnsureDirectionAxisVisual()
        {
            if (directionAxisRoot != null)
            {
                return;
            }

            directionAxisRoot = new GameObject("M5 Runtime Direction Axis");
            directionAxisMajor = CreateAxisLine(directionAxisRoot.transform, "Generic Right", Color.red);
            directionAxisMiddle = CreateAxisLine(directionAxisRoot.transform, "Generic Up", Color.green);
            directionAxisNormal = CreateAxisLine(directionAxisRoot.transform, "Generic Forward", Color.blue);
            directionAxisRoot.SetActive(false);
            DontDestroyOnLoad(directionAxisRoot);
        }

        private LineRenderer CreateAxisLine(Transform parent, string name, Color color)
        {
            var axisObject = new GameObject(name);
            axisObject.transform.SetParent(parent, false);
            var line = axisObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = directionAxisThicknessMeters;
            line.endWidth = directionAxisThicknessMeters;
            line.numCapVertices = 6;
            line.material = CreateAxisMaterial(color);
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private static Material CreateAxisMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = shader != null ? new Material(shader) : null;
            if (material != null)
            {
                material.color = color;
            }
            return material;
        }

        private static void SetAxisLine(LineRenderer line, Vector3 start, Vector3 end)
        {
            if (line == null)
            {
                return;
            }

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void HideDirectionAxis()
        {
            if (directionAxisRoot != null)
            {
                directionAxisRoot.SetActive(false);
            }
        }

        private void DisplayTargetCenterSphere(GenericPoseFrame frame, Transform cameraTransform)
        {
            EnsureTargetCenterSphere();
            targetCenterSphere.transform.position = cameraTransform.TransformPoint(frame.CenterCamera);
            targetCenterSphere.transform.localScale = Vector3.one * Mathf.Max(0.005f, targetCenterSphereDiameterMeters);
            if (!targetCenterSphere.activeSelf)
            {
                targetCenterSphere.SetActive(true);
            }
        }

        private void EnsureTargetCenterSphere()
        {
            if (targetCenterSphere != null)
            {
                return;
            }

            targetCenterSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetCenterSphere.name = "M5 Target Center Sphere";
            var sphereCollider = targetCenterSphere.GetComponent<Collider>();
            if (sphereCollider != null)
            {
                Destroy(sphereCollider);
            }

            targetCenterSphereRenderer = targetCenterSphere.GetComponent<MeshRenderer>();
            if (targetCenterSphereRenderer != null)
            {
                targetCenterSphereRenderer.sharedMaterial = CreateTargetCenterSphereMaterial();
            }

            targetCenterSphere.SetActive(false);
            DontDestroyOnLoad(targetCenterSphere);
        }

        private static Material CreateTargetCenterSphereMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            material.color = new Color(1f, 0.88f, 0.05f, 1f);
            return material;
        }

        private void HideTargetCenterSphere()
        {
            if (targetCenterSphere != null && targetCenterSphere.activeSelf)
            {
                targetCenterSphere.SetActive(false);
            }
        }

        private float ComputeAxisLength(GenericPoseFrame frame)
        {
            var extentMax = Mathf.Max(frame.ExtentMeters.x, Mathf.Max(frame.ExtentMeters.y, frame.ExtentMeters.z));
            var adaptive = extentMax > 0f ? extentMax * 0.65f : directionAxisLengthMeters;
            return Mathf.Clamp(
                adaptive,
                genericPoseConfig != null ? genericPoseConfig.MinimumAxisLengthMeters : 0.03f,
                genericPoseConfig != null ? genericPoseConfig.MaximumAxisLengthMeters : 0.20f);
        }

        private XRCpuImage TryAcquireConfidenceImage()
        {
            if (occlusionManager != null &&
                occlusionManager.TryAcquireEnvironmentDepthConfidenceCpuImage(out var confidenceImage))
            {
                return confidenceImage;
            }

            return default;
        }

        private DepthFrame CreateDepthFrame(XRCpuImage depthImage, XRCpuImage confidenceImage)
        {
            var width = depthImage.width;
            var height = depthImage.height;
            var depth = new float[width * height];
            var depthPlane = depthImage.GetPlane(0);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    depth[y * width + x] = ReadDepthMeters(depthPlane.data, depthPlane.rowStride, depthPlane.pixelStride, x, y);
                }
            }

            var confidence = confidenceImage.valid ? CreateConfidenceMap(confidenceImage, width, height) : null;
            return new DepthFrame
            {
                DepthMeters = depth,
                Confidence = confidence,
                Width = width,
                Height = height,
                Timestamp = Time.timeAsDouble
            };
        }

        private static byte[] CreateConfidenceMap(XRCpuImage confidenceImage, int targetWidth, int targetHeight)
        {
            var confidence = new byte[targetWidth * targetHeight];
            var plane = confidenceImage.GetPlane(0);
            for (var y = 0; y < targetHeight; y++)
            {
                for (var x = 0; x < targetWidth; x++)
                {
                    var sourceX = Mathf.Clamp(x * confidenceImage.width / Mathf.Max(1, targetWidth), 0, confidenceImage.width - 1);
                    var sourceY = Mathf.Clamp(y * confidenceImage.height / Mathf.Max(1, targetHeight), 0, confidenceImage.height - 1);
                    var offset = sourceY * plane.rowStride + sourceX * plane.pixelStride;
                    confidence[y * targetWidth + x] = offset >= 0 && offset < plane.data.Length ? plane.data[offset] : (byte)0;
                }
            }

            return confidence;
        }

        private static float ReadDepthMeters(NativeArray<byte> data, int rowStride, int pixelStride, int x, int y)
        {
            var offset = y * rowStride + x * pixelStride;
            if (pixelStride == 4 && offset + 3 < data.Length)
            {
                var bytes = new byte[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
                return BitConverter.ToSingle(bytes, 0);
            }

            if (pixelStride == 2 && offset + 1 < data.Length)
            {
                var millimeters = (ushort)(data[offset] | (data[offset + 1] << 8));
                return millimeters * 0.001f;
            }

            return offset >= 0 && offset < data.Length ? data[offset] * 0.001f : -1f;
        }

        private bool HasFreshYoloDetection()
        {
            return hasLatestDetection &&
                   Time.timeAsDouble - latestDetection.Timestamp < yoloDetectionHoldSeconds;
        }

        private RectInt CreateYoloRoi(Rect screenRect, int depthWidth, int depthHeight)
        {
            return BoundingBoxMapper.ExpandAndClip(
                BoundingBoxMapper.ScreenRectToImageRoi(
                    screenRect,
                    new Vector2Int(Screen.width, Screen.height),
                    new Vector2Int(depthWidth, depthHeight)),
                depthWidth,
                depthHeight,
                yoloRoiExpandRatio,
                yoloRoiMinExpandPixels);
        }

        private RectInt CreateCenterRoi(int width, int height)
        {
            var roiW = Mathf.Clamp(roiWidth, 1, width);
            var roiH = Mathf.Clamp(roiHeight, 1, height);
            return new RectInt((width - roiW) / 2, (height - roiH) / 2, roiW, roiH);
        }

        private static string BuildOutputPath()
        {
            var fileName = $"pointcloud_{DateTime.Now:yyyyMMdd_HHmmss}.ply";
            return Path.Combine(Application.persistentDataPath, "PointCloud", fileName);
        }

        private static string BuildLatestOutputPath()
        {
            return Path.Combine(Application.persistentDataPath, "latest_pointcloud.ply");
        }

        private static void WriteLatestManifest(string timestampedPath, string latestPath, PointCloudData pointCloud)
        {
            var manifestPath = Path.Combine(Application.persistentDataPath, "latest_pointcloud_path.txt");
            var content =
                $"timestamped_path={timestampedPath}\n" +
                $"latest_path={latestPath}\n" +
                $"raw_points={pointCloud.RawPointCount}\n" +
                $"filtered_points={pointCloud.FilteredPointCount}\n" +
                $"voxel_size_m={pointCloud.VoxelSizeMeters:F4}\n" +
                $"timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            File.WriteAllText(manifestPath, content);
        }

        private void CreateControls()
        {
            EnsureEventSystem();

            var canvas = new GameObject("M4 PointCloud Controls").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170f, 2532f);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvas.gameObject);

            var button = CreateButton(canvas.transform, "Capture PointCloud Button", "捕获并显示点云", new Vector2(32f, 420f));
            button.onClick.AddListener(CapturePointCloud);

            cupButton = CreateButton(canvas.transform, "Trial Cup Button", "杯子", new Vector2(32f, 268f), new Vector2(136f, 88f));
            cupButton.onClick.AddListener(() => SelectTrialObject(TrialObjectKind.Cup));
            phoneButton = CreateButton(canvas.transform, "Trial Phone Button", "手机", new Vector2(176f, 268f), new Vector2(136f, 88f));
            phoneButton.onClick.AddListener(() => SelectTrialObject(TrialObjectKind.Phone));
            laptopButton = CreateButton(canvas.transform, "Trial Laptop Button", "电脑", new Vector2(320f, 268f), new Vector2(136f, 88f));
            laptopButton.onClick.AddListener(() => SelectTrialObject(TrialObjectKind.Laptop));

            recordButton = CreateButton(canvas.transform, "Trial Record Button", "开始记录", new Vector2(32f, 140f), new Vector2(420f, 100f));
            recordButton.onClick.AddListener(ToggleTrialRecording);
            recordButtonLabel = recordButton.GetComponentInChildren<Text>();

            statusText = CreateStatusText(canvas.transform);
            directionText = CreateDirectionText(canvas.transform);
            trialStatusText = CreateTrialStatusText(canvas.transform);
            CreateDetectionBox(canvas.transform);
            SetDetectionLabel("YOLO starting...");
            SetDirectionText("Direction: waiting");
            RefreshTrialUi();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                {
                    EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                return;
            }

            var eventSystemObject = new GameObject("M4 EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            return CreateButton(parent, name, label, anchoredPosition, new Vector2(420f, 128f));
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.08f, 0.1f, 0.86f);

            var button = buttonObject.AddComponent<Button>();
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size.y < 110f ? 32 : 38;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static Text CreateStatusText(Transform parent)
        {
            var textObject = new GameObject("M4 PointCloud Status");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.text = "PointCloud: ready";

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(480f, 410f);
            rect.sizeDelta = new Vector2(650f, 180f);
            return text;
        }

        private static Text CreateDirectionText(Transform parent)
        {
            var textObject = new GameObject("M5 Runtime Direction Status");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(1f, 1f, 1f, 0.96f);
            text.text = "Direction: waiting";

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(32f, 600f);
            rect.sizeDelta = new Vector2(760f, 220f);
            return text;
        }

        private void CreateDetectionBox(Transform parent)
        {
            var boxObject = new GameObject("YOLO Detection Box");
            boxObject.transform.SetParent(parent, false);
            detectionBox = boxObject.AddComponent<RectTransform>();
            detectionBox.anchorMin = new Vector2(0f, 0f);
            detectionBox.anchorMax = new Vector2(0f, 0f);
            detectionBox.pivot = new Vector2(0f, 0f);

            CreateBorder(boxObject.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(0f, 0f));
            CreateBorder(boxObject.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 6f));
            CreateBorder(boxObject.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(6f, 0f));
            CreateBorder(boxObject.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-6f, 0f), new Vector2(0f, 0f));

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(boxObject.transform, false);
            detectionText = labelObject.AddComponent<Text>();
            detectionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            detectionText.fontSize = 30;
            detectionText.alignment = TextAnchor.LowerLeft;
            detectionText.color = new Color(0f, 1f, 0.75f, 1f);
            var labelRect = detectionText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 8f);
            labelRect.sizeDelta = new Vector2(0f, 42f);

            var anchorObject = new GameObject("Anchor Marker");
            anchorObject.transform.SetParent(boxObject.transform, false);
            var anchorImage = anchorObject.AddComponent<Image>();
            anchorImage.color = new Color(1f, 0.05f, 0.95f, 0.95f);
            detectionAnchorMarker = anchorImage.rectTransform;
            detectionAnchorMarker.anchorMin = new Vector2(0f, 0f);
            detectionAnchorMarker.anchorMax = new Vector2(0f, 0f);
            detectionAnchorMarker.pivot = new Vector2(0.5f, 0.5f);
            detectionAnchorMarker.sizeDelta = new Vector2(26f, 26f);

            boxObject.SetActive(false);
        }

        private static void CreateBorder(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var borderObject = new GameObject(name);
            borderObject.transform.SetParent(parent, false);
            var image = borderObject.AddComponent<Image>();
            image.color = new Color(0f, 1f, 0.75f, 0.95f);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void ShowDetectionBox(DetectionResult detection)
        {
            if (detectionBox == null)
            {
                return;
            }

            var rect = detection.PixelRect;
            detectionBox.anchoredPosition = new Vector2(rect.xMin, Screen.height - rect.yMax);
            detectionBox.sizeDelta = new Vector2(rect.width, rect.height);
            if (detectionAnchorMarker != null)
            {
                var anchorPixel = latestAnchorSelection.IsValid
                    ? latestAnchorSelection.Pixel
                    : detection.HasMaskCenter ? detection.MaskCenter : detection.Center;
                var anchorScreenPoint = DetectionPixelToUnityScreenPoint(anchorPixel);
                detectionAnchorMarker.anchoredPosition = anchorScreenPoint - detectionBox.anchoredPosition;
            }

            if (detectionText != null)
            {
                var anchorLabel = latestAnchorSelection.IsValid ? latestAnchorSelection.Label : "anchor pending";
                detectionText.text = $"YOLO {CocoClassNames.GetName(detection.ClassId)} {detection.Confidence:F2} {anchorLabel}";
            }

            detectionBox.gameObject.SetActive(true);
        }

        private void HideDetectionBox()
        {
            if (detectionBox != null)
            {
                detectionBox.gameObject.SetActive(false);
            }
        }

        private void SetDetectionLabel(string text)
        {
            if (detectionText == null)
            {
                return;
            }

            detectionText.text = text;
            detectionBox.gameObject.SetActive(true);
            detectionBox.anchoredPosition = new Vector2(480f, 608f);
            detectionBox.sizeDelta = new Vector2(560f, 70f);
        }

        private void SetStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }

            Debug.Log($"[M4 PointCloud] {status}");
        }

        private void SetDirectionText(string text)
        {
            if (directionText != null)
            {
                directionText.text = text;
            }
        }

        private string GetReplacementModelLabel()
        {
            if (replacementModelController == null)
            {
                return "none";
            }

            var state = replacementModelController.IsLoading
                ? "loading"
                : replacementModelController.IsUsingSceneRegistry ? "scene"
                : replacementModelController.HasRealModel ? "real" : "fallback";
            return $"{replacementModelController.ActiveResourceName ?? "none"} ({state})";
        }

        private static Text CreateTrialStatusText(Transform parent)
        {
            var textObject = new GameObject("Trial Status");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.UpperLeft;
            text.color = new Color(1f, 0.92f, 0.45f, 1f);
            text.text = "试验: 先选杯子/手机/电脑";

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(900f, 140f);
            return text;
        }

        private void SelectTrialObject(TrialObjectKind kind)
        {
            if (kind == selectedTrialObject)
            {
                SetStatus(trialLogger.IsRecording
                    ? $"正在记录 {TrialObjectCatalog.GetChineseLabel(kind)}"
                    : $"已选择 {TrialObjectCatalog.GetChineseLabel(kind)}，对准物体后点开始记录");
                RefreshTrialUi();
                return;
            }

            if (trialLogger.IsRecording)
            {
                trialLogger.Stop();
            }

            selectedTrialObject = kind;
            RefreshTrialUi();
            SetStatus($"已选择 {TrialObjectCatalog.GetChineseLabel(kind)}，对准物体后点开始记录");
        }

        private void ToggleTrialRecording()
        {
            if (trialLogger.IsRecording)
            {
                trialLogger.Stop();
                SetStatus($"记录已停止\ncsv={trialLogger.CsvPath}\n用文件App从App的Documents/Trials取出");
                RefreshTrialUi();
                return;
            }

            if (selectedTrialObject == TrialObjectKind.None)
            {
                SetStatus("请先选择 杯子 / 手机 / 电脑");
                return;
            }

            var trialId = $"{TrialObjectCatalog.GetEnglishName(selectedTrialObject)}_{DateTime.Now:yyyyMMdd_HHmmss}";
            if (!trialLogger.Start(selectedTrialObject, trialId))
            {
                SetStatus("无法开始记录");
                return;
            }

            SetStatus($"正在记录 {TrialObjectCatalog.GetChineseLabel(selectedTrialObject)}\n{trialId}");
            RefreshTrialUi();
        }

        private void RefreshTrialUi()
        {
            HighlightButton(cupButton, selectedTrialObject == TrialObjectKind.Cup);
            HighlightButton(phoneButton, selectedTrialObject == TrialObjectKind.Phone);
            HighlightButton(laptopButton, selectedTrialObject == TrialObjectKind.Laptop);
            if (recordButton != null)
            {
                var image = recordButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = trialLogger.IsRecording
                        ? new Color(0.72f, 0.12f, 0.12f, 0.92f)
                        : new Color(0.05f, 0.08f, 0.1f, 0.86f);
                }
            }

            if (recordButtonLabel != null)
            {
                recordButtonLabel.text = trialLogger.IsRecording ? "停止记录" : "开始记录";
            }

            if (trialStatusText != null)
            {
                var objectLabel = TrialObjectCatalog.GetChineseLabel(selectedTrialObject);
                if (trialLogger.IsRecording)
                {
                    trialStatusText.text =
                        $"试验: {objectLabel}  记录中  frames={trialLogger.FrameCount}\n" +
                        $"{trialLogger.TrialId}\n" +
                        "对准物体，必要时再点“捕获并显示点云”";
                }
                else
                {
                    trialStatusText.text =
                        $"试验: {objectLabel}  未记录\n" +
                        "选择物体 → 开始记录 → 保存CSV到Documents/Trials";
                }
            }
        }

        private static void HighlightButton(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.color = selected
                ? new Color(0.08f, 0.42f, 0.78f, 0.94f)
                : new Color(0.05f, 0.08f, 0.1f, 0.86f);
        }

        private int ResolveReplacementClassId(int detectedClassId)
        {
            return selectedTrialObject == TrialObjectKind.None
                ? detectedClassId
                : TrialObjectCatalog.GetForcedClassId(selectedTrialObject);
        }

        private void LogTrialFrame(
            string eventType,
            GenericPoseFrame frame,
            PointCloudData pointCloud,
            DetectionResult detection,
            bool hasDetection,
            string roiSource,
            RuntimePoseMode activeMode,
            float detectMs,
            float cloudMs,
            float poseMs,
            float exportMs,
            float e2eMs,
            string plyPath)
        {
            if (!trialLogger.IsRecording)
            {
                RefreshTrialUi();
                return;
            }

            var detectedClassId = hasDetection && detection.IsValid ? detection.ClassId : -1;
            var classMatch = TrialObjectCatalog.MatchesDetectedClass(selectedTrialObject, detectedClassId);
            var failReason = BuildFailReason(hasDetection, detection, frame, classMatch);
            var cameraTransform = cameraManager != null ? cameraManager.transform : null;
            var centerWorld = cameraTransform != null && frame.IsValid
                ? cameraTransform.TransformPoint(frame.CenterCamera)
                : Vector3.zero;
            trialLogger.Append(new TrialFrameRecord
            {
                EventType = eventType,
                FrameId = trialLogger.FrameCount + 1,
                UnixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                UnityTime = Time.timeAsDouble,
                ObjectLabel = TrialObjectCatalog.GetEnglishName(selectedTrialObject),
                ExpectedClassId = TrialObjectCatalog.GetForcedClassId(selectedTrialObject),
                ExpectedClassName = TrialObjectCatalog.GetExpectedClassName(selectedTrialObject),
                YoloAvailable = detector.IsAvailable,
                DetectedClassId = detectedClassId,
                DetectedClassName = detectedClassId >= 0 ? CocoClassNames.GetName(detectedClassId) : "none",
                Confidence = hasDetection && detection.IsValid ? detection.Confidence : 0f,
                ClassMatch = classMatch,
                RoiSource = roiSource,
                AnchorLabel = latestAnchorSelection.IsValid ? latestAnchorSelection.Label : "none",
                RawPoints = pointCloud != null ? pointCloud.RawPointCount : 0,
                FilteredPoints = pointCloud != null ? pointCloud.FilteredPointCount : 0,
                VoxelSizeMeters = pointCloud != null ? pointCloud.VoxelSizeMeters : voxelSizeMeters,
                PoseMode = activeMode.ToString(),
                Shape = frame.ShapeType.ToString(),
                Stability = frame.Stability.ToString(),
                CenterCamera = frame.CenterCamera,
                CenterWorld = centerWorld,
                RightCamera = frame.RightCamera,
                UpCamera = frame.UpCamera,
                ForwardCamera = frame.ForwardCamera,
                ExtentMeters = frame.ExtentMeters,
                GeometryConfidence = frame.GeometryConfidence,
                OrientationConfidence = frame.OrientationConfidence,
                TrackingConfidence = frame.TrackingConfidence,
                OverallConfidence = frame.OverallConfidence,
                DetectMs = detectMs,
                CloudMs = cloudMs,
                PoseMs = poseMs,
                ExportMs = exportMs,
                E2eMs = e2eMs,
                ReplacementEnabled = replacementModelEnabled,
                ModelName = GetReplacementModelLabel(),
                PlyPath = plyPath ?? string.Empty,
                Success = string.IsNullOrEmpty(failReason),
                FailReason = failReason
            });
            RefreshTrialUi();
        }

        private static string BuildFailReason(
            bool hasDetection,
            DetectionResult detection,
            GenericPoseFrame frame,
            bool classMatch)
        {
            if (!hasDetection || !detection.IsValid)
            {
                return "no_detection";
            }

            if (!classMatch)
            {
                return "class_mismatch";
            }

            if (!frame.IsValid && frame.Stability != GenericPoseStability.TrackingLost)
            {
                return string.IsNullOrEmpty(frame.Message) ? "invalid_pose" : frame.Message;
            }

            if (frame.PointCount < 30)
            {
                return "too_few_points";
            }

            return string.Empty;
        }

        private void LogGenericPoseFrame(GenericPoseFrame frame, string roiSource, RuntimePoseMode activeMode)
        {
            if (genericPoseConfig != null &&
                Time.time - lastGenericPoseLogTime < genericPoseConfig.LogIntervalSeconds)
            {
                return;
            }

            lastGenericPoseLogTime = Time.time;
            Debug.Log(
                $"[M5 RuntimePose] mode={activeMode} source={roiSource.Replace(' ', '_')} " +
                $"shape={frame.ShapeType} status={frame.Stability} points={frame.PointCount} " +
                $"confidence={frame.OverallConfidence:F2} geometry={frame.GeometryConfidence:F2} " +
                $"orientation={frame.OrientationConfidence:F2} tracking={frame.TrackingConfidence:F2} " +
                $"linearity={frame.Linearity:F2} planarity={frame.Planarity:F2} scattering={frame.Scattering:F2} " +
                $"center={FormatVector(frame.CenterCamera)} right={FormatVector(frame.RightCamera)} " +
                $"up={FormatVector(frame.UpCamera)} forward={FormatVector(frame.ForwardCamera)} " +
                $"extent=({frame.ExtentMeters.x:F3}, {frame.ExtentMeters.y:F3}, {frame.ExtentMeters.z:F3}) " +
                $"message=\"{frame.Message}\"");
        }

        private static string FormatVector(Vector3 vector)
        {
            return $"({vector.x:+0.00;-0.00;0.00}, {vector.y:+0.00;-0.00;0.00}, {vector.z:+0.00;-0.00;0.00})";
        }
    }
}
