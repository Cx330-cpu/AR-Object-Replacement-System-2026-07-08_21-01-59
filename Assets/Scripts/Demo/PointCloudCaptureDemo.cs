using System;
using System.Diagnostics;
using System.IO;
using ARObjectReplacement.Detection;
using ARObjectReplacement.PointCloud;
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
        [SerializeField] private int roiWidth = 120;
        [SerializeField] private int roiHeight = 120;
        [SerializeField] private float voxelSizeMeters = 0.01f;
        [SerializeField] private float minDepthMeters = 0.05f;
        [SerializeField] private float maxDepthMeters = 8f;
        [SerializeField] private float minimumConfidence = 0.5f;
        [SerializeField] private float outlierRadiusMeters = 0.035f;
        [SerializeField] private int outlierMinimumNeighbors = 2;
        [SerializeField] private int maxVisualPoints = 2500;
        [SerializeField] private float pointVisualSizeMeters = 0.006f;
        [SerializeField] private bool realtimeYoloEnabled = true;
        [SerializeField] private float realtimeYoloIntervalSeconds = 0.5f;
        [SerializeField] private int yoloInputWidth = 640;
        [SerializeField] private int yoloInputHeight = 480;

        private readonly PointCloudBuilder builder = new PointCloudBuilder();
        private readonly PointCloudDownSampler downSampler = new PointCloudDownSampler();
        private readonly PointCloudCleaner cleaner = new PointCloudCleaner();
        private readonly PointCloudExporter exporter = new PointCloudExporter();
        private readonly YoloCoreMLDetector detector = new YoloCoreMLDetector();
        private Text statusText;
        private GameObject pointCloudVisual;
        private MeshFilter pointCloudMeshFilter;
        private MeshRenderer pointCloudRenderer;
        private RectTransform detectionBox;
        private Text detectionText;
        private bool hasLatestDetection;
        private DetectionResult latestDetection;
        private float lastRealtimeYoloTime;

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

            CreateControls();
        }

        private void Update()
        {
            if (!realtimeYoloEnabled || Time.time - lastRealtimeYoloTime < realtimeYoloIntervalSeconds)
            {
                return;
            }

            lastRealtimeYoloTime = Time.time;
            hasLatestDetection = TryDetectCenterObject(out latestDetection, true);
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

            var hasDetection = hasLatestDetection && Time.timeAsDouble - latestDetection.Timestamp < 2.0;
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
                    ? BoundingBoxMapper.ExpandAndClip(
                        BoundingBoxMapper.ScreenRectToImageRoi(
                            detection.PixelRect,
                            new Vector2Int(Screen.width, Screen.height),
                            new Vector2Int(depthFrame.Width, depthFrame.Height)),
                        depthFrame.Width,
                        depthFrame.Height,
                        0.12f)
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

            var fps = stopwatch.Elapsed.TotalSeconds > 0 ? 1.0 / stopwatch.Elapsed.TotalSeconds : 0.0;
            var message =
                $"{(displayed ? "PointCloud displayed" : "PointCloud saved, no visible points")}\n" +
                $"{(hasDetection ? $"YOLO {CocoClassNames.GetName(detection.ClassId)}, conf={detection.Confidence:F2}" : "YOLO unavailable: center ROI fallback")}\n" +
                $"raw={filteredCloud.RawPointCount}, filtered={filteredCloud.FilteredPointCount}\n" +
                $"voxel={voxelSizeMeters:F3}m, export={filteredCloud.ExportTimeMs:F1}ms, fps={fps:F1}\n" +
                $"latest={latestPath}";

            SetStatus(message);
            Debug.Log($"[M4 PointCloud] ROI={roi} raw_points={filteredCloud.RawPointCount} " +
                      $"filtered_points={filteredCloud.FilteredPointCount} voxel_size={voxelSizeMeters:F3}m " +
                      $"yolo={(hasDetection ? $"class={detection.ClassId}, confidence={detection.Confidence:F3}, bbox={detection.PixelRect}" : "fallback_center_roi")} " +
                      $"export_time_ms={filteredCloud.ExportTimeMs:F1} capture_time_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
                      $"fps={fps:F1} path={path} latest_path={latestPath}");
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
                        0.25f,
                        0.45f,
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

            statusText = CreateStatusText(canvas.transform);
            CreateDetectionBox(canvas.transform);
            SetDetectionLabel("YOLO starting...");
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
            rect.sizeDelta = new Vector2(420f, 128f);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 38;
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
            if (detectionText != null)
            {
                detectionText.text = $"YOLO {CocoClassNames.GetName(detection.ClassId)} {detection.Confidence:F2}";
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
    }
}
