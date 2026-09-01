using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EntropyOnline.Import;
using UnityEngine;

namespace KOImport
{
    /// <summary>
    /// Open-KO birebir: CN3FXBundle::Load + CN3FXPartBase::Load + CN3FXPartParticles::Load
    /// .fxb binary dosya parser.
    ///
    /// .fxb format (N3FXBundle.cpp:317-372):
    ///   [int32]  version (0, 1, 2)
    ///   [float]  life0
    ///   [float]  velocity
    ///   [bool]   dependScale
    ///   for i in 0..partCount:
    ///     [int32] partType (0=none, 1=particle, 2=board, 3=mesh, 4=bottomboard)
    ///     if partType != 0:
    ///       [float] startTime
    ///       [part data]
    ///   if version >= 2:
    ///     [bool] bStatic
    /// </summary>
    public static class FxBundleParser
    {
        // Open-KO birebir: N3FXDef.h:17-29
        private const int MAX_FX_PART_V0 = 8;  // N3FXDef.h:17
        private const int MAX_FX_PART_V1 = 26; // N3FXDef.h:23
        private const int MAX_PATH_SIZE = 260;  // Windows MAX_PATH

        /// <summary>
        /// .fxb dosyasını parse et.
        /// Open-KO birebir: CN3FXBundle::Load (N3FXBundle.cpp:317-372)
        /// </summary>
        public static FxBundleData Parse(string fxbPath)
        {
            try
            {
                byte[] bytes = KOTableProvider.LoadRaw(fxbPath);
                if (bytes == null)
                {
                    Debug.LogWarning($"[FxBundleParser] .fxb bulunamadı: {fxbPath}");
                    return null;
                }
                using var ms = new MemoryStream(bytes);
                using var reader = new BinaryReader(ms, Encoding.ASCII);

                var bundle = new FxBundleData();
                bundle.FilePath = fxbPath;

                // N3FXBundle.cpp:319 — version
                bundle.Version = reader.ReadInt32();

                // N3FXBundle.cpp:331 — life0
                bundle.Life = reader.ReadSingle();
                if (bundle.Life > 10.0f) bundle.Life = 10.0f;

                // N3FXBundle.cpp:335 — velocity
                bundle.Velocity = reader.ReadSingle();

                // N3FXBundle.cpp:336 — dependScale
                bundle.DependScale = reader.ReadBoolean();

                // N3FXBundle.cpp:338 — part count by version
                // Open-KO birebir: CN3FXBundle::GetPartCountForVersion (N3FXBundle.cpp:303-312)
                int partCount = GetPartCountForVersion(bundle.Version);

                // N3FXBundle.cpp:339-366 — part loop
                for (int i = 0; i < partCount; i++)
                {
                    // Stream sonu kontrolü — C++ fread 0 döner, BinaryReader exception fırlatır
                    if (ms.Position + 4 > ms.Length) break;

                    // N3FXBundle.cpp:341-342
                    int partType = reader.ReadInt32();

                    if (partType == 0) // FX_PART_TYPE_NONE
                        continue;

                    // N3FXBundle.cpp:356-357
                    if (ms.Position + 4 > ms.Length) break;
                    float startTime = reader.ReadSingle();

                    // N3FXBundle.cpp:365 — part->Load(file)
                    var part = ParsePartBase(reader, partType);
                    if (part != null)
                    {
                        part.StartTime = startTime;
                        part.SlotIndex = i;
                        bundle.Parts.Add(part);
                    }
                }

                // N3FXBundle.cpp:368-369 — version >= 2
                if (bundle.Version >= 2)
                    bundle.IsStatic = reader.ReadBoolean();

                return bundle;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FxBundleParser] Parse hatası: {fxbPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// byte[] verisinden parse et — Resources.Load&lt;TextAsset&gt; ile
        /// yüklenen .bytes dosyaları için.
        /// </summary>
        public static FxBundleData ParseFromBytes(byte[] bytes, string label = "")
        {
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(bytes);
                using var reader = new BinaryReader(ms, Encoding.ASCII);

                var bundle = new FxBundleData();
                bundle.FilePath = label;

                bundle.Version = reader.ReadInt32();
                bundle.Life = reader.ReadSingle();
                if (bundle.Life > 10.0f) bundle.Life = 10.0f;
                bundle.Velocity = reader.ReadSingle();
                bundle.DependScale = reader.ReadBoolean();

                int partCount = GetPartCountForVersion(bundle.Version);

                for (int i = 0; i < partCount; i++)
                {
                    if (ms.Position + 4 > ms.Length) break;
                    int partType = reader.ReadInt32();
                    if (partType == 0) continue;
                    if (ms.Position + 4 > ms.Length) break;
                    float startTime = reader.ReadSingle();

                    var part = ParsePartBase(reader, partType);
                    if (part != null)
                    {
                        part.StartTime = startTime;
                        part.SlotIndex = i;
                        bundle.Parts.Add(part);
                    }
                }

                if (bundle.Version >= 2)
                    bundle.IsStatic = reader.ReadBoolean();

                return bundle;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FxBundleParser] ParseFromBytes hatası ({label}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// CN3FXPartBase::Load (N3FXPartBase.cpp:476-583) birebir.
        /// Part base alanlarını oku, sonra part tipine göre alt sınıf verilerini oku.
        /// </summary>
        private static FxPartData ParsePartBase(BinaryReader reader, int partType)
        {
            try
            {
                var part = new FxPartData();
                part.Type = (FxPartType)partType;

                // N3FXPartBase.cpp:484-485 — version (uint8)
                part.Version = reader.ReadByte();

                // N3FXPartBase.cpp:487-488 — baseVersion (uint8)
                part.BaseVersion = reader.ReadByte();

                // N3FXPartBase.cpp:490-492 — life (float)
                part.Life = reader.ReadSingle();
                if (part.Life > 10.0f) part.Life = 10.0f;

                // N3FXPartBase.cpp:494-499 — baseVersion >= 3: skip 2 ints
                if (part.BaseVersion >= 3)
                {
                    reader.ReadInt32(); // iIDK0
                    reader.ReadInt32(); // iIDK1
                }

                // N3FXPartBase.cpp:501-502 — type override (uint8)
                byte typeOverride = reader.ReadByte();
                // part type was already set from bundle

                // N3FXPartBase.cpp:504-506 — velocity, acceleration, rotVelocity (3x Vector3)
                part.Velocity = ReadVector3(reader);
                part.Acceleration = ReadVector3(reader);
                part.RotVelocity = ReadVector3(reader);

                // N3FXPartBase.cpp:508 — onGround (bool)
                part.OnGround = reader.ReadBoolean();

                // N3FXPartBase.cpp:510 — pos (Vector3)
                part.Position = ReadVector3(reader);

                // N3FXPartBase.cpp:512-514 — numTex, texFPS, texName
                part.NumTextures = reader.ReadInt32();
                part.TextureFPS = reader.ReadSingle();
                part.TextureName = ReadFixedString(reader, MAX_PATH_SIZE);

                // N3FXPartBase.cpp:516-554 — blend/render params (version-dependent)
                if (part.BaseVersion < 2)
                {
                    // N3FXPartBase.cpp:518-523
                    part.Alpha = reader.ReadInt32();       // BOOL (4 bytes)
                    part.SrcBlend = reader.ReadUInt32();
                    part.DestBlend = reader.ReadUInt32();
                    part.FadeOut = reader.ReadSingle();
                    part.FadeIn = reader.ReadSingle();
                }
                else
                {
                    // N3FXPartBase.cpp:527-533
                    part.SrcBlend = reader.ReadUInt32();
                    part.DestBlend = reader.ReadUInt32();
                    part.FadeOut = reader.ReadSingle();
                    part.FadeIn = reader.ReadSingle();
                    part.RenderFlags = reader.ReadUInt32();

                    // Derive state from flags — N3FXPartBase.cpp:535-554
                    part.Alpha = (part.RenderFlags & 0x01) != 0 ? 1 : 0; // RF_ALPHABLENDING
                }

                // N3FXPartBase.cpp:557-559 — baseVersion >= 4: skip MAX_PATH (shape_hdrname)
                if (part.BaseVersion >= 4)
                    reader.BaseStream.Seek(MAX_PATH_SIZE, SeekOrigin.Current);

                // Now parse type-specific data
                switch (part.Type)
                {
                    case FxPartType.Particle:
                        ParseParticleData(reader, part);
                        break;
                    case FxPartType.Billboard:
                        ParseBillboardData(reader, part);
                        break;
                    case FxPartType.Mesh:
                        ParseMeshData(reader, part);
                        break;
                    case FxPartType.BottomBoard:
                        ParseBottomBoardData(reader, part);
                        break;
                }

                return part;
            }
            catch (EndOfStreamException)
            {
                // Truncated FXB — C++ fread silently returns 0 at EOF
                Debug.LogWarning($"[FxBundleParser] Part parse truncated (partType={partType})");
                return null;
            }
        }

        /// <summary>
        /// CN3FXPartParticles::Load (N3FXPartParticles.cpp:388-513) birebir.
        /// </summary>
        private static void ParseParticleData(BinaryReader reader, FxPartData part)
        {
            if (part.Version < 3) return; // cpp:393-394

            var p = new FxParticleExtra();

            // cpp:396 — numParticle
            p.NumParticles = reader.ReadInt32();

            // cpp:400-410 — particleSize
            if (part.Version < 4)
            {
                float size = reader.ReadSingle();
                p.ParticleSizeMin = p.ParticleSizeMax = size;
            }
            else
            {
                p.ParticleSizeMin = reader.ReadSingle();
                p.ParticleSizeMax = reader.ReadSingle();
            }

            // cpp:412-413 — particleLife
            p.ParticleLifeMin = reader.ReadSingle();
            p.ParticleLifeMax = reader.ReadSingle();

            // cpp:415-416 — createRange
            p.MinCreateRange = ReadVector3(reader);
            p.MaxCreateRange = ReadVector3(reader);

            // cpp:418-419 — createDelay, numCreate
            p.CreateDelay = reader.ReadSingle();
            p.NumCreate = reader.ReadInt32();

            // cpp:421 — emitType
            p.EmitType = reader.ReadUInt32();

            // cpp:423-432 — emit condition data
            if (p.EmitType == 1) // FX_PART_PARTICLE_EMIT_TYPE_SPREAD
            {
                p.EmitAngle = reader.ReadSingle();
            }
            else if (p.EmitType == 2) // FX_PART_PARTICLE_EMIT_TYPE_GATHER
            {
                p.GatherPoint = ReadVector3(reader);
            }

            // cpp:434-438 — particle physics
            p.EmitDir = ReadVector3(reader);
            p.PtVelocity = reader.ReadSingle();
            p.PtAccel = reader.ReadSingle();
            p.PtRotVelocity = reader.ReadSingle();
            p.PtGravity = reader.ReadSingle();

            // cpp:440-446 — color change
            p.ChangeColor = reader.ReadBoolean();
            if (p.ChangeColor)
            {
                int numKeyColor = reader.ReadInt32();
                p.ColorKeys = new uint[numKeyColor];
                for (int i = 0; i < numKeyColor; i++)
                    p.ColorKeys[i] = reader.ReadUInt32();
            }

            // cpp:448-461 — animKey + shape
            p.AnimKey = reader.ReadBoolean();
            if (p.AnimKey)
            {
                p.MeshFPS = reader.ReadSingle();
                p.ShapeFileName = ReadFixedString(reader, MAX_PATH_SIZE);
            }

            // cpp:463-468 — version >= 5: texture rotate + scale
            if (part.Version >= 5)
            {
                p.TexRotateVelocity = reader.ReadSingle();
                p.ScaleVelX = reader.ReadSingle();
                p.ScaleVelY = reader.ReadSingle();
            }

            // cpp:471-472 — version >= 6: distanceNumFix
            if (part.Version >= 6)
                p.DistanceNumFix = reader.ReadBoolean();

            // cpp:475-476 — version >= 7: particleYAxisFix
            if (part.Version >= 7)
                p.ParticleYAxisFix = reader.ReadBoolean();

            // cpp:479-483 — version >= 8: notRotate + axis
            if (part.Version >= 8)
            {
                p.ParticleNotRotate = reader.ReadBoolean();
                p.NotRotateAxis = ReadVector3(reader);
            }

            // cpp:486-490 — version >= 9: ptRange
            if (part.Version >= 9)
            {
                p.PtRangeMin = reader.ReadSingle();
                p.PtRangeMax = reader.ReadSingle();
            }

            // cpp:492-493 — version >= 10: skip 5 bytes
            if (part.Version >= 10)
                reader.BaseStream.Seek(5, SeekOrigin.Current);

            // cpp:495-496 — version >= 11: skip 12 bytes
            if (part.Version >= 11)
                reader.BaseStream.Seek(12, SeekOrigin.Current);

            part.ParticleData = p;
        }

        /// <summary>
        /// CN3FXPartBillBoard::Load — billboard part verisi.
        /// N3FXPartBillBoard.cpp birebir.
        /// </summary>
        private static void ParseBillboardData(BinaryReader reader, FxPartData part)
        {
            var b = new FxBillboardExtra();

            // N3FXPartBillBoard.cpp:189-194 — count, sizeX, sizeY, texLoop, radius
            b.Count = reader.ReadInt32();
            b.Width = reader.ReadSingle();
            b.Height = reader.ReadSingle();
            b.TexLoop = reader.ReadBoolean();
            b.Radius = reader.ReadSingle();

            // cpp:196-197 — version >= 3: rotateOnlyY
            if (part.Version >= 3)
                b.RotateOnlyY = reader.ReadBoolean();

            // cpp:199-205 — version >= 4: scale vel + accel
            if (part.Version >= 4)
            {
                b.ScaleVelX = reader.ReadSingle();
                b.ScaleVelY = reader.ReadSingle();
                b.ScaleAccelX = reader.ReadSingle();
                b.ScaleAccelY = reader.ReadSingle();
            }

            // cpp:207-208 — version >= 5: rotation matrix (4x4 float = 64 bytes)
            if (part.Version >= 5)
                reader.BaseStream.Seek(64, SeekOrigin.Current); // __Matrix44 skip

            // cpp:211-212 — version >= 6: onScreen (bool)
            if (part.Version >= 6)
                reader.ReadBoolean();

            // cpp:215-216 — version >= 7: rotationRate (bool)
            if (part.Version >= 7)
                reader.ReadBoolean();

            // cpp:218-219 — version >= 8: skip 13 bytes
            if (part.Version >= 8)
                reader.BaseStream.Seek(13, SeekOrigin.Current);

            // cpp:221-222 — version >= 9: skip 12 bytes
            if (part.Version >= 9)
                reader.BaseStream.Seek(12, SeekOrigin.Current);

            part.BillboardData = b;
        }

        /// <summary>
        /// CN3FXPartMesh::Load — mesh part verisi.
        /// N3FXPartMesh.cpp birebir.
        /// </summary>
        private static void ParseMeshData(BinaryReader reader, FxPartData part)
        {
            var m = new FxMeshExtra();

            // N3FXPartMesh.cpp:226-227 — shape file name (MAX_PATH)
            m.MeshFileName = ReadFixedString(reader, MAX_PATH_SIZE);

            // cpp:243-246 — textureMoveDir (1 byte), fu, fv, scaleVel (3 floats)
            m.TextureMoveDir = reader.ReadByte();
            m.TexU = reader.ReadSingle();
            m.TexV = reader.ReadSingle();
            m.ScaleVel = ReadVector3(reader);

            // cpp:249-250 — version >= 2: texLoop
            if (part.Version >= 2)
                m.TexLoop = reader.ReadBoolean();

            // cpp:252-253 — version >= 3: scaleAccel (Vector3)
            if (part.Version >= 3)
                m.ScaleAccel = ReadVector3(reader);

            // cpp:255-256 — version >= 4: meshFPS
            if (part.Version >= 4)
                m.MeshFPS = reader.ReadSingle();

            // cpp:258-259 — version >= 5: unitScale (Vector3)
            if (part.Version >= 5)
                m.UnitScale = ReadVector3(reader);

            // cpp:262-263 — version >= 6: shapeLoop (bool)
            if (part.Version >= 6)
                reader.ReadBoolean();

            // cpp:266-267 — version >= 7: viewFix (bool)
            if (part.Version >= 7)
                reader.ReadBoolean();

            // cpp:270-271 — version >= 8: useFadeShowLife (bool)
            if (part.Version >= 8)
                reader.ReadBoolean();

            // cpp:273-274 — version >= 9: skip MAX_PATH
            if (part.Version >= 9)
                reader.BaseStream.Seek(MAX_PATH_SIZE, SeekOrigin.Current);

            part.MeshData = m;
        }

        /// <summary>
        /// CN3FXPartBottomBoard::Load — ground-aligned quad part verisi.
        /// N3FXPartBottomBoard.cpp birebir.
        /// </summary>
        private static void ParseBottomBoardData(BinaryReader reader, FxPartData part)
        {
            var bb = new FxBottomBoardExtra();

            // N3FXPartBottomBoard.cpp:183-189 — sizeX, sizeZ, scaleVelX, scaleVelZ, texLoop
            bb.Width = reader.ReadSingle();
            bb.Height = reader.ReadSingle();
            bb.ScaleVelX = reader.ReadSingle();
            bb.ScaleVelY = reader.ReadSingle();
            bb.TexLoop = reader.ReadBoolean();

            // cpp:191-192 — version >= 1: gap
            if (part.Version >= 1)
                bb.Gap = reader.ReadSingle();

            // cpp:195-196 — version >= 2: newUv (bool)
            if (part.Version >= 2)
                reader.ReadBoolean();

            // cpp:199-200 — version >= 3: hdrUv (bool)
            if (part.Version >= 3)
                reader.ReadBoolean();

            part.BottomBoardData = bb;
        }

        // === Helpers ===

        /// <summary>
        /// Open-KO birebir: CN3FXBundle::GetPartCountForVersion (N3FXBundle.cpp:303-312)
        /// </summary>
        private static int GetPartCountForVersion(int version)
        {
            if (version < 0) return 0;          // cpp:305-306
            if (version == 0) return MAX_FX_PART_V0; // cpp:308-309
            return MAX_FX_PART_V1;               // cpp:311
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        private static string ReadFixedString(BinaryReader reader, int maxLen)
        {
            byte[] buf = reader.ReadBytes(maxLen);
            int end = Array.IndexOf(buf, (byte)0);
            if (end < 0) end = buf.Length;
            return Encoding.ASCII.GetString(buf, 0, end);
        }
    }

    // ========================================
    // Data Classes
    // ========================================

    /// <summary>Open-KO: CN3FXBundle binary data.</summary>
    public class FxBundleData
    {
        public string FilePath;
        public int Version;
        public float Life;
        public float Velocity;
        public bool DependScale;
        public bool IsStatic;
        public List<FxPartData> Parts = new();
    }

    /// <summary>Open-KO: e_FXPartType (N3FXDef.h:37-44)</summary>
    public enum FxPartType
    {
        None = 0,
        Particle = 1,
        Billboard = 2,
        Mesh = 3,
        BottomBoard = 4
    }

    /// <summary>Open-KO: CN3FXPartBase binary data + type-specific extras.</summary>
    public class FxPartData
    {
        // Bundle slot info
        public int SlotIndex;
        public float StartTime;

        // Base data — N3FXPartBase.cpp:476-583
        public FxPartType Type;
        public int Version;
        public int BaseVersion;
        public float Life;
        public Vector3 Velocity;
        public Vector3 Acceleration;
        public Vector3 RotVelocity;
        public bool OnGround;
        public Vector3 Position;
        public int NumTextures;
        public float TextureFPS;
        public string TextureName = "";
        public int Alpha;
        public uint SrcBlend;
        public uint DestBlend;
        public float FadeOut;
        public float FadeIn;
        public uint RenderFlags;

        // Type-specific extra data
        public FxParticleExtra ParticleData;
        public FxBillboardExtra BillboardData;
        public FxMeshExtra MeshData;
        public FxBottomBoardExtra BottomBoardData;
    }

    /// <summary>Open-KO: CN3FXPartParticles type-specific data.</summary>
    public class FxParticleExtra
    {
        public int NumParticles;
        public float ParticleSizeMin, ParticleSizeMax;
        public float ParticleLifeMin, ParticleLifeMax;
        public Vector3 MinCreateRange, MaxCreateRange;
        public float CreateDelay;
        public int NumCreate;
        public uint EmitType;
        public float EmitAngle;
        public Vector3 GatherPoint;
        public Vector3 EmitDir;
        public float PtVelocity, PtAccel, PtRotVelocity, PtGravity;
        public bool ChangeColor;
        public uint[] ColorKeys;
        public bool AnimKey;
        public float MeshFPS;
        public string ShapeFileName = "";
        public float TexRotateVelocity;
        public float ScaleVelX, ScaleVelY;
        public bool DistanceNumFix;
        public bool ParticleYAxisFix;
        public bool ParticleNotRotate;
        public Vector3 NotRotateAxis;
        public float PtRangeMin, PtRangeMax;
    }

    /// <summary>Open-KO: CN3FXPartBillBoard type-specific data.</summary>
    public class FxBillboardExtra
    {
        public int Count;
        public float Width, Height;
        public bool TexLoop;
        public float Radius;
        public bool RotateOnlyY;
        public float ScaleVelX, ScaleVelY;
        public float ScaleAccelX, ScaleAccelY;
    }

    /// <summary>Open-KO: CN3FXPartMesh type-specific data.</summary>
    public class FxMeshExtra
    {
        public string MeshFileName = "";
        public byte TextureMoveDir;
        public float TexU, TexV;
        public Vector3 ScaleVel;
        public bool TexLoop;
        public Vector3 ScaleAccel;
        public float MeshFPS;
        public Vector3 UnitScale = Vector3.one;
    }

    /// <summary>Open-KO: CN3FXPartBottomBoard type-specific data.</summary>
    public class FxBottomBoardExtra
    {
        public float Width, Height;
        public float ScaleVelX, ScaleVelY;
        public bool TexLoop;
        public float Gap;
    }
}
