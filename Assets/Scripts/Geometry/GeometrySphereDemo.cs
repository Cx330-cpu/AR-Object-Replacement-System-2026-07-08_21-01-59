using ARObjectReplacement.Depth;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ARObjectReplacement.Geometry
{
    public sealed class GeometrySphereDemo : MonoBehaviour
    {
        [SerializeField] private ARDepthCrosshairMeasure depthMeasure;
        [SerializeField] private Camera arCamera;
        [SerializeField] private float sphereDiameterMeters = 0.06f;
        [SerializeField] private float positionSmoothing = 0.35f;
        [SerializeField] private float logIntervalSeconds = 0.5f;

        private readonly GeometryService geometryService = new GeometryService();
        private GameObject sphere;
        private Renderer sphereRenderer;
        private Canvas controlCanvas;
        private Text modeText;
        private float lastLogTime;
        private bool hasSmoothedPosition;
        private Vector3 smoothedPosition;
        private bool isPinned;
        private WorldPoint pinnedWorldPoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[M3 Geometry] Main Camera was not found. Geometry sphere demo was not installed.");
                return;
            }

            if (camera.GetComponent<GeometrySphereDemo>() == null)
            {
                camera.gameObject.AddComponent<GeometrySphereDemo>();
            }
        }

        private void Awake()
        {
            arCamera = arCamera != null ? arCamera : Camera.main;
            CreateControls();
            CreateSphere();
        }

        private void Update()
        {
            if (arCamera == null)
            {
                arCamera = Camera.main;
                if (arCamera == null)
                {
                    SetSphereVisible(false);
                    return;
                }
            }

            if (depthMeasure == null)
            {
                depthMeasure = FindObjectOfType<ARDepthCrosshairMeasure>();
                if (depthMeasure == null)
                {
                    SetSphereVisible(false);
                    return;
                }
            }

            var depthResult = depthMeasure.LatestResult;
            if (!depthResult.IsValid || !depthResult.HasIntrinsics)
            {
                SetSphereVisible(false);
                return;
            }

            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var worldPoint = geometryService.ScreenPixelToWorld(
                screenCenter,
                new Vector2Int(Screen.width, Screen.height),
                depthResult.DistanceMeters,
                depthResult.Intrinsics,
                arCamera.transform,
                depthResult.Timestamp);

            if (!worldPoint.IsValid)
            {
                if (!isPinned)
                {
                    SetSphereVisible(false);
                }
                return;
            }

            if (!isPinned)
            {
                UpdateSphere(worldPoint, true);
                LogWorldPoint(worldPoint, "FOLLOW");
            }
            else
            {
                UpdateSphere(pinnedWorldPoint, false);
                LogWorldPoint(pinnedWorldPoint, "PINNED");
            }
        }

        private void CreateSphere()
        {
            sphere = new GameObject("M3 WorldPoint Sphere");
            var meshFilter = sphere.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateSphereMesh();
            sphereRenderer = sphere.AddComponent<MeshRenderer>();
            sphere.name = "M3 WorldPoint Sphere";
            sphere.transform.localScale = Vector3.one * sphereDiameterMeters;
            if (sphereRenderer != null)
            {
                var shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    sphereRenderer.sharedMaterial = new Material(shader);
                    sphereRenderer.sharedMaterial.color = new Color(0.62f, 0.2f, 1f, 1f);
                }
            }

            SetSphereVisible(false);
        }

        private static Mesh CreateSphereMesh(int longitudeSegments = 24, int latitudeSegments = 16)
        {
            var vertices = new Vector3[(latitudeSegments + 1) * (longitudeSegments + 1)];
            var triangles = new int[latitudeSegments * longitudeSegments * 6];

            var vertexIndex = 0;
            for (var lat = 0; lat <= latitudeSegments; lat++)
            {
                var theta = Mathf.PI * lat / latitudeSegments;
                var sinTheta = Mathf.Sin(theta);
                var cosTheta = Mathf.Cos(theta);

                for (var lon = 0; lon <= longitudeSegments; lon++)
                {
                    var phi = 2f * Mathf.PI * lon / longitudeSegments;
                    vertices[vertexIndex++] = new Vector3(
                        0.5f * sinTheta * Mathf.Cos(phi),
                        0.5f * cosTheta,
                        0.5f * sinTheta * Mathf.Sin(phi));
                }
            }

            var triangleIndex = 0;
            for (var lat = 0; lat < latitudeSegments; lat++)
            {
                for (var lon = 0; lon < longitudeSegments; lon++)
                {
                    var current = lat * (longitudeSegments + 1) + lon;
                    var next = current + longitudeSegments + 1;

                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = next + 1;
                }
            }

            var mesh = new Mesh { name = "M3 Visual Sphere Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void UpdateSphere(WorldPoint worldPoint, bool smooth)
        {
            if (sphere == null)
            {
                CreateSphere();
            }

            smoothedPosition = smooth && hasSmoothedPosition
                ? Vector3.Lerp(smoothedPosition, worldPoint.Position, Mathf.Clamp01(positionSmoothing))
                : worldPoint.Position;
            hasSmoothedPosition = true;

            sphere.transform.position = smoothedPosition;
            SetSphereVisible(true);
            UpdateModeText();
        }

        private void SetSphereVisible(bool visible)
        {
            if (sphere != null && sphere.activeSelf != visible)
            {
                sphere.SetActive(visible);
            }
        }

        private void LogWorldPoint(WorldPoint worldPoint, string mode)
        {
            if (Time.time - lastLogTime < logIntervalSeconds)
            {
                return;
            }

            lastLogTime = Time.time;
            Debug.Log(
                $"[M3 Geometry] {mode} WorldPoint x={worldPoint.Position.x:F3}, " +
                $"y={worldPoint.Position.y:F3}, z={worldPoint.Position.z:F3}, " +
                $"depth={worldPoint.DepthMeters:F3}m, " +
                $"pixel=({worldPoint.Pixel.x:F1}, {worldPoint.Pixel.y:F1})");
        }

        private void PinCurrentPoint()
        {
            if (!hasSmoothedPosition)
            {
                return;
            }

            pinnedWorldPoint = new WorldPoint
            {
                IsValid = true,
                Position = smoothedPosition,
                Pixel = Vector2.zero,
                CameraPoint = Vector3.zero,
                DepthMeters = 0f,
                Timestamp = Time.timeAsDouble
            };
            isPinned = true;
            UpdateSphere(pinnedWorldPoint, false);
            UpdateModeText();
            Debug.Log($"[M3 Geometry] Pinned sphere at x={pinnedWorldPoint.Position.x:F3}, y={pinnedWorldPoint.Position.y:F3}, z={pinnedWorldPoint.Position.z:F3}");
        }

        private void Unpin()
        {
            isPinned = false;
            hasSmoothedPosition = false;
            UpdateModeText();
            Debug.Log("[M3 Geometry] Sphere unpinned; returning to crosshair follow mode.");
        }

        private void CreateControls()
        {
            EnsureEventSystem();

            controlCanvas = new GameObject("M3 Geometry Controls").AddComponent<Canvas>();
            controlCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            controlCanvas.sortingOrder = 1100;
            var scaler = controlCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170f, 2532f);
            scaler.matchWidthOrHeight = 0.5f;
            controlCanvas.gameObject.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(controlCanvas.gameObject);

            var pinButton = CreateButton(controlCanvas.transform, "Pin Sphere Button", "固定", new Vector2(32f, 250f));
            pinButton.onClick.AddListener(PinCurrentPoint);

            var unpinButton = CreateButton(controlCanvas.transform, "Unpin Sphere Button", "取消固定", new Vector2(32f, 96f));
            unpinButton.onClick.AddListener(Unpin);

            modeText = CreateModeText(controlCanvas.transform);
            UpdateModeText();
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

            var eventSystemObject = new GameObject("M3 EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
            DontDestroyOnLoad(eventSystemObject);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.06f, 0.06f, 0.08f, 0.82f);

            var button = buttonObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.16f, 0.12f, 0.22f, 0.9f);
            colors.pressedColor = new Color(0.35f, 0.18f, 0.55f, 0.95f);
            button.colors = colors;

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
            text.fontSize = 48;
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

        private static Text CreateModeText(Transform parent)
        {
            var textObject = new GameObject("M3 Mode Text");
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.LowerLeft;
            text.color = new Color(0.72f, 0.42f, 1f, 1f);

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(480f, 128f);
            rect.sizeDelta = new Vector2(520f, 120f);
            return text;
        }

        private void UpdateModeText()
        {
            if (modeText != null)
            {
                modeText.text = isPinned ? "模式：固定" : "模式：跟随十字";
            }
        }
    }
}
