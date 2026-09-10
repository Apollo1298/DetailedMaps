using System.Diagnostics;
using MelonLoader;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DetailedMaps
{
    /// <summary>
    /// GPU recipe: desat/chroma + grid/noise + always-on vignette on BC7 base → RGBA32 bake.
    /// </summary>
    internal static class MapRecipe
    {
        private static Material? recipeMaterial;

        public static float LastSceneRadius { get; private set; }

        public static void SetRecipeShader(Shader? shader)
        {
            if (recipeMaterial != null)
            {
                Object.Destroy(recipeMaterial);
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
                throw new System.InvalidOperationException(
                    "MapRecipe shader not loaded. Ensure DetailedMaps.Recipe.bundle is inside DetailedMaps.zip.");
            }

            Stopwatch sw = Stopwatch.StartNew();
            int w = baseTex.width;
            int h = baseTex.height;
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

            Material mat = recipeMaterial;
            mat.SetFloat("_ChromaKeep", charcoal ? 0f : RecipeConstants.ColorChromaKeep);
            mat.SetFloat("_GridOn", settings.Grid ? 1f : 0f);
            mat.SetFloat("_MapExtent", mapExtent);
            mat.SetFloat("_GridSpacingMap", gridSpacingMap);
            mat.SetFloat("_GridWidthPx", RecipeConstants.GridWidthPx);
            mat.SetFloat("_Vignette", RecipeConstants.VignetteStrength);
            mat.SetFloat("_NoiseOn", settings.Noise ? 1f : 0f);
            mat.SetFloat("_StippleDensity", RecipeConstants.StippleDensity);
            mat.SetFloat("_StippleStrength", RecipeConstants.StippleStrength);
            mat.SetFloat("_GrainSigma", RecipeConstants.GrainSigma);
            mat.SetVector("_MapSize", new Vector4(w, h, 0f, 0f));

            string styleTag = charcoal ? "charcoal" : "color";
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, GraphicsFormat.R8G8B8A8_UNorm);
            rt.filterMode = FilterMode.Bilinear;
            try
            {
                Graphics.Blit(baseTex, rt, mat);

                var baked = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
                {
                    name = $"{baseTex.name}_{styleTag}_rgba",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                baked.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                baked.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                RenderTexture.active = prev;

                sw.Stop();
                MelonLogger.Msg(
                    $"GPU recipe bake {w}x{h} style={styleTag} " +
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
    }
}
