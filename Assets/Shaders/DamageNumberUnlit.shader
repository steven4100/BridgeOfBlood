Shader "BridgeOfBlood/DamageNumberUnlit"
{
    Properties
    {
        _DigitAtlas ("Digit Atlas", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite Off
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
                float2 localPos;
                float  opacity;
                uint   packedDigits;
                int    digitCount;
            };

            StructuredBuffer<InstanceData> _InstanceData;

            float4x4 _LocalToWorld;
            float _DigitScale;

            TEXTURE2D(_DigitAtlas);
            SAMPLER(sampler_DigitAtlas);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                nointerpolation float  opacity      : TEXCOORD1;
                nointerpolation uint   packedDigits : TEXCOORD2;
                nointerpolation int    digitCount   : TEXCOORD3;
            };

            Varyings vert(Attributes v, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                uint instanceID = GetIndirectInstanceID(svInstanceID);

                InstanceData data = _InstanceData[instanceID];

                // Stretch the quad horizontally by digitCount, keep height = _DigitScale
                float width = _DigitScale * (float)data.digitCount;
                float height = _DigitScale;

                // v.positionOS is a unit quad (-0.5 to 0.5); scale and offset so it sits above the position
                float3 localPos = float3(
                    data.localPos.x + v.positionOS.x * width,
                    data.localPos.y + v.positionOS.y * height + height,
                    0.0
                );
                float3 worldPos = mul(_LocalToWorld, float4(localPos, 1.0)).xyz;

                Varyings o;
                o.positionCS = TransformWorldToHClip(worldPos);
                o.uv = v.uv;
                o.opacity = data.opacity;
                o.packedDigits = data.packedDigits;
                o.digitCount = data.digitCount;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                int digitCount = i.digitCount;
                float columnF = i.uv.x * (float)digitCount;
                int column = clamp((int)floor(columnF), 0, digitCount - 1);

                // Extract digit from packed nibbles (nibble 0 = most significant)
                uint digit = (i.packedDigits >> ((uint)column * 4u)) & 0xFu;

                // Atlas UV: 10 cells horizontally, digit selects cell, frac gives position within cell
                float fracInCell = frac(columnF);
                float atlasU = ((float)digit + fracInCell) * 0.1;
                float atlasV = i.uv.y;

                float4 texColor = SAMPLE_TEXTURE2D(_DigitAtlas, sampler_DigitAtlas, float2(atlasU, atlasV));
                texColor.a *= i.opacity;

                clip(texColor.a - 0.01);
                return texColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
