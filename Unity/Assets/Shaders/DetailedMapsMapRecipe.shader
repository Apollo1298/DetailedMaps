Shader "Hidden/DetailedMaps/MapRecipe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ChromaKeep ("Chroma Keep", Float) = 0
        _GridOn ("Grid On", Float) = 0
        _MapExtent ("Map Extent", Float) = 300
        _GridSpacingMap ("Grid Spacing Map", Float) = 100
        _GridWidthPx ("Grid Width Px", Float) = 1.5
        _Vignette ("Vignette", Float) = 0.2
        _NoiseOn ("Noise On", Float) = 0
        _MapSize ("Map Size", Vector) = (4096, 4096, 0, 0)
        _StippleDensity ("Stipple Density", Float) = 0.012
        _StippleStrength ("Stipple Strength", Float) = 55
        _GrainSigma ("Grain Sigma", Float) = 36
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _ChromaKeep;
            float _GridOn;
            float _MapExtent;
            float _GridSpacingMap;
            float _GridWidthPx;
            float _Vignette;
            float _NoiseOn;
            float4 _MapSize;
            float _StippleDensity;
            float _StippleStrength;
            float _GrainSigma;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Better 2D hash (Inigo Quilez-style) — avoids lattice / diagonal streaks.
            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Hash21S(float2 p, float salt)
            {
                return Hash21(p + salt * 17.13);
            }

            // Approx gaussian from two uniforms (Box-Muller lite).
            float GrainSample(float2 p)
            {
                float u1 = max(Hash21S(p, 1.1), 1e-4);
                float u2 = Hash21S(p, 2.7);
                return sqrt(-2.0 * log(u1)) * cos(6.2831853 * u2);
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 rgb = c.rgb;
                float a = c.a;

                // Levels + color balance are prebaked into map_bg_*_base.
                // Live: desat/chroma → grid → stipple → vignette → grain.

                float gray = dot(rgb, float3(0.299, 0.587, 0.114));
                float keep = saturate(_ChromaKeep);
                rgb = gray.xxx + keep * (rgb - gray.xxx);

                if (_GridOn > 0.5 && a > 0.001)
                {
                    float mx = lerp(-_MapExtent, _MapExtent, i.uv.x);
                    float my = lerp(-_MapExtent, _MapExtent, i.uv.y);
                    float spacing = max(_GridSpacingMap, 1e-3);
                    float dmx = abs(mx - round(mx / spacing) * spacing);
                    float dmy = abs(my - round(my / spacing) * spacing);
                    float mppX = (2.0 * _MapExtent) * _MainTex_TexelSize.x;
                    float mppY = (2.0 * _MapExtent) * _MainTex_TexelSize.y;
                    float distPx = min(dmx / max(mppX, 1e-6), dmy / max(mppY, 1e-6));
                    float halfW = 0.5 * _GridWidthPx;
                    float cov = saturate(halfW + 0.5 - distPx);
                    rgb *= (1.0 - cov);
                }

                // Use explicit _MapSize — do not trust _MainTex_TexelSize on Blit (coarse noise if wrong).
                float2 mapPx = max(_MapSize.xy, float2(1.0, 1.0));
                float2 pix = floor(i.uv * mapPx);

                if (_NoiseOn > 0.5 && a > 0.001)
                {
                    float h = Hash21S(pix, 99.0);
                    if (h < _StippleDensity)
                    {
                        float amt = (0.4 + Hash21S(pix, 12.0) * 0.6) * (_StippleStrength / 255.0);
                        rgb = saturate(rgb - amt);
                    }
                }

                if (_Vignette > 0.0 && a > 0.001)
                {
                    float2 d = (i.uv - 0.5) * 2.0;
                    float r = length(d);
                    r = saturate(r / 1.35);
                    float fall = r * r * _Vignette;
                    rgb = saturate(rgb - fall);
                }

                if (_NoiseOn > 0.5 && a > 0.001)
                {
                    float grain = GrainSample(pix + 0.5) * (_GrainSigma / 255.0);
                    rgb = saturate(rgb + grain);
                }

                return float4(rgb, a);
            }
            ENDCG
        }
    }
    FallBack Off
}
