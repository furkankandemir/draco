using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 Character Part & Animation Parsers
    /// 
    /// CN3CPart::Load, CN3CPartSkins::Load, CN3Skin::Load (CN3IMesh::Load),
    /// CN3AnimControl::Load birebir portları.
    /// 
    /// Kalıtım: CN3IMesh → CN3Skin → CN3CPartSkins(4x LOD)
    /// </summary>
    public static class N3CPartImporter
    {
        // MAX_CHR_LOD = 4
        private const int MAX_CHR_LOD = 4;

        // __Material = 92 bytes
        private const int SIZEOF_MATERIAL = 92;

        // __VertexXyzNormal = Vec3(12) + Vec3 normal(12) = 24 bytes
        private const int SIZEOF_VERTEX_XYZ_NORMAL = 24;

        #region Data Structures

        /// <summary>Tek bir LOD seviyesinin skin verisi.</summary>
        public class SkinLODData
        {
            // CN3IMesh verileri
            public int FaceCount;
            public int VertexCount;
            public int UVCount;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public int[] VtxIndices;   // face*3 index
            public float[] UVs;        // uvCount * 2 (u,v çiftleri)
            public int[] UVIndices;    // face*3 UV index

            // CN3Skin verileri (skinning weights)
            public SkinVertex[] SkinVertices;
        }

        /// <summary>Skinned vertex — joint ağırlıkları.</summary>
        public class SkinVertex
        {
            public Vector3 Origin;     // Orijinal pozisyon (bind pose)
            public int AffectCount;    // Etki eden joint sayısı
            public int[] JointIndices; // Joint index'leri
            public float[] Weights;    // Joint ağırlıkları
        }

        /// <summary>CPartSkins — 4 LOD seviyesi skin verisi.</summary>
        public class CPartSkinsData
        {
            public string Name;
            public SkinLODData[] LODs = new SkinLODData[MAX_CHR_LOD];
        }

        /// <summary>CPart — karakter parçası (kafa, gövde, bacak vb.).</summary>
        public class CPartData
        {
            public string Name;
            public uint Reserved;
            public Color Diffuse;    // __Material'dan sadece diffuse
            public uint RenderFlags;
            public string TextureFileName;  // .dxt referansı
            public string SkinsFileName;    // .n3cskins referansı
            public CPartSkinsData Skins;    // Parse edilmiş skin verisi
        }

        /// <summary>Animasyon verisi.</summary>
        public class AnimData
        {
            public string Name;
            public float FrmStart;
            public float FrmEnd;
            public float FrmPerSec;
            public float FrmPlugTraceStart;
            public float FrmPlugTraceEnd;
            public float FrmSound0;
            public float FrmSound1;
            public float TimeBlend;
            public int BlendFlags;
            public float FrmStrike0;
            public float FrmStrike1;
        }

        /// <summary>Animation controller — tüm animasyon tanımları.</summary>
        public class AnimControlData
        {
            public List<AnimData> Animations = new();
        }

        #endregion

        #region N3CPart Parser

        /// <summary>
        /// .N3CPart dosyasını parse eder.
        /// CN3CPart::Load() birebir portu.
        /// </summary>
        public static CPartData LoadCPart(string cpartPath)
        {
            try
            {
                using var reader = KOBinaryProvider.OpenReader(cpartPath);
                if (reader == null) return null;

                var data = new CPartData();

                // CN3BaseFileAccess::Load: name
                data.Name = ReadLenString(reader);

                // dwReserved (4 bytes)
                data.Reserved = reader.ReadUInt32();

                // __Material (92 bytes) — sadece diffuse ve renderflags'i oku
                data.Diffuse = ReadColorValue(reader);   // Diffuse (16 bytes)
                reader.BaseStream.Seek(48, SeekOrigin.Current); // Ambient+Specular+Emissive (48)
                reader.ReadSingle(); // Power (4)
                reader.ReadUInt32(); // ColorOp
                reader.ReadUInt32(); // ColorArg1
                reader.ReadUInt32(); // ColorArg2
                data.RenderFlags = reader.ReadUInt32();
                reader.ReadUInt32(); // SrcBlend
                reader.ReadUInt32(); // DestBlend

                // Texture filename
                data.TextureFileName = ReadLenString(reader);

                // Skins filename
                data.SkinsFileName = ReadLenString(reader);

                // Skins dosyasını yüklemeyi dene
                if (!string.IsNullOrEmpty(data.SkinsFileName))
                {
                    string dir = Path.GetDirectoryName(cpartPath);
                    string skinsPath = FindFile(dir, data.SkinsFileName);
                    if (skinsPath != null)
                    {
                        data.Skins = LoadCPartSkins(skinsPath);
                    }
                }

                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3CPart] Parse hatası: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region N3CPartSkins Parser

        /// <summary>
        /// .N3CSkins dosyasını parse eder.
        /// CN3CPartSkins::Load() birebir portu.
        /// Format: CN3BaseFileAccess::Load + CN3Skin::Load x MAX_CHR_LOD(4)
        /// </summary>
        public static CPartSkinsData LoadCPartSkins(string skinsPath)
        {
            try
            {
                using var reader = KOBinaryProvider.OpenReader(skinsPath);
                if (reader == null) return null;

                var data = new CPartSkinsData();

                // CN3BaseFileAccess::Load: name
                data.Name = ReadLenString(reader);

                // 4x LOD seviyeleri
                for (int i = 0; i < MAX_CHR_LOD; i++)
                {
                    data.LODs[i] = LoadSkin(reader);
                }

                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3CSkins] Parse hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// CN3Skin::Load() birebir portu.
        /// CN3IMesh::Load() → CN3Skin skin data
        /// </summary>
        private static SkinLODData LoadSkin(BinaryReader reader)
        {
            var skin = new SkinLODData();

            // ============================================
            // CN3IMesh::Load
            // ============================================

            // CN3BaseFileAccess::Load (IMesh name — genellikle boş)
            string meshName = ReadLenString(reader);

            // Face/vertex/UV counts
            skin.FaceCount = reader.ReadInt32();
            skin.VertexCount = reader.ReadInt32();
            skin.UVCount = reader.ReadInt32();

            if (skin.FaceCount > 0 && skin.VertexCount > 0)
            {
                // __VertexXyzNormal[nVC]: position(12) + normal(12) = 24 bytes
                skin.Positions = new Vector3[skin.VertexCount];
                skin.Normals = new Vector3[skin.VertexCount];
                for (int i = 0; i < skin.VertexCount; i++)
                {
                    skin.Positions[i] = ReadVector3(reader);
                    skin.Normals[i] = ReadVector3(reader);
                }

                // uint16[nFC * 3]: vertex indices
                int idxCount = skin.FaceCount * 3;
                skin.VtxIndices = new int[idxCount];
                for (int i = 0; i < idxCount; i++)
                {
                    skin.VtxIndices[i] = reader.ReadUInt16();
                }
            }

            // UV data
            if (skin.UVCount > 0 && skin.FaceCount > 0)
            {
                // float[nUVC * 2]: UV coordinates
                skin.UVs = new float[skin.UVCount * 2];
                for (int i = 0; i < skin.UVCount * 2; i++)
                {
                    skin.UVs[i] = reader.ReadSingle();
                }

                // uint16[nFC * 3]: UV indices
                int uvIdxCount = skin.FaceCount * 3;
                skin.UVIndices = new int[uvIdxCount];
                for (int i = 0; i < uvIdxCount; i++)
                {
                    skin.UVIndices[i] = reader.ReadUInt16();
                }
            }

            // ============================================
            // CN3Skin::Load (skinning data)
            // ============================================
            if (skin.VertexCount > 0)
            {
                skin.SkinVertices = new SkinVertex[skin.VertexCount];
                for (int i = 0; i < skin.VertexCount; i++)
                {
                    var sv = new SkinVertex();

                    // vOrigin (Vector3 = 12 bytes)
                    sv.Origin = ReadVector3(reader);

                    // nAffect (int = 4 bytes)
                    sv.AffectCount = reader.ReadInt32();

                    // Skip 2x unused 32-bit pointers (pnJoints, pfWeights)
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    if (sv.AffectCount > 1)
                    {
                        // Joint indices (int32[nAffect])
                        sv.JointIndices = new int[sv.AffectCount];
                        for (int j = 0; j < sv.AffectCount; j++)
                            sv.JointIndices[j] = reader.ReadInt32();

                        // Weights (float[nAffect])
                        sv.Weights = new float[sv.AffectCount];
                        for (int j = 0; j < sv.AffectCount; j++)
                            sv.Weights[j] = reader.ReadSingle();
                    }
                    else if (sv.AffectCount == 1)
                    {
                        sv.JointIndices = new int[1];
                        sv.JointIndices[0] = reader.ReadInt32();
                        sv.Weights = new float[] { 1.0f };
                    }

                    skin.SkinVertices[i] = sv;
                }
            }

            return skin;
        }

        /// <summary>
        /// SkinLODData'dan Unity Mesh oluşturur.
        /// IMesh'in indexed vertex + separate UV index yapısını
        /// Unity'nin unified vertex formatına dönüştürür.
        /// </summary>
        public static Mesh CreateUnityMesh(SkinLODData skin)
        {
            if (skin == null || skin.FaceCount <= 0 || skin.VertexCount <= 0)
                return null;

            int triCount = skin.FaceCount * 3;
            var positions = new Vector3[triCount];
            var normals = new Vector3[triCount];
            var uvs = new Vector2[triCount];
            var indices = new int[triCount];

            bool hasUV = (skin.UVCount > 0 && skin.UVs != null && skin.UVIndices != null);

            for (int i = 0; i < triCount; i++)
            {
                int vIdx = (skin.VtxIndices != null && i < skin.VtxIndices.Length)
                    ? skin.VtxIndices[i] : 0;

                if (vIdx >= 0 && vIdx < skin.VertexCount)
                {
                    positions[i] = skin.Positions[vIdx];
                    normals[i] = skin.Normals[vIdx];
                }

                if (hasUV && i < skin.UVIndices.Length)
                {
                    int uvIdx = skin.UVIndices[i];
                    if (uvIdx >= 0 && uvIdx < skin.UVCount)
                    {
                        // D3D→Unity: V flip (1-v) — BuildSkinnedMeshFromSkin ile aynı
                        uvs[i] = new Vector2(
                            skin.UVs[uvIdx * 2],
                            1.0f - skin.UVs[uvIdx * 2 + 1]);
                    }
                }

                indices[i] = i;
            }

            var mesh = new Mesh();
            mesh.name = "N3Skin";
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateBounds();

            return mesh;
        }

        #endregion

        #region N3AnimControl Parser

        /// <summary>
        /// .N3Anim dosyasını parse eder.
        /// CN3AnimControl dosya formatı:
        ///   CN3BaseFileAccess header (LoadFromFile → LoadSupportedVersions → Load)
        ///   → CN3AnimControl::Load: int32 count + __AnimData[count]
        /// </summary>
        public static AnimControlData LoadAnimControl(string animPath)
        {
            try
            {
                using var reader = KOBinaryProvider.OpenReader(animPath);
                if (reader == null) return null;

                var data = new AnimControlData();

                // N3BaseFileAccess dosya header'ı yok — doğrudan Load çağrılıyor
                // Ama LoadFromFile → Load zincirinde CN3BaseFileAccess::Load'ın
                // çağrılıp çağrılmadığı belirsiz. AnimControl, Load'ı override ediyor
                // ve CN3BaseFileAccess::Load'ı çağırmıyor!

                // __AnimData count
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    var anim = new AnimData();

                    // int32 nL (eski string pointer yeri — uyumluluk)
                    reader.ReadInt32();

                    anim.FrmStart = reader.ReadSingle();
                    anim.FrmEnd = reader.ReadSingle();
                    anim.FrmPerSec = reader.ReadSingle();

                    anim.FrmPlugTraceStart = reader.ReadSingle();
                    anim.FrmPlugTraceEnd = reader.ReadSingle();

                    anim.FrmSound0 = reader.ReadSingle();
                    anim.FrmSound1 = reader.ReadSingle();

                    anim.TimeBlend = reader.ReadSingle();
                    anim.BlendFlags = reader.ReadInt32();

                    anim.FrmStrike0 = reader.ReadSingle();
                    anim.FrmStrike1 = reader.ReadSingle();

                    // Animation name
                    anim.Name = ReadLenString(reader);

                    data.Animations.Add(anim);
                }


                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3Anim] Parse hatası: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helpers

        private static string ReadLenString(BinaryReader reader)
        {
            int len = reader.ReadInt32();
            if (len <= 0) return string.Empty;
            if (len > 512) return string.Empty;
            byte[] bytes = reader.ReadBytes(len);
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            return System.Text.Encoding.ASCII.GetString(
                bytes, 0, nullIdx >= 0 ? nullIdx : len).Trim();
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static Color ReadColorValue(BinaryReader reader)
        {
            return new Color(reader.ReadSingle(), reader.ReadSingle(),
                             reader.ReadSingle(), reader.ReadSingle());
        }

        /// <summary>KO path normalizasyonu ile dosya bul.</summary>
        private static string FindFile(string baseDir, string koPath)
        {
            string normalized = koPath.Replace('\\', '/');
            string direct = Path.Combine(baseDir, Path.GetFileName(normalized));
            if (KOBinaryProvider.Exists(direct)) return direct;

            string full = Path.Combine(baseDir, normalized);
            if (KOBinaryProvider.Exists(full)) return full;

            // Sadece dosya adıyla dene
            if (KOBinaryProvider.Exists(normalized)) return normalized;

            return null;
        }

        #endregion
    }
}
