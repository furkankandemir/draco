using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 OPD (Object Placement Data) Parser
    /// 
    /// N3ShapeMgr::LoadCollisionData() birebir portu.
    /// 
    /// OPD'den okunan veriler:
    /// 1. Map boyutu (width/length metre)
    /// 2. Collision mesh vertex'leri (dünya koordinatlarında üçgenler)
    /// 3. Her CellMain'deki shape indeksleri
    /// 
    /// Tam N3Shape mesh import yerine, collision üçgenlerinden
    /// obje AABB'leri (bounding box) çıkarılır ve placeholder olarak yerleştirilir.
    /// 
    /// Binary Layout:
    ///   Header (v1264+):
    ///     int32: version, int32: nameLength, char[]: name
    ///   CollisionData:
    ///     float: mapWidth
    ///     float: mapLength
    ///     int32: collisionFaceCount
    ///     Vector3[collisionFaceCount * 3]: collision vertices
    ///     CellMain grid (shape references + subcell collision indices)
    /// </summary>
    public static class OpdObjectImporter
    {
        private const int CELL_MAIN_DIVIDE = 4;
        private const int CELL_SUB_SIZE = 4;
        private const int CELL_MAIN_SIZE = CELL_MAIN_DIVIDE * CELL_SUB_SIZE; // 16m
        private const int MAX_CELL_MAIN = 4096 / CELL_MAIN_SIZE; // 256

        /// <summary>
        /// Collision'dan türetilen obje bounding box bilgisi.
        /// </summary>
        public class ObjectBounds
        {
            public Vector3 Min;
            public Vector3 Max;
            public Vector3 Center => (Min + Max) * 0.5f;
            public Vector3 Size => Max - Min;
            public int ShapeIndex; // orijinal shape referansı
        }

        /// <summary>
        /// OPD parse sonucu.
        /// </summary>
        public class OpdData
        {
            public float MapWidth;
            public float MapLength;
            public int CollisionFaceCount;
            public Vector3[] CollisionVertices; // [faceCount * 3]
            public List<ObjectBounds> Objects;  // collision'dan türetilen obje sınırları
        }

        /// <summary>
        /// .opd dosyasından collision verilerini ve shape bilgilerini parse eder.
        /// </summary>
        public static OpdData Parse(string opdPath)
        {
            if (!KOBinaryProvider.Exists(opdPath))
            {
                Debug.LogError($"[OPD] Dosya bulunamadı: {opdPath}");
                return null;
            }

            var data = new OpdData();

            using var stream = File.OpenRead(opdPath);
            using var reader = new BinaryReader(stream);

            // ============================================
            // HEADER (v1264+ format — aynı GTD gibi)
            // ============================================
            TrySkipHeader(reader);

            // ============================================
            // COLLISION DATA (N3ShapeMgr::LoadCollisionData)
            // ============================================
            data.MapWidth = reader.ReadSingle();
            data.MapLength = reader.ReadSingle();

            if (data.MapWidth <= 0 || data.MapLength <= 0 ||
                data.MapWidth > MAX_CELL_MAIN * CELL_MAIN_SIZE ||
                data.MapLength > MAX_CELL_MAIN * CELL_MAIN_SIZE)
            {
                Debug.LogError($"[OPD] Geçersiz map boyutu: {data.MapWidth}x{data.MapLength}");
                return null;
            }


            // Collision polygons
            data.CollisionFaceCount = reader.ReadInt32();

            if (data.CollisionFaceCount > 0 && data.CollisionFaceCount < 500000)
            {
                data.CollisionVertices = new Vector3[data.CollisionFaceCount * 3];
                for (int i = 0; i < data.CollisionFaceCount * 3; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    data.CollisionVertices[i] = new Vector3(x, y, z);
                }
            }
            else
            {
                data.CollisionVertices = Array.Empty<Vector3>();
            }

            // ============================================
            // CELL DATA (shape referansları + subcell collision)
            // ============================================
            int mapCellsX = (int)(data.MapWidth / CELL_MAIN_SIZE);
            int mapCellsZ = (int)(data.MapLength / CELL_MAIN_SIZE);

            // Hücrelerdeki shape index'lerini topla
            var shapePositions = new Dictionary<int, List<Vector3>>();

            try
            {
                for (int z = 0; z < mapCellsZ; z++)
                {
                    for (int x = 0; x < mapCellsX; x++)
                    {
                        int exists = reader.ReadInt32();
                        if (exists == 0) continue;

                        // CellMain::Load
                        int shapeCount = reader.ReadInt32();
                        if (shapeCount > 0 && shapeCount < 10000)
                        {
                            for (int s = 0; s < shapeCount; s++)
                            {
                                ushort shapeIdx = reader.ReadUInt16();
                                
                                // Bu shape'in bulunduğu hücrenin merkez pozisyonunu kaydet
                                float cx = (x + 0.5f) * CELL_MAIN_SIZE;
                                float cz = (z + 0.5f) * CELL_MAIN_SIZE;

                                if (!shapePositions.ContainsKey(shapeIdx))
                                    shapePositions[shapeIdx] = new List<Vector3>();
                                shapePositions[shapeIdx].Add(new Vector3(cx, 0, cz));
                            }
                        }

                        // SubCells [4x4]
                        for (int sz = 0; sz < CELL_MAIN_DIVIDE; sz++)
                        {
                            for (int sx = 0; sx < CELL_MAIN_DIVIDE; sx++)
                            {
                                int ccPolyCount = reader.ReadInt32();
                                if (ccPolyCount > 0)
                                {
                                    // uint32_t[ccPolyCount * 3] — vertex indices
                                    stream.Seek(ccPolyCount * 3 * 4, SeekOrigin.Current);
                                }
                            }
                        }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                Debug.LogWarning("[OPD] Cell data sonu — devam ediliyor.");
            }

            // ============================================
            // COLLISION MESH'LERDEN OBJE AABB TÜRETİMİ
            // ============================================
            data.Objects = ExtractObjectBounds(data.CollisionVertices, data.CollisionFaceCount);


            return data;
        }

        /// <summary>
        /// Collision üçgenlerini gruplayarak obje sınırlarını (AABB) çıkarır.
        /// Bitişik/yakın üçgenleri aynı objeye atar.
        /// </summary>
        private static List<ObjectBounds> ExtractObjectBounds(
            Vector3[] vertices, int faceCount)
        {
            var objects = new List<ObjectBounds>();
            if (vertices == null || faceCount <= 0) return objects;

            // Basit yaklaşım: collision üçgenlerini hücre bazlı grupla
            var cellGroups = new Dictionary<long, ObjectBounds>();
            float gridSize = 16f; // CELL_MAIN_SIZE ile aynı

            for (int i = 0; i < faceCount; i++)
            {
                var v0 = vertices[i * 3];
                var v1 = vertices[i * 3 + 1];
                var v2 = vertices[i * 3 + 2];

                // Üçgen merkezi
                var center = (v0 + v1 + v2) / 3f;
                int cellX = (int)(center.x / gridSize);
                int cellZ = (int)(center.z / gridSize);
                long key = ((long)cellX << 32) | (uint)cellZ;

                if (!cellGroups.TryGetValue(key, out var bounds))
                {
                    bounds = new ObjectBounds
                    {
                        Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                        Max = new Vector3(float.MinValue, float.MinValue, float.MinValue),
                        ShapeIndex = i
                    };
                    cellGroups[key] = bounds;
                }

                // AABB genişlet
                ExpandBounds(bounds, v0);
                ExpandBounds(bounds, v1);
                ExpandBounds(bounds, v2);
            }

            // Çok küçük objeleri filtrele (zemin collision parçaları vs.)
            foreach (var bounds in cellGroups.Values)
            {
                var size = bounds.Size;
                // Minimum boyut filtresi: en az 1m³
                if (size.x >= 1f && size.z >= 1f && size.y >= 0.5f)
                {
                    objects.Add(bounds);
                }
            }

            return objects;
        }

        private static void ExpandBounds(ObjectBounds b, Vector3 v)
        {
            b.Min = Vector3.Min(b.Min, v);
            b.Max = Vector3.Max(b.Max, v);
        }

        private static void TrySkipHeader(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int nameLen = reader.ReadInt32();
            if (nameLen > 0)
                reader.BaseStream.Seek(nameLen, SeekOrigin.Current);
        }
    }
}
