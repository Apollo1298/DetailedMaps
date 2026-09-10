using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds one BC7 AssetBundle per map texture (per-region streaming).
/// Menu: DetailedMaps → Build …
/// Batch: -executeMethod BuildDetailedMapsBundle.Build / BuildCharcoal / BuildColor
/// </summary>
public static class BuildDetailedMapsBundle
{
    private const string AssetsMapsFolder = "Assets/Maps";

    [MenuItem("DetailedMaps/Build Both Per-Region Bundles (BC7)")]
    public static void Build()
    {
        BuildInternal(includeCharcoal: true, includeColor: true);
    }

    [MenuItem("DetailedMaps/Build Charcoal Per-Region Bundles (BC7)")]
    public static void BuildCharcoal()
    {
        BuildInternal(includeCharcoal: true, includeColor: false);
    }

    [MenuItem("DetailedMaps/Build Color Per-Region Bundles (BC7)")]
    public static void BuildColor()
    {
        BuildInternal(includeCharcoal: false, includeColor: true);
    }

    [MenuItem("DetailedMaps/Build Base Bundles (BC7)")]
    public static void BuildLayeredBases()
    {
        BuildInternal(includeCharcoal: false, includeColor: false, includeBase: true);
    }

    [MenuItem("DetailedMaps/Build Recipe Shader Bundle")]
    public static void BuildLayeredRecipe()
    {
        BuildSingleAssetBundle(
            assetPath: "Assets/Shaders/DetailedMapsMapRecipe.shader",
            relativeFileUnderAssets: Path.Combine("Shaders", "DetailedMapsMapRecipe.shader"),
            bundleName: "detailedmaps.recipe",
            destFileName: "DetailedMaps.Recipe.bundle");
    }

    /// <summary>Zip per-region *_base + blank + recipe into out/DetailedMaps.zip</summary>
    [MenuItem("DetailedMaps/Pack DetailedMaps.zip")]
    public static void PackReleaseZip()
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            string destDir = Path.Combine(repoRoot, "out", "bundles");
            string zipPath = Path.Combine(repoRoot, "out", "DetailedMaps.zip");

            if (!Directory.Exists(destDir))
            {
                Fail($"Missing bundles dir: {destDir}");
                return;
            }

