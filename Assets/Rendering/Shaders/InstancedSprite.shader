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
                float3 position;
                float scale;
                float4 uvRect; // (xMin, yMin, xMax, yMax)
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
            };

            Varyings vert(Attributes v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                InstanceData data = _InstanceData[instanceID];

                float3 localPos = float3(data.position.xy + v.positionOS.xy * data.scale, 0.0);
                float3 worldPos = mul(_LocalToWorld, float4(localPos, 1.0)).xyz;

                Varyings o;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.uv = lerp(data.uvRect.xy, data.uvRect.zw, v.uv);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(col.a - 0.01);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
