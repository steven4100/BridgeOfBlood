Shader "BridgeOfBlood/EnemyIndirectUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (0.9, 0.2, 0.2, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct InstanceData
            {
                float2 localPos;
            };

            StructuredBuffer<InstanceData> _InstanceData;

            float4x4 _LocalToWorld;
            float _InstanceScale;
            float4 _Color;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                InstanceData data = _InstanceData[instanceID];

                float3 localPos = float3(data.localPos + v.positionOS.xy * _InstanceScale, 0.0);
                float3 worldPos = mul(_LocalToWorld, float4(localPos, 1.0)).xyz;

                Varyings o;
                o.positionCS = TransformWorldToHClip(worldPos);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
