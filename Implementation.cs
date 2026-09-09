using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Il2CppObjectList = Il2CppSystem.Collections.Generic.List<UnityEngine.Object>;

namespace DetailedMaps
{
    public class Implementation : MelonMod
    {
        private const string CharcoalZipFileName = "DetailedMaps.Charcoal.zip";
        private const string ColorZipFileName = "DetailedMaps.Color.zip";
        private const string BlankDetailsName = "map_details_blank";

        private static readonly Dictionary<string, Texture2D> textures = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> entryByAssetName = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> zipByAssetName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Vanilla FogOfWar background filenames that do not match scene / pack asset names.
        /// </summary>
        private static readonly Dictionary<string, string> backgroundNameAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["map_bg_AshCanyon"] = "map_bg_AshCanyonRegion",
            ["map_bg_MountainTownRegionSandbox"] = "map_bg_MountainTownRegion",
            ["map_bg_DamRiverTransitionZone"] = "map_bg_DamRiverTransitionZoneB",
        };

        // Managed Dictionary does NOT keep Il2Cpp objects alive. This list does.
        private static readonly Il2CppObjectList textureRoots = new();

        private static string? packDirectory;
        private static string? charcoalZipPath;
        private static string? colorZipPath;
        private static string? currentRegionMapName;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg($"Version {Info.Version} loaded!");

