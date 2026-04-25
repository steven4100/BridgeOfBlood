Shader "BridgeOfBlood/InstancedSprite"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct InstanceData
            {
                float4 positionScale; // xyz = position, w = scale (matches C# interop; avoids float3 padding mismatch)
                float4 uvRect; // (xMin, yMin, xMax, yMax)
                float4 color; // rgb = additive ailment tint; default (0,0,0,0); a unused
            };

            StructuredBuffer<InstanceData> _InstanceData;

            float4x4 _LocalToWorld;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                // Per-instance additive overlay (same for all verts).
                nointerpolation float4 additive : COLOR0;
            };

            Varyings vert(Attributes v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                InstanceData data = _InstanceData[instanceID];

                float3 localPos = float3(
                    data.positionScale.xy + v.positionOS.xy * data.positionScale.w,
                    data.positionScale.z);
                float3 worldPos = mul(_LocalToWorld, float4(localPos, 1.0)).xyz;

                Varyings o;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.uv = lerp(data.uvRect.xy, data.uvRect.zw, v.uv);
                o.additive = data.color;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(col.a - 0.01);
                float3 rgb = saturate(col.rgb + i.additive.rgb);
                return float4(rgb, col.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
