using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace ARObjectReplacement.Editor
{
    public static class IOSXcodeDuplicateTaskFix
    {
        [PostProcessBuild(999)]
        public static void PostprocessIOSBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            if (!File.Exists(projectPath))
            {
                Debug.LogWarning("[iOS Xcode Fix] project.pbxproj was not found.");
                return;
            }

            DisableUserScriptSandboxing(projectPath);
            var removed = DeduplicateNamedRunScripts(projectPath, "GameAssembly");
            if (removed > 0)
            {
                Debug.Log($"[iOS Xcode Fix] Removed {removed} duplicate GameAssembly Run Script phase(s). Use Replace (not Append) on the next Unity iOS build if Xcode still reports duplicate tasks.");
            }
        }

        private static void DisableUserScriptSandboxing(string projectPath)
        {
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainGuid = project.GetUnityMainTargetGuid();
            if (!string.IsNullOrEmpty(mainGuid))
            {
                project.SetBuildProperty(mainGuid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
            }

            var frameworkGuid = project.GetUnityFrameworkTargetGuid();
            if (!string.IsNullOrEmpty(frameworkGuid))
            {
                project.SetBuildProperty(frameworkGuid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
            }

            var gameAssemblyGuid = FindNativeTargetGuid(File.ReadAllText(projectPath), "GameAssembly");
            if (!string.IsNullOrEmpty(gameAssemblyGuid))
            {
                project.SetBuildProperty(gameAssemblyGuid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
            }

            project.WriteToFile(projectPath);
        }

        private static string FindNativeTargetGuid(string pbxprojText, string targetName)
        {
            var match = Regex.Match(
                pbxprojText,
                @"([A-Fa-f0-9]{24}) /\* " + Regex.Escape(targetName) + @" \*/ = \{\s*isa = PBXNativeTarget;",
                RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static int DeduplicateNamedRunScripts(string projectPath, string targetName)
        {
            var text = File.ReadAllText(projectPath);
            var targetMatch = Regex.Match(
                text,
                @"isa = PBXNativeTarget;\s*buildConfigurationList = [^\n]+\n\s*buildPhases = \((?<phases>[\s\S]*?)\);(?<mid>[\s\S]*?)name = " + Regex.Escape(targetName) + @";",
                RegexOptions.Multiline);

            if (!targetMatch.Success)
            {
                return 0;
            }

            var phasesBlock = targetMatch.Groups["phases"].Value;
            var runScriptMatches = Regex.Matches(phasesBlock, @"([A-Fa-f0-9]{24}) /\* Run Script \*/");
            if (runScriptMatches.Count < 2)
            {
                return 0;
            }

            var keepId = runScriptMatches[0].Groups[1].Value;
            var duplicateIds = new List<string>();
            for (var i = 1; i < runScriptMatches.Count; i++)
            {
                duplicateIds.Add(runScriptMatches[i].Groups[1].Value);
            }

            var cleanedPhases = phasesBlock;
            foreach (var duplicateId in duplicateIds)
            {
                cleanedPhases = Regex.Replace(
                    cleanedPhases,
                    @"\s*" + duplicateId + @" /\* Run Script \*/,",
                    string.Empty);
            }

            text = text.Remove(targetMatch.Groups["phases"].Index, targetMatch.Groups["phases"].Length)
                .Insert(targetMatch.Groups["phases"].Index, cleanedPhases);

            foreach (var duplicateId in duplicateIds)
            {
                text = Regex.Replace(
                    text,
                    @"\t\t" + duplicateId + @" /\* Run Script \*/ = \{[\s\S]*?\n\t\t\};\n",
                    string.Empty,
                    RegexOptions.Multiline);
            }

            File.WriteAllText(projectPath, text);
            Debug.Log($"[iOS Xcode Fix] Kept GameAssembly Run Script {keepId}, removed {duplicateIds.Count} duplicate(s).");
            return duplicateIds.Count;
        }
    }
}
