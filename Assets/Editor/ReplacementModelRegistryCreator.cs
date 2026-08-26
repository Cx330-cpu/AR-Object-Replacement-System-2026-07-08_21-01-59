using ARObjectReplacement.Rendering;
using UnityEditor;
using UnityEngine;

public static class ReplacementModelRegistryCreator
{
    private const string ModelFolder = "Assets/Resources/ReplacementModels";
    private const float PreviewMaxExtentMeters = 0.35f;

    private static readonly string[] ResourceNames =
    {
        "酒",
        "手提箱",
        "帆布包",
        "retro_computer",
        "tv__old_tv__retro_tv(1)",
        "手持电话",
        "怀表",
        "玻璃罐",
        "DefaultReplacement"
    };

    [MenuItem("AR Object Replacement/Create Scene Replacement Registry")]
    public static void CreateSceneRegistry()
    {
        var existing = Object.FindObjectOfType<ReplacementModelRegistry>();
        var registryObject = existing != null
            ? existing.gameObject
            : new GameObject("Replacement Model Registry");

        var registry = registryObject.GetComponent<ReplacementModelRegistry>();
        if (registry == null)
        {
            registry = registryObject.AddComponent<ReplacementModelRegistry>();
        }

        if (existing == null)
        {
            var xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null)
            {
                Undo.SetTransformParent(registryObject.transform, xrOrigin.transform, "Parent replacement registry");
                registryObject.transform.localPosition = Vector3.zero;
                registryObject.transform.localRotation = Quaternion.identity;
                registryObject.transform.localScale = Vector3.one;
            }
        }

        var serializedRegistry = new SerializedObject(registry);
        var entriesProperty = serializedRegistry.FindProperty("entries");
        entriesProperty.arraySize = ResourceNames.Length;

        for (var i = 0; i < ResourceNames.Length; i++)
        {
            var resourceName = ResourceNames[i];
            var modelRoot = FindOrCreateChild(registryObject.transform, $"Replacement_{resourceName}");
            var visual = FindOrCreateChild(modelRoot.transform, "Visual");
            EnsureVisualModel(visual.transform, resourceName);
            modelRoot.SetActive(i == 0);

            var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
            entryProperty.FindPropertyRelative("ResourceName").stringValue = resourceName;
            entryProperty.FindPropertyRelative("ModelRoot").objectReferenceValue = modelRoot;
        }

        serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
        Selection.activeObject = registryObject;
        Debug.Log("Scene replacement registry is ready. Showing the first replacement model only.");
    }

    [MenuItem("AR Object Replacement/Show Only Selected Replacement Model")]
    public static void ShowOnlySelectedReplacementModel()
    {
        var selectedRoot = FindSelectedReplacementRoot();
        if (selectedRoot == null)
        {
            Debug.LogWarning("Select a Replacement_<name>, Visual child, or model child first.");
            return;
        }

        ShowOnly(selectedRoot.name.Replace("Replacement_", string.Empty));
    }

    [MenuItem("AR Object Replacement/Show Only/酒")]
    public static void ShowOnlyWine()
    {
        ShowOnly("酒");
    }

    [MenuItem("AR Object Replacement/Show Only/手提箱")]
    public static void ShowOnlySuitcase()
    {
        ShowOnly("手提箱");
    }

    [MenuItem("AR Object Replacement/Show Only/帆布包")]
    public static void ShowOnlyHandbag()
    {
        ShowOnly("帆布包");
    }

    [MenuItem("AR Object Replacement/Show Only/retro_computer")]
    public static void ShowOnlyRetroComputer()
    {
        ShowOnly("retro_computer");
    }

    [MenuItem("AR Object Replacement/Show Only/tv__old_tv__retro_tv(1)")]
    public static void ShowOnlyTv()
    {
        ShowOnly("tv__old_tv__retro_tv(1)");
    }

    [MenuItem("AR Object Replacement/Hide All Scene Replacement Models")]
    public static void HideAllSceneReplacementModels()
    {
        var registry = Object.FindObjectOfType<ReplacementModelRegistry>();
        if (registry == null)
        {
            Debug.LogWarning("No ReplacementModelRegistry was found in the current scene.");
            return;
        }

        SetOnlyActive(registry, null);
        Debug.Log("All scene replacement models are hidden.");
    }

    [MenuItem("AR Object Replacement/Fit Selected Replacement Model For Editing")]
    public static void FitSceneReplacementModelsForEditing()
    {
        var selectedRoot = FindSelectedReplacementRoot();
        if (selectedRoot == null)
        {
            Debug.LogWarning("Select a Replacement_<name>, Visual child, or model child first.");
            return;
        }

        selectedRoot.SetActive(true);
        var visual = selectedRoot.transform.Find("Visual");
        if (visual == null || visual.childCount == 0)
        {
            Debug.LogWarning($"No Visual child with model content was found under {selectedRoot.name}.");
            return;
        }

        var fitted = TryFitVisualForEditing(visual, PreviewMaxExtentMeters);
        Debug.Log(fitted
            ? $"Fitted {selectedRoot.name} for editing."
            : $"Could not calculate renderer bounds for {selectedRoot.name}.");
    }

    private static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child != null)
        {
            return child.gameObject;
        }

        var childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        Undo.SetTransformParent(childObject.transform, parent, $"Parent {childName}");
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject;
    }

    private static void EnsureVisualModel(Transform visualRoot, string resourceName)
    {
        if (visualRoot.childCount > 0)
        {
            return;
        }

        var prefab = LoadModelAsset(resourceName, "prefab");
        if (prefab == null)
        {
            prefab = LoadModelAsset(resourceName, "glb");
        }
        if (prefab == null)
        {
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {resourceName}");
        Undo.SetTransformParent(instance.transform, visualRoot, $"Parent {resourceName}");
        instance.name = resourceName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
    }

    private static GameObject LoadModelAsset(string resourceName, string extension)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{resourceName}.{extension}");
    }

    private static void ShowOnly(string resourceName)
    {
        var registry = Object.FindObjectOfType<ReplacementModelRegistry>();
        if (registry == null)
        {
            Debug.LogWarning("No ReplacementModelRegistry was found in the current scene.");
            return;
        }

        var shownRoot = SetOnlyActive(registry, resourceName);
        if (shownRoot != null)
        {
            Selection.activeObject = shownRoot;
            Debug.Log($"Showing only {shownRoot.name}.");
        }
        else
        {
            Debug.LogWarning($"No replacement model named {resourceName} was found in the current scene registry.");
        }
    }

    private static GameObject SetOnlyActive(ReplacementModelRegistry registry, string resourceName)
    {
        GameObject shownRoot = null;
        foreach (var entry in registry.Entries)
        {
            if (entry?.ModelRoot == null)
            {
                continue;
            }

            var shouldShow = !string.IsNullOrEmpty(resourceName) && entry.ResourceName == resourceName;
            Undo.RecordObject(entry.ModelRoot, "Set replacement model visibility");
            entry.ModelRoot.SetActive(shouldShow);
            if (shouldShow)
            {
                shownRoot = entry.ModelRoot;
            }
        }

        return shownRoot;
    }

    private static GameObject FindSelectedReplacementRoot()
    {
        var selected = Selection.activeTransform;
        while (selected != null)
        {
            if (selected.name.StartsWith("Replacement_"))
            {
                return selected.gameObject;
            }

            selected = selected.parent;
        }

        return null;
    }

    private static bool TryFitVisualForEditing(Transform visualRoot, float targetMaxExtent)
    {
        if (!TryCalculateWorldBounds(visualRoot.gameObject, out var bounds))
        {
            return false;
        }

        var maxExtent = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxExtent <= 0.00001f)
        {
            return false;
        }

        var scaleFactor = targetMaxExtent / maxExtent;
        Undo.RecordObject(visualRoot, "Fit replacement visual");
        visualRoot.localScale *= scaleFactor;

        if (!TryCalculateWorldBounds(visualRoot.gameObject, out var scaledBounds))
        {
            return true;
        }

        var bottomCenter = new Vector3(
            scaledBounds.center.x,
            scaledBounds.min.y,
            scaledBounds.center.z);
        var worldOffset = visualRoot.position - bottomCenter;
        var localOffset = visualRoot.parent != null
            ? visualRoot.parent.InverseTransformVector(worldOffset)
            : worldOffset;
        visualRoot.localPosition += localOffset;
        EditorUtility.SetDirty(visualRoot);
        return true;
    }

    private static bool TryCalculateWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
        {
            return false;
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        foreach (var renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}
