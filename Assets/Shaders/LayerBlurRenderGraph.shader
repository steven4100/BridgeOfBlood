Shader "LayerBlurRenderGraph"
{
    Properties
    {
        _BlurRadius ("Blur Radius", Range(0.25, 4)) = 1.5
        _CelAlphaThreshold ("Cel Alpha Threshold", Range(0, 1)) = 0.35
        _CelShadeSteps ("Cel Shade Steps", Range(2, 8)) = 4
        _OverlayColor ("Overlay Color", Color) = (0.55, 0.02, 0.02, 1)
        _OverlayDepth ("Overlay Depth", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "HorizontalBlur"
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                float2 texel = float2(_BlitTexture_TexelSize.x * _BlurRadius, 0.0);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord) * 0.4h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + texel) * 0.3h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - texel) * 0.3h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "VerticalBlur"
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                float2 texel = float2(0.0, _BlitTexture_TexelSize.y * _BlurRadius);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord) * 0.4h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord + texel) * 0.3h;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord - texel) * 0.3h;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "FinalOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertQuad
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            TEXTURE2D_X(_BlurTex);

            CBUFFER_START(UnityPerMaterial)
                float _CelAlphaThreshold;
                float _CelShadeSteps;
                half4 _OverlayColor;
                float _OverlayDepth;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings VertQuad(Attributes input)
            {
                Varyings output;
                output.uv = input.uv;

                float2 ndcXY = input.positionOS.xy;
                float overlay01 = saturate(_OverlayDepth);

                if (IsPerspectiveProjection())
                {
                    // Linear01Depth: 0 = near, 1 = far. Overlay slider is the inverse (0 = far, 1 = near).
                    float linear01Depth = 1.0 - overlay01;
                    float3 viewRay = mul(unity_CameraInvProjection, float4(ndcXY, 1.0, 1.0) * _ProjectionParams.z).xyz;
                    float3 viewPos = viewRay * linear01Depth;
                    output.positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
                }
                else
                {
                    float nearPlane = _ProjectionParams.y;
                    float farPlane = _ProjectionParams.z;
                    float viewZ = lerp(farPlane, nearPlane, overlay01);
                    float3 viewPos = float3(ndcXY.x / unity_OrthoParams.x, ndcXY.y / unity_OrthoParams.y, viewZ);
                    output.positionCS = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
                }

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = float2(input.uv.x, input.uv.y);
                half4 color = SAMPLE_TEXTURE2D_X(_BlurTex, sampler_LinearClamp, uv);

                if (color.r >= _CelAlphaThreshold)
                    return half4(_OverlayColor.rgb, 1.0h);

                return half4(0.0h, 0.0h, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
}
