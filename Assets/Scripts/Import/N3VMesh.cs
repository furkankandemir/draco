using System;
using System.IO;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: CN3VMesh (N3VMesh.h/N3VMesh.cpp)
    /// Collision mesh — vertex + index dizileri ile line-segment intersection testi.
    ///
    /// Binary format (.n3vmesh):
    ///   1. int nL (4 byte) — name length (CN3BaseFileAccess::Load)
    ///   2. char[nL] — name string
    ///   3. int nVC (4 byte) — vertex count
    ///   4. Vector3[nVC] — vertices (her biri 12 byte = x,y,z float)
    ///   5. int nIC (4 byte) — index count
    ///   6. ushort[nIC] — indices (her biri 2 byte)
    ///
    /// Referans:
    ///   CN3VMesh::Load — N3VMesh.cpp:51-73
    ///   CN3VMesh::CheckCollision — N3VMesh.cpp:277-377
    ///   CN3VMesh::FindMinMax — N3VMesh.cpp:246-275
    /// </summary>
    public class N3VMesh
    {
        // N3VMesh.h:17-21
        public Vector3[] Vertices;  // m_pVertices
        public int VertexCount;     // m_nVC
        public ushort[] Indices;    // m_pwIndices
        public int IndexCount;      // m_nIC

        // N3VMesh.h:23-25
        public Vector3 VMin;        // m_vMin
        public Vector3 VMax;        // m_vMax
        public float Radius;        // m_fRadius

        public string Name;         // m_szName

        /// <summary>
        /// Open-KO birebir: CN3VMesh::Load (N3VMesh.cpp:51-73)
        /// Binary stream'den collision mesh yükle.
        /// </summary>
        public bool Load(BinaryReader br)
        {
            // CN3BaseFileAccess::Load (N3BaseFileAccess.cpp:49-68) — name header
            int nL = br.ReadInt32();
            if (nL < 0 || nL > 256) return false;
            if (nL > 0)
                Name = new string(br.ReadChars(nL));
            else
                Name = "";

            // cpp:56 — int nVC
            int nVC = br.ReadInt32();
            if (nVC > 0)
            {
                // cpp:59-60 — CreateVertices + Read
                VertexCount = nVC;
                Vertices = new Vector3[nVC];
                for (int i = 0; i < nVC; i++)
                {
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    Vertices[i] = new Vector3(x, y, z);
                }
            }
            else
            {
                VertexCount = 0;
                Vertices = null;
            }

            // cpp:63 — int nIC
            int nIC = br.ReadInt32();
            if (nIC > 0)
            {
                // cpp:66-67 — CreateIndex + Read
                IndexCount = nIC;
                Indices = new ushort[nIC];
                for (int i = 0; i < nIC; i++)
                    Indices[i] = br.ReadUInt16();
            }
            else
            {
                IndexCount = 0;
                Indices = null;
            }

            // cpp:70 — FindMinMax
            FindMinMax();
            return true;
        }

        /// <summary>
        /// Open-KO birebir: CN3VMesh::FindMinMax (N3VMesh.cpp:246-275)
        /// Vertex'lerden min/max ve radius hesapla.
        /// </summary>
        public void FindMinMax()
        {
            // cpp:248-250
            VMin = Vector3.zero;
            VMax = Vector3.zero;
            Radius = 0;

            if (VertexCount <= 0 || Vertices == null) return;

            // cpp:255-256
            VMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            VMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            // cpp:257-271
            for (int i = 0; i < VertexCount; i++)
            {
                if (Vertices[i].x < VMin.x) VMin.x = Vertices[i].x;
                if (Vertices[i].y < VMin.y) VMin.y = Vertices[i].y;
                if (Vertices[i].z < VMin.z) VMin.z = Vertices[i].z;
                if (Vertices[i].x > VMax.x) VMax.x = Vertices[i].x;
                if (Vertices[i].y > VMax.y) VMax.y = Vertices[i].y;
                if (Vertices[i].z > VMax.z) VMax.z = Vertices[i].z;
            }

            // cpp:274
            Radius = (VMax - VMin).magnitude * 0.5f;
        }

        /// <summary>
        /// Open-KO birebir: CN3VMesh::CreateCube (N3VMesh.cpp:140-193)
        /// Verilen min/max noktalarından AABB collision küpü oluşturur.
        /// CN3Chr::RegenerateCollisionMesh tarafından çağrılır.
        /// 8 vertex, 36 index (12 triangle = 6 face × 2 tri).
        /// </summary>
        public void CreateCube(Vector3 vMin, Vector3 vMax)
        {
            // cpp:142-143
            VertexCount = 8;
            Vertices = new Vector3[8];
            IndexCount = 36;
            Indices = new ushort[36];

            // cpp:145-153
            Vertices[0] = new Vector3(vMin.x, vMax.y, vMin.z);
            Vertices[1] = new Vector3(vMax.x, vMax.y, vMin.z);
            Vertices[2] = new Vector3(vMax.x, vMin.y, vMin.z);
            Vertices[3] = new Vector3(vMin.x, vMin.y, vMin.z);
            Vertices[4] = new Vector3(vMax.x, vMax.y, vMax.z);
            Vertices[5] = new Vector3(vMin.x, vMax.y, vMax.z);
            Vertices[6] = new Vector3(vMin.x, vMin.y, vMax.z);
            Vertices[7] = new Vector3(vMax.x, vMin.y, vMax.z);

            // cpp:155-190
            Indices[0]  = 0; Indices[1]  = 1; Indices[2]  = 2;
            Indices[3]  = 0; Indices[4]  = 2; Indices[5]  = 3;
            Indices[6]  = 1; Indices[7]  = 4; Indices[8]  = 7;
            Indices[9]  = 1; Indices[10] = 7; Indices[11] = 2;
            Indices[12] = 4; Indices[13] = 5; Indices[14] = 6;
            Indices[15] = 4; Indices[16] = 6; Indices[17] = 7;
            Indices[18] = 5; Indices[19] = 0; Indices[20] = 3;
            Indices[21] = 5; Indices[22] = 3; Indices[23] = 6;
            Indices[24] = 5; Indices[25] = 4; Indices[26] = 1;
            Indices[27] = 5; Indices[28] = 1; Indices[29] = 0;
            Indices[30] = 3; Indices[31] = 2; Indices[32] = 7;
            Indices[33] = 3; Indices[34] = 7; Indices[35] = 6;

            // cpp:192
            FindMinMax();
        }

        /// <summary>
        /// Open-KO birebir: CN3VMesh::CheckCollision (N3VMesh.cpp:277-377)
        /// Line-segment (v0→v1) ile collision mesh triangle'ları arasında intersection testi.
        ///
        /// Mantık:
        ///   1. v0,v1'i world→local space'e dönüştür (mtxWorld inverse)
        ///   2. Her üçgen için: v0'dan vDir yönünde intersect edip, v1'den etmiyorsa → collision
        ///   3. En yakın collision noktasını bul, world space'e geri dönüştür
        ///   4. Hiç bulunamazsa: her iki nokta mesh içindeyse → true (cpp:350-376)
        /// </summary>
        public bool CheckCollision(Matrix4x4 mtxWorld, Vector3 v0, Vector3 v1,
            out Vector3 vCol)
        {
            vCol = Vector3.zero;

            // cpp:280-281
            if (VertexCount <= 0 || Vertices == null) return false;

            // cpp:288 — mtxWI = MtxWorld.Inverse()
            Matrix4x4 mtxWI = mtxWorld.inverse;

            // cpp:291-292 — rotation-only matrices (pozisyon sıfırlanmış)
            Matrix4x4 mtxRot = mtxWorld;
            mtxRot.SetColumn(3, new Vector4(0, 0, 0, 1));

            // cpp:294-296 — local space'e dönüştür
            Vector3 vPos0 = mtxWI.MultiplyPoint3x4(v0);
            Vector3 vPos1 = mtxWI.MultiplyPoint3x4(v1);
            Vector3 vDir = vPos1 - vPos0;

            // cpp:298-302 — face count
            int nFC;
            if (IndexCount > 0 && Indices != null)
                nFC = IndexCount / 3;
            else
                nFC = VertexCount / 3;

            // cpp:286
            float fDistClosest = float.MaxValue;
            bool found = false;

            // cpp:305-346 — her face için intersection testi
            for (int i = 0; i < nFC; i++)
            {
                int nCI0, nCI1, nCI2;
                // cpp:307-318
                if (IndexCount > 0 && Indices != null)
                {
                    nCI0 = Indices[i * 3 + 0];
                    nCI1 = Indices[i * 3 + 1];
                    nCI2 = Indices[i * 3 + 2];
                }
                else
                {
                    nCI0 = i * 3;
                    nCI1 = i * 3 + 1;
                    nCI2 = i * 3 + 2;
                }

                // cpp:320-323 — v0'dan vDir yönünde triangle'a intersect?
                if (!IntersectTriangleFull(vPos0, vDir,
                    Vertices[nCI0], Vertices[nCI1], Vertices[nCI2],
                    out float fT, out float fU, out float fV, out Vector3 vColTmp))
                    continue;

                // cpp:324-326 — v1'den de intersect ediyorsa → her iki uç triangle'ın aynı tarafında, skip
                if (IntersectTriangleSimple(vPos1, vDir,
                    Vertices[nCI0], Vertices[nCI1], Vertices[nCI2]))
                    continue;

                // cpp:328-344 — en yakın collision noktasını bul
                float fDistTmp = (vPos0 - vColTmp).magnitude;
                if (fDistTmp < fDistClosest)
                {
                    fDistClosest = fDistTmp;
                    // cpp:334 — collision noktasını world space'e dönüştür
                    vCol = mtxWorld.MultiplyPoint3x4(vColTmp);
                    found = true;
                }
            }

            // cpp:347-348
            if (found) return true;

            // cpp:350-376 — iki nokta da mesh içindeyse
            for (int i = 0; i < nFC; i++)
            {
                int nCI0, nCI1, nCI2;
                if (IndexCount > 0 && Indices != null)
                {
                    nCI0 = Indices[i * 3 + 0];
                    nCI1 = Indices[i * 3 + 1];
                    nCI2 = Indices[i * 3 + 2];
                }
                else
                {
                    nCI0 = i * 3;
                    nCI1 = i * 3 + 1;
                    nCI2 = i * 3 + 2;
                }

                // cpp:368-371 — face normal ve plane distance
                Vector3 tmpNormal = Vector3.Cross(
                    Vertices[nCI1] - Vertices[nCI0],
                    Vertices[nCI2] - Vertices[nCI1]);
                float d = -(tmpNormal.x * Vertices[nCI0].x)
                        - (tmpNormal.y * Vertices[nCI0].y)
                        - (tmpNormal.z * Vertices[nCI0].z);

                // cpp:372-373 — v0 plane'ın önündeyse → mesh dışında
                if ((tmpNormal.x * vPos0.x + tmpNormal.y * vPos0.y + tmpNormal.z * vPos0.z + d) > 0)
                    return false;
            }

            // cpp:376 — tüm face'lerin arkasında → mesh içinde
            return true;
        }

        /// <summary>
        /// Open-KO birebir: _IntersectTriangle (tam versiyon) — MathUtils.cpp:84-146
        /// Ray-triangle intersection with barycentric coordinates and collision point.
        /// </summary>
        public static bool IntersectTriangleFull(Vector3 vOrig, Vector3 vDir,
            Vector3 v0, Vector3 v1, Vector3 v2,
            out float fT, out float fU, out float fV, out Vector3 vCol)
        {
            fT = 0; fU = 0; fV = 0;
            vCol = Vector3.zero;

            // cpp:88-89
            Vector3 vEdge1 = v1 - v0;
            Vector3 vEdge2 = v2 - v0;

            // cpp:93-98 — ilk determinant check (backface culling benzeri)
            Vector3 pVec = Vector3.Cross(vEdge1, vEdge2);
            float fDet = Vector3.Dot(pVec, vDir);
            if (fDet > -0.0001f) return false;

            // cpp:100-105 — asıl determinant
            pVec = Vector3.Cross(vDir, vEdge2);
            fDet = Vector3.Dot(vEdge1, pVec);
            if (fDet < 0.0001f) return false;

            // cpp:108 — distance from vert0 to ray origin
            Vector3 tVec = vOrig - v0;

            // cpp:111-113 — U parameter
            fU = Vector3.Dot(tVec, pVec);
            if (fU < 0.0f || fU > fDet) return false;

            // cpp:116-117 — prepare V
            Vector3 qVec = Vector3.Cross(tVec, vEdge1);

            // cpp:120-122 — V parameter
            fV = Vector3.Dot(vDir, qVec);
            if (fV < 0.0f || fU + fV > fDet) return false;

            // cpp:125-130 — scale parameters
            fT = Vector3.Dot(vEdge2, qVec);
            float fInvDet = 1.0f / fDet;
            fT *= fInvDet;
            fU *= fInvDet;
            fV *= fInvDet;

            // cpp:138-139 — collision point
            vCol = vOrig + (vDir * fT);

            // cpp:142-143 — t < 0 ise ray arkasında
            if (fT < 0.0f) return false;

            return true;
        }

        /// <summary>
        /// Open-KO birebir: _IntersectTriangle (basit versiyon) — MathUtils.cpp:148-199
        /// Ray-triangle intersection, sadece bool döner.
        /// </summary>
        public static bool IntersectTriangleSimple(Vector3 vOrig, Vector3 vDir,
            Vector3 v0, Vector3 v1, Vector3 v2)
        {
            // cpp:153-154
            Vector3 vEdge1 = v1 - v0;
            Vector3 vEdge2 = v2 - v0;

            // cpp:161-164 — ilk determinant check
            Vector3 pVec = Vector3.Cross(vEdge1, vEdge2);
            float fDet = Vector3.Dot(pVec, vDir);
            if (fDet > -0.0001f) return false;

            // cpp:168-173 — asıl determinant
            pVec = Vector3.Cross(vDir, vEdge2);
            fDet = Vector3.Dot(vEdge1, pVec);
            if (fDet < 0.0001f) return false;

            // cpp:176-181 — U parameter
            Vector3 tVec = vOrig - v0;
            float fU = Vector3.Dot(tVec, pVec);
            if (fU < 0.0f || fU > fDet) return false;

            // cpp:184-189 — V parameter
            Vector3 qVec = Vector3.Cross(tVec, vEdge1);
            float fV = Vector3.Dot(vDir, qVec);
            if (fV < 0.0f || fU + fV > fDet) return false;

            // cpp:192-196 — t parameter
            float fT = Vector3.Dot(vEdge2, qVec) / fDet;
            if (fT < 0.0f) return false;

            return true;
        }
    }
}
