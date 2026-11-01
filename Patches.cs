using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DetailedMaps
{
    internal class Patches
    {
        // LoadTextureAsset is private; Harmony resolves by name string.
        [HarmonyPatch(typeof(FogOfWar), "LoadTextureAsset")]
        internal class FogOfWar_LoadTextureAsset
        {
            private static bool Prefix(FogOfWar __instance, string filename, ref AsyncOperationHandle<Texture2D> __result)
            {
                if (string.IsNullOrEmpty(filename))
                {
                    return true;
                }

                if (Implementation.TryGetOrBakeBackground(filename, __instance.m_SceneRadius, out Texture2D replacement))
                {
                    __result = Implementation.CreateCompletedTextureHandle(replacement);
                    return false;
                }

                if (filename.StartsWith("map_details", System.StringComparison.OrdinalIgnoreCase) &&
                    Implementation.TryGetBlankDetails(out Texture2D blank))
                {
                    __result = Implementation.CreateCompletedTextureHandle(blank);
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
                        Implementation.TryGetOrBakeBackground(bgFile, __instance.m_SceneRadius, out Texture2D bg))
                    {
                        __instance.m_MapBackgroundHandle = Implementation.CreateCompletedTextureHandle(bg);
                        changed = true;
                    }

                    if (Implementation.TryGetBlankDetails(out Texture2D details))
                    {
                        __instance.m_MapDetailsHandle = Implementation.CreateCompletedTextureHandle(details);
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
