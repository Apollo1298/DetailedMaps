using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DetailedMaps
{
    internal class Patches
    {
        [HarmonyPatch(typeof(FogOfWar), nameof(FogOfWar.MaybeLoadTextureAsset))]
        internal class FogOfWar_MaybeLoadTextureAsset
        {
            private static bool Prefix(FogOfWar __instance, string filename, ref Texture2D texture)
            {
                if (string.IsNullOrEmpty(filename))
                {
                    return true;
                }

                if (Implementation.TryGetOrBakeBackground(filename, __instance.m_SceneRadius, out Texture2D replacement))
                {
                    texture = replacement;
                    return false;
                }

                if (filename.StartsWith("map_details", System.StringComparison.OrdinalIgnoreCase) &&
                    Implementation.TryGetBlankDetails(out Texture2D blank))
                {
                    texture = blank;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(FogOfWar), "OnEnable")]
        internal class FogOfWar_OnEnable
        {
            private static void Postfix(FogOfWar __instance)
            {
                try
                {
                    bool changed = false;
                    string bgFile = __instance.m_MapBackgroundFilename;

                    if (!string.IsNullOrEmpty(bgFile) &&
                        Implementation.TryGetOrBakeBackground(bgFile, __instance.m_SceneRadius, out Texture2D bg) &&
                        !ReferenceEquals(__instance.m_MapBackground, bg))
                    {
                        __instance.m_MapBackground = bg;
                        changed = true;
                    }

                    if (Implementation.TryGetBlankDetails(out Texture2D details) &&
                        !ReferenceEquals(__instance.m_MapDetails, details))
                    {
                        __instance.m_MapDetails = details;
                        changed = true;
                    }

                    if (changed)
                    {
                        __instance.m_Redraw = true;
                        MelonLogger.Msg(
                            $"OnEnable applied maps for '{bgFile}' sceneR={__instance.m_SceneRadius:F1}, redraw requested.");
                    }
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Error($"FogOfWar.OnEnable postfix failed: {ex}");
                }
            }
        }
    }
}
