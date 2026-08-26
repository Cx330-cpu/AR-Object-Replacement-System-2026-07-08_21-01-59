using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARObjectReplacement.Editor
{
    public static class PrepareIosTrialBuild
    {
        private const string ScenePath = "Assets/samplescene.unity";

        [MenuItem("AR Object Replacement/Prepare iOS Trial Build")]
        public static void Prepare()
        {
            EnsureSceneInBuild();
            PlayerSettings.iOS.cameraUsageDescription =
                "AR object detection, LiDAR depth, and model replacement experiments";
            LogReplacementModels();
            Debug.Log("[Prepare iOS Trial Build] samplescene added to build, camera usage updated.");
            Debug.Log("[Prepare iOS Trial Build] When Unity asks Append vs Replace, choose Replace. Then open the new Xcode project and build.");
        }

        private static void EnsureSceneInBuild()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;
        }

        private static void LogReplacementModels()
        {
            var folder = Path.Combine(Application.dataPath, "Resources", "ReplacementModels");
            foreach (var name in new[] { "酒.glb", "手持电话.glb", "电脑.glb", "retro_computer.glb" })
            {
                var path = Path.Combine(folder, name);
                Debug.Log(File.Exists(path)
                    ? $"[Prepare iOS Trial Build] found {name}"
                    : $"[Prepare iOS Trial Build] MISSING {name}");
            }
        }
    }
}
