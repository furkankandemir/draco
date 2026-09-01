using UnityEngine;
using UnityEngine.Rendering;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO birebir: CN3CPlug sword trail rendering — triangle strip mesh
    ///
    /// C++ referans (doğrulanmış satır numaraları):
    ///
    /// CN3CPlug fields — N3Chr.h:238-242:
    ///   m_nTraceStep:  trail segment sayısı
    ///   m_crTrace:     trail rengi (D3DCOLOR ARGB — 0xAARRGGBB)
    ///   m_fTrace0:     silahın Y ekseninde alt nokta (kabza tarafı)
    ///   m_fTrace1:     silahın Y ekseninde üst nokta (uç tarafı)
    ///
    /// Vertex oluşturma — N3Chr.cpp:1743-1769 (TickPlugTrace):
    ///   Her frame geri gidip skeleton'ı o frame'e tick eder:
    ///     for j=0..nTraceStep, k=nTraceStep..0:
    ///       fFrmTmp = fFrmCur - (j * 0.2f)
    ///       m_pRootJointRef->Tick(fFrmTmp)
    ///       vTrace0 = (0, fTrace0, 0) * plugMatrix * jointMatrix  — alt vertex
    ///       vTrace1 = (0, fTrace1, 0) * plugMatrix * jointMatrix  — üst vertex
    ///       crTraceU = crTrace * (k / nTraceStep)  — RGB fade, alpha sabit (satır 1759-1765)
    ///       crTraceL = (crTraceU & 0xff000000) | ((crTraceU & 0x00fcfcfc) >> 2) — alt 1/4 parlak (satır 1766)
    ///       m_vTraces[j*2+0] = {vTrace0, crTraceL}  — alt vertex
    ///       m_vTraces[j*2+1] = {vTrace1, crTraceU}  — üst vertex
    ///
    /// Render — N3Chr.cpp:1857-1886:
    ///   D3DPT_TRIANGLESTRIP, FVF_CV (__VertexColor = pos + color)
    ///   nVertexCount = nTraceStep * 2
    ///   nPrimitiveCount = (nTraceStep - 1) * 2
    ///   SrcBlend=SRCCOLOR, DestBlend=ONE (additive)
    ///   RF_DOUBLESIDED | RF_DIFFUSEALPHA | RF_NOTUSELIGHT | RF_NOTZWRITE
    ///   Texture = null (sadece vertex color)
    ///
    /// Trail aktivasyon — N3Chr.cpp:1733-1736:
    ///   if (fFrmCur >= pAniData->fFrmPlugTraceStart &&
    ///       fFrmCur <= pAniData->fFrmPlugTraceEnd)
    ///     m_bRenderTrace = true
    ///
    /// Unity adaptasyonu:
    ///   C++ her frame skeleton'ı geri sararak önceki konumları hesaplar.
    ///   Unity'de Animation.Sample() ile bunu yapmak pahalı ve kararsız.
    ///   Bunun yerine her frame'de plug transform'ının world-space pozisyonlarını
    ///   ring buffer'da saklıyoruz — fonksiyonel olarak aynı sonuç.
    /// </summary>
    public class KOWeaponTrail : MonoBehaviour
    {
        // Open-KO birebir: CN3CPlug fields — N3Chr.h:238-242
        [HideInInspector] public int nTraceStep;     // m_nTraceStep
        [HideInInspector] public uint crTrace;       // m_crTrace (ARGB)
        [HideInInspector] public float fTrace0;      // m_fTrace0
        [HideInInspector] public float fTrace1;      // m_fTrace1

        // Open-KO birebir: m_bRenderTrace — N3Chr.h:238
        private bool _isTracing = false;

        // Ring buffer — son nTraceStep frame'in world-space pozisyonları
        // C++ karşılığı: m_pRootJointRef->Tick(fFrmTmp) ile hesaplanan
        // vTrace0/vTrace1 pozisyonları
        private Vector3[] _tracePositions0; // alt nokta (fTrace0) pozisyon geçmişi
        private Vector3[] _tracePositions1; // üst nokta (fTrace1) pozisyon geçmişi
        private int _ringHead = 0;          // en yeni pozisyonun index'i
        private int _ringCount = 0;         // buffer'daki geçerli pozisyon sayısı

        // Mesh rendering
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _trailMesh;
        private Material _trailMaterial;



        // Alt/üst nokta child transform'ları
        // C++: vTrace0.Set(0, pPlug->m_fTrace0, 0) — plug local space'te Y offset
        private Transform _pointLower;
        private Transform _pointUpper;

        /// <summary>
        /// Plug oluşturulduktan sonra çağrılır.
        /// N3CPlugImporter.CPlugData'dan trace verilerini alır.
        /// </summary>
        public void Initialize(int traceStep, uint traceColor, float trace0, float trace1)
        {
            nTraceStep = traceStep;
            crTrace = traceColor;
            fTrace0 = trace0;
            fTrace1 = trace1;

            if (nTraceStep <= 1)
            {
                enabled = false;
                return;
            }

            // Ring buffer oluştur
            _tracePositions0 = new Vector3[nTraceStep];
            _tracePositions1 = new Vector3[nTraceStep];
            _ringHead = 0;
            _ringCount = 0;



            // Alt ve üst nokta referans transform'ları oluştur
            // C++: vTrace0.Set(0, fTrace0, 0) — plug local space
            _pointLower = new GameObject("TracePoint0").transform;
            _pointLower.SetParent(transform);
            _pointLower.localPosition = new Vector3(0, fTrace0, 0);
            _pointLower.localRotation = Quaternion.identity;
            _pointLower.localScale = Vector3.one;

            _pointUpper = new GameObject("TracePoint1").transform;
            _pointUpper.SetParent(transform);
            _pointUpper.localPosition = new Vector3(0, fTrace1, 0);
            _pointUpper.localRotation = Quaternion.identity;
            _pointUpper.localScale = Vector3.one;

            // Mesh oluştur
            _trailMesh = new Mesh();
            _trailMesh.name = "WeaponTrail";
            _trailMesh.MarkDynamic(); // Her frame güncelleneceği için

            // MeshFilter + MeshRenderer — plug GameObject'in üstüne değil, ayrı child'a
            // (plug'ın transform'ından bağımsız world-space mesh)
            var trailObj = new GameObject("TrailMesh");
            trailObj.transform.SetParent(null); // World space'te
            trailObj.transform.position = Vector3.zero;
            trailObj.transform.rotation = Quaternion.identity;
            trailObj.transform.localScale = Vector3.one;

            _meshFilter = trailObj.AddComponent<MeshFilter>();
            _meshFilter.mesh = _trailMesh;

            _meshRenderer = trailObj.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            // Material — N3Chr.cpp:1867-1881 birebir
            // SrcBlend=SRCCOLOR(5), DestBlend=ONE(2) — additive
            // RF_DOUBLESIDED | RF_DIFFUSEALPHA | RF_NOTUSELIGHT | RF_NOTZWRITE
            // Texture = null (vertex color only)
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _trailMaterial = new Material(shader);
            // Additive blending — N3Chr.cpp:1868-1872
            _trailMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcColor);
            _trailMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            _trailMaterial.SetInt("_ZWrite", 0);        // RF_NOTZWRITE
            _trailMaterial.SetInt("_Cull", (int)CullMode.Off); // RF_DOUBLESIDED
            _trailMaterial.SetColor("_Color", Color.white);
            // Particles/Standard Unlit vertex color'ları varsayılan olarak kullanır.
            _trailMaterial.renderQueue = 3100;

            _meshRenderer.material = _trailMaterial;
            _meshRenderer.enabled = false; // Başlangıçta gizli

        }

        /// <summary>
        /// Open-KO birebir: m_bRenderTrace = true/false (N3Chr.cpp:1736)
        /// </summary>
        public void SetTracing(bool active)
        {
            _isTracing = active;
            if (!active)
            {
                // Trail kapanınca ring buffer'ı sıfırla
                _ringCount = 0;
                _ringHead = 0;
                if (_meshRenderer != null)
                    _meshRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Trail'leri temizle (silah çıkarılınca)
        /// </summary>
        public void ClearTrails()
        {
            _ringCount = 0;
            _ringHead = 0;
            if (_trailMesh != null)
                _trailMesh.Clear();
            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (EntropyOnline.UI.GameOptionsManager.Instance != null && EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideHandTrailFX)
            {
                if (_meshRenderer != null && _meshRenderer.enabled)
                    _meshRenderer.enabled = false;
                return;
            }

            if (!_isTracing || _pointLower == null || _pointUpper == null)
            {
                return;
            }

            // 1. Yeni frame'in world-space pozisyonlarını ring buffer'a ekle
            // C++ karşılığı: m_pRootJointRef->Tick(fFrmCur) sonucu
            //   vTrace0 = (0, fTrace0, 0) * plugMatrix * jointMatrix
            //   vTrace1 = (0, fTrace1, 0) * plugMatrix * jointMatrix
            // Unity'de: child transform'ın world position'ı zaten bunu verir
            _tracePositions0[_ringHead] = _pointLower.position;
            _tracePositions1[_ringHead] = _pointUpper.position;
            _ringHead = (_ringHead + 1) % nTraceStep;
            if (_ringCount < nTraceStep)
                _ringCount++;

            // En az 2 frame gerekli (1 triangle strip quad)
            if (_ringCount < 2)
            {
                if (_meshRenderer != null)
                    _meshRenderer.enabled = false;
                return;
            }

            // 2. Triangle strip mesh oluştur — N3Chr.cpp:1743-1769 birebir
            // Vertex düzeni: [j*2+0]=alt(Lower), [j*2+1]=üst(Upper)
            // j=0 en yeni, j=ringCount-1 en eski
            int vertCount = _ringCount * 2;
            var vertices = new Vector3[vertCount];
            var colors = new Color32[vertCount];

            for (int j = 0; j < _ringCount; j++)
            {
                // Ring buffer'dan: j=0 en yeni → ringHead-1, j=ringCount-1 en eski
                int bufIdx = (_ringHead - 1 - j + nTraceStep) % nTraceStep;

                vertices[j * 2 + 0] = _tracePositions0[bufIdx]; // alt
                vertices[j * 2 + 1] = _tracePositions1[bufIdx]; // üst

                // Renk: önceden hesaplanmış _vertexColors kullan
                // Ama _vertexColors nTraceStep boyutunda, ringCount daha az olabilir
                // C++ birebir: k = ringCount - j
                int k = _ringCount - j;
                byte srcA = (byte)((crTrace >> 24) & 0xFF);
                byte srcR = (byte)((crTrace >> 16) & 0xFF);
                byte srcG = (byte)((crTrace >> 8) & 0xFF);
                byte srcB = (byte)(crTrace & 0xFF);

                byte rU = (byte)(srcR * k / _ringCount);
                byte gU = (byte)(srcG * k / _ringCount);
                byte bU = (byte)(srcB * k / _ringCount);
                byte rL = (byte)(rU >> 2);
                byte gL = (byte)(gU >> 2);
                byte bL = (byte)(bU >> 2);

                colors[j * 2 + 0] = new Color32(rL, gL, bL, srcA);
                colors[j * 2 + 1] = new Color32(rU, gU, bU, srcA);
            }

            // 3. Triangle strip → triangle list index'leri
            // D3DPT_TRIANGLESTRIP: nPrimitiveCount = (nTraceStep - 1) * 2
            // Her quad: 2 üçgen
            int quadCount = _ringCount - 1;
            int triCount = quadCount * 2;
            var indices = new int[triCount * 3];

            for (int q = 0; q < quadCount; q++)
            {
                int v0 = q * 2;     // alt sol
                int v1 = q * 2 + 1; // üst sol
                int v2 = q * 2 + 2; // alt sağ
                int v3 = q * 2 + 3; // üst sağ

                // Triangle strip sırası: v0, v1, v2, v3
                // → Tri 1: v0, v1, v2
                // → Tri 2: v2, v1, v3
                int idx = q * 6;
                indices[idx + 0] = v0;
                indices[idx + 1] = v1;
                indices[idx + 2] = v2;
                indices[idx + 3] = v2;
                indices[idx + 4] = v1;
                indices[idx + 5] = v3;
            }

            // 4. Mesh güncelle
            _trailMesh.Clear();
            _trailMesh.vertices = vertices;
            _trailMesh.colors32 = colors;
            _trailMesh.triangles = indices;

            _meshRenderer.enabled = true;
        }

        private void OnDestroy()
        {
            // TrailMesh child'ı world space'te — manual temizle
            if (_meshFilter != null && _meshFilter.gameObject != null)
                Destroy(_meshFilter.gameObject);
            if (_trailMesh != null)
                Destroy(_trailMesh);
            if (_trailMaterial != null)
                Destroy(_trailMaterial);
        }
    }
}
