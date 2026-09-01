Shader "Custom/MobileSimpleWater"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.15, 0.35, 0.45, 0.7)
        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalTiling("Normal Tiling", Vector) = (2, 2, 0, 0)
        _ScrollSpeed("Scroll Speed (XY = Map 1, ZW = Map 2)", Vector) = (0.05, 0.05, -0.03, 0.03)
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                float4 tangentOS    : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD3;
                float3 tangentWS    : TEXCOORD4;
                float3 bitangentWS  : TEXCOORD5;
                float3 positionWS   : TEXCOORD6;
                float2 uv           : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _NormalMap_ST;
                float4 _ScrollSpeed;
                float2 _NormalTiling;
                float _NormalStrength;
                float _Smoothness;
            CBUFFER_END

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Time-based UV scrolling for two normal map layers to create organic waves
                float2 uv1 = input.uv * _NormalTiling + _Time.yy * _ScrollSpeed.xy;
                float2 uv2 = input.uv * _NormalTiling + _Time.yy * _ScrollSpeed.zw;

                // Sample and unpack normal maps
                float3 normal1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1));
                float3 normal2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2));
                
                // Combine normals
                float3 blendedNormalTS = normal1 + normal2;
                blendedNormalTS.xy *= _NormalStrength;
                blendedNormalTS = normalize(blendedNormalTS);

                // Transform normal from Tangent Space to World Space
                float3x3 tangentToWorld = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = normalize(mul(blendedNormalTS, tangentToWorld));

                // Basic Lighting
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight();

                // Specular (reflection highlight)
                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specPower = exp2(10.0 * _Smoothness + 1.0);
                float specular = pow(NdotH, specPower) * _Smoothness;

                // Combine color and specular reflection
                float3 finalColor = _BaseColor.rgb + specular * mainLight.color;
                
                // Fresnel effect for water edge reflectivity
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 4.0);
                float alpha = lerp(_BaseColor.a, 1.0, fresnel * 0.5);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Transparent/VertexLit"
}
