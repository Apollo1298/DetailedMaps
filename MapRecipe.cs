using System;
using System.Diagnostics;
using MelonLoader;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DetailedMaps
{
    /// <summary>
    /// GPU recipe: desat/chroma + grid/noise + always-on vignette on BC7 base → RGBA32 display texture.
    /// </summary>
    internal static class MapRecipe
    {
        private static Material? recipeMaterial;

        public static float LastSceneRadius { get; private set; }

        public static void SetRecipeShader(Shader? shader)
        {
            if (recipeMaterial != null)
            {
                UnityEngine.Object.Destroy(recipeMaterial);
                recipeMaterial = null;
            }

            if (shader == null)
            {
                MelonLogger.Error("MapRecipe shader is null.");
                return;
            }

            recipeMaterial = new Material(shader)
            {
                name = "DetailedMaps.MapRecipe",
                hideFlags = HideFlags.HideAndDontSave
            };
            MelonLogger.Msg($"MapRecipe material ready (shader='{shader.name}').");
        }

        public static bool HasShader => recipeMaterial != null;

        public static Texture2D Bake(Texture2D baseTex, Settings settings, float sceneRadius)
        {
            if (recipeMaterial == null)
            {
                throw new InvalidOperationException(
                    "MapRecipe shader not loaded. Ensure DetailedMaps.Recipe.bundle is inside DetailedMaps.zip.");
            }

            Stopwatch sw = Stopwatch.StartNew();
            int srcW = baseTex.width;
            int srcH = baseTex.height;
            ResolveOutputSize(srcW, srcH, RecipeConstants.DisplayMaxEdge, out int outW, out int outH);

            bool charcoal = settings.MapStyle == 0;
            LastSceneRadius = sceneRadius;

            float mapExtent = RecipeConstants.MapExtent;
            float gridSpacingMap = RecipeConstants.GridMeters;
            if (sceneRadius > 1f)
            {
                float mapPerMeter = mapExtent * 1.41421356f / sceneRadius;
                gridSpacingMap = RecipeConstants.GridMeters * mapPerMeter;
            }
            else
            {
                MelonLogger.Warning(
                    $"sceneRadius={sceneRadius} invalid; grid using identity spacing ({RecipeConstants.GridMeters} map units).");
            }

            // Grid width is authored in source texels; scale so line thickness stays similar on smaller outputs.
            float gridWidthPx = RecipeConstants.GridWidthPx;
            if (outW > 0 && srcW > outW)
            {
                gridWidthPx *= srcW / (float)outW;
            }

            Material mat = recipeMaterial;
            mat.SetFloat("_ChromaKeep", charcoal ? 0f : RecipeConstants.ColorChromaKeep);
            mat.SetFloat("_GridOn", settings.Grid ? 1f : 0f);
            mat.SetFloat("_MapExtent", mapExtent);
            mat.SetFloat("_GridSpacingMap", gridSpacingMap);
            mat.SetFloat("_GridWidthPx", gridWidthPx);
            mat.SetFloat("_Vignette", RecipeConstants.VignetteStrength);
            mat.SetFloat("_NoiseOn", settings.Noise ? 1f : 0f);
            mat.SetFloat("_StippleDensity", RecipeConstants.StippleDensity);
            mat.SetFloat("_StippleStrength", RecipeConstants.StippleStrength);
            mat.SetFloat("_GrainSigma", RecipeConstants.GrainSigma);
            mat.SetVector("_MapSize", new Vector4(outW, outH, 0f, 0f));

            string styleTag = charcoal ? "charcoal" : "color";
            RenderTexture rt = RenderTexture.GetTemporary(outW, outH, 0, GraphicsFormat.R8G8B8A8_UNorm);
            rt.filterMode = FilterMode.Bilinear;
            try
            {
                Graphics.Blit(baseTex, rt, mat);

                var baked = new Texture2D(outW, outH, TextureFormat.RGBA32, mipChain: false)
                {
                    name = $"{baseTex.name}_{styleTag}_rgba",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                // Prefer GPU copy when supported (avoids ReadPixels round-trip hitch).
                if (!TryCopyRtToTexture(rt, baked))
                {
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    baked.ReadPixels(new Rect(0, 0, outW, outH), 0, 0);
                    RenderTexture.active = prev;
                    baked.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                }

                sw.Stop();
                MelonLogger.Msg(
                    $"GPU recipe {srcW}x{srcH}→{outW}x{outH} style={styleTag} " +
                    $"grid={settings.Grid} spacingMap={gridSpacingMap:F2} (100m, sceneR={sceneRadius:F1}) " +
                    $"noise={settings.Noise} vignette=on " +
                    $"format={baked.format} in {sw.ElapsedMilliseconds} ms.");
                return baked;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        internal static void ResolveOutputSize(int srcW, int srcH, int maxEdge, out int outW, out int outH)
        {
            if (srcW <= 0 || srcH <= 0)
            {
                outW = Math.Max(1, maxEdge);
                outH = Math.Max(1, maxEdge);
                return;
            }

            int longest = Math.Max(srcW, srcH);
            if (longest <= maxEdge)
            {
                outW = srcW;
                outH = srcH;
                return;
            }

            float scale = maxEdge / (float)longest;
            outW = Math.Max(1, Mathf.RoundToInt(srcW * scale));
            outH = Math.Max(1, Mathf.RoundToInt(srcH * scale));
        }

        private static bool TryCopyRtToTexture(RenderTexture rt, Texture2D dest)
        {
            try
            {
                if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
                {
                    return false;
                }

                // Allocate GPU storage without pulling pixels back to the CPU.
                dest.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                Graphics.CopyTexture(rt, dest);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"CopyTexture failed, falling back to ReadPixels: {ex.Message}");
                return false;
            }
        }
    }
}
