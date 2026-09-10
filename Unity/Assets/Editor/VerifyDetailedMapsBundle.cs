using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch: -executeMethod VerifyDetailedMapsBundle.Verify
/// Loads a sample per-region bundle and checks charcoal outputs exist.
/// </summary>
public static class VerifyDetailedMapsBundle
{
    public static void Verify()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string destDir = Path.Combine(repoRoot, "out", "bundles");
        if (!Directory.Exists(destDir))
        {
            Debug.LogError($"[Verify] Missing output dir: {destDir}");
            EditorApplication.Exit(1);
            return;
        }

        string[] bundles = Directory.GetFiles(destDir, "*.bundle");
        if (bundles.Length == 0)
        {
            Debug.LogError($"[Verify] No .bundle files in {destDir}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[Verify] Found {bundles.Length} bundles in {destDir}");

        string sample = Path.Combine(destDir, "map_bg_AirfieldRegion_base.bundle");
        if (!File.Exists(sample))
        {
            sample = Path.Combine(destDir, "map_bg_AirfieldRegion_new.bundle");
        }
        if (!File.Exists(sample))
        {
            sample = bundles[0];
        }

        Debug.Log($"[Verify] Loading sample: {sample} size={new FileInfo(sample).Length}");
        AssetBundle bundle = AssetBundle.LoadFromFile(sample);
        if (bundle == null)
        {
            Debug.LogError("[Verify] LoadFromFile returned null (incompatible or AssetBundle module disabled).");
            EditorApplication.Exit(1);
            return;
        }

        string[] names = bundle.GetAllAssetNames();
        Debug.Log($"[Verify] LoadFromFile OK. assets={names.Length}");
        Object sampleAsset = bundle.LoadAsset(names[0], typeof(Texture2D));
        Texture2D tex = sampleAsset as Texture2D;
        if (tex == null)
        {
            Debug.LogError($"[Verify] Sample LoadAsset failed for {names[0]}");
            bundle.Unload(true);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[Verify] Sample {names[0]} → {tex.width}x{tex.height} {tex.format}");
        bundle.Unload(true);

        string blank = Path.Combine(destDir, "map_details_blank.bundle");
        if (!File.Exists(blank))
        {
            Debug.LogWarning("[Verify] map_details_blank.bundle missing (optional for charcoal details blanking).");
        }

        EditorApplication.Exit(0);
    }
}
