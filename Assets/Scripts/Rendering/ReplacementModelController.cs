using ARObjectReplacement.Pose;
using GLTFast;
using System.IO;
using UnityEngine;

namespace ARObjectReplacement.Rendering
{
    public sealed class ReplacementModelController
    {
        private readonly string resourceFolder;
        private const string ProfileResourceFolder = "ReplacementProfiles";
        private readonly float defaultScaleMeters;
        private readonly ReplacementModelRegistry registry;
        private GameObject activeModel;
        private string activeResourceName;
        private string loadingResourceName;
        private bool isLoading;
        private bool activeModelFromRegistry;

        public string ActiveResourceName => activeResourceName;
        public bool HasRealModel => hasRealModel;
        public bool IsLoading => isLoading;
        public bool IsUsingSceneRegistry => activeModelFromRegistry;
        private bool hasRealModel;

        public ReplacementModelController(
            string resourceFolder = "ReplacementModels",
            float defaultScaleMeters = 0.18f,
            ReplacementModelRegistry registry = null)
        {
            this.resourceFolder = resourceFolder;
            this.defaultScaleMeters = defaultScaleMeters;
            this.registry = registry;
            if (Application.isPlaying)
            {
                this.registry?.HideAll();
            }
        }

        public void UpdateModel(GenericPoseFrame frame, Transform cameraTransform, int classId, RuntimePoseMode mode)
        {
            if (cameraTransform == null || (!frame.IsValid && frame.Stability != GenericPoseStability.TrackingLost))
            {
                Hide();
                return;
            }

            var resourceName = ReplacementModelMapper.GetResourceName(classId);
            var profile = GetProfile(resourceName);
            EnsureModel(resourceName);
            if (activeModel == null)
            {
                return;
            }

            var centerWorld = cameraTransform.TransformPoint(frame.CenterCamera);
            var forwardWorld = cameraTransform.TransformDirection(frame.ForwardCamera).normalized;
            var upWorld = cameraTransform.TransformDirection(frame.UpCamera).normalized;
            if (forwardWorld.sqrMagnitude < 1e-6f)
            {
                forwardWorld = cameraTransform.forward;
            }
            if (upWorld.sqrMagnitude < 1e-6f)
            {
                upWorld = Vector3.up;
            }

            var poseRotation = Quaternion.LookRotation(forwardWorld, upWorld);
            var modelRotation = poseRotation * Quaternion.Euler(profile.RotationOffsetEuler);
            var scale = ComputeScale(frame, profile);
            var pivotWorldOffset = poseRotation * profile.PivotOffsetMeters;
            var verticalWorldOffset = upWorld * profile.VerticalOffsetMeters;

            activeModel.transform.position = centerWorld + pivotWorldOffset + verticalWorldOffset;
            activeModel.transform.rotation = modelRotation;
            activeModel.transform.localScale = Vector3.one * scale;
            if (!activeModel.activeSelf)
            {
                activeModel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (activeModel != null && activeModel.activeSelf)
            {
                activeModel.SetActive(false);
            }
            registry?.HideAll();
        }

        private void EnsureModel(string resourceName)
        {
            if (activeModel != null && activeResourceName == resourceName)
            {
                return;
            }

            if (activeModel != null)
            {
                if (activeModelFromRegistry)
                {
                    activeModel.SetActive(false);
                }
                else
                {
                    Object.Destroy(activeModel);
                }
                activeModel = null;
            }

            activeResourceName = resourceName;
            hasRealModel = false;
            activeModelFromRegistry = false;
            isLoading = false;

            if (registry != null && registry.TryGetModel(resourceName, out var sceneModel))
            {
                activeModel = sceneModel;
                activeModelFromRegistry = true;
                hasRealModel = true;
                registry.HideAllExcept(sceneModel);
                return;
            }

            var prefab = Resources.Load<GameObject>($"{resourceFolder}/{resourceName}");
            activeModel = prefab != null ? Object.Instantiate(prefab) : CreateFallbackModel(resourceName);
            activeModel.name = $"Replacement Model - {resourceName}";
            Object.DontDestroyOnLoad(activeModel);
            if (prefab != null)
            {
                hasRealModel = true;
            }
            if (prefab == null)
            {
                LoadGlbAsync(resourceName, activeModel);
            }
        }

        private async void LoadGlbAsync(string resourceName, GameObject targetRoot)
        {
            if (isLoading && loadingResourceName == resourceName)
            {
                return;
            }

            isLoading = true;
            loadingResourceName = resourceName;
            var glbPath = BuildGlbPath(resourceName);
            if (!File.Exists(glbPath))
            {
                isLoading = false;
                return;
            }

            var gltf = new GltfImport();
            var loaded = await gltf.LoadFile(glbPath);
            if (!loaded || targetRoot == null || activeResourceName != resourceName)
            {
                gltf.Dispose();
                isLoading = false;
                return;
            }

            var instantiated = await gltf.InstantiateMainSceneAsync(targetRoot.transform);
            if (instantiated)
            {
                ClearFallbackChildren(targetRoot);
                hasRealModel = true;
            }
            else
            {
                gltf.Dispose();
            }

            isLoading = false;
        }

        private string BuildGlbPath(string resourceName)
        {
            var streamingPath = Path.Combine(Application.streamingAssetsPath, resourceFolder, $"{resourceName}.glb");
            if (File.Exists(streamingPath))
            {
                return streamingPath;
            }

            return Path.Combine(Application.dataPath, "Resources", resourceFolder, $"{resourceName}.glb");
        }

        private static void ClearFallbackChildren(GameObject root)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "Fallback Body" || child.name == "Forward Marker")
                {
                    Object.Destroy(child.gameObject);
                }
            }
        }

        private float ComputeScale(GenericPoseFrame frame, ReplacementModelProfile profile)
        {
            var targetSize = GetTargetSize(frame, profile.FitMode);
            if (targetSize <= 0.01f)
            {
                return defaultScaleMeters;
            }

            var modelSize = hasRealModel ? GetModelSize(activeModel, profile.FitMode) : 1f;
            if (modelSize <= 0.0001f)
            {
                modelSize = 1f;
            }

            return Mathf.Clamp(
                targetSize / modelSize * profile.ScaleMultiplier,
                profile.MinimumScale,
                profile.MaximumScale);
        }

        private static float GetTargetSize(GenericPoseFrame frame, ReplacementFitMode fitMode)
        {
            switch (fitMode)
            {
                case ReplacementFitMode.Height:
                    return Mathf.Max(frame.ExtentMeters.y, 0.01f);
                case ReplacementFitMode.Width:
                    return Mathf.Max(frame.ExtentMeters.x, frame.ExtentMeters.z);
                default:
                    return Mathf.Max(frame.ExtentMeters.x, Mathf.Max(frame.ExtentMeters.y, frame.ExtentMeters.z));
            }
        }

        private static float GetModelSize(GameObject model, ReplacementFitMode fitMode)
        {
            if (!TryCalculateLocalBounds(model, out var bounds))
            {
                return 1f;
            }

            var size = bounds.size;
            switch (fitMode)
            {
                case ReplacementFitMode.Height:
                    return Mathf.Max(size.y, 0.0001f);
                case ReplacementFitMode.Width:
                    return Mathf.Max(size.x, size.z);
                default:
                    return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            }
        }

        private static bool TryCalculateLocalBounds(GameObject model, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (model == null)
            {
                return false;
            }

            var originalPosition = model.transform.position;
            var originalRotation = model.transform.rotation;
            var originalScale = model.transform.localScale;
            model.transform.position = Vector3.zero;
            model.transform.rotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            try
            {
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                var hasBounds = false;
                foreach (var renderer in renderers)
                {
                    var rendererBounds = renderer.bounds;
                    var localBounds = new Bounds(rendererBounds.center, rendererBounds.size);
                    if (!hasBounds)
                    {
                        bounds = localBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localBounds);
                    }
                }

                return hasBounds;
            }
            finally
            {
                model.transform.position = originalPosition;
                model.transform.rotation = originalRotation;
                model.transform.localScale = originalScale;
            }
        }

        private static ReplacementModelProfile GetProfile(string resourceName)
        {
            var profileAsset = Resources.Load<ReplacementModelProfileAsset>($"{ProfileResourceFolder}/{resourceName}");
            if (profileAsset != null)
            {
                return profileAsset.ToProfile(resourceName);
            }

            return CreateDefaultProfile(resourceName);
        }

        public static ReplacementModelProfile CreateDefaultProfile(string resourceName)
        {
            var profile = ReplacementModelProfile.Default(resourceName);
            switch (resourceName)
            {
                case "酒":
                    profile.FitMode = ReplacementFitMode.Height;
                    profile.ScaleMultiplier = 1.15f;
                    profile.VerticalOffsetMeters = 0.02f;
                    profile.RotationOffsetEuler = new Vector3(0f, 180f, 0f);
                    profile.MinimumScale = 0.005f;
                    profile.MaximumScale = 2.5f;
                    break;
                case "电脑":
                case "retro_computer":
                    profile.FitMode = ReplacementFitMode.Width;
                    profile.ScaleMultiplier = 1.05f;
                    profile.RotationOffsetEuler = new Vector3(0f, 180f, 0f);
                    break;
                case "手持电话":
                    profile.FitMode = ReplacementFitMode.Width;
                    profile.ScaleMultiplier = 1.1f;
                    profile.RotationOffsetEuler = new Vector3(0f, 180f, 0f);
                    break;
                case "手提箱":
                case "帆布包":
                    profile.FitMode = ReplacementFitMode.Width;
                    profile.ScaleMultiplier = 1.05f;
                    profile.RotationOffsetEuler = new Vector3(0f, 180f, 0f);
                    break;
                default:
                    profile.ScaleMultiplier = 1f;
                    break;
            }

            return profile;
        }

        private static GameObject CreateFallbackModel(string resourceName)
        {
            var root = new GameObject($"Fallback Replacement - {resourceName}");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Fallback Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1f, 0.75f, 0.65f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Forward Marker";
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            marker.transform.localScale = new Vector3(0.18f, 0.08f, 0.18f);

            ApplyMaterial(body, new Color(0.25f, 0.72f, 1f, 0.86f));
            ApplyMaterial(marker, new Color(1f, 0.38f, 0.12f, 0.95f));
            return root;
        }

        private static void ApplyMaterial(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

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
                return;
            }

            renderer.sharedMaterial = new Material(shader) { color = color };
        }
    }
}
