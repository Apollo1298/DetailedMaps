using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DetailedMaps
{
    internal class Patches
    {
        /// <summary>
        /// TLD 2.55: private bool MaybeLoadTextureAsset(string filename, ref Texture2D texture)
        /// Replace via the ref parameter so OnEnable picks up our texture.
        /// </summary>
        [HarmonyPatch(typeof(FogOfWar), nameof(FogOfWar.MaybeLoadTextureAsset))]
        internal class FogOfWar_MaybeLoadTextureAsset
        {
            private static bool Prefix(string filename, ref Texture2D texture)
            {
                if (string.IsNullOrEmpty(filename))
                {
                    return true;
                }

                if (Implementation.TryGetOrLoadBackground(filename, out Texture2D replacement))
                {
                    texture = replacement;
                    MelonLogger.Msg($"Replaced background '{filename}' with selected map style.");
                    return false;
                }

                // Any details atlas → blank overlay so charcoal details don't fight the new map art
                if (filename.StartsWith("map_details", System.StringComparison.OrdinalIgnoreCase) &&
                    Implementation.TryGetOrLoadTexture("map_details_blank", out Texture2D blank))
                {
                    texture = blank;
                    MelonLogger.Msg($"Replaced details '{filename}' → 'map_details_blank'");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// After FogOfWar finishes enabling, force our textures onto the fields and request a redraw.
        /// </summary>
        [HarmonyPatch(typeof(FogOfWar), "OnEnable")]
        internal class FogOfWar_OnEnable
        {
            private static void Postfix(FogOfWar __instance)
            {
                try
                {
                    string bgFile = __instance.m_MapBackgroundFilename;
                    if (!string.IsNullOrEmpty(bgFile) &&
                        Implementation.TryGetOrLoadBackground(bgFile, out Texture2D bg))
                    {
                        __instance.m_MapBackground = bg;
                    }

                    if (Implementation.TryGetOrLoadTexture("map_details_blank", out Texture2D details))
                    {
                        __instance.m_MapDetails = details;
                    }

                    __instance.m_Redraw = true;
                    MelonLogger.Msg($"OnEnable applied maps for '{bgFile}', redraw requested.");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"FogOfWar.OnEnable postfix failed: {ex}");
                }
            }
        }
    }
}
