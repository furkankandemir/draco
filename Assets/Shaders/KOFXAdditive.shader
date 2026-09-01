// Knight Online FX — Additive Blend Shader
// Open-KO birebir: CN3AlphaPrimitiveManager::Render (N3AlphaPrimitiveManager.cpp:18-220)
//
// C++ render state eşleme:
//   D3DRS_ALPHABLENDENABLE = TRUE (satır 61-65)
//   D3DRS_SRCBLEND  = m_dwSrcBlend  (satır 135-138) → Blend One One
//   D3DRS_DESTBLEND = m_dwDestBlend (satır 139-142) → Blend One One
//   D3DRS_FOGENABLE = FALSE (RF_NOTUSEFOG, satır 72-75)
//   D3DRS_CULLMODE  = D3DCULL_NONE (RF_DOUBLESIDED, satır 76-79)
//   D3DRS_LIGHTING  = FALSE (RF_NOTUSELIGHT, satır 80-83)
//   D3DRS_ZWRITEENABLE = RF_NOTZWRITE'a bağlı (satır 84-87)
//   D3DRS_ZENABLE = RF_NOTZBUFFER'a bağlı (satır 88-91)
//
// Texture stage (satır 149-159):
//   ColorOp = D3DTOP_MODULATE, ColorArg1 = D3DTA_DIFFUSE, ColorArg2 = D3DTA_TEXTURE
//   → finalColor = texture * diffuse (vertex color)
//
// Alpha stage (RF_DIFFUSEALPHA, satır 92-104):
//   AlphaOp = D3DTOP_MODULATE, AlphaArg1 = D3DTA_DIFFUSE, AlphaArg2 = D3DTA_TEXTURE
//   → finalAlpha = textureAlpha * diffuseAlpha (vertex alpha)
//
// Additive blend: siyah pikseller (0,0,0) otomatik transparent olur
// D3DBLEND_ONE + D3DBLEND_ONE veya D3DBLEND_SRCALPHA + D3DBLEND_ONE
Shader "KO/FX/Additive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        // C++ RF_DOUBLESIDED (0x4) → D3DCULL_NONE (N3FXPartBase.cpp:543-546)
        // 0=Off (D3DCULL_NONE), 2=Back (D3DCULL_CCW)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
    }
    SubShader
    {
        // C++ AlphaPrimitiveManager: tüm FX Transparent queue'da çizilir
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            // Open-KO birebir: D3DBLEND_ONE + D3DBLEND_ONE (N3FXPartBase.cpp:38-39 default)
            // D3DBLEND_SRCALPHA + D3DBLEND_ONE varyantı da aynı shader kullanır
            Blend One One

            // C++ RF_NOTZWRITE kontrolü — default FX partlarda RF_NOTZWRITE YOK
            // ama additive efektlerde Z yazma genelde kapalı tutulur
            ZWrite Off

            // C++ RF_DOUBLESIDED → D3DCULL_NONE (N3FXPartBase.cpp:55)
            // mat.SetInt("_Cull", CullMode) ile per-part kontrol edilir
            Cull [_Cull]

            // C++ RF_NOTUSELIGHT → D3DRS_LIGHTING = FALSE (N3FXPartBase.cpp:54)
            Lighting Off

            // C++ RF_NOTUSEFOG → D3DRS_FOGENABLE = FALSE (N3FXPartBase.cpp:49)
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
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
