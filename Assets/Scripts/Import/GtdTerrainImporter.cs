using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 GTD Terrain Binary Parser
    /// 
    /// N3Terrain.cpp Load() birebir portu.
    /// .gtd dosyasını okuyarak heightmap verilerini Unity TerrainData'ya dönüştürür.
    /// 
    /// Binary Layout (N3FORMAT_VER_1264+):
    ///   int32   version (0-2)
    ///   int32   nameLength
    ///   char[]  name
    ///   int32   ti_MapSize
    ///   MAPDATA[ti_MapSize * ti_MapSize]  (8 bytes each: float height + uint32 bitfield)
    ///   float[pat_MapSize * pat_MapSize]  middleY per patch
    ///   float[pat_MapSize * pat_MapSize]  radius per patch
    ///   byte[ti_MapSize * ti_MapSize]     grass attributes (skip)
    ///   char[260]                         grass filename (skip)
    ///   TileInfo...                       tile textures (parsed but optional)
    /// 
    /// Constants (N3TerrainDef.h):
    ///   TILE_SIZE       = 4.0f metres
    ///   PATCH_TILE_SIZE = 8
    /// </summary>
    public static class GtdTerrainImporter
    {
        // KO terrain constants
        private const float TILE_SIZE = 4.0f;
        private const int PATCH_TILE_SIZE = 8;
        private const int MAX_PATH_LEN = 260;

        /// <summary>
        /// GTD'den parse edilen raw heightmap verileri.
        /// </summary>
        /// <summary>
        /// Per-cell tile texture bilgisi.
        /// MAPDATA bitfield (N3TerrainDef.h:47-65) — MSVC LSB-first packing.
        /// </summary>
        public struct MapCellData
        {
            public bool IsTileFull; // bit 0  — tile tam dolu mu?
            public int Tex1Dir;     // bit 1-5  — 1. texture yönü (rotation/mirror)
            public int Tex2Dir;     // bit 6-10 — 2. texture yönü
            public int Tex1Idx;    // bit 11-20 — 1. tile texture indeksi
            public int Tex2Idx;    // bit 21-30 — 2. tile texture indeksi (blend)
        }

        public class GtdData
        {
            public string MapName;
            public int MapSize;           // ti_MapSize (e.g. 257, 513, 1025)
            public int Version;           // GTD version (0-2)
            public float[,] Heights;      // [x, z] = world height in metres
            public float MinHeight;
            public float MaxHeight;

            /// <summary>Terrain'in gerçek dünya boyutu (metre).</summary>
            public float WorldSize => (MapSize - 1) * TILE_SIZE;

            // Per-cell tile texture bilgisi (MAPDATA bitfield'dan)
            public MapCellData[,] CellData;

            // Tile texture bilgisi (opsiyonel)
            public List<TileTexInfo> TileTextures;
            public List<string> TileTexSources;

            // River verileri (CN3River::Load birebir)
            public List<RiverMeshData> Rivers = new List<RiverMeshData>();

            // Pond verileri (CN3Pond::Load birebir)
            public List<PondMeshData> Ponds = new List<PondMeshData>();
        }

        /// <summary>
        /// __VertexRiver / __VertexPond — aynı yapı, 44 byte.
        /// x,y,z(12) + nx,ny,nz(12) + diffuseColor(4) + u,v,u2,v2(16)
        /// </summary>
        public struct WaterVertex
        {
            public float x, y, z;
            public float nx, ny, nz;
            public uint diffuse;
            public float u, v, u2, v2;
        }

        public class RiverMeshData
        {
            public int VertexCount;        // iVC
            public int IndexCount;         // iIC
            public WaterVertex[] Vertices;
            public string TextureName;     // wave texture
        }

        public class PondMeshData
        {
            public int VertexCount;        // iVC
            public int WidthVertex;        // iWidthVtx
            public int HeightVertex;       // iVC / iWidthVtx
            public WaterVertex[] Vertices;
            public float WaveVariance;     // v2+ only
            public int IndexCount;         // iIC
            public string TextureName;     // wave texture
        }

        public struct TileTexInfo
        {
            public short SrcIdx;
            public short TileIdx;
        }

        /// <summary>
        /// .gtd binary dosyasını parse eder.
        /// N3Terrain::Load() birebir portu.
        /// </summary>
        public static GtdData Parse(string gtdPath)
        {
            using var reader = KOBinaryProvider.OpenReader(gtdPath);
            if (reader == null)
            {
                Debug.LogError($"[GTD] Dosya bulunamadı: {gtdPath}");
                return null;
            }

            var data = new GtdData();
            var stream = reader.BaseStream;

            // ============================================
            // HEADER (N3FORMAT_VER_1264+ format)
            // ============================================
            // Önce header ile dene, başarısız olursa headersız dene
            long startPos = stream.Position;
            int gtdVersion = 0;
            bool hasHeader = TryReadHeader(reader, data, out gtdVersion);
            data.Version = gtdVersion;

            if (!hasHeader)
            {
                // Header okunamadı — 1098 formatı olabilir, başa dön
                stream.Position = startPos;
                data.MapName = Path.GetFileNameWithoutExtension(gtdPath);
            }

            // ============================================
            // MAP SIZE
            // ============================================
            if (!hasHeader)
            {
                data.MapSize = reader.ReadInt32();
            }


            if (data.MapSize <= 0 || data.MapSize > 4097)
            {
                Debug.LogError($"[GTD] Geçersiz map size: {data.MapSize}");
                return null;
            }

            // ============================================
            // MAPDATA (height + tile bilgi)
            // ============================================
            // Her MAPDATA: float fHeight(4) + uint32 bitfield(4) = 8 byte
            int totalCells = data.MapSize * data.MapSize;
            data.Heights = new float[data.MapSize, data.MapSize];
            data.CellData = new MapCellData[data.MapSize, data.MapSize];
            data.MinHeight = float.MaxValue;
            data.MaxHeight = float.MinValue;

            for (int x = 0; x < data.MapSize; x++)
            {
                for (int z = 0; z < data.MapSize; z++)
                {
                    float height = reader.ReadSingle();
                    uint bitfield = reader.ReadUInt32();

                    // MAPDATA bitfield extract (N3TerrainDef.h:47-65)
                    // MSVC LSB-first: bIsTileFull(1) | Tex1Dir(5) | Tex2Dir(5) | Tex1Idx(10) | Tex2Idx(10)
                    data.CellData[x, z] = new MapCellData
                    {
                        IsTileFull = (bitfield & 0x1) != 0,
                        Tex1Dir    = (int)((bitfield >> 1) & 0x1F),
                        Tex2Dir    = (int)((bitfield >> 6) & 0x1F),
                        Tex1Idx    = (int)((bitfield >> 11) & 0x3FF),
                        Tex2Idx    = (int)((bitfield >> 21) & 0x3FF)
                    };

                    // FLT_MIN = uninitialized cell — NaN ile işaretle, ikinci geçişte doldurulacak
                    if (height <= -3.4e+37f)
                        height = float.NaN;

                    data.Heights[x, z] = height;

                    if (!float.IsNaN(height))
                    {
                        if (height < data.MinHeight) data.MinHeight = height;
                        if (height > data.MaxHeight) data.MaxHeight = height;
                    }
                }
            }

            // İkinci geçiş: NaN (FLT_MIN) hücreleri komşu ortalamalarıyla doldur
            // Bu, terrain'de derin çukurlar oluşmasını önler
            float fallbackHeight = (data.MinHeight + data.MaxHeight) * 0.5f;
            if (float.IsInfinity(fallbackHeight) || float.IsNaN(fallbackHeight))
                fallbackHeight = 0f;

            for (int x = 0; x < data.MapSize; x++)
            {
                for (int z = 0; z < data.MapSize; z++)
                {
                    if (!float.IsNaN(data.Heights[x, z])) continue;

                    // Komşu geçerli yüksekliklerin ortalamasını bul
                    float sum = 0f;
                    int count = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if (nx >= 0 && nx < data.MapSize && nz >= 0 && nz < data.MapSize
                                && !float.IsNaN(data.Heights[nx, nz]))
                            {
                                sum += data.Heights[nx, nz];
                                count++;
                            }
                        }
                    }
                    data.Heights[x, z] = count > 0 ? sum / count : fallbackHeight;
                }
            }


            // ============================================
            // PATCH DATA (middleY + radius per patch)
            // ============================================
            int floatsPerPatch = 2;
            int patMapSize = (data.MapSize - 1) / PATCH_TILE_SIZE;
            for (int x = 0; x < patMapSize; x++)
            {
                for (int z = 0; z < patMapSize; z++)
                {
                    float middleY = reader.ReadSingle(); // skip
                    float radius = reader.ReadSingle();  // skip
                    if (floatsPerPatch == 3)
                    {
                        reader.ReadSingle(); // skip third patch float
                    }
                }
            }

            // ============================================
            // GRASS DATA (skip)
            // ============================================
            if (stream.Position + totalCells + MAX_PATH_LEN <= stream.Length)
            {
                stream.Seek(totalCells, SeekOrigin.Current);  // grass attributes
                stream.Seek(MAX_PATH_LEN, SeekOrigin.Current); // grass filename
            }

            // ============================================
            // TILE TEXTURE INFO (opsiyonel)
            // ============================================
            data.TileTextures = new List<TileTexInfo>();
            data.TileTexSources = new List<string>();

            if (stream.Position + 4 <= stream.Length)
            {
                try
                {
                    int tileTexCount = reader.ReadInt32();
                    if (tileTexCount > 0 && tileTexCount < 4096)
                    {
                        int numTexSrc = reader.ReadInt32();
                        if (numTexSrc > 0 && numTexSrc < 1024)
                        {
                            int texPathLen = MAX_PATH_LEN;
                            for (int i = 0; i < numTexSrc; i++)
                            {
                                byte[] pathBytes = reader.ReadBytes(texPathLen);
                                int nullIdx = Array.IndexOf(pathBytes, (byte)0);
                                string path = System.Text.Encoding.ASCII.GetString(
                                    pathBytes, 0, nullIdx >= 0 ? nullIdx : texPathLen);
                                data.TileTexSources.Add(path.Trim());
                            }

                            for (int i = 0; i < tileTexCount; i++)
                            {
                                data.TileTextures.Add(new TileTexInfo
                                {
                                    SrcIdx = reader.ReadInt16(),
                                    TileIdx = reader.ReadInt16()
                                });
                            }
                        }
                    }
                }
                catch (EndOfStreamException)
                {
                    // OK — tile bilgisi opsiyonel
                }
            }

            // ============================================
            // LIGHTMAP COUNT (deprecated, always 0)
            // ============================================
            try
            {
                if (stream.Position + 4 <= stream.Length)
                {
                    int numLightMap = reader.ReadInt32();
                    // CN3Terrain.cpp:449 — always 0 in v1.298
                }

                // ============================================
                // RIVER DATA (CN3River::Load birebir)
                // ============================================
                ParseRiverData(reader, data);

                // ============================================
                // POND DATA (CN3Pond::Load birebir)
                // ============================================
                ParsePondData(reader, data, gtdVersion);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GTD] River/Pond verisi okunamadı: {ex.Message}");
            }


            return data;
        }

        /// <summary>
        /// v1264+ header okuma denemesi.
        /// Başarısız olursa false döner.
        /// </summary>
        private static bool TryReadHeader(BinaryReader reader, GtdData data, out int version)
        {
            version = 0;
            return TryReadHeaderInternal(reader, data, ref version);
        }

        private static bool TryReadHeaderInternal(BinaryReader reader, GtdData data, ref int version)
        {
            long startPos = reader.BaseStream.Position;
            try
            {
                // Try Name-First format: nameLen (Int32) + name (string) + version (Int32) + MapSize (Int32)
                int nameLen = reader.ReadInt32();
                if (nameLen >= 0 && nameLen <= 512)
                {
                    string name = "unnamed";
                    if (nameLen > 0)
                    {
                        byte[] nameBytes = reader.ReadBytes(nameLen);
                        name = System.Text.Encoding.ASCII.GetString(nameBytes).Trim('\0');
                    }
                    int ver = reader.ReadInt32();
                    if (ver >= 0 && ver <= 2)
                    {
                        int mapSize = reader.ReadInt32();
                        if (mapSize > 0 && mapSize <= 4097 && ((mapSize - 1) % 4) == 0)
                        {
                            // Match Name-First!
                            data.MapName = name;
                            version = ver;
                            data.MapSize = mapSize;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fall through
            }

            try
            {
                // Try Version-First format: version (Int32) + nameLen (Int32) + name (string) + MapSize (Int32)
                reader.BaseStream.Position = startPos;
                int ver = reader.ReadInt32();
                if (ver >= 0 && ver <= 2)
                {
                    int nameLen = reader.ReadInt32();
                    if (nameLen >= 0 && nameLen <= 512)
                    {
                        string name = "unnamed";
                        if (nameLen > 0)
                        {
                            byte[] nameBytes = reader.ReadBytes(nameLen);
                            name = System.Text.Encoding.ASCII.GetString(nameBytes).Trim('\0');
                        }
                        int mapSize = reader.ReadInt32();
                        if (mapSize > 0 && mapSize <= 4097 && ((mapSize - 1) % 4) == 0)
                        {
                            // Match Version-First!
                            data.MapName = name;
                            version = ver;
                            data.MapSize = mapSize;
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fall through
            }

            // Reset stream to startPos
            reader.BaseStream.Position = startPos;
            return false;
        }

        // ============================================
        // RIVER PARSE — CN3River::Load birebir
        // ============================================
        private static void ParseRiverData(BinaryReader reader, GtdData data)
        {
            int riverCount = reader.ReadInt32();
            if (riverCount <= 0) return;
            if (riverCount > 1024)
                throw new System.Exception($"[GTD] Invalid river count: {riverCount}");

            for (int r = 0; r < riverCount; r++)
            {
                var river = new RiverMeshData();

                river.VertexCount = reader.ReadInt32();
                if (river.VertexCount <= 0 || (river.VertexCount % 4) != 0)
                    throw new System.Exception($"[GTD] Invalid river vertex count: {river.VertexCount}");

                // __VertexRiver: 44 bytes each (x,y,z + nx,ny,nz + color + u,v,u2,v2)
                river.Vertices = ReadWaterVertices(reader, river.VertexCount);

                river.IndexCount = reader.ReadInt32();

                // Texture name
                int texNameLen = reader.ReadInt32();
                if (texNameLen > 0 && texNameLen <= 50)
                {
                    byte[] nameBytes = reader.ReadBytes(texNameLen);
                    river.TextureName = System.Text.Encoding.ASCII.GetString(nameBytes).Trim('\0');
                }

                data.Rivers.Add(river);
            }

        }

        // ============================================
        // POND PARSE — CN3Pond::Load birebir
        // ============================================
        private static void ParsePondData(BinaryReader reader, GtdData data, int gtdVersion)
        {
            int pondCount = reader.ReadInt32();
            if (pondCount <= 0) return;
            if (pondCount > 1024)
                throw new System.Exception($"[GTD] Invalid pond count: {pondCount}");

            for (int p = 0; p < pondCount; p++)
            {
                var pond = new PondMeshData();

                pond.VertexCount = reader.ReadInt32();
                if (pond.VertexCount <= 0) continue;

                pond.WidthVertex = reader.ReadInt32();
                pond.HeightVertex = pond.VertexCount / pond.WidthVertex;

                // Texture name
                int texNameLen = reader.ReadInt32();
                if (texNameLen > 0 && texNameLen <= 50)
                {
                    byte[] nameBytes = reader.ReadBytes(texNameLen);
                    pond.TextureName = System.Text.Encoding.ASCII.GetString(nameBytes).Trim('\0');
                }

                // __VertexPond: 44 bytes each (same as __VertexRiver)
                pond.Vertices = ReadWaterVertices(reader, pond.VertexCount);

                // WaveVariance: v2+ only
                pond.WaveVariance = 0.2f;
                if (gtdVersion >= 2)
                    pond.WaveVariance = reader.ReadSingle();

                // Index count
                pond.IndexCount = reader.ReadInt32();

                data.Ponds.Add(pond);
            }

        }

        // ============================================
        // WATER VERTEX OKUMA — 44 byte per vertex
        // ============================================
        private static WaterVertex[] ReadWaterVertices(BinaryReader reader, int count)
        {
            var vertices = new WaterVertex[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i].x = reader.ReadSingle();
                vertices[i].y = reader.ReadSingle();
                vertices[i].z = reader.ReadSingle();
                vertices[i].nx = reader.ReadSingle();
                vertices[i].ny = reader.ReadSingle();
                vertices[i].nz = reader.ReadSingle();
                vertices[i].diffuse = reader.ReadUInt32();
                vertices[i].u = reader.ReadSingle();
                vertices[i].v = reader.ReadSingle();
                vertices[i].u2 = reader.ReadSingle();
                vertices[i].v2 = reader.ReadSingle();
            }
            return vertices;
        }

        /// <summary>
        /// GtdData'yı Unity TerrainData'ya dönüştürür.
        /// </summary>
        public static TerrainData CreateTerrainData(GtdData gtd)
        {
            if (gtd == null) return null;

            var terrainData = new TerrainData();

            // Resolution: KO mapSize zaten 2^n+1 (257, 513, 1025)
            int resolution = Mathf.Clamp(gtd.MapSize, 33, 4097);
            terrainData.heightmapResolution = resolution;

            // KO koordinat sistemi birebir korunur:
            // Unity Terrain world Y = terrain.position.y + normalized * size.y
            // Hedef: world Y = koHeight
            // Çözüm: position.y = minHeight, size.y = maxHeight - minHeight
            //         normalized = (koHeight - minHeight) / (maxHeight - minHeight)
            float heightRange = gtd.MaxHeight - gtd.MinHeight;
            if (heightRange < 1f) heightRange = 1f;

            // size.y = height range (biraz buffer ekle ki sınır değerler clamp olmasın)
            float sizeY = heightRange + 2f;

            terrainData.size = new Vector3(
                gtd.WorldSize,    // x boyutu (metre)
                sizeY,            // y boyutu (metre) — tam KO height range
                gtd.WorldSize     // z boyutu (metre)
            );

            // Heightmap normalize: KO height → [0, 1]
            // Unity world Y = terrain.position.y + normalized * sizeY
            // terrain.position.y = minHeight - 1  (1m buffer)
            // normalized = (koHeight - (minHeight - 1)) / sizeY
            float terrainBaseY = gtd.MinHeight - 1f;
            float[,] heights = new float[resolution, resolution];

            for (int z = 0; z < resolution && z < gtd.MapSize; z++)
            {
                for (int x = 0; x < resolution && x < gtd.MapSize; x++)
                {
                    float koH = gtd.Heights[x, z];
                    heights[z, x] = Mathf.Clamp01((koH - terrainBaseY) / sizeY);
                }
            }

            terrainData.SetHeights(0, 0, heights);

            // TerrainLayer'lar WorldBuilder.ApplyTileTextures tarafından atanacak


            // terrainBaseY'yi static field'a kaydet — WorldBuilder terrain position.y olarak kullanacak
            LastTerrainBaseY = terrainBaseY;

            return terrainData;
        }

        /// <summary>
        /// Son oluşturulan terrain'in base Y pozisyonu.
        /// WorldBuilder bu değeri terrain.transform.position.y olarak kullanır.
        /// </summary>
        public static float LastTerrainBaseY { get; private set; } = 0f;

        /// <summary>
        /// Basit prosedürel çimen dokusu.
        /// </summary>
        private static Texture2D CreateDefaultTerrainTexture()
        {
            var tex = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    float r = Mathf.Lerp(0.18f, 0.35f, noise);
                    float g = Mathf.Lerp(0.42f, 0.62f, noise);
                    float b = Mathf.Lerp(0.08f, 0.18f, noise);
                    tex.SetPixel(x, y, new Color(r, g, b));
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }
    }
}
