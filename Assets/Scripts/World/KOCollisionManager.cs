using UnityEngine;
using EntropyOnline.Import;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO v1.298 CN3ShapeMgr collision sistemi — birebir port.
    /// 
    /// Referanslar:
    ///   N3ShapeMgr.h          — Veri yapıları, sabitler
    ///   N3ShapeMgr.cpp         — GetHeight, GetHeightNearstPos, CheckCollision, SubCellPathThru, SubCell
    ///   Server/shared-server/N3ShapeMgr.cpp — Server versiyonu (client ile aynı collision logic)
    ///   MathUtils.cpp          — _IntersectTriangle (Möller-Trumbore + backface check)
    /// 
    /// Kullanım:
    ///   1. WorldBuilder OPD parse sonrasında Initialize() çağırır
    ///   2. Tek MeshCollider oluşturulur (CharacterController uyumluluğu)
    ///   3. GetHeight/CheckCollision programatik erişim sağlar
    /// </summary>
    public class KOCollisionManager : MonoBehaviour
    {
        public static KOCollisionManager Instance { get; private set; }

        // N3ShapeMgr.h sabitleri
        private const int CELL_MAIN_DIVIDE = 4;
        private const int CELL_SUB_SIZE = 4;
        private const int CELL_MAIN_SIZE = CELL_MAIN_DIVIDE * CELL_SUB_SIZE; // 16
        private const int MAX_CELL_MAIN = 256; // 4096 / 16

        // Collision verileri
        private Vector3[] _collisionVertices;
        private int _collisionFaceCount;
        private N3ShapeParser.CellMainData[,] _cells;
        private int _cellsX, _cellsZ;
        private float _mapWidth, _mapLength;
        private bool _initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// OPD parse sonuçlarını yükle ve tek MeshCollider oluştur.
        /// WorldBuilder.LoadObjects() sonrasında çağrılır.
        /// </summary>
        public void Initialize(N3ShapeParser.OpdFullData opdData)
        {
            if (opdData == null) return;

            _collisionVertices = opdData.CollisionVertices;
            _collisionFaceCount = opdData.CollisionFaceCount;
            _cells = opdData.Cells;
            _cellsX = opdData.CellsX;
            _cellsZ = opdData.CellsZ;
            _mapWidth = opdData.MapWidth;
            _mapLength = opdData.MapLength;
            _initialized = true;

            // Tek birleşik MeshCollider oluştur — CharacterController ile çalışır
            BuildCombinedCollider();

        }

        /// <summary>
        /// Convert edilmiş collision mesh'ten başlat.
        /// MeshCollider zaten WorldBuilder tarafından oluşturulmuş —
        /// burada sadece programatik erişim için vertex verilerini yüklüyoruz.
        /// </summary>
        public void InitializeFromConvertedMesh(Mesh collisionMesh, float mapWidth, float mapLength)
        {
            if (collisionMesh == null) return;

            _collisionVertices = collisionMesh.vertices;
            _collisionFaceCount = collisionMesh.triangles.Length / 3;
            _mapWidth = mapWidth;
            _mapLength = mapLength;
            _cellsX = Mathf.Max(1, (int)(mapWidth / CELL_MAIN_SIZE));
            _cellsZ = Mathf.Max(1, (int)(mapLength / CELL_MAIN_SIZE));
            _initialized = true;

            // Not: SubCell verileri olmadan GetHeight çalışmaz ama
            // MeshCollider zaten fiziksel collision sağlıyor.
            // Programatik height query için raycast kullanılabilir.

        }

        /// <summary>
        /// OPD collision verilerinden tek birleşik MeshCollider oluşturur.
        /// ~2500 ayrı MeshCollider yerine 1 tane — mobil performans için kritik.
        /// Collision vertex'leri zaten world-space (CN3ShapeMgr::GenerateCollisionData).
        /// </summary>
        private void BuildCombinedCollider()
        {
            if (_collisionVertices == null || _collisionFaceCount <= 0) return;

            int vertCount = _collisionFaceCount * 3;
            if (vertCount != _collisionVertices.Length)
            {
                Debug.LogWarning($"[COLLISION] Vertex count mismatch: {vertCount} vs {_collisionVertices.Length}");
                vertCount = Mathf.Min(vertCount, _collisionVertices.Length);
            }

            // Büyük üçgenleri filtrele — PhysX uyarısını önle
            // "distance between any 2 vertices is greater than 500 units"
            const float MAX_EDGE_LENGTH = 500f;
            const float MAX_EDGE_SQR = MAX_EDGE_LENGTH * MAX_EDGE_LENGTH;

            var filteredVerts = new System.Collections.Generic.List<Vector3>(vertCount);
            var filteredTris = new System.Collections.Generic.List<int>(vertCount);
            int skippedCount = 0;

            for (int i = 0; i + 2 < vertCount; i += 3)
            {
                Vector3 v0 = _collisionVertices[i];
                Vector3 v1 = _collisionVertices[i + 1];
                Vector3 v2 = _collisionVertices[i + 2];

                // Kenar uzunluklarını kontrol et (sqrMagnitude ile hızlı karşılaştırma)
                float e0 = (v1 - v0).sqrMagnitude;
                float e1 = (v2 - v1).sqrMagnitude;
                float e2 = (v0 - v2).sqrMagnitude;

                if (e0 > MAX_EDGE_SQR || e1 > MAX_EDGE_SQR || e2 > MAX_EDGE_SQR)
                {
                    skippedCount++;
                    continue;
                }

                int baseIdx = filteredVerts.Count;
                filteredVerts.Add(v0);
                filteredVerts.Add(v1);
                filteredVerts.Add(v2);
                filteredTris.Add(baseIdx);
                filteredTris.Add(baseIdx + 1);
                filteredTris.Add(baseIdx + 2);
            }

            if (filteredVerts.Count == 0) return;

            var mesh = new Mesh();
            mesh.name = "OPD_CollisionMesh";

            if (filteredVerts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = filteredVerts.ToArray();
            mesh.triangles = filteredTris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Collider GameObject — transform sıfırda (vertex'ler zaten world-space)
            var colliderObj = new GameObject("OPD_Collision");
            colliderObj.transform.SetParent(transform, false);
            colliderObj.transform.position = Vector3.zero;
            colliderObj.transform.rotation = Quaternion.identity;
            colliderObj.transform.localScale = Vector3.one;

            var mc = colliderObj.AddComponent<MeshCollider>();
            mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                              | MeshColliderCookingOptions.EnableMeshCleaning
                              | MeshColliderCookingOptions.WeldColocatedVertices;
            mc.sharedMesh = mesh;

            int validFaces = filteredVerts.Count / 3;
        }

        #region SubCell Lookup — N3ShapeMgr::SubCell (N3ShapeMgr.h:171-187)

        /// <summary>
        /// Koordinattan SubCell döndürür.
        /// N3ShapeMgr.h satır 171-187 birebir.
        /// </summary>
        private N3ShapeParser.CellSubData GetSubCell(float fX, float fZ)
        {
            int x = (int)(fX / CELL_MAIN_SIZE);
            int z = (int)(fZ / CELL_MAIN_SIZE);

            if (x < 0 || x >= _cellsX || z < 0 || z >= _cellsZ)
                return null;

            if (_cells == null)
                return null;

            var cell = _cells[x, z];
            if (cell == null)
                return null;

            int xx = (((int)fX) % CELL_MAIN_SIZE) / CELL_SUB_SIZE;
            int zz = (((int)fZ) % CELL_MAIN_SIZE) / CELL_SUB_SIZE;

            if (xx < 0 || xx >= CELL_MAIN_DIVIDE || zz < 0 || zz >= CELL_MAIN_DIVIDE)
                return null;

            return cell.SubCells[xx, zz];
        }

        #endregion

        #region GetHeight — N3ShapeMgr.cpp:1304-1344

        /// <summary>
        /// Verilen (fX, fZ) noktasında en yüksek collision yüzey yüksekliğini döndürür.
        /// Yoksa float.MinValue döndürür.
        /// N3ShapeMgr::GetHeight birebir portu (satır 1304-1344).
        /// </summary>
        public float GetHeight(float fX, float fZ)
        {
            if (!_initialized) return float.MinValue;

            var cell = GetSubCell(fX, fZ);
            if (cell == null || cell.CCPolyCount <= 0)
                return float.MinValue;

            // Yukarıdan aşağı ray — N3ShapeMgr.cpp:1312-1314
            Vector3 vPosV = new Vector3(fX, 5000.0f, fZ);
            Vector3 vDir = new Vector3(0, -1, 0);

            float fMaxHeight = float.MinValue;

            for (int i = 0; i < cell.CCPolyCount; i++)
            {
                int n0 = cell.CCVertIndices[i * 3];
                int n1 = cell.CCVertIndices[i * 3 + 1];
                int n2 = cell.CCVertIndices[i * 3 + 2];

                // Bounds check
                if (n0 >= _collisionVertices.Length || n1 >= _collisionVertices.Length ||
                    n2 >= _collisionVertices.Length)
                    continue;

                if (IntersectTriangle(vPosV, vDir,
                    _collisionVertices[n0], _collisionVertices[n1], _collisionVertices[n2],
                    out float fT, out float fU, out float fV, out Vector3 vCol))
                {
                    // N3ShapeMgr.cpp:1330-1332
                    if (vCol.y > fMaxHeight)
                        fMaxHeight = vCol.y;
                }
            }

            return fMaxHeight;
        }

        #endregion

        #region GetHeightNearstPos — N3ShapeMgr.cpp:1253-1301

        /// <summary>
        /// Verilen pozisyona en yakın collision yüzey yüksekliğini döndürür.
        /// N3ShapeMgr::GetHeightNearstPos birebir portu.
        /// </summary>
        public float GetHeightNearstPos(Vector3 vPos)
        {
            if (!_initialized) return float.MinValue;

            var cell = GetSubCell(vPos.x, vPos.z);
            if (cell == null || cell.CCPolyCount <= 0)
                return float.MinValue;

            // N3ShapeMgr.cpp:1262-1266
            Vector3 vPosV = new Vector3(vPos.x, 5000.0f, vPos.z);
            Vector3 vDir = new Vector3(0, -1, 0);

            float fNearst = float.MaxValue;
            float fHeight = float.MinValue;

            for (int i = 0; i < cell.CCPolyCount; i++)
            {
                int n0 = cell.CCVertIndices[i * 3];
                int n1 = cell.CCVertIndices[i * 3 + 1];
                int n2 = cell.CCVertIndices[i * 3 + 2];

                if (n0 >= _collisionVertices.Length || n1 >= _collisionVertices.Length ||
                    n2 >= _collisionVertices.Length)
                    continue;

                if (IntersectTriangle(vPosV, vDir,
                    _collisionVertices[n0], _collisionVertices[n1], _collisionVertices[n2],
                    out float fT, out float fU, out float fV, out Vector3 vCol))
                {
                    // N3ShapeMgr.cpp:1283-1289
                    float fMinTmp = (vCol - vPos).magnitude;
                    if (fMinTmp < fNearst)
                    {
                        fNearst = fMinTmp;
                        fHeight = vCol.y;
                    }
                }
            }

            return fHeight;
        }

        #endregion

        #region CheckCollision — N3ShapeMgr.cpp:813-939 (Server: satır 153-245)

        /// <summary>
        /// Hareket yönünde duvar çarpışması kontrolü.
        /// N3ShapeMgr::CheckCollision birebir portu (server versiyonu — client visual check hariç).
        /// </summary>
        /// <param name="vPos">Mevcut pozisyon</param>
        /// <param name="vDir">Hareket yönü (normalize)</param>
        /// <param name="fSpeedPerSec">Hız (saniye başına metre)</param>
        /// <param name="colPoint">Çarpışma noktası (out)</param>
        /// <returns>Çarpışma varsa true</returns>
        public bool CheckCollision(Vector3 vPos, Vector3 vDir, float fSpeedPerSec,
            out Vector3 colPoint)
        {
            colPoint = Vector3.zero;

            if (!_initialized) return false;

            // N3ShapeMgr.cpp:821
            if (fSpeedPerSec <= 0) return false;

            // N3ShapeMgr.cpp:827
            Vector3 vPosNext = vPos + vDir * fSpeedPerSec;

            // N3ShapeMgr.cpp:831-842
            N3ShapeParser.CellSubData[] ppCells = new N3ShapeParser.CellSubData[128];
            int iSubCellCount;

            if (fSpeedPerSec < 4.0f)
            {
                Vector3 vPos2 = vPos + vDir * 4.0f;
                iSubCellCount = SubCellPathThru(vPos, vPos2, 128, ppCells);
            }
            else
            {
                iSubCellCount = SubCellPathThru(vPos, vPosNext, 128, ppCells);
            }

            // N3ShapeMgr.cpp:845-846
            if (iSubCellCount <= 0) return false;

            float fDistClosest = float.MaxValue;
            bool hasCollision = false;

            // N3ShapeMgr.cpp:853-897
            for (int i = 0; i < iSubCellCount; i++)
            {
                var cell = ppCells[i];
                if (cell == null || cell.CCPolyCount <= 0) continue;

                for (int j = 0; j < cell.CCPolyCount; j++)
                {
                    int n0 = cell.CCVertIndices[j * 3];
                    int n1 = cell.CCVertIndices[j * 3 + 1];
                    int n2 = cell.CCVertIndices[j * 3 + 2];

                    if (n0 >= _collisionVertices.Length || n1 >= _collisionVertices.Length ||
                        n2 >= _collisionVertices.Length)
                        continue;

                    // N3ShapeMgr.cpp:865-867
                    // İlk test: mevcut pozisyondan ray üçgeni kesiyor mu?
                    if (!IntersectTriangle(vPos, vDir,
                        _collisionVertices[n0], _collisionVertices[n1], _collisionVertices[n2],
                        out float fT, out float fU, out float fV, out Vector3 vColTmp))
                        continue;

                    // N3ShapeMgr.cpp:869-871
                    // İkinci test: sonraki pozisyondan da kesiyor mu?
                    // Evetse zaten geçmişiz, collision değil
                    if (IntersectTriangleSimple(vPosNext, vDir,
                        _collisionVertices[n0], _collisionVertices[n1], _collisionVertices[n2]))
                        continue;

                    // N3ShapeMgr.cpp:873-895
                    float fDistTmp = (vPos - vColTmp).magnitude;
                    if (fDistTmp < fDistClosest)
                    {
                        fDistClosest = fDistTmp;
                        colPoint = vColTmp;
                        hasCollision = true;
                    }
                }
            }

            return hasCollision;
        }

        #endregion

        #region SubCellPathThru — N3ShapeMgr.cpp:1122-1250

        /// <summary>
        /// İki nokta arasındaki alt hücreleri bulur (Cohen-Sutherland).
        /// N3ShapeMgr::SubCellPathThru birebir portu.
        /// </summary>
        private int SubCellPathThru(Vector3 vFrom, Vector3 vAt, int iMaxSubCell,
            N3ShapeParser.CellSubData[] ppSubCells)
        {
            // N3ShapeMgr.cpp:1130-1152 — Aralık belirleme
            int xx1, xx2, zz1, zz2;

            if (vFrom.x < vAt.x)
            {
                xx1 = (int)(vFrom.x / CELL_SUB_SIZE);
                xx2 = (int)(vAt.x / CELL_SUB_SIZE);
            }
            else
            {
                xx1 = (int)(vAt.x / CELL_SUB_SIZE);
                xx2 = (int)(vFrom.x / CELL_SUB_SIZE);
            }

            if (vFrom.z < vAt.z)
            {
                zz1 = (int)(vFrom.z / CELL_SUB_SIZE);
                zz2 = (int)(vAt.z / CELL_SUB_SIZE);
            }
            else
            {
                zz1 = (int)(vAt.z / CELL_SUB_SIZE);
                zz2 = (int)(vFrom.z / CELL_SUB_SIZE);
            }

            int iSubCellCount = 0;

            // N3ShapeMgr.cpp:1157-1249 — Cohen-Sutherland
            for (int z = zz1; z <= zz2; z++)
            {
                float fZMin = z * CELL_SUB_SIZE;
                float fZMax = (z + 1) * CELL_SUB_SIZE;

                for (int x = xx1; x <= xx2; x++)
                {
                    float fXMin = x * CELL_SUB_SIZE;
                    float fXMax = (x + 1) * CELL_SUB_SIZE;

                    // Cohen-Sutherland OutCode hesaplama
                    uint dwOC0 = 0, dwOC1 = 0;
                    if (vFrom.z > fZMax) dwOC0 |= 0xf000;
                    if (vFrom.z < fZMin) dwOC0 |= 0x0f00;
                    if (vFrom.x > fXMax) dwOC0 |= 0x00f0;
                    if (vFrom.x < fXMin) dwOC0 |= 0x000f;

                    if (vAt.z > fZMax) dwOC1 |= 0xf000;
                    if (vAt.z < fZMin) dwOC1 |= 0x0f00;
                    if (vAt.x > fXMax) dwOC1 |= 0x00f0;
                    if (vAt.x < fXMin) dwOC1 |= 0x000f;

                    bool bPathThru = false;

                    // N3ShapeMgr.cpp:1196-1216
                    if ((dwOC0 & dwOC1) != 0)
                    {
                        bPathThru = false;
                    }
                    else if ((dwOC0 == 0 && dwOC1 == 0) ||
                             (dwOC0 == 0 && dwOC1 != 0) ||
                             (dwOC0 != 0 && dwOC1 == 0))
                    {
                        bPathThru = true;
                    }
                    else if ((dwOC0 & dwOC1) == 0)
                    {
                        // N3ShapeMgr.cpp:1211
                        float fXCross = vFrom.x + (fZMax - vFrom.z) *
                            (vAt.x - vFrom.x) / (vAt.z - vFrom.z);
                        bPathThru = fXCross >= fXMin;
                    }

                    if (!bPathThru) continue;

                    // N3ShapeMgr.cpp:1222-1242
                    int nX = x / CELL_MAIN_DIVIDE;
                    int nZ = z / CELL_MAIN_DIVIDE;

                    if (nX < 0 || nX >= _cellsX || nZ < 0 || nZ >= _cellsZ)
                        continue;

                    if (_cells[nX, nZ] == null)
                        continue;

                    int nXSub = x % CELL_MAIN_DIVIDE;
                    int nZSub = z % CELL_MAIN_DIVIDE;

                    if (nXSub < 0 || nXSub >= CELL_MAIN_DIVIDE ||
                        nZSub < 0 || nZSub >= CELL_MAIN_DIVIDE)
                        continue;

                    ppSubCells[iSubCellCount++] = _cells[nX, nZ].SubCells[nXSub, nZSub];

                    if (iSubCellCount >= iMaxSubCell)
                        return iMaxSubCell;
                }
            }

            return iSubCellCount;
        }

        #endregion

        #region IntersectTriangle — MathUtils.cpp:84-199

        /// <summary>
        /// Ray-Triangle kesişim testi (tam versiyon — çarpışma noktası dahil).
        /// MathUtils.cpp:84-146 birebir portu.
        /// 
        /// KO'nun özel implementasyonu:
        /// 1. Backface check: face normal . dir &gt; -0.0001 → false (sadece ön yüz)
        /// 2. Möller-Trumbore ray-triangle intersection
        /// </summary>
        private static bool IntersectTriangle(Vector3 vOrig, Vector3 vDir,
            Vector3 v0, Vector3 v1, Vector3 v2,
            out float fT, out float fU, out float fV, out Vector3 vCol)
        {
            fT = 0; fU = 0; fV = 0;
            vCol = Vector3.zero;

            // MathUtils.cpp:88-89
            Vector3 vEdge1 = v1 - v0;
            Vector3 vEdge2 = v2 - v0;

            // MathUtils.cpp:93-98
            // Backface check: face normal dot direction
            Vector3 faceNormal = Vector3.Cross(vEdge1, vEdge2);
            float fDetN = Vector3.Dot(faceNormal, vDir);
            if (fDetN > -0.0001f)
                return false;

            // MathUtils.cpp:100-105
            // Möller-Trumbore determinant
            Vector3 pVec = Vector3.Cross(vDir, vEdge2);
            float fDet = Vector3.Dot(vEdge1, pVec);
            if (fDet < 0.0001f)
                return false;

            // MathUtils.cpp:108-113
            Vector3 tVec = vOrig - v0;
            fU = Vector3.Dot(tVec, pVec);
            if (fU < 0.0f || fU > fDet)
                return false;

            // MathUtils.cpp:116-122
            Vector3 qVec = Vector3.Cross(tVec, vEdge1);
            fV = Vector3.Dot(vDir, qVec);
            if (fV < 0.0f || fU + fV > fDet)
                return false;

            // MathUtils.cpp:125-130
            fT = Vector3.Dot(vEdge2, qVec);
            float fInvDet = 1.0f / fDet;
            fT *= fInvDet;
            fU *= fInvDet;
            fV *= fInvDet;

            // MathUtils.cpp:138-139
            vCol = vOrig + vDir * fT;

            // MathUtils.cpp:142-143
            if (fT < 0.0f)
                return false;

            return true;
        }

        /// <summary>
        /// Ray-Triangle kesişim testi (basit versiyon — sadece bool).
        /// MathUtils.cpp:148-199 birebir portu.
        /// CheckCollision'daki ikinci test için kullanılır.
        /// </summary>
        private static bool IntersectTriangleSimple(Vector3 vOrig, Vector3 vDir,
            Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // MathUtils.cpp:156-157
            Vector3 vEdge1 = v1 - v0;
            Vector3 vEdge2 = v2 - v0;

            // MathUtils.cpp:161-164
            Vector3 faceNormal = Vector3.Cross(vEdge1, vEdge2);
            float fDetN = Vector3.Dot(faceNormal, vDir);
            if (fDetN > -0.0001f)
                return false;

            // MathUtils.cpp:168-173
            Vector3 pVec = Vector3.Cross(vDir, vEdge2);
            float fDet = Vector3.Dot(vEdge1, pVec);
            if (fDet < 0.0001f)
                return false;

            // MathUtils.cpp:176-181
            Vector3 tVec = vOrig - v0;
            float fU = Vector3.Dot(tVec, pVec);
            if (fU < 0.0f || fU > fDet)
                return false;

            // MathUtils.cpp:184-189
            Vector3 qVec = Vector3.Cross(tVec, vEdge1);
            float fV = Vector3.Dot(vDir, qVec);
            if (fV < 0.0f || fU + fV > fDet)
                return false;

            // MathUtils.cpp:192-196
            float fT = Vector3.Dot(vEdge2, qVec) / fDet;
            if (fT < 0.0f)
                return false;

            return true;
        }

        #endregion

        #region Combined Height Query

        /// <summary>
        /// Terrain + OPD collision yüksekliğini kombine eder.
        /// İkisinden yüksek olanı döndürür (bina/duvar/köprü üstünde durma).
        /// </summary>
        public float GetCombinedHeight(float fX, float fZ, Terrain terrain)
        {
            float terrainY = float.MinValue;
            if (terrain != null)
                terrainY = terrain.transform.position.y + terrain.SampleHeight(new Vector3(fX, 0, fZ));

            float collisionY = GetHeight(fX, fZ);

            // İkisinden yüksek olanı kullan
            return Mathf.Max(terrainY, collisionY);
        }

        #endregion
    }
}
