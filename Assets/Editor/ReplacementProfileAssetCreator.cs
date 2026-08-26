using ARObjectReplacement.Rendering;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ReplacementProfileAssetCreator
{
    private const string ProfileFolder = "Assets/Resources/ReplacementProfiles";

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

    [MenuItem("AR Object Replacement/Create Missing Replacement Profiles")]
    public static void CreateMissingProfiles()
    {
        EnsureFolder(ProfileFolder);

        var createdCount = 0;
        foreach (var resourceName in ResourceNames)
        {
            var assetPath = $"{ProfileFolder}/{resourceName}.asset";
            if (AssetDatabase.LoadAssetAtPath<ReplacementModelProfileAsset>(assetPath) != null)
            {
                continue;
            }

            var profile = ReplacementModelController.CreateDefaultProfile(resourceName);
            var asset = ScriptableObject.CreateInstance<ReplacementModelProfileAsset>();
            asset.SetFromProfile(profile);
            AssetDatabase.CreateAsset(asset, assetPath);
            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created {createdCount} missing replacement profile asset(s) in {ProfileFolder}.");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath);
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
        {
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
