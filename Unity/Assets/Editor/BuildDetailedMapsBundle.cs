using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the DetailedMaps asset bundle for Unity 6 / TLD 2.50+.
/// Menu: DetailedMaps → Build Asset Bundle
/// Batch: -executeMethod BuildDetailedMapsBundle.Build
/// </summary>
public static class BuildDetailedMapsBundle
{
    private const string BundleName = "detailedmaps";
    private const string AssetsMapsFolder = "Assets/Maps";

    [MenuItem("DetailedMaps/Build Asset Bundle")]
    public static void Build()
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            string sourceMaps = Path.Combine(repoRoot, "Maps");
            string outputDir = Path.Combine(projectRoot, "AssetBundles");
            string destBundle = Path.Combine(repoRoot, BundleName);

            if (!Directory.Exists(sourceMaps))
            {
                Fail($"Source maps folder not found: {sourceMaps}");
                return;
            }

            SyncMaps(sourceMaps);
            ConfigureTextureImporters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }

            Directory.CreateDirectory(outputDir);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                outputDir,
                BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows64);

            if (manifest == null)
            {
                Fail("BuildAssetBundles returned null. Check the Unity console for errors.");
                return;
            }

            string builtBundle = Path.Combine(outputDir, BundleName);
            if (!File.Exists(builtBundle))
            {
                Fail($"Expected bundle missing after build: {builtBundle}");
                return;
            }

            File.Copy(builtBundle, destBundle, overwrite: true);
            Debug.Log($"[DetailedMaps] Built {BundleName} ({new FileInfo(destBundle).Length / (1024 * 1024)} MB) → {destBundle}");
            ExitIfBatchMode(0);
        }
        catch (System.Exception ex)
        {
            Fail(ex.ToString());
        }
    }

    private static void SyncMaps(string sourceMaps)
    {
        string destAbs = Path.GetFullPath(Path.Combine(Application.dataPath, "Maps"));
        Directory.CreateDirectory(destAbs);

        foreach (string png in Directory.GetFiles(sourceMaps, "*.png"))
        {
            string fileName = Path.GetFileName(png);
            string dest = Path.Combine(destAbs, fileName);
            File.Copy(png, dest, overwrite: true);
            Debug.Log($"[DetailedMaps] Synced {fileName}");
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureTextureImporters()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AssetsMapsFolder });
        if (guids.Length == 0)
        {
            Fail($"No textures found under {AssetsMapsFolder}");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.assetBundleName = BundleName;
            importer.assetBundleVariant = string.Empty;
            importer.SaveAndReimport();
            Debug.Log($"[DetailedMaps] Configured {path}");
        }
    }

    private static void Fail(string message)
    {
        Debug.LogError($"[DetailedMaps] {message}");
        ExitIfBatchMode(1);
        throw new System.InvalidOperationException(message);
    }

    private static void ExitIfBatchMode(int code)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(code);
        }
    }
}
