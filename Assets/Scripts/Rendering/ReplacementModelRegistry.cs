using System;
using UnityEngine;

namespace ARObjectReplacement.Rendering
{
    public sealed class ReplacementModelRegistry : MonoBehaviour
    {
        [SerializeField] private ReplacementModelEntry[] entries = Array.Empty<ReplacementModelEntry>();
        [SerializeField] private bool hideModelsOnAwake = true;
        [SerializeField] private bool drawEditorBounds = true;

        public ReplacementModelEntry[] Entries => entries;

        private void Awake()
        {
            if (hideModelsOnAwake)
            {
                HideAll();
            }
        }

        public bool TryGetModel(string resourceName, out GameObject modelRoot)
        {
            modelRoot = null;
            if (string.IsNullOrEmpty(resourceName) || entries == null)
            {
                return false;
            }

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ResourceName) || entry.ModelRoot == null)
                {
                    continue;
                }

                if (entry.ResourceName == resourceName)
                {
                    modelRoot = entry.ModelRoot;
                    return true;
                }
            }

            return false;
        }

        public void HideAll()
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry?.ModelRoot != null)
                {
                    entry.ModelRoot.SetActive(false);
                }
            }
        }

        public void HideAllExcept(GameObject modelRoot)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry?.ModelRoot != null && entry.ModelRoot != modelRoot)
                {
                    entry.ModelRoot.SetActive(false);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawEditorBounds || entries == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.84f, 0.1f, 0.9f);
            foreach (var entry in entries)
            {
                if (entry?.ModelRoot == null ||
                    !entry.ModelRoot.activeInHierarchy ||
                    !TryCalculateBounds(entry.ModelRoot, out var bounds))
                {
                    continue;
                }

                Gizmos.DrawWireCube(bounds.center, bounds.size);
                Gizmos.DrawSphere(bounds.center, 0.015f);
            }
        }

        private static bool TryCalculateBounds(GameObject root, out Bounds bounds)
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

    [Serializable]
    public sealed class ReplacementModelEntry
    {
        public string ResourceName;
        public GameObject ModelRoot;
    }
}
