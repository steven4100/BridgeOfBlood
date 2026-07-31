Shader "Hidden/BridgeOfBlood/MeshNormalCapture"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "MeshNormalCapture"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _FlipY;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Camera/view-space normals = tangent space of a camera-facing quad/sprite.
                float3 normalVS = TransformWorldToViewDir(normalize(input.normalWS), true);
                if (_FlipY > 0.5)
                    normalVS.y = -normalVS.y;

                float3 encoded = normalVS * 0.5 + 0.5;
                return half4(encoded, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
