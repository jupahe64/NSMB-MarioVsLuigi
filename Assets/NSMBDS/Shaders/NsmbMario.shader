// Replaces the base project's PlayerShader2, which fails to compile on this Unity version.
Shader "NSMBDS/Mario" {
    Properties {
        _MainTex ("Albedo", 2D) = "white" {}
        _IsEyeTexture ("Is Eye Texture", Float) = 0
        _EyeState ("Eye State", Float) = 0
        _MultiplyColor ("Multiply Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "AlphaTest" }

        Pass {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            static const float EyeFrames = 4.0;
            static const float3 LightDirection = float3(0.3, 0.6, -0.74);
            static const float AmbientLight = 0.8;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _IsEyeTexture;
                float _EyeState;
                float4 _MultiplyColor;
                float _Cutoff;
            CBUFFER_END

            struct Attributes {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 position : SV_POSITION;
                float3 normal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vertex(Attributes input) {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.normal = TransformObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target {
                float2 uv = input.uv;
                if (_IsEyeTexture > 0.5) {
                    uv.y = (frac(uv.y) + (EyeFrames - 1.0 - _EyeState)) / EyeFrames;
                }

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                clip(color.a - _Cutoff);

                float light = AmbientLight + ((1.0 - AmbientLight) * saturate(dot(normalize(input.normal), LightDirection)));
                return float4(color.rgb * _MultiplyColor.rgb * light, 1.0);
            }
            ENDHLSL
        }
    }
}
