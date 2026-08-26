using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace ARObjectReplacement.Editor
{
    public static class IOSFileSharingPostprocessor
    {
        [PostProcessBuild]
        public static void PostprocessIOSBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            EnableFileSharing(pathToBuiltProject);
            AddYoloModelsToStreamingAssets(pathToBuiltProject);
            AddReplacementModelsToStreamingAssets(pathToBuiltProject);
            AddNativeFrameworks(pathToBuiltProject);
        }

        private static void EnableFileSharing(string pathToBuiltProject)
        {
            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var root = plist.root;
            root.SetBoolean("UIFileSharingEnabled", true);
            root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

            plist.WriteToFile(plistPath);
        }

        private static void AddNativeFrameworks(string pathToBuiltProject)
        {
            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            AddFrameworkIfMissing(project, frameworkTargetGuid, "CoreML.framework");
            AddFrameworkIfMissing(project, frameworkTargetGuid, "Vision.framework");
            AddFrameworkIfMissing(project, frameworkTargetGuid, "CoreVideo.framework");

            project.WriteToFile(projectPath);
        }

        private static void AddFrameworkIfMissing(PBXProject project, string targetGuid, string framework)
        {
            if (string.IsNullOrEmpty(targetGuid) || project.ContainsFramework(targetGuid, framework))
            {
                return;
            }

            project.AddFrameworkToProject(targetGuid, framework, false);
        }

        private static void AddYoloModelsToStreamingAssets(string pathToBuiltProject)
        {
            CopyYoloModelToStreamingAssets(pathToBuiltProject, "yolov8n-seg.mlpackage");
            CopyYoloModelToStreamingAssets(pathToBuiltProject, "yolov8n.mlpackage");
        }

        private static void CopyYoloModelToStreamingAssets(string pathToBuiltProject, string modelDirectoryName)
        {
            var sourceModelPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", modelDirectoryName);
            if (!Directory.Exists(sourceModelPath))
            {
                sourceModelPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "models", modelDirectoryName);
            }

            if (!Directory.Exists(sourceModelPath))
            {
                return;
            }

            var streamingRawPath = Path.Combine(pathToBuiltProject, "Data", "Raw");
            Directory.CreateDirectory(streamingRawPath);
            var streamingModelPath = Path.Combine(streamingRawPath, modelDirectoryName);
            if (Directory.Exists(streamingModelPath))
            {
                Directory.Delete(streamingModelPath, true);
            }

            CopyDirectory(sourceModelPath, streamingModelPath);
        }

        private static void AddReplacementModelsToStreamingAssets(string pathToBuiltProject)
        {
            var sourceDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "Resources",
                "ReplacementModels");
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            var destinationDirectory = Path.Combine(
                pathToBuiltProject,
                "Data",
                "Raw",
                "ReplacementModels");
            Directory.CreateDirectory(destinationDirectory);

            foreach (var sourceFile in Directory.GetFiles(sourceDirectory, "*.glb", SearchOption.TopDirectoryOnly))
            {
                var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destinationFile, true);
            }
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = file.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                File.Copy(file, Path.Combine(destinationDirectory, relativePath), true);
            }
        }
    }
}
