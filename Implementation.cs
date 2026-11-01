using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Il2CppObjectList = Il2CppSystem.Collections.Generic.List<UnityEngine.Object>;

namespace DetailedMaps
{
    public class Implementation : MelonMod
    {
        private const string PackZipFileName = "DetailedMaps.zip";
        private const string RecipeBundleFileName = "DetailedMaps.Recipe.bundle";
        private const string BlankDetailsName = "map_details_blank";
        private const string BaseSuffix = "_base";

        private static readonly Dictionary<string, string> zipEntryByAssetName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AssetBundle> loadedBundles = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> backgroundNameAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["map_bg_AshCanyon"] = "map_bg_AshCanyonRegion",
            ["map_bg_MountainTownRegionSandbox"] = "map_bg_MountainTownRegion",
            ["map_bg_DamRiverTransitionZone"] = "map_bg_DamRiverTransitionZoneB",
        };

        private static readonly Il2CppObjectList roots = new();

        private static string? packDirectory;
        private static string? packZipPath;
        private static Texture2D? blankTexture;
        private static Texture2D? bakedTexture;
        private static string? bakedKey;
        private static AssetBundle? recipeBundle;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg($"Version {Info.Version} loaded!");

            try
            {
                Settings.Instance.AddToModSettings("Detailed Maps");
                packDirectory = ResolvePackDirectory();
                packZipPath = FindPack(PackZipFileName);
                if (packZipPath == null)
                {
                    throw new FileNotFoundException(
                        $"Missing '{PackZipFileName}' next to the DLL or in Mods/DetailedMaps/. " +
                        "Old charcoal/color PNG zips and loose *.bundle files are unsupported.");
                }

                IndexPackZip(packZipPath);
                MelonLogger.Msg($"Pack: {packZipPath} ({zipEntryByAssetName.Count} bundle entries).");

                if (!TryLoadRecipeShader())
                {
                    MelonLogger.Error($"Missing '{RecipeBundleFileName}' inside '{PackZipFileName}'.");
                }

                if (!TryGetBlankDetails(out _))
                {
                    MelonLogger.Warning($"Optional '{BlankDetailsName}' not in pack; details overlay may show vanilla.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize map pack: {ex}");
            }
        }

        internal static void OnSettingsChanged()
        {
            MelonLogger.Msg(
                $"Settings: style={(Settings.Instance.MapStyle == 0 ? "Charcoal" : "Color")} " +
                $"grid={Settings.Instance.Grid} noise={Settings.Instance.Noise}");
            InvalidateBake();
            TryRebakeOpenMaps();
        }

        internal static AsyncOperationHandle<Texture2D> CreateCompletedTextureHandle(Texture2D texture)
        {
            return Addressables.ResourceManager.CreateCompletedOperation(texture, string.Empty);
        }

        internal static bool TryGetBlankDetails(out Texture2D texture)
        {
            texture = null!;
            if (blankTexture != null)
            {
                texture = blankTexture;
                return true;
            }

            if (!zipEntryByAssetName.ContainsKey(BlankDetailsName))
            {
                return false;
            }

            try
            {
                blankTexture = LoadTextureFromPackBundle(BlankDetailsName);
                roots.Add(blankTexture);
                texture = blankTexture;
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load blank details: {ex}");
                return false;
            }
        }

