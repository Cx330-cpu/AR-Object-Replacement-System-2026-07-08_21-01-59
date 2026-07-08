using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARObjectReplacement.Depth
{
    public sealed class ARDepthCrosshairMeasure : MonoBehaviour
    {
        [Header("AR Foundation")]
        public ARCameraManager CameraManager;
        public AROcclusionManager OcclusionManager;

        [Header("Sampling")]
        [SerializeField] private int sampleRadiusPixels = 3;
        [SerializeField] private int minimumConfidence = 1;
        [SerializeField] private float maxValidDistanceMeters = 8f;
        [SerializeField] private float smoothing = 0.25f;

        [Header("UI")]
        [SerializeField] private bool showHeatMap = false;
        [SerializeField] private int heatMapWidth = 160;
        [SerializeField] private int heatMapHeight = 120;

        private Canvas canvas;
        private Text distanceText;
        private RawImage heatMapImage;
        private Texture2D heatMapTexture;
        private readonly List<float> samples = new List<float>();
        private float smoothedDistance = -1f;
        private DepthResult latestResult;

        public DepthResult LatestResult => latestResult;

        private void Awake()
        {
            if (CameraManager == null)
            {
                CameraManager = GetComponent<ARCameraManager>();
            }

            if (OcclusionManager == null)
            {
                OcclusionManager = GetComponent<AROcclusionManager>();
            }

            CreateUi();
        }

        private void OnEnable()
        {
            if (OcclusionManager != null)
            {
                OcclusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
                OcclusionManager.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
            }
        }

        private void Update()
        {
            if (OcclusionManager == null)
            {
                SetStatus("LiDAR depth unavailable: missing AROcclusionManager");
                return;
            }

            if (!OcclusionManager.TryAcquireEnvironmentDepthCpuImage(out var depthImage))
            {
                SetStatus("Waiting for LiDAR depth...");
                return;
            }

            using (depthImage)
            {
                var confidenceImage = TryAcquireConfidenceImage();
                try
                {
                    var hasConfidence = confidenceImage.valid;
                    var depthPixel = new Vector2Int(depthImage.width / 2, depthImage.height / 2);
                    var distance = SampleMedianDepth(depthImage, confidenceImage, hasConfidence, depthPixel);
                    XRCameraIntrinsics intrinsics = default;
                    var hasIntrinsics = CameraManager != null && CameraManager.TryGetIntrinsics(out intrinsics);

                    latestResult = new DepthResult
                    {
                        IsValid = distance > 0f,
                        DistanceMeters = distance,
                        Confidence = hasConfidence ? SampleConfidence(confidenceImage, depthPixel, depthImage.width, depthImage.height) : -1,
                        DepthPixel = depthPixel,
                        DepthResolution = new Vector2Int(depthImage.width, depthImage.height),
                        Intrinsics = hasIntrinsics ? intrinsics : default,
                        HasIntrinsics = hasIntrinsics,
                        Timestamp = Time.timeAsDouble
                    };

                    UpdateDistanceText(latestResult);

                    if (showHeatMap)
                    {
                        UpdateHeatMap(depthImage);
                    }
                }
                finally
                {
                    if (confidenceImage.valid)
                    {
                        confidenceImage.Dispose();
                    }
                }
            }
        }

        private XRCpuImage TryAcquireConfidenceImage()
        {
            if (OcclusionManager != null &&
                OcclusionManager.TryAcquireEnvironmentDepthConfidenceCpuImage(out var confidenceImage))
            {
                return confidenceImage;
            }

            return default;
        }

        private float SampleMedianDepth(
            XRCpuImage depthImage,
            XRCpuImage confidenceImage,
            bool hasConfidence,
            Vector2Int center)
        {
            samples.Clear();

            var plane = depthImage.GetPlane(0);
            for (var y = center.y - sampleRadiusPixels; y <= center.y + sampleRadiusPixels; y++)
            {
                for (var x = center.x - sampleRadiusPixels; x <= center.x + sampleRadiusPixels; x++)
                {
                    if (x < 0 || y < 0 || x >= depthImage.width || y >= depthImage.height)
                    {
                        continue;
                    }

                    if (hasConfidence && SampleConfidence(confidenceImage, new Vector2Int(x, y), depthImage.width, depthImage.height) < minimumConfidence)
                    {
                        continue;
                    }

                    var value = ReadDepthMeters(plane.data, plane.rowStride, plane.pixelStride, x, y);
                    if (value > 0.05f && value <= maxValidDistanceMeters && !float.IsNaN(value) && !float.IsInfinity(value))
                    {
                        samples.Add(value);
                    }
                }
            }

            if (samples.Count == 0)
            {
                return -1f;
            }

            samples.Sort();
            var median = samples[samples.Count / 2];
            smoothedDistance = smoothedDistance < 0f
                ? median
                : Mathf.Lerp(smoothedDistance, median, Mathf.Clamp01(smoothing));
            return smoothedDistance;
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

            if (offset < data.Length)
            {
                return data[offset] * 0.001f;
            }

            return -1f;
        }

        private static int SampleConfidence(XRCpuImage confidenceImage, Vector2Int depthPixel, int depthWidth, int depthHeight)
        {
            if (!confidenceImage.valid)
            {
                return -1;
            }

            var x = Mathf.Clamp(depthPixel.x * confidenceImage.width / Mathf.Max(1, depthWidth), 0, confidenceImage.width - 1);
            var y = Mathf.Clamp(depthPixel.y * confidenceImage.height / Mathf.Max(1, depthHeight), 0, confidenceImage.height - 1);
            var plane = confidenceImage.GetPlane(0);
            var offset = y * plane.rowStride + x * plane.pixelStride;
            return offset >= 0 && offset < plane.data.Length ? plane.data[offset] : -1;
        }

        private void UpdateDistanceText(DepthResult result)
        {
            if (!result.IsValid)
            {
                SetStatus("Center distance: --");
                return;
            }

            var confidenceLabel = result.Confidence < 0 ? "n/a" : result.Confidence.ToString();
            var intrinsicsLabel = result.HasIntrinsics ? "intrinsics ok" : "intrinsics missing";
            SetStatus(
                $"Center distance: {result.DistanceMeters:F2} m\n" +
                $"Confidence: {confidenceLabel}\n" +
                $"Depth: {result.DepthResolution.x}x{result.DepthResolution.y} | {intrinsicsLabel}");
        }

        private void UpdateHeatMap(XRCpuImage depthImage)
        {
            if (heatMapTexture == null)
            {
                heatMapTexture = new Texture2D(heatMapWidth, heatMapHeight, TextureFormat.RGBA32, false);
                heatMapImage.texture = heatMapTexture;
            }

            var plane = depthImage.GetPlane(0);
            for (var y = 0; y < heatMapHeight; y++)
            {
                for (var x = 0; x < heatMapWidth; x++)
                {
                    var sourceX = x * depthImage.width / heatMapWidth;
                    var sourceY = y * depthImage.height / heatMapHeight;
                    var depth = ReadDepthMeters(plane.data, plane.rowStride, plane.pixelStride, sourceX, sourceY);
                    heatMapTexture.SetPixel(x, y, DepthToColor(depth));
                }
            }

            heatMapTexture.Apply(false);
        }

        private Color DepthToColor(float depth)
        {
            if (depth <= 0f || float.IsNaN(depth) || float.IsInfinity(depth))
            {
                return new Color(0f, 0f, 0f, 0.55f);
            }

            var t = Mathf.Clamp01(depth / maxValidDistanceMeters);
            return Color.Lerp(Color.red, Color.blue, t);
        }

        private void SetStatus(string status)
        {
            if (distanceText != null)
            {
                distanceText.text = status;
            }
        }

        private void CreateUi()
        {
            canvas = new GameObject("M2 Depth Canvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvas.gameObject);

            CreateCrosshair(canvas.transform);
            distanceText = CreateDistanceText(canvas.transform);
            heatMapImage = CreateHeatMap(canvas.transform);
        }

        private static void CreateCrosshair(Transform parent)
        {
            var horizontal = CreateUiImage("Crosshair Horizontal", parent, new Color(0f, 1f, 0f, 0.95f));
            var horizontalRect = horizontal.rectTransform;
            horizontalRect.anchorMin = new Vector2(0.5f, 0.5f);
            horizontalRect.anchorMax = new Vector2(0.5f, 0.5f);
            horizontalRect.anchoredPosition = Vector2.zero;
            horizontalRect.sizeDelta = new Vector2(44f, 3f);

            var vertical = CreateUiImage("Crosshair Vertical", parent, new Color(0f, 1f, 0f, 0.95f));
            var verticalRect = vertical.rectTransform;
            verticalRect.anchorMin = new Vector2(0.5f, 0.5f);
            verticalRect.anchorMax = new Vector2(0.5f, 0.5f);
            verticalRect.anchoredPosition = Vector2.zero;
            verticalRect.sizeDelta = new Vector2(3f, 44f);
        }

        private static Text CreateDistanceText(Transform parent)
        {
            var textObject = new GameObject("Center Distance Text");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 42;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.text = "Waiting for LiDAR depth...";

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -24f);
            rect.sizeDelta = new Vector2(760f, 190f);
            return text;
        }

        private RawImage CreateHeatMap(Transform parent)
        {
            var imageObject = new GameObject("Depth HeatMap");
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<RawImage>();
            image.color = new Color(1f, 1f, 1f, 0.75f);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -24f);
            rect.sizeDelta = new Vector2(160f, 120f);
            image.gameObject.SetActive(showHeatMap);
            return image;
        }

        private static Image CreateUiImage(string name, Transform parent, Color color)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
