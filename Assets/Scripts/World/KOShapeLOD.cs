using UnityEngine;
using EntropyOnline.Import;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO birebir LOD sistemi.
    /// CN3PMeshInstance::SetLOD + CN3SPart::Tick + CN3Shape::Tick portu.
    ///
    /// Her frame kamera mesafesine göre progressive mesh vertex sayısını ayarlar.
    /// Referanslar:
    ///   N3Shape.cpp:566-605  — CN3Shape::Tick (frustum + distance culling)
    ///   N3Shape.cpp:90-97    — CN3SPart::Tick (LOD hesaplama)
    ///   N3PMeshInstance.cpp:142-210 — SetLOD (LODCtrlValue tablosu)
    ///   N3PMeshInstance.cpp:106-140 — SetLODByNumVertices (incremental split/collapse)
    ///   N3PMeshInstance.cpp:212-258 — CollapseOne / SplitOne
    /// </summary>
    public class KOShapeLOD : MonoBehaviour
    {
        // === PMeshInstance State (N3PMeshInstance.h birebir) ===
        private int[] _indices;                // Mutable index buffer kopyası
        private int _numVertices;              // Şu anki aktif vertex sayısı
        private int _numIndices;               // Şu anki aktif index sayısı
        private int _collapseUpTo;             // Collapse pointer (0 = min LOD)

        // === Shared Data (N3PMeshData'dan referans) ===
        private N3PMeshImporter.EdgeCollapseData[] _collapses;
        private int[] _allIndexChanges;
        private N3PMeshImporter.LODCtrlValueData[] _lodCtrlValues;
        private int _minNumVertices;
        private int _minNumIndices;
        private int _maxNumVertices;
        private int _numCollapses;

        // === Unity References ===
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private int _lastAppliedNumIndices = -1;  // Son mesh.triangles güncellemesindeki index sayısı

        // === Shape-level culling (CN3Shape::Tick birebir) ===
        private float _shapeRadius;            // CN3Shape::m_fRadius
        private float _shapeScale = 1f;        // max(scale.x, scale.y, scale.z)

        private bool _initialized;

        /// <summary>
        /// LOD component'ını N3PMeshData ile başlat.
        /// Open-KO CN3PMeshInstance::Create() birebir.
        /// </summary>
        public void Initialize(N3PMeshImporter.N3PMeshData data, MeshFilter mf, MeshRenderer mr)
        {
            if (data == null || data.MinLODIndices == null || data.Collapses == null)
                return;

            _meshFilter = mf;
            _meshRenderer = mr;
            _mesh = mf.mesh; // Instance kopyası (shared değil)

            // CN3PMeshInstance::Create birebir (N3PMeshInstance.cpp:63-93)
            _indices = (int[])data.MinLODIndices.Clone();
            _numVertices = data.MinNumVertices;
            _numIndices = data.MinNumIndices;
            _collapseUpTo = 0; // m_pCollapseUpTo = m_pPMesh->m_pCollapses

            _collapses = data.Collapses;
            _allIndexChanges = data.AllIndexChanges;
            _lodCtrlValues = data.LODCtrlValues;
            _minNumVertices = data.MinNumVertices;
            _minNumIndices = data.MinNumIndices;
            _maxNumVertices = data.MaxNumVertices;
            _numCollapses = data.Collapses.Length;

            // Shape radius ve scale
            if (_mesh != null)
                _shapeRadius = _mesh.bounds.extents.magnitude;

            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            _shapeScale = Mathf.Max(parentScale.x, Mathf.Max(parentScale.y, parentScale.z));
            if (_shapeScale < 0.001f) _shapeScale = 1f;

            // Max LOD'a expand et (yakın objeler için)
            // İlk frame'de LateUpdate doğru seviyeye ayarlayacak
            SetLODByNumVertices(0x7fffffff);
            ApplyToMesh();

            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            // === CN3Shape::Tick distance culling (N3Shape.cpp:580-584) ===
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 shapePos = transform.parent != null ? transform.parent.position : transform.position;
            float fDist = (shapePos - cam.transform.position).magnitude;

            // Open-KO: if (fDist > s_CameraData.fFP + m_fRadius * fScale * 2.0f) → dontRender
            float farPlane = cam.farClipPlane;
            if (fDist > farPlane + _shapeRadius * _shapeScale * 2f)
            {
                if (_meshRenderer != null && _meshRenderer.enabled)
                    _meshRenderer.enabled = false;
                return;
            }

            if (_meshRenderer != null && !_meshRenderer.enabled)
                _meshRenderer.enabled = true;

            // === CN3SPart::Tick LOD hesaplama (N3Shape.cpp:90-97) ===
            // Open-KO: float fLOD = fDist * s_CameraData.fFOV / fScale;
            // s_CameraData.fFOV = tan(FOV/2) — Open-KO kamera verisi
            float fFOV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float fLOD = fDist * fFOV / _shapeScale;

            // === CN3PMeshInstance::SetLOD (N3PMeshInstance.cpp:142-210) ===
            SetLOD(fLOD);

            // Mesh'i sadece index sayısı değiştiyse güncelle
            if (_numIndices != _lastAppliedNumIndices)
                ApplyToMesh();
        }

        /// <summary>
        /// CN3PMeshInstance::SetLOD birebir (N3PMeshInstance.cpp:142-210)
        /// _USE_LODCONTROL_VALUE branch.
        /// </summary>
        private void SetLOD(float value)
        {
            if (_lodCtrlValues == null || _lodCtrlValues.Length == 0)
            {
                // LODCtrlValue yoksa tüm vertex'leri göster
                SetLODByNumVertices(0x7fffffff);
                return;
            }

            int count = _lodCtrlValues.Length;
            var last = _lodCtrlValues[count - 1];

            if (value < _lodCtrlValues[0].Dist)
            {
                // En yakın — en çok vertex
                SetLODByNumVertices(_lodCtrlValues[0].NumVertices);
            }
            else if (last.Dist < value)
            {
                // En uzak — en az vertex
                SetLODByNumVertices(last.NumVertices);
            }
            else
            {
                // Arada — lineer interpolasyon
                for (int i = 1; i < count; i++)
                {
                    if (value < _lodCtrlValues[i].Dist)
                    {
                        var hi = _lodCtrlValues[i];
                        var lo = _lodCtrlValues[i - 1];
                        float fVertices = (hi.NumVertices - lo.NumVertices)
                                          * (value - lo.Dist)
                                          / (hi.Dist - lo.Dist);
                        SetLODByNumVertices(lo.NumVertices + (int)fVertices);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// CN3PMeshInstance::SetLODByNumVertices birebir (N3PMeshInstance.cpp:106-140)
        /// </summary>
        private void SetLODByNumVertices(int iNumVertices)
        {
            int iDiff = iNumVertices - _numVertices;
            if (iDiff == 0) return;

            if (iDiff > 0)
            {
                while (iNumVertices > _numVertices)
                {
                    if (_collapseUpTo < _numCollapses &&
                        _collapses[_collapseUpTo].NumVerticesToLose + _numVertices > iNumVertices)
                        break; // 깜박임 방지 코드 (N3PMeshInstance.cpp:119-120)

                    if (!SplitOne()) break;
                }
            }
            else
            {
                while (iNumVertices < _numVertices)
                {
                    if (!CollapseOne()) break;
                }
            }

            // bShouldCollapse: 구멍 방지 (N3PMeshInstance.cpp:135-139)
            while (_collapseUpTo < _numCollapses && _collapses[_collapseUpTo].ShouldCollapse)
            {
                if (!SplitOne()) break;
            }
        }

        /// <summary>
        /// CN3PMeshInstance::CollapseOne birebir (N3PMeshInstance.cpp:212-232)
        /// </summary>
        private bool CollapseOne()
        {
            if (_collapseUpTo <= 0) return false;

            _collapseUpTo--;

            _numIndices -= _collapses[_collapseUpTo].NumIndicesToLose;

            int changeStart = _collapses[_collapseUpTo].IndexChangesOffset;
            int changeCount = _collapses[_collapseUpTo].NumIndicesToChange;
            int collapseTo = _collapses[_collapseUpTo].CollapseTo;

            for (int i = changeStart; i < changeStart + changeCount; i++)
            {
                if (i >= 0 && i < _allIndexChanges.Length)
                {
                    int slot = _allIndexChanges[i];
                    if (slot >= 0 && slot < _indices.Length)
                        _indices[slot] = collapseTo;
                }
            }

            _numVertices -= _collapses[_collapseUpTo].NumVerticesToLose;
            return true;
        }

        /// <summary>
        /// CN3PMeshInstance::SplitOne birebir (N3PMeshInstance.cpp:234-258)
        /// </summary>
        private bool SplitOne()
        {
            if (_collapseUpTo >= _numCollapses) return false;

            _numIndices += _collapses[_collapseUpTo].NumIndicesToLose;
            _numVertices += _collapses[_collapseUpTo].NumVerticesToLose;

            if (_allIndexChanges != null)
            {
                int changeStart = _collapses[_collapseUpTo].IndexChangesOffset;
                int changeCount = _collapses[_collapseUpTo].NumIndicesToChange;

                for (int i = changeStart; i < changeStart + changeCount; i++)
                {
                    if (i >= 0 && i < _allIndexChanges.Length)
                    {
                        int slot = _allIndexChanges[i];
                        if (slot >= 0 && slot < _indices.Length)
                            _indices[slot] = _numVertices - 1;
                    }
                }
            }

            _collapseUpTo++;
            return true;
        }

        /// <summary>
        /// Güncel index buffer'ı Unity Mesh'e uygula.
        /// Sadece aktif index'leri (0..numIndices-1) kopyalar.
        /// </summary>
        private void ApplyToMesh()
        {
            if (_mesh == null || _indices == null) return;

            int triCount = _numIndices;
            if (triCount < 3) triCount = 0;

            // Sadece aktif index'leri al ve doğrula
            var tris = new int[triCount];
            int maxVert = _mesh.vertexCount;
            for (int i = 0; i < triCount; i++)
            {
                int idx = _indices[i];
                tris[i] = (idx >= 0 && idx < maxVert) ? idx : 0;
            }

            _mesh.triangles = tris;
            _lastAppliedNumIndices = _numIndices;
        }
    }
}