            var files = new List<string>();
            foreach (string path in Directory.GetFiles(destDir, "*.bundle"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_base") ||
                    name == "map_details_blank" ||
                    name.Equals("DetailedMaps.Recipe", StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(path);
                }
            }

            if (files.Count == 0)
            {
                Fail("No *_base / blank / Recipe bundles to pack. Build bases + recipe first.");
                return;
            }

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string path in files)
                {
                    zip.CreateEntryFromFile(path, Path.GetFileName(path), System.IO.Compression.CompressionLevel.Optimal);
                }
            }

            long bytes = new FileInfo(zipPath).Length;
            Debug.Log($"[DetailedMaps] Packed {files.Count} bundles → {zipPath} ({bytes / (1024 * 1024)} MB)");
            ExitIfBatchMode(0);
        }
        catch (System.Exception ex)
        {
            Fail(ex.ToString());
        }
    }

    private static void BuildSingleAssetBundle(
        string assetPath,
        string relativeFileUnderAssets,
        string bundleName,
        string destFileName)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            string unityOutDir = Path.Combine(projectRoot, "AssetBundles");
            string destDir = Path.Combine(repoRoot, "out", "bundles");

            if (!File.Exists(Path.Combine(Application.dataPath, relativeFileUnderAssets)))
            {
                Fail($"Missing asset at {assetPath}");
                return;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory(unityOutDir);
            Directory.CreateDirectory(destDir);

            var builds = new List<AssetBundleBuild>
            {
                new AssetBundleBuild
                {
                    assetBundleName = bundleName,
                    assetNames = new[] { assetPath }
                }
            };

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                unityOutDir,
                builds.ToArray(),
                BuildAssetBundleOptions.ForceRebuildAssetBundle
                    | BuildAssetBundleOptions.StrictMode
                    | BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);

            if (manifest == null)
            {
                Fail($"{destFileName}: BuildAssetBundles returned null.");
                return;
            }

            string builtPath = Path.Combine(unityOutDir, bundleName);
            if (!File.Exists(builtPath))
            {
                Fail($"Expected bundle missing: {builtPath}");
                return;
            }

            string destPath = Path.Combine(destDir, destFileName);
            File.Copy(builtPath, destPath, overwrite: true);
            Debug.Log($"[DetailedMaps] Built {destPath} ({new FileInfo(destPath).Length / 1024} KB)");
            ExitIfBatchMode(0);
        }
        catch (System.Exception ex)
        {
            Fail(ex.ToString());
        }
    }

    private static void BuildInternal(bool includeCharcoal, bool includeColor, bool includeBase = false)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            string sourceMaps = Path.Combine(repoRoot, "Maps");
            string unityOutDir = Path.Combine(projectRoot, "AssetBundles");
            string destDir = Path.Combine(repoRoot, "out", "bundles");

            if (!Directory.Exists(sourceMaps))
            {
                Fail($"Source maps folder not found: {sourceMaps}");
                return;
            }

            SyncMaps(sourceMaps, includeCharcoal, includeColor, includeBase);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            List<AssetBundleBuild> builds = CollectPerTextureBuilds(includeCharcoal, includeColor, includeBase);
            if (builds.Count == 0)
            {
                Fail("No textures to build.");
                return;
            }

            AssetDatabase.SaveAssets();

            if (Directory.Exists(unityOutDir))
            {
                Directory.Delete(unityOutDir, true);
            }

            Directory.CreateDirectory(unityOutDir);
            Directory.CreateDirectory(destDir);

            // Clear previous shipped bundles for the packs we are rebuilding.
            ClearDestBundles(destDir, includeCharcoal, includeColor, includeBase);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                unityOutDir,
                builds.ToArray(),
                BuildAssetBundleOptions.ForceRebuildAssetBundle
                    | BuildAssetBundleOptions.StrictMode
                    | BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);

            if (manifest == null)
            {
                Fail("BuildAssetBundles returned null. Check the Unity console for errors.");
                return;
            }

            int copied = 0;
            long totalBytes = 0;
            foreach (AssetBundleBuild build in builds)
            {
                string assetName = Path.GetFileNameWithoutExtension(build.assetNames[0]);
                string builtPath = Path.Combine(unityOutDir, build.assetBundleName);
                if (!File.Exists(builtPath))
                {
                    Fail($"Expected bundle missing after build: {builtPath}");
                    return;
                }

                string destPath = Path.Combine(destDir, assetName + ".bundle");
                File.Copy(builtPath, destPath, overwrite: true);
                long len = new FileInfo(destPath).Length;
                totalBytes += len;
                copied++;
                Debug.Log($"[DetailedMaps] Built {Path.GetFileName(destPath)} ({len / 1024} KB)");
            }

            Debug.Log(
                $"[DetailedMaps] Copied {copied} per-region bundles ({totalBytes / (1024 * 1024)} MB total) → {destDir}");
            LogCompressionFormats();
            ExitIfBatchMode(0);
        }
        catch (System.Exception ex)
        {
            Fail(ex.ToString());
        }
    }

    private static void ClearDestBundles(string destDir, bool includeCharcoal, bool includeColor, bool includeBase = false)
    {
        foreach (string path in Directory.GetFiles(destDir, "*.bundle"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            bool charcoal = name.EndsWith("_new");
            bool color = name.EndsWith("_color");
            bool bas = name.EndsWith("_base") || name == "map_details_blank";
            if ((includeCharcoal && (charcoal || name == "map_details_blank")) ||
                (includeColor && color) ||
                (includeBase && bas))
            {
                File.Delete(path);
            }
        }

        // Remove legacy mega-packs if present.
        foreach (string legacy in new[] { "DetailedMaps.Charcoal.bundle", "DetailedMaps.Color.bundle" })
        {
            string legacyPath = Path.Combine(destDir, legacy);
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }
        }
    }

    private static bool IsCharcoalMap(string fileName)
    {
        return fileName.EndsWith("_new.png") || fileName == "map_details_blank.png";
    }

    private static bool IsColorMap(string fileName)
    {
        return fileName.EndsWith("_color.png");
    }

    private static bool IsBaseMap(string fileName)
    {
        return fileName.EndsWith("_base.png") || fileName == "map_details_blank.png";
    }

    private static void SyncMaps(string sourceMaps, bool includeCharcoal, bool includeColor, bool includeBase = false)
    {
        string destAbs = Path.GetFullPath(Path.Combine(Application.dataPath, "Maps"));
        Directory.CreateDirectory(destAbs);

        foreach (string existing in Directory.GetFiles(destAbs, "*.png"))
        {
            File.Delete(existing);
            string meta = existing + ".meta";
            if (File.Exists(meta))
            {
                File.Delete(meta);
            }
        }

        foreach (string png in Directory.GetFiles(sourceMaps, "*.png"))
        {
            string fileName = Path.GetFileName(png);
            bool want = (includeCharcoal && IsCharcoalMap(fileName))
                        || (includeColor && IsColorMap(fileName))
                        || (includeBase && IsBaseMap(fileName));
            if (!want)
            {
                continue;
            }

            File.Copy(png, Path.Combine(destAbs, fileName), overwrite: true);
            Debug.Log($"[DetailedMaps] Synced {fileName}");
        }
    }

    private static List<AssetBundleBuild> CollectPerTextureBuilds(bool includeCharcoal, bool includeColor, bool includeBase = false)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AssetsMapsFolder });
        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileName(path);
            bool want = (includeCharcoal && IsCharcoalMap(fileName))
                        || (includeColor && IsColorMap(fileName))
                        || (includeBase && IsBaseMap(fileName));
            if (!want)
            {
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            ConfigureBc7Importer(importer);
            importer.assetBundleName = string.Empty;
            importer.assetBundleVariant = string.Empty;
            importer.SaveAndReimport();

            TextureImporter reloaded = AssetImporter.GetAtPath(path) as TextureImporter;
            TextureImporterPlatformSettings standalone = reloaded != null
                ? reloaded.GetPlatformTextureSettings("Standalone")
                : null;
            string formatLabel = standalone != null && standalone.overridden
                ? standalone.format.ToString()
                : (reloaded != null ? reloaded.textureCompression.ToString() : "unknown");
            Debug.Log($"[DetailedMaps] Configured {path} → Standalone format={formatLabel}");

            string assetName = Path.GetFileNameWithoutExtension(path);
            // Unity bundle names are lowercase; shipping filename keeps original asset name.
            string bundleName = assetName.ToLowerInvariant();

            builds.Add(new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = new[] { path }
            });
        }

        builds = builds.OrderBy(b => b.assetBundleName).ToList();
        Debug.Log($"[DetailedMaps] Queued {builds.Count} per-region bundles.");
        return builds;
    }

    private static void ConfigureBc7Importer(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 8192;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 100;

        TextureImporterPlatformSettings standalone = new TextureImporterPlatformSettings
        {
            name = "Standalone",
            overridden = true,
            maxTextureSize = 8192,
            resizeAlgorithm = TextureResizeAlgorithm.Mitchell,
            format = TextureImporterFormat.BC7,
            textureCompression = TextureImporterCompression.CompressedHQ,
            compressionQuality = 100,
            crunchedCompression = false,
            allowsAlphaSplitting = false
        };
        importer.SetPlatformTextureSettings(standalone);
    }

    private static void LogCompressionFormats()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AssetsMapsFolder });
        foreach (string guid in guids.Take(3))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                Debug.Log($"[DetailedMaps] Sample runtime format {Path.GetFileName(path)}: {tex.format} ({tex.width}x{tex.height})");
            }
        }

        Debug.Log("[DetailedMaps] Per-region build complete. Confirm formats above are BC7 / DXT5, not RGBA32.");
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
