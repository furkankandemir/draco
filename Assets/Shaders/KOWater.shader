Shader "KO/Water"
{
    Properties
    {
        _MainTex ("Caustic Texture (Stage 0)", 2D) = "white" {}
        _WaveTex ("Wave Texture (Stage 1)", 2D) = "white" {}
        _BaseColor ("Color Tint", Color) = (0.4, 0.6, 0.8, 0.55)
        _UVOffset ("UV Offset (X, Y)", Vector) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 // LEqual
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float2 uv2          : TEXCOORD1;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uv2          : TEXCOORD1;
                float4 color        : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_WaveTex);
            SAMPLER(sampler_WaveTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _WaveTex_ST;
            float4 _BaseColor;
            float4 _UVOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw + _UVOffset.xy;
                output.uv2 = input.uv2 * _WaveTex_ST.xy + _WaveTex_ST.zw + _UVOffset.xy;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 caustic = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 wave = SAMPLE_TEXTURE2D(_WaveTex, sampler_WaveTex, input.uv2);
                
                // Color = Diffuse (vertex color) * Caustic * Wave * Tint * 2.0 (brightness modulate)
                half4 col;
                col.rgb = input.color.rgb * caustic.rgb * wave.rgb * _BaseColor.rgb * 2.0;
                col.a = input.color.a * caustic.a * wave.a * _BaseColor.a;
                return col;
            }
            ENDHLSL
        }
    }
}
