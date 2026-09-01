// Knight Online FX — Generic Blend Shader
// Open-KO birebir: CN3AlphaPrimitiveManager::Render (N3AlphaPrimitiveManager.cpp:18-220)
//
// C++ her primitive kendi dwBlendSrc/dwBlendDest değerlerini kullanır (satır 135-142).
// Bu shader per-material _SrcBlend/_DstBlend property ile C++ davranışını birebir karşılar.
//
// D3DBLEND enum → Unity BlendMode:
//   1=ZERO, 2=ONE, 3=SRCCOLOR, 4=INVSRCCOLOR, 5=SRCALPHA, 6=INVSRCALPHA
//
// Texture stage (satır 149-159):
//   ColorOp = D3DTOP_MODULATE: finalColor = texture.rgb * diffuse.rgb
// Alpha stage (RF_DIFFUSEALPHA, satır 92-104):
//   AlphaOp = D3DTOP_MODULATE: finalAlpha = texture.a * diffuse.a
Shader "KO/FX/Generic"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        // C++ per-part blend mode — D3DBLEND → Unity BlendMode
        // mat.SetInt("_SrcBlend", ...) ile runtime'da ayarlanır
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        // C++ RF_DOUBLESIDED (0x4) → D3DCULL_NONE
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
    }
    SubShader
    {
        // C++ AlphaPrimitiveManager: tüm FX Transparent queue'da çizilir
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            // C++ birebir: per-primitive SrcBlend/DestBlend (satır 135-142)
            Blend [_SrcBlend] [_DstBlend]

            // C++ RF_NOTZWRITE — çoğu FX part'ta ZWrite kapalı
            ZWrite Off

            // C++ RF_DOUBLESIDED → per-part Cull mode
            Cull [_Cull]

            // C++ RF_NOTUSELIGHT → D3DRS_LIGHTING = FALSE
            Lighting Off

            // C++ RF_NOTUSEFOG → D3DRS_FOGENABLE = FALSE
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                // C++ FVF_XYZCOLORT1 — vertex color (m_dwCurrColor)
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // C++ vertex color: m_dwCurrColor (N3FXPartBillBoard.cpp:567-570)
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // C++ AlphaPrimitiveManager satır 149-159 birebir:
                // ColorOp = MODULATE: finalColor = texture.rgb * diffuse.rgb
                // AlphaOp = MODULATE (RF_DIFFUSEALPHA): finalAlpha = texture.a * diffuse.a
                fixed4 texCol = tex2D(_MainTex, i.uv);
                fixed4 col;
                col.rgb = texCol.rgb * _Color.rgb * i.color.rgb;
                col.a = texCol.a * _Color.a * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