        internal static bool TryGetOrBakeBackground(string vanillaFilename, float sceneRadius, out Texture2D texture)
        {
            texture = null!;
            if (string.IsNullOrEmpty(vanillaFilename))
            {
                return false;
            }

            if (!vanillaFilename.StartsWith("map_bg_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string regionKey = ResolveRegionKey(vanillaFilename);
            string baseAsset = regionKey + BaseSuffix;
            if (!zipEntryByAssetName.ContainsKey(baseAsset))
            {
                MelonLogger.Warning($"No base bundle '{baseAsset}' in '{PackZipFileName}' for '{vanillaFilename}'.");
                return false;
            }

            float radius = sceneRadius > 1f ? sceneRadius : MapRecipe.LastSceneRadius;
            string key = BakeCacheKey(baseAsset, radius);
            if (bakedTexture != null && bakedKey == key)
            {
                texture = bakedTexture;
                return true;
            }

            if (!MapRecipe.HasShader && !TryLoadRecipeShader())
            {
                MelonLogger.Error("Cannot build map texture: recipe shader missing.");
                return false;
            }

            try
            {
                Texture2D baseTex = LoadTextureFromPackBundle(baseAsset);
                try
                {
                    InvalidateBake();
                    bakedTexture = MapRecipe.Bake(baseTex, Settings.Instance, radius);
                    bakedKey = BakeCacheKey(baseAsset, MapRecipe.LastSceneRadius);
                    roots.Add(bakedTexture);
                    texture = bakedTexture;
                    return true;
                }
                finally
                {
                    UnloadPackBundle(baseAsset);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to build map texture '{baseAsset}': {ex}");
                return false;
            }
        }

        private static string ResolveRegionKey(string vanillaFilename)
        {
            if (backgroundNameAliases.TryGetValue(vanillaFilename, out string? aliased))
            {
                vanillaFilename = aliased;
            }

            if (vanillaFilename.EndsWith(BaseSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return vanillaFilename[..^BaseSuffix.Length];
            }

            if (vanillaFilename.EndsWith("_new", StringComparison.OrdinalIgnoreCase) ||
                vanillaFilename.EndsWith("_color", StringComparison.OrdinalIgnoreCase))
            {
                int cut = vanillaFilename.LastIndexOf('_');
                return vanillaFilename[..cut];
            }

            return vanillaFilename;
        }

        private static string BakeCacheKey(string baseAsset, float sceneRadius)
        {
            Settings s = Settings.Instance;
            return $"{baseAsset}|{s.MapStyle}|{s.Grid}|{s.Noise}|r={sceneRadius:F1}";
        }

        private static void InvalidateBake()
        {
            if (bakedTexture != null)
            {
                roots.Remove(bakedTexture);
                UnityEngine.Object.Destroy(bakedTexture);
                bakedTexture = null;
            }

            bakedKey = null;
        }

        private static void TryRebakeOpenMaps()
        {
            try
            {
                FogOfWar[]? maps = UnityEngine.Object.FindObjectsOfType<FogOfWar>();
                if (maps == null)
                {
                    return;
                }

                foreach (FogOfWar fow in maps)
                {
                    if (fow == null)
                    {
                        continue;
                    }

                    string bgFile = fow.m_MapBackgroundFilename;
                    if (string.IsNullOrEmpty(bgFile) ||
                        !bgFile.StartsWith("map_bg_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (TryGetOrBakeBackground(bgFile, fow.m_SceneRadius, out Texture2D bg))
                    {
                        fow.m_MapBackgroundHandle = CreateCompletedTextureHandle(bg);
                        fow.m_Redraw = true;
                        MelonLogger.Msg($"Updated open map '{bgFile}' sceneR={fow.m_SceneRadius:F1}.");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Updating open maps failed: {ex}");
            }
        }

        private static void IndexPackZip(string zipPath)
        {
            zipEntryByAssetName.Clear();
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) ||
                    !entry.Name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetName = Path.GetFileNameWithoutExtension(entry.Name);
                zipEntryByAssetName[assetName] = entry.FullName;
            }

            if (zipEntryByAssetName.Count == 0)
            {
                throw new InvalidOperationException(
                    $"'{zipPath}' has no .bundle entries. Expected per-region map_bg_*_base.bundle files.");
            }
        }

        private static bool TryLoadRecipeShader()
        {
            if (MapRecipe.HasShader)
            {
                return true;
            }

            const string recipeKey = "DetailedMaps.Recipe";
            if (packZipPath == null || !zipEntryByAssetName.ContainsKey(recipeKey))
            {
                return false;
            }

            try
            {
                byte[] bytes = ReadZipEntryBytes(recipeKey);
                recipeBundle = Unity6AssetBundleLoader.LoadFromMemory(bytes);
                if (recipeBundle == null)
                {
                    MelonLogger.Error("Failed to LoadFromMemory recipe bundle.");
                    return false;
                }

                roots.Add(recipeBundle);
                string[] names = ToManagedStrings(recipeBundle.GetAllAssetNames());
                Shader? shader = null;
                foreach (string name in names)
                {
                    UnityEngine.Object obj = recipeBundle.LoadAsset(name);
                    shader = obj.TryCast<Shader>();
                    if (shader != null)
                    {
                        break;
                    }
                }

                if (shader == null)
                {
                    MelonLogger.Error($"No Shader in recipe bundle. Assets: {string.Join(", ", names)}");
                    return false;
                }

                MapRecipe.SetRecipeShader(shader);
                roots.Add(shader);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Recipe shader load failed: {ex}");
                return false;
            }
        }

        private static Texture2D LoadTextureFromPackBundle(string assetName)
        {
            if (!loadedBundles.TryGetValue(assetName, out AssetBundle? bundle) || bundle == null)
            {
                byte[] bytes = ReadZipEntryBytes(assetName);
                Stopwatch openSw = Stopwatch.StartNew();
                bundle = Unity6AssetBundleLoader.LoadFromMemory(bytes);
                openSw.Stop();
                if (bundle == null)
                {
                    throw new InvalidOperationException(
                        $"LoadFromMemory null for '{assetName}' ({bytes.Length} bytes).");
                }

                loadedBundles[assetName] = bundle;
                roots.Add(bundle);
                MelonLogger.Msg(
                    $"Opened pack bundle '{assetName}' ({bytes.Length / 1024} KB) in {openSw.ElapsedMilliseconds} ms.");
            }

            string[] assetPaths = ToManagedStrings(bundle.GetAllAssetNames());
            if (assetPaths.Length == 0)
            {
                throw new InvalidOperationException($"Bundle '{assetName}' contains no assets.");
            }

            string loadPath = assetPaths[0];
            foreach (string p in assetPaths)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(p), assetName, StringComparison.OrdinalIgnoreCase))
                {
                    loadPath = p;
                    break;
                }
            }

            Stopwatch sw = Stopwatch.StartNew();
            UnityEngine.Object obj = bundle.LoadAsset(loadPath);
            Texture2D? tex = obj == null ? null : obj.TryCast<Texture2D>();
            sw.Stop();
            if (tex == null)
            {
                throw new InvalidOperationException($"LoadAsset did not return Texture2D for '{assetName}'.");
            }

            tex.hideFlags = HideFlags.HideAndDontSave;
            MelonLogger.Msg(
                $"Loaded '{assetName}' ({tex.width}x{tex.height}, {tex.format}) in {sw.ElapsedMilliseconds} ms.");
            return tex;
        }

        private static void UnloadPackBundle(string assetName)
        {
            if (loadedBundles.TryGetValue(assetName, out AssetBundle? bundle) && bundle != null)
            {
                loadedBundles.Remove(assetName);
                roots.Remove(bundle);
                bundle.Unload(true);
                MelonLogger.Msg($"Unloaded pack bundle '{assetName}'.");
            }
        }

        private static byte[] ReadZipEntryBytes(string assetName)
        {
            if (packZipPath == null || !zipEntryByAssetName.TryGetValue(assetName, out string? entryFullName))
            {
                throw new FileNotFoundException($"Pack entry not indexed: '{assetName}'");
            }

            using ZipArchive zip = ZipFile.OpenRead(packZipPath);
            ZipArchiveEntry? entry = zip.GetEntry(entryFullName);
            if (entry == null)
            {
                throw new FileNotFoundException($"Zip entry missing: {entryFullName} in {packZipPath}");
            }

            using Stream stream = entry.Open();
            using MemoryStream ms = new MemoryStream(entry.Length > 0 ? (int)entry.Length : 0);
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static string ResolvePackDirectory()
        {
            string? asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return !string.IsNullOrEmpty(asmDir)
                ? asmDir
                : MelonLoader.Utils.MelonEnvironment.ModsDirectory;
        }

        private static string? FindPack(string fileName)
        {
            foreach (string dir in GetPackSearchDirs())
            {
                string path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetPackSearchDirs()
        {
            if (!string.IsNullOrEmpty(packDirectory))
            {
                yield return packDirectory;
                yield return Path.Combine(packDirectory, "DetailedMaps");
            }

            string modsDir = MelonLoader.Utils.MelonEnvironment.ModsDirectory;
            if (!string.IsNullOrEmpty(modsDir) &&
                !string.Equals(modsDir, packDirectory, StringComparison.OrdinalIgnoreCase))
            {
                yield return modsDir;
                yield return Path.Combine(modsDir, "DetailedMaps");
            }
        }

        private static string[] ToManagedStrings(Il2CppStringArray array)
        {
            string[] result = new string[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                result[i] = array[i];
            }

            return result;
        }
    }
}