            try
            {
                Settings.Instance.AddToModSettings("Detailed Maps");
                IndexMapPacks();

                if (!TryGetOrLoadTexture(BlankDetailsName, out _))
                {
                    MelonLogger.Warning($"Optional '{BlankDetailsName}' not found in charcoal pack; charcoal details overlay will not be blanked.");
                }

                MelonLogger.Msg($"{entryByAssetName.Count} map entries indexed (on-demand load from zip packs).");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to index map packs: {ex}");
            }
        }

        internal static void OnMapStyleChanged()
        {
            string style = Settings.Instance.MapStyle == 1 ? "Color" : "Charcoal";
            MelonLogger.Msg($"Map style set to {style}.");

            if (string.IsNullOrEmpty(currentRegionMapName))
            {
                return;
            }

            UnloadRegionMap(currentRegionMapName);
            currentRegionMapName = null;
        }

        /// <summary>
        /// Loads the background for a vanilla FogOfWar filename using the selected map style.
        /// </summary>
        internal static bool TryGetOrLoadBackground(string vanillaFilename, out Texture2D texture)
        {
            texture = null!;
            if (string.IsNullOrEmpty(vanillaFilename))
            {
                return false;
            }

            if (backgroundNameAliases.TryGetValue(vanillaFilename, out string? packBaseName))
            {
                vanillaFilename = packBaseName;
            }

            string suffix = Settings.Instance.MapStyle == 1 ? "_color" : "_new";
            return TryGetOrLoadTexture(vanillaFilename + suffix, out texture);
        }

        /// <summary>
        /// Returns a cached texture or loads it from a zip pack entry.
        /// Region backgrounds unload the previous region map to limit VRAM.
        /// </summary>
        internal static bool TryGetOrLoadTexture(string name, out Texture2D texture)
        {
            texture = null!;

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (textures.TryGetValue(name, out Texture2D? cached) && cached != null)
            {
                texture = cached;
                return true;
            }

            if (!entryByAssetName.TryGetValue(name, out string? entryName) ||
                !zipByAssetName.TryGetValue(name, out string? zipPath))
            {
                if (name.EndsWith("_color", StringComparison.OrdinalIgnoreCase) && colorZipPath == null)
                {
                    MelonLogger.Warning($"Color map '{name}' requested but '{ColorZipFileName}' was not found next to the DLL.");
                }

                return false;
            }

            try
            {
                Texture2D loaded = LoadPngFromZip(zipPath, entryName, name);
                RegisterTexture(name, loaded);

                if (IsRegionBackground(name))
                {
                    UnloadPreviousRegionMap(name);
                    currentRegionMapName = name;
                }

                texture = loaded;
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to load map texture '{name}': {ex}");
                return false;
            }
        }

        private static void IndexMapPacks()
        {
            entryByAssetName.Clear();
            zipByAssetName.Clear();

            packDirectory = ResolvePackDirectory();
            charcoalZipPath = FindPack(CharcoalZipFileName);
            colorZipPath = FindPack(ColorZipFileName);

            if (charcoalZipPath == null)
            {
                throw new FileNotFoundException(
                    $"Required map pack '{CharcoalZipFileName}' not found next to the DLL or in Mods/DetailedMaps/.");
            }

            IndexZip(charcoalZipPath);
            MelonLogger.Msg($"Charcoal pack: {charcoalZipPath}");

            if (colorZipPath != null)
            {
                IndexZip(colorZipPath);
                MelonLogger.Msg($"Color pack: {colorZipPath}");
            }
            else
            {
                MelonLogger.Msg($"Color pack '{ColorZipFileName}' not found (Color style unavailable until installed).");
            }
        }

        private static string ResolvePackDirectory()
        {
            string? asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(asmDir))
            {
                return asmDir;
            }

            return MelonLoader.Utils.MelonEnvironment.ModsDirectory;
        }

        private static string? FindPack(string fileName)
        {
            if (string.IsNullOrEmpty(packDirectory))
            {
                return null;
            }

            string besideDll = Path.Combine(packDirectory, fileName);
            if (File.Exists(besideDll))
            {
                return besideDll;
            }

            string subfolder = Path.Combine(packDirectory, "DetailedMaps", fileName);
            if (File.Exists(subfolder))
            {
                return subfolder;
            }

            string modsDir = MelonLoader.Utils.MelonEnvironment.ModsDirectory;
            string inMods = Path.Combine(modsDir, fileName);
            if (File.Exists(inMods))
            {
                return inMods;
            }

            string inModsSub = Path.Combine(modsDir, "DetailedMaps", fileName);
            if (File.Exists(inModsSub))
            {
                return inModsSub;
            }

            return null;
        }

        private static void IndexZip(string zipPath)
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) ||
                    !entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Prefer the file name only so nested zip paths still work.
                string assetName = Path.GetFileNameWithoutExtension(entry.Name);
                entryByAssetName[assetName] = entry.FullName;
                zipByAssetName[assetName] = zipPath;
            }
        }

        private static Texture2D LoadPngFromZip(string zipPath, string entryFullName, string assetName)
        {
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry? entry = zip.GetEntry(entryFullName);
            if (entry == null)
            {
                throw new FileNotFoundException($"Zip entry missing: {entryFullName} in {zipPath}");
            }

            byte[] pngBytes;
            using (Stream stream = entry.Open())
            using (MemoryStream ms = new MemoryStream((int)entry.Length))
            {
                stream.CopyTo(ms);
                pngBytes = ms.ToArray();
            }

            Stopwatch sw = Stopwatch.StartNew();
            Texture2D tex = Unity6ImageLoader.LoadPng(pngBytes, assetName);
            sw.Stop();

            tex.hideFlags = HideFlags.HideAndDontSave;
            MelonLogger.Msg($"Loaded map texture '{assetName}' ({tex.width}x{tex.height}) in {sw.ElapsedMilliseconds} ms.");
            return tex;
        }

        private static void RegisterTexture(string name, Texture2D tex)
        {
            textures[name] = tex;
            textureRoots.Add(tex);
        }

        private static bool IsRegionBackground(string name)
        {
            return name.StartsWith("map_bg_", StringComparison.OrdinalIgnoreCase);
        }

        private static void UnloadPreviousRegionMap(string newName)
        {
            if (string.IsNullOrEmpty(currentRegionMapName) ||
                string.Equals(currentRegionMapName, newName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UnloadRegionMap(currentRegionMapName);
        }

        private static void UnloadRegionMap(string name)
        {
            if (!textures.TryGetValue(name, out Texture2D? oldTex) || oldTex == null)
            {
                textures.Remove(name);
                return;
            }

            textures.Remove(name);
            textureRoots.Remove(oldTex);
            UnityEngine.Object.Destroy(oldTex);
            MelonLogger.Msg($"Unloaded previous map texture '{name}'.");
        }
    }
}
