Shader "Custom/EnemySDFShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        [Header(Enemy Shape)]
        _Circle0Center("Circle 0 Center", Vector) = (-0.35, 0.15, 0, 0)
        _Circle1Center("Circle 1 Center", Vector) = (0.35, 0.15, 0, 0)
        _Circle0Radius("Circle 0 Radius", Float) = 0.22
        _Circle1Radius("Circle 1 Radius", Float) = 0.22
        _CircleMerge("Circle Merge", Range(0, 0.5)) = 0.08
        [Header(Circle Colors)]
        _Circle0Color("Circle 0 Color", Color) = (1, 0.35, 0.35, 1)
        _Circle1Color("Circle 1 Color", Color) = (0.35, 0.55, 1, 1)
        [Header(Circle Edge Noise)]
        _CircleEdgeNoise("Noise Amount", Range(0, 0.08)) = 0.025
        _CircleNoiseScale("Noise Scale", Float) = 14
        _CircleNoiseEdgeWidth("Edge Band", Range(0.001, 0.2)) = 0.05
        _CircleNoiseSeed("Noise Seed", Float) = 0
        _CircleNoiseScrollRate("Noise Scroll Rate", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _Circle0Center;
                float4 _Circle1Center;
                float _Circle0Radius;
                float _Circle1Radius;
                float _CircleMerge;
                half4 _Circle0Color;
                half4 _Circle1Color;
                float _CircleEdgeNoise;
                float _CircleNoiseScale;
                float _CircleNoiseEdgeWidth;
                float _CircleNoiseSeed;
                float _CircleNoiseScrollRate;
            CBUFFER_END

            float sdCircle(float2 p, float r)
            {
                return length(p) - r;
            }

            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy) * 2.0 - 1.0;
            }

            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float n00 = dot(hash22(i + float2(0.0, 0.0)), f - float2(0.0, 0.0));
                float n10 = dot(hash22(i + float2(1.0, 0.0)), f - float2(1.0, 0.0));
                float n01 = dot(hash22(i + float2(0.0, 1.0)), f - float2(0.0, 1.0));
                float n11 = dot(hash22(i + float2(1.0, 1.0)), f - float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y);
            }

            // Noise is applied once on the merged circle field (world-space sampling) so smin stays valid.
            float applyMergedCircleEdgeNoise(float2 worldP, float d)
            {
                if (_CircleEdgeNoise <= 0.0)
                    return d;

                float2 noiseScroll = float2(1.0, 0.55) * (_CircleNoiseScrollRate * _Time.y);
                float2 noiseP = worldP * _CircleNoiseScale + _CircleNoiseSeed + noiseScroll;
                float n = gradientNoise(noiseP);
                float edgeMask = 1.0 - smoothstep(0.0, _CircleNoiseEdgeWidth, abs(d));
                return d + n * _CircleEdgeNoise * edgeMask;
            }

            // Polynomial smooth union (Inigo Quilez). k = 0 is a hard min; larger k = softer merge.
            float smin(float a, float b, float k)
            {
                if (k <= 0.0)
                    return min(a, b);

                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * k * 0.25;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half3 evalCircleColor(float d0, float d1)
            {
                float blend = _CircleMerge > 0.0
                    ? saturate(0.5 + 0.5 * (d0 - d1) / _CircleMerge)
                    : (d0 < d1 ? 0.0 : 1.0);
                return lerp(_Circle0Color.rgb, _Circle1Color.rgb, blend);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = (IN.uv - 0.5) * 2.0;

                float d0 = sdCircle(p - _Circle0Center.xy, _Circle0Radius);
                float d1 = sdCircle(p - _Circle1Center.xy, _Circle1Radius);
                float d = applyMergedCircleEdgeNoise(p, smin(d0, d1, _CircleMerge));

                float aa = fwidth(d) * 0.5;
                float mask = 1.0 - smoothstep(-aa, aa, d);

                half4 color = half4(evalCircleColor(d0, d1), _BaseColor.a);
                color.a *= half(mask);
                return color;
            }
            ENDHLSL
        }
    }
}
