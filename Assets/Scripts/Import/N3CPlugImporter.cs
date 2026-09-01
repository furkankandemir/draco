using System;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 N3CPlug (.n3cplug) Binary Parser
    /// 
    /// CN3CPlugBase::Load() + CN3CPlug::Load() birebir portu.
    /// N3Chr.cpp:458-499 (CN3CPlugBase::Load) ve 626-665 (CN3CPlug::Load)
    /// 
    /// .n3cplug dosya formatı:
    ///   CN3BaseFileAccess::Load:
    ///     int32: nameLen
    ///     char[nameLen]: name
    ///   CN3CPlugBase::Load:
    ///     int32: ePlugType (PLUGTYPE_NORMAL=0, PLUGTYPE_CLOAK=1)
    ///     int32: nJointIndex
    ///     Vector3: position (12 bytes)
    ///     Matrix44: rotation (64 bytes)
    ///     Vector3: scale (12 bytes)
    ///     Material: __Material (92 bytes — same as CPartData)
    ///     int32: meshNameLen
    ///     char[meshNameLen]: meshFileName (.N3PMesh)
    ///     int32: texNameLen
    ///     char[texNameLen]: texFileName (.DXT)
    ///   CN3CPlug::Load:
    ///     int32: nTraceStep (궤적 갯수)
    ///     if nTraceStep > 0:
    ///       int32: crTrace (궤적 색깔)
    ///       float: fTrace0
    ///       float: fTrace1
    ///     int32: iUseVMesh (FX에 쓸 PMesh가 있는가)
    ///     if iUseVMesh != 0:
    ///       [inline CN3PMesh::Load() — FX mesh, skip]
    /// </summary>
    public static class N3CPlugImporter
    {
        public class CPlugData
        {
            public string Name;
            public int PlugType;       // 0=NORMAL, 1=CLOAK
            public int JointIndex;     // 붙는 위치 (bone index)

            // Local transform
            public Vector3 Position;
            public Matrix4x4 RotationMatrix;
            public Vector3 Scale;

            // Material
            public int RenderFlags;
            public Color Diffuse;
            public Color Ambient;
            public Color Specular;
            public Color Emissive;
            public float Power;

            // References
            public string MeshFileName;    // .N3PMesh
            public string TextureFileName; // .DXT

            // Trace (sword trail)
            // Open-KO birebir: CN3CPlug — N3Chr.h:237-241
            public int TraceStep;      // m_nTraceStep — 궤적의 길이 (segment sayısı)
            public uint TraceColor;    // m_crTrace — 궤적 색깔 (D3DCOLOR ARGB)
            public float Trace0;       // m_fTrace0 — 궤적 başlangıç Y
            public float Trace1;       // m_fTrace1 — 궤적 bitiş Y

            // FX Mesh — CN3CPlug::Load (N3Chr.cpp:643-658)
            // iUseVMesh != 0 ise inline CN3PMesh::Load() ile gömülü mesh
            public N3PMeshImporter.N3PMeshData FXMeshData; // m_PMeshInstFX
        }

        /// <summary>
        /// .n3cplug dosyasını parse eder.
        /// CN3CPlugBase::Load() + CN3CPlug::Load() birebir portu.
        /// </summary>
        public static CPlugData Load(string path)
        {
            try
            {
                using var reader = KOBinaryProvider.OpenReader(path);
                if (reader == null)
                {
                    Debug.LogWarning($"[N3CPlug] Dosya bulunamadı: {path}");
                    return null;
                }

                var data = new CPlugData();

                // ============================================
                // CN3BaseFileAccess::Load: name
                // ============================================
                int nameLen = reader.ReadInt32();
                if (nameLen > 0 && nameLen < 512)
                {
                    byte[] nameBytes = reader.ReadBytes(nameLen);
                    data.Name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                }
                else if (nameLen < 0 || nameLen >= 512)
                {
                    Debug.LogWarning($"[N3CPlug] Geçersiz name length: {nameLen} — {path}");
                    return null;
                }

                // ============================================
                // CN3CPlugBase::Load — N3Chr.cpp:465-496
                // ============================================

                // int32: ePlugType — N3Chr.cpp:465
                data.PlugType = reader.ReadInt32();
                if (data.PlugType > 2) // PLUGTYPE_MAX check
                    data.PlugType = 0; // PLUGTYPE_NORMAL

                // int32: nJointIndex — N3Chr.cpp:472
                data.JointIndex = reader.ReadInt32();

                // Vector3: position (12 bytes) — N3Chr.cpp:474
                data.Position = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                // Matrix44: rotation (64 bytes = 4x4 float) — N3Chr.cpp:475
                // DirectX row-major → dosyadan row-first okunur
                // Unity Matrix4x4 column-major → [row, col] indexing ile doğru transpose olur
                var mtx = new Matrix4x4();
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 4; col++)
                        mtx[row, col] = reader.ReadSingle();
                data.RotationMatrix = mtx;

                // Vector3: scale (12 bytes) — N3Chr.cpp:476
                data.Scale = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                // __Material (My_3DStruct.h:95-107)
                // Base class _D3DMATERIAL9 comes FIRST:
                //   D3DCOLORVALUE Diffuse(16) + Ambient(16) + Specular(16) + Emissive(16) + float Power(4) = 68 bytes
                // Then additional members:
                //   uint32_t dwColorOp(4) + dwColorArg1(4) + dwColorArg2(4) = 12 bytes
                //   uint32_t nRenderFlags(4) + dwSrcBlend(4) + dwDestBlend(4) = 12 bytes
                // Total = 92 bytes
                data.Diffuse = ReadColor(reader);     // _D3DMATERIAL9.Diffuse
                data.Ambient = ReadColor(reader);     // _D3DMATERIAL9.Ambient
                data.Specular = ReadColor(reader);    // _D3DMATERIAL9.Specular
                data.Emissive = ReadColor(reader);    // _D3DMATERIAL9.Emissive
                data.Power = reader.ReadSingle();     // _D3DMATERIAL9.Power
                reader.ReadInt32(); // dwColorOp
                reader.ReadInt32(); // dwColorArg1
                reader.ReadInt32(); // dwColorArg2
                data.RenderFlags = reader.ReadInt32(); // nRenderFlags
                reader.ReadInt32(); // dwSrcBlend
                reader.ReadInt32(); // dwDestBlend

                // string: meshFileName (.N3PMesh) — N3Chr.cpp:480-486
                int meshNameLen = reader.ReadInt32();
                if (meshNameLen > 0 && meshNameLen < 512)
                {
                    byte[] meshBytes = reader.ReadBytes(meshNameLen);
                    data.MeshFileName = System.Text.Encoding.ASCII.GetString(meshBytes).TrimEnd('\0');
                }

                // string: texFileName (.DXT) — N3Chr.cpp:488-494
                int texNameLen = reader.ReadInt32();
                if (texNameLen > 0 && texNameLen < 512)
                {
                    byte[] texBytes = reader.ReadBytes(texNameLen);
                    data.TextureFileName = System.Text.Encoding.ASCII.GetString(texBytes).TrimEnd('\0');
                }

                // ============================================
                // CN3CPlug::Load — N3Chr.cpp:626-665
                // ============================================

                // int32: nTraceStep — N3Chr.cpp:630
                data.TraceStep = reader.ReadInt32();
                if (data.TraceStep > 0)
                {
                    data.TraceColor = reader.ReadUInt32(); // crTrace — N3Chr.cpp:634
                    data.Trace0 = reader.ReadSingle();      // fTrace0 — N3Chr.cpp:635
                    data.Trace1 = reader.ReadSingle();      // fTrace1 — N3Chr.cpp:636
                }

                // ============================================
                // CN3CPlug::Load — N3Chr.cpp:643-658
                // iUseVMesh: FX mesh gömülü mü?
                // C++ file.Read(&iUseVMesh, 4) — EOF'da sessizce 0 döner,
                // bazı .n3cplug dosyaları nTraceStep'te bitiyor (iUseVMesh yok).
                // ============================================
                int iUseVMesh = 0;
                if (reader.BaseStream.Position + 4 <= reader.BaseStream.Length) // C++ EOF-safe Read birebir
                {
                    iUseVMesh = reader.ReadInt32(); // N3Chr.cpp:644
                }
                if (iUseVMesh != 0) // N3Chr.cpp:646
                {
                    // N3Chr.cpp:648-650: inline CN3PMesh::Load(file)
                    // pPMesh->m_iFileFormatVersion = m_iFileFormatVersion;
                    // pPMesh->Load(file);
                    try
                    {
                        data.FXMeshData = N3PMeshImporter.LoadFromReader(reader);
                        if (data.FXMeshData != null)
                        {
                        }
                    }
                    catch (Exception fxEx)
                    {
                        Debug.LogWarning($"[N3CPlug] FX mesh parse hatası: {fxEx.Message}");
                    }
                }


                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3CPlug] Parse hatası ({Path.GetFileName(path)}): {ex.Message}");
                return null;
            }
        }

        private static Color ReadColor(BinaryReader reader)
        {
            float r = reader.ReadSingle();
            float g = reader.ReadSingle();
            float b = reader.ReadSingle();
            float a = reader.ReadSingle();
            return new Color(r, g, b, a);
        }
    }
}
