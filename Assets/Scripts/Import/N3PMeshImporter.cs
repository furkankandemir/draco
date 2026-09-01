using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 N3PMesh (Progressive Mesh) Binary Parser
    /// 
    /// CN3PMesh::Load() birebir portu.
    /// 
    /// .N3PMesh dosya formatı:
    ///   CN3BaseFileAccess::Load:
    ///     int32: nameLen
    ///     char[nameLen]: name
    ///   int32: numCollapses
    ///   int32: totalIndexChanges
    ///   int32: maxNumVertices
    ///   int32: maxNumIndices
    ///   int32: minNumVertices
    ///   int32: minNumIndices
    ///   __VertexT1[maxNumVertices]:
    ///     Vector3 position (12 bytes)
    ///     Vector3 normal (12 bytes)
    ///     float tu, tv (8 bytes)
    ///     = 32 bytes per vertex
    ///   uint16[maxNumIndices]: index buffer
    ///   __EdgeCollapse[numCollapses]: LOD collapse data (24 bytes each)
    ///   int32[totalIndexChanges]: index change map
    ///   int32: lodCtrlValueCount
    ///   __LODCtrlValue[lodCtrlValueCount]: (8 bytes each)
    /// </summary>
    public static class N3PMeshImporter
    {
        // __VertexT1: Vec3 pos(12) + Vec3 normal(12) + float tu(4) + float tv(4) = 32 bytes
        private const int SIZEOF_VERTEX_T1 = 32;

        // __EdgeCollapse: 5x int32 + bool (padded to 24 bytes)
        private const int SIZEOF_EDGE_COLLAPSE = 24;

        // __LODCtrlValue: float + int = 8 bytes
        private const int SIZEOF_LOD_CTRL = 8;

        /// <summary>
        /// Parse edilmiş mesh verileri.
        /// </summary>
        public class N3PMeshData
        {
            public string Name;
            public int MaxNumVertices;
            public int MaxNumIndices;
            public int MinNumVertices;
            public int MinNumIndices;

            // Vertex data
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector2[] UVs;

            // Index data (triangle list) — max LOD'a expand edilmiş
            public int[] Indices;

            // LOD Runtime Data (Open-KO CN3PMeshInstance birebir)
            public int[] MinLODIndices;       // Split öncesi index buffer kopyası
            public EdgeCollapseData[] Collapses;
            public int[] AllIndexChanges;
            public LODCtrlValueData[] LODCtrlValues;
        }

        // Open-KO CN3PMesh::__EdgeCollapse birebir (N3PMesh.h:27-38)
        public struct EdgeCollapseData
        {
            public int NumIndicesToLose;
            public int NumIndicesToChange;
            public int NumVerticesToLose;
            public int IndexChangesOffset;
            public int CollapseTo;
            public bool ShouldCollapse;
        }

        // Open-KO CN3PMesh::__LODCtrlValue birebir (N3PMesh.h:20-24)
        public struct LODCtrlValueData
        {
            public float Dist;
            public int NumVertices;
        }

        // Mesh dosya cache'i — aynı mesh birden fazla shape tarafından referans edilebilir
        private static readonly Dictionary<string, N3PMeshData> _cache = new();

        /// <summary>
        /// .N3PMesh dosyasını parse ederek Unity-uyumlu mesh verisi döndürür.
        /// Dosya bulunamazsa null döner.
        /// </summary>
        public static N3PMeshData Load(string meshPath)
        {
            // Normalize path
            string normalizedPath = meshPath.Replace('\\', '/').ToLowerInvariant();

            // Cache kontrolü
            if (_cache.TryGetValue(normalizedPath, out var cached))
                return cached;

            try
            {
                var data = ParseFile(meshPath);
                if (data != null)
                    _cache[normalizedPath] = data;
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3PMesh] Parse hatası ({Path.GetFileName(meshPath)}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// CN3PMesh::Load() birebir portu — dosyadan.
        /// </summary>
        private static N3PMeshData ParseFile(string path)
        {
            using var reader = KOBinaryProvider.OpenReader(path);
            if (reader == null) return null;
            return LoadFromReader(reader);
        }

        /// <summary>
        /// CN3PMesh::Load() birebir portu — BinaryReader'dan.
        /// CN3CPlug::Load (N3Chr.cpp:648-650) inline PMesh için gerekli.
        /// </summary>
        public static N3PMeshData LoadFromReader(BinaryReader reader)
        {
            var data = new N3PMeshData();

            // ============================================
            // CN3BaseFileAccess::Load: name
            // ============================================
            int nameLen = reader.ReadInt32();
            if (nameLen > 0 && nameLen < 256)
            {
                byte[] nameBytes = reader.ReadBytes(nameLen);
                data.Name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
            }
            else if (nameLen < 0 || nameLen >= 256)
            {
                Debug.LogWarning($"[N3PMesh] Geçersiz name length: {nameLen}");
                return null;
            }

            // ============================================
            // Progressive mesh header
            // ============================================
            int numCollapses = reader.ReadInt32();
            int totalIndexChanges = reader.ReadInt32();
            data.MaxNumVertices = reader.ReadInt32();
            data.MaxNumIndices = reader.ReadInt32();
            data.MinNumVertices = reader.ReadInt32();
            data.MinNumIndices = reader.ReadInt32();

            // Doğrulama
            if (data.MaxNumVertices < 0 || data.MaxNumVertices > 100000 ||
                data.MaxNumIndices < 0 || data.MaxNumIndices > 300000)
            {
                Debug.LogWarning($"[N3PMesh] Geçersiz vertex/index sayısı: " +
                                 $"V={data.MaxNumVertices} I={data.MaxNumIndices}");
                return null;
            }

            // ============================================
            // VERTEX DATA: __VertexT1[maxNumVertices]
            // Her vertex = 32 bytes: Vec3 pos(12) + Vec3 normal(12) + float tu(4) + float tv(4)
            // ============================================
            if (data.MaxNumVertices > 0)
            {
                data.Positions = new Vector3[data.MaxNumVertices];
                data.Normals = new Vector3[data.MaxNumVertices];
                data.UVs = new Vector2[data.MaxNumVertices];

                for (int i = 0; i < data.MaxNumVertices; i++)
                {
                    // Position
                    float px = reader.ReadSingle();
                    float py = reader.ReadSingle();
                    float pz = reader.ReadSingle();
                    data.Positions[i] = new Vector3(px, py, pz);

                    // Normal
                    float nx = reader.ReadSingle();
                    float ny = reader.ReadSingle();
                    float nz = reader.ReadSingle();
                    data.Normals[i] = new Vector3(nx, ny, nz);

                    // UV
                    float tu = reader.ReadSingle();
                    float tv = reader.ReadSingle();
                    data.UVs[i] = new Vector2(tu, tv);
                }
            }

            // ============================================
            // INDEX DATA: uint16[maxNumIndices]
            // ============================================
            if (data.MaxNumIndices > 0)
            {
                data.Indices = new int[data.MaxNumIndices];
                for (int i = 0; i < data.MaxNumIndices; i++)
                {
                    data.Indices[i] = reader.ReadUInt16();
                }
            }

            // ============================================
            // COLLAPSE DATA: __EdgeCollapse[numCollapses]
            // CN3PMeshInstance::SplitOne() birebir portu.
            // Dosyadaki index buffer sadece minNumIndices için geçerli!
            // Max LOD için tüm split'leri uygulamalıyız.
            // __EdgeCollapse = { NumIndicesToLose(4), NumIndicesToChange(4),
            //   NumVerticesToLose(4), iIndexChanges(4), CollapseTo(4),
            //   bShouldCollapse(1) + padding(3) } = 24 bytes
            // ============================================
            if (numCollapses > 0)
            {
                data.Collapses = new EdgeCollapseData[numCollapses];
                for (int i = 0; i < numCollapses; i++)
                {
                    data.Collapses[i].NumIndicesToLose = reader.ReadInt32();
                    data.Collapses[i].NumIndicesToChange = reader.ReadInt32();
                    data.Collapses[i].NumVerticesToLose = reader.ReadInt32();
                    data.Collapses[i].IndexChangesOffset = reader.ReadInt32();
                    data.Collapses[i].CollapseTo = reader.ReadInt32();
                    data.Collapses[i].ShouldCollapse = reader.ReadInt32() != 0;
                }
            }

            // ============================================
            // INDEX CHANGES: int32[totalIndexChanges]
            // ============================================
            if (totalIndexChanges > 0)
            {
                data.AllIndexChanges = new int[totalIndexChanges];
                for (int i = 0; i < totalIndexChanges; i++)
                {
                    data.AllIndexChanges[i] = reader.ReadInt32();
                }
            }

            // ============================================
            // PROGRESSIVE MESH SPLIT: min LOD → max LOD
            // CN3PMeshInstance::SplitOne() birebir portu
            // ============================================
            // Split öncesi index buffer'ı sakla (LOD runtime için)
            if (data.Indices != null)
                data.MinLODIndices = (int[])data.Indices.Clone();

            // Min LOD → Max LOD: tüm split'leri uygula (CN3PMeshInstance::SplitOne birebir)
            if (numCollapses > 0 && data.Indices != null && data.AllIndexChanges != null)
            {
                int currentNumVertices = data.MinNumVertices;
                int currentNumIndices = data.MinNumIndices;

                for (int c = 0; c < numCollapses; c++)
                {
                    currentNumIndices += data.Collapses[c].NumIndicesToLose;
                    currentNumVertices += data.Collapses[c].NumVerticesToLose;

                    int changeStart = data.Collapses[c].IndexChangesOffset;
                    int changeCount = data.Collapses[c].NumIndicesToChange;
                    for (int ic = changeStart; ic < changeStart + changeCount && ic < totalIndexChanges; ic++)
                    {
                        int indexSlot = data.AllIndexChanges[ic];
                        if (indexSlot >= 0 && indexSlot < data.Indices.Length)
                        {
                            data.Indices[indexSlot] = currentNumVertices - 1;
                        }
                    }
                }
            }

            // ============================================
            // LOD CTRL VALUES: int32 count + __LODCtrlValue[count] (8 bytes each)
            // ============================================
            int lodCtrlCount = reader.ReadInt32();
            if (lodCtrlCount > 0)
            {
                data.LODCtrlValues = new LODCtrlValueData[lodCtrlCount];
                for (int i = 0; i < lodCtrlCount; i++)
                {
                    data.LODCtrlValues[i].Dist = reader.ReadSingle();
                    data.LODCtrlValues[i].NumVertices = reader.ReadInt32();
                }
            }

            return data;
        }

        /// <summary>
        /// Parse edilmiş N3PMesh verisinden Unity Mesh oluşturur.
        /// </summary>
        public static Mesh CreateUnityMesh(N3PMeshData data)
        {
            if (data == null || data.Positions == null || data.Positions.Length == 0)
                return null;

            var mesh = new Mesh();
            mesh.name = data.Name ?? "N3PMesh";

            mesh.vertices = data.Positions;

            if (data.Normals != null && data.Normals.Length == data.Positions.Length)
                mesh.normals = data.Normals;

            if (data.UVs != null && data.UVs.Length == data.Positions.Length)
                mesh.uv = data.UVs;

            if (data.Indices != null && data.Indices.Length >= 3)
            {
                // Index doğrulama — vertex sınırı dışına çıkanları filtrele
                var validIndices = new List<int>(data.Indices.Length);
                for (int i = 0; i + 2 < data.Indices.Length; i += 3)
                {
                    int i0 = data.Indices[i];
                    int i1 = data.Indices[i + 1];
                    int i2 = data.Indices[i + 2];

                    if (i0 >= 0 && i0 < data.Positions.Length &&
                        i1 >= 0 && i1 < data.Positions.Length &&
                        i2 >= 0 && i2 < data.Positions.Length)
                    {
                        validIndices.Add(i0);
                        validIndices.Add(i1);
                        validIndices.Add(i2);
                    }
                }

                mesh.triangles = validIndices.ToArray();
            }

            // Normal yoksa veya eksikse yeniden hesapla
            if (mesh.normals == null || mesh.normals.Length == 0)
                mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Cache'i temizle (zone değişiminde).
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
