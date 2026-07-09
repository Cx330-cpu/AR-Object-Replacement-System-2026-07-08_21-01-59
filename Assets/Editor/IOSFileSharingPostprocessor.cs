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
            AddYoloModelToXcodeProject(pathToBuiltProject);
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

        private static void AddYoloModelToXcodeProject(string pathToBuiltProject)
        {
            var sourceModelPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "yolov8n.mlpackage");
            if (!Directory.Exists(sourceModelPath))
            {
                sourceModelPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "models", "yolov8n.mlpackage");
            }

            if (!Directory.Exists(sourceModelPath))
            {
                return;
            }

            var streamingRawPath = Path.Combine(pathToBuiltProject, "Data", "Raw");
            Directory.CreateDirectory(streamingRawPath);
            var streamingModelPath = Path.Combine(streamingRawPath, "yolov8n.mlpackage");
            if (Directory.Exists(streamingModelPath))
            {
                Directory.Delete(streamingModelPath, true);
            }

            CopyDirectory(sourceModelPath, streamingModelPath);

            var destinationModelPath = Path.Combine(pathToBuiltProject, "yolov8n.mlpackage");
            if (Directory.Exists(destinationModelPath))
            {
                Directory.Delete(destinationModelPath, true);
            }

            CopyDirectory(sourceModelPath, destinationModelPath);

            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTargetGuid = project.GetUnityMainTargetGuid();
            var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            var modelGuid = project.AddFile("yolov8n.mlpackage", "yolov8n.mlpackage", PBXSourceTree.Source);
            project.AddFileToBuild(mainTargetGuid, modelGuid);

            project.AddFrameworkToProject(frameworkTargetGuid, "CoreML.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "Vision.framework", false);
            project.AddFrameworkToProject(frameworkTargetGuid, "CoreVideo.framework", false);

            project.WriteToFile(projectPath);
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
