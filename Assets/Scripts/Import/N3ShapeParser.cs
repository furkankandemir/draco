using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 N3Shape Parser — OPD dosyasındaki Shape objelerini tam olarak parse eder.
    /// 
    /// Kalıtım zinciri (C++ → C# birebir port):
    ///   CN3BaseFileAccess::Load  → int32 nameLen + char[] name
    ///   CN3Transform::Load       → Vec3 pos + Quat rot + Vec3 scale + 3x AnimKey
    ///   CN3TransformCollision::Load → collision mesh name + climb mesh name
    ///   CN3Shape::Load           → parts + belong/event data
    ///   CN3SPart::Load           → pivot + mesh filename + material + textures
    /// </summary>
    public static class N3ShapeParser
    {
        // D3DMATERIAL9 (68) + extended fields (24) = 92 bytes
        private const int SIZEOF_MATERIAL = 92;

        // OBJ_SHAPE_EXTRA flag
        private const uint OBJ_SHAPE_EXTRA = 0x1000;

        #region Data Structures

        /// <summary>Bir animation key seti (Open-KO: CN3AnimKey birebir).</summary>
        public class N3AnimKeyData
        {
            public int Count;
            public uint Type; // 0=Vector3, 1=Quaternion
            public float SamplingRate = 30f;
            public Vector3[] VectorKeys;
            public Quaternion[] QuatKeys;

            public bool SampleVector(float frame, out Vector3 result)
            {
                if (VectorKeys == null || VectorKeys.Length == 0)
                {
                    result = Vector3.zero;
                    return false;
                }
                if (VectorKeys.Length == 1 || frame <= 0f)
                {
                    result = VectorKeys[0];
                    return true;
                }
                if (frame >= VectorKeys.Length - 1)
                {
                    result = VectorKeys[VectorKeys.Length - 1];
                    return true;
                }

                int idx0 = (int)frame;
                int idx1 = Mathf.Min(idx0 + 1, VectorKeys.Length - 1);
                float t = frame - idx0;
                result = Vector3.Lerp(VectorKeys[idx0], VectorKeys[idx1], t);
                return true;
            }
        }

        /// <summary>CN3Transform verileri.</summary>
        public class N3TransformData
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public N3AnimKeyData KeyPos;
            public N3AnimKeyData KeyRot;
            public N3AnimKeyData KeyScale;
        }

        /// <summary>KO __Material yapısı (D3D9 material).</summary>
        public class N3MaterialData
        {
            // D3DMATERIAL9 base
            public Color Diffuse;
            public Color Ambient;
            public Color Specular;
            public Color Emissive;
            public float Power;
            // Extended
            public uint ColorOp;
            public uint ColorArg1;
            public uint ColorArg2;
            public uint RenderFlags;
            public uint SrcBlend;
            public uint DestBlend;
        }

        /// <summary>CN3SPart verileri — bir shape parçası.</summary>
        public class N3PartData
        {
            public Vector3 Pivot;
            public string MeshFileName; // .N3PMesh referansı
            public N3MaterialData Material;
            public float TexFPS;
            public List<string> TextureFileNames; // .DXT referansları
        }

        /// <summary>CN3Shape verileri — bir tam obje.</summary>
        public class N3ShapeData
        {
            public uint ShapeType;
            public N3TransformData Transform;
            public string CollisionMeshName;
            public string ClimbMeshName;
            public List<N3PartData> Parts;
            public int Belong;
            public int EventID;
            public int EventType;
            public int NPC_ID;
            public int NPC_Status;
        }

        /// <summary>Alt hücre collision verileri — N3ShapeMgr::__CellSub birebir.</summary>
        public class CellSubData
        {
            public int CCPolyCount;
            public int[] CCVertIndices; // CCPolyCount * 3 adet vertex indeks
        }

        /// <summary>Ana hücre verileri — N3ShapeMgr::__CellMain birebir.</summary>
        public class CellMainData
        {
            public int ShapeCount;
            public ushort[] ShapeIndices;
            public CellSubData[,] SubCells; // [4,4] — CELL_MAIN_DIVIDE=4
        }

        /// <summary>OPD tam parse sonucu.</summary>
        public class OpdFullData
        {
            public float MapWidth;
            public float MapLength;
            public int CollisionFaceCount;
            public Vector3[] CollisionVertices;
            public CellMainData[,] Cells; // [cellsX, cellsZ]
            public int CellsX;
            public int CellsZ;
            public List<N3ShapeData> Shapes;
        }

        #endregion

        #region Main Parser

        /// <summary>
        /// OPD dosyasını tam olarak parse eder — collision + tüm Shape'ler.
        /// N3ShapeMgr::Load() birebir portu.
        /// </summary>
        public static OpdFullData ParseFull(string opdPath)
        {
            using var reader = KOBinaryProvider.OpenReader(opdPath);
            if (reader == null)
            {
                Debug.LogError($"[N3Shape] OPD bulunamadı: {opdPath}");
                return null;
            }

            var data = new OpdFullData();

            // ============================================
            // HEADER (N3ShapeMgr, v1264+)
            // N3ShapeMgr::Load line 142-160
            // ============================================
            int version = TryReadShapeMgrHeader(reader);

            // ============================================
            // COLLISION DATA (N3ShapeMgr::LoadCollisionData)
            // ============================================
            data.MapWidth = reader.ReadSingle();
            data.MapLength = reader.ReadSingle();

            if (data.MapWidth <= 0 || data.MapLength <= 0 ||
                data.MapWidth > 65536 || data.MapLength > 65536)
            {
                Debug.LogError($"[N3Shape] Geçersiz map boyutu: {data.MapWidth}x{data.MapLength}");
                return null;
            }

            // Collision faces
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
                if (data.CollisionFaceCount > 0)
                    Debug.LogWarning($"[N3Shape] Çok fazla collision face: {data.CollisionFaceCount}");
            }

            // Cell grid (shape index referansları + subcell collision)
            // N3ShapeMgr.h: CELL_MAIN_SIZE = CELL_MAIN_DIVIDE * CELL_SUB_SIZE = 4 * 4 = 16
            data.CellsX = (int)(data.MapWidth / 16);
            data.CellsZ = (int)(data.MapLength / 16);
            data.Cells = ParseCellGrid(reader, data.CellsX, data.CellsZ);

            // ============================================
            // SHAPE COUNT + SHAPES (N3ShapeMgr::Load line 170-233)
            // ============================================
            int shapeCount = reader.ReadInt32();

            data.Shapes = new List<N3ShapeData>(shapeCount);

            int parseErrors = 0;
            for (int i = 0; i < shapeCount; i++)
            {
                try
                {
                    uint dwType = reader.ReadUInt32(); // Shape type flag
                    var shape = ReadShape(reader, dwType);
                    shape.ShapeType = dwType;

                    // OBJ_SHAPE_EXTRA (0x1000) — CN3ShapeExtra::Load
                    // C++ N3ShapeExtra.cpp satır 25-37: Load() sadece CN3Shape::Load() çağırır,
                    // dosyadan ek veri OKUMAZ. Sadece m_Rotations in-memory initialize edilir.
                    // Dolayısıyla parse açısından normal shape ile aynı.

                    data.Shapes.Add(shape);
                }
                catch (EndOfStreamException)
                {
                    Debug.LogWarning($"[N3Shape] Shape {i}/{shapeCount} okumada EOF — parse durduruluyor");
                    break;
                }
                catch (Exception ex)
                {
                    parseErrors++;
                    if (parseErrors <= 5)
                        Debug.LogWarning($"[N3Shape] Shape {i} parse hatası (pos={reader.BaseStream.Position}): {ex.Message}");
                    if (parseErrors > 50)
                    {
                        Debug.LogError($"[N3Shape] Çok fazla parse hatası ({parseErrors}), parse durduruluyor.");
                        break;
                    }
                    // Devam et — tek bir hatalı shape tüm yüklemeyi durdumamalı
                    continue;
                }
            }

            if (parseErrors > 0)
                Debug.LogWarning($"[N3Shape] Toplam {parseErrors} parse hatası (başarılı: {data.Shapes.Count}/{shapeCount})");


            return data;
        }

        /// <summary>
        /// Standalone .n3shape dosyasını parse eder.
        /// OPD'deki ReadShape ile aynı format, tek fark: dosya başında dwType yok.
        /// Open-KO: CN3Shape::LoadFromFile() → CN3BaseFileAccess::LoadFromFile() → Load(File&)
        ///   Load: CN3TransformCollision::Load → CN3Transform::Load → CN3BaseFileAccess::Load
        ///         + collision/climb mesh + parts + metadata
        /// </summary>
        public static N3ShapeData ParseShapeFile(string shapePath)
        {
            using var reader = KOBinaryProvider.OpenReader(shapePath);
            if (reader == null)
            {
                Debug.LogError($"[N3Shape] Shape dosyası bulunamadı: {shapePath}");
                return null;
            }

            try
            {
                // Standalone .n3shape: dwType yok, direkt CN3Shape::Load formatı
                var shape = ReadShape(reader, 0);
                return shape;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[N3Shape] Shape parse hatası: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Shape Reading

        /// <summary>
        /// CN3Shape::Load() birebir portu.
        /// </summary>
        private static N3ShapeData ReadShape(BinaryReader reader, uint dwType)
        {
            var shape = new N3ShapeData();

            // CN3TransformCollision::Load → CN3Transform::Load → CN3BaseFileAccess::Load
            shape.Transform = ReadTransform(reader);

            // CN3TransformCollision: collision mesh + climb mesh filenames
            shape.CollisionMeshName = ReadLenString(reader);
            shape.ClimbMeshName = ReadLenString(reader);

            // CN3Shape::Load: Part Count + Parts
            int partCount = reader.ReadInt32();
            shape.Parts = new List<N3PartData>(partCount);

            for (int p = 0; p < partCount; p++)
            {
                shape.Parts.Add(ReadPart(reader));
            }

            // Shape metadata
            shape.Belong = reader.ReadInt32();
            shape.EventID = reader.ReadInt32();
            shape.EventType = reader.ReadInt32();
            shape.NPC_ID = reader.ReadInt32();
            shape.NPC_Status = reader.ReadInt32();

            return shape;
        }

        /// <summary>
        /// CN3Transform::Load() birebir portu.
        /// CN3BaseFileAccess::Load + Position + Rotation + Scale + 3x AnimKey
        /// </summary>
        private static N3TransformData ReadTransform(BinaryReader reader)
        {
            var t = new N3TransformData();

            // CN3BaseFileAccess::Load: int32 nameLen + char[] name
            t.Name = ReadLenString(reader);

            // Position (Vector3 = 12 bytes)
            float px = reader.ReadSingle();
            float py = reader.ReadSingle();
            float pz = reader.ReadSingle();
            t.Position = new Vector3(px, py, pz);

            // Rotation (Quaternion = 16 bytes: x,y,z,w — D3DXQUATERNION order)
            // D3D ve Unity aynı left-handed sistem, Transform.rotation quaternion sandwich
            // product (q*v*q^-1) kullanır — birebir aynı, dönüşüm gerekmez.
            float qx = reader.ReadSingle();
            float qy = reader.ReadSingle();
            float qz = reader.ReadSingle();
            float qw = reader.ReadSingle();
            // C++ birebir: m_qRot.w != 0 ise rotasyon uygulanır, aksi halde identity kabul edilir.
            // Dosyadan okunan qw sıfır veya sıfıra çok yakınsa Quaternion.identity yapıyoruz.
            if (Mathf.Abs(qw) < 0.0001f)
                t.Rotation = Quaternion.identity;
            else
                t.Rotation = new Quaternion(qx, qy, qz, qw);

            // Scale (Vector3 = 12 bytes)
            float sx = reader.ReadSingle();
            float sy = reader.ReadSingle();
            float sz = reader.ReadSingle();
            t.Scale = new Vector3(sx, sy, sz);

            // Animation keys (3x: pos, rot, scale)
            t.KeyPos = ReadAnimKey(reader);   // m_KeyPos
            t.KeyRot = ReadAnimKey(reader);   // m_KeyRot
            t.KeyScale = ReadAnimKey(reader); // m_KeyScale

            return t;
        }

        /// <summary>
        /// CN3SPart::Load() birebir portu.
        /// </summary>
        private static N3PartData ReadPart(BinaryReader reader)
        {
            var part = new N3PartData();

            // Pivot (Vector3 = 12 bytes)
            float pvx = reader.ReadSingle();
            float pvy = reader.ReadSingle();
            float pvz = reader.ReadSingle();
            part.Pivot = new Vector3(pvx, pvy, pvz);

            // Mesh filename (length-prefixed string)
            part.MeshFileName = ReadLenString(reader);

            // Material (__Material = 92 bytes)
            part.Material = ReadMaterial(reader);

            // Texture count + FPS
            int texCount = reader.ReadInt32();
            part.TexFPS = reader.ReadSingle();

            // Texture filenames
            part.TextureFileNames = new List<string>(texCount);
            for (int t = 0; t < texCount; t++)
            {
                part.TextureFileNames.Add(ReadLenString(reader));
            }

            return part;
        }

        #endregion

        #region Material Reading

        /// <summary>
        /// __Material (D3DMATERIAL9 + extended) okuma — 92 bytes.
        /// </summary>
        private static N3MaterialData ReadMaterial(BinaryReader reader)
        {
            var mat = new N3MaterialData();

            // D3DMATERIAL9: Diffuse (D3DCOLORVALUE = 4 floats = 16 bytes)
            mat.Diffuse = ReadColorValue(reader);
            // Ambient
            mat.Ambient = ReadColorValue(reader);
            // Specular
            mat.Specular = ReadColorValue(reader);
            // Emissive
            mat.Emissive = ReadColorValue(reader);
            // Power (float)
            mat.Power = reader.ReadSingle();

            // Extended fields
            mat.ColorOp = reader.ReadUInt32();
            mat.ColorArg1 = reader.ReadUInt32();
            mat.ColorArg2 = reader.ReadUInt32();
            mat.RenderFlags = reader.ReadUInt32();
            mat.SrcBlend = reader.ReadUInt32();
            mat.DestBlend = reader.ReadUInt32();

            return mat;
        }

        private static Color ReadColorValue(BinaryReader reader)
        {
            float r = reader.ReadSingle();
            float g = reader.ReadSingle();
            float b = reader.ReadSingle();
            float a = reader.ReadSingle();
            return new Color(r, g, b, a);
        }

        #endregion

        #region AnimKey & Cell Skipping

        /// <summary>
        /// CN3AnimKey::Load() (N3AnimKey.cpp:70-100) birebir portu.
        /// Binary: int32 count, [if count>0: uint32 type + float rate + data[]]
        /// </summary>
        private static N3AnimKeyData ReadAnimKey(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0) return null;

            var key = new N3AnimKeyData();
            key.Count = count;
            key.Type = reader.ReadUInt32(); // KEY_VECTOR3=0, KEY_QUATERNION=1
            key.SamplingRate = reader.ReadSingle();

            if (key.Type == 0) // KEY_VECTOR3
            {
                key.VectorKeys = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    key.VectorKeys[i] = new Vector3(x, y, z);
                }
            }
            else // KEY_QUATERNION
            {
                key.QuatKeys = new Quaternion[count];
                for (int i = 0; i < count; i++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    float w = reader.ReadSingle();
                    if (Mathf.Abs(w) < 0.0001f)
                        key.QuatKeys[i] = Quaternion.identity;
                    else
                        key.QuatKeys[i] = new Quaternion(x, y, z, w);
                }
            }

            return key;
        }

        /// <summary>
        /// Cell grid parse — N3ShapeMgr::LoadCollisionData birebir portu.
        /// C++ loop sırası: for z { for x { m_pCells[x][z] } }
        /// SubCells: for z { for x { SubCells[x][z] } }
        /// </summary>
        private static CellMainData[,] ParseCellGrid(BinaryReader reader, int cellsX, int cellsZ)
        {
            var cells = new CellMainData[cellsX, cellsZ];

            try
            {
                // N3ShapeMgr::LoadCollisionData satır 271-287
                for (int z = 0; z < cellsZ; z++)
                {
                    for (int x = 0; x < cellsX; x++)
                    {
                        int exists = reader.ReadInt32();
                        if (exists == 0) continue;

                        var cell = new CellMainData();

                        // __CellMain::Load — satır 87-105
                        cell.ShapeCount = reader.ReadInt32();
                        if (cell.ShapeCount > 0)
                        {
                            cell.ShapeIndices = new ushort[cell.ShapeCount];
                            for (int i = 0; i < cell.ShapeCount; i++)
                                cell.ShapeIndices[i] = reader.ReadUInt16();
                        }

                        // SubCells [4x4] — __CellMain::Load satır 100-104
                        // C++ loop: for(z=0..3) { for(x=0..3) SubCells[x][z].Load() }
                        cell.SubCells = new CellSubData[4, 4];
                        for (int sz = 0; sz < 4; sz++)
                        {
                            for (int sx = 0; sx < 4; sx++)
                            {
                                var sub = new CellSubData();

                                // __CellSub::Load — satır 46-61
                                sub.CCPolyCount = reader.ReadInt32();
                                if (sub.CCPolyCount > 0)
                                {
                                    sub.CCVertIndices = new int[sub.CCPolyCount * 3];
                                    for (int i = 0; i < sub.CCPolyCount * 3; i++)
                                        sub.CCVertIndices[i] = (int)reader.ReadUInt32();
                                }

                                cell.SubCells[sx, sz] = sub;
                            }
                        }

                        cells[x, z] = cell;
                    }
                }
            }
            catch (EndOfStreamException)
            {
                Debug.LogWarning("[N3Shape] Cell grid okuma erken sonlandı");
            }

            return cells;
        }

        // NOT: CN3ShapeExtra::Load() dosyadan ek veri OKUMAZ (sadece CN3Shape::Load çağırır).
        // Bu yüzden parse açısından extra shape'ler normal shape ile aynıdır.

        #endregion

        #region Header & String Helpers

        private static int TryReadShapeMgrHeader(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            int nameLen = reader.ReadInt32();
            if (nameLen > 0)
                reader.BaseStream.Seek(nameLen, SeekOrigin.Current);
            return version;
        }

        /// <summary>
        /// Length-prefixed string okuma.
        /// Format: int32 length + char[length]
        /// </summary>
        private static string ReadLenString(BinaryReader reader)
        {
            int len = reader.ReadInt32();
            if (len <= 0) return string.Empty;
            if (len > 512) // güvenlik limiti
            {
                Debug.LogWarning($"[N3Shape] Çok uzun string: {len}");
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(len);
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            return System.Text.Encoding.ASCII.GetString(
                bytes, 0, nullIdx >= 0 ? nullIdx : len).Trim();
        }

        #endregion
    }
}
