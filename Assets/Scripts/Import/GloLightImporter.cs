using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// .glo (Game Light Object) dosyalarını parse eder.
    /// Open-KO v1.298: LightMgr::LoadZoneLight → CN3Light::Load → CN3Transform::Load
    /// 
    /// Binary format:
    ///   int32  version
    ///   int32  lightCount
    ///   for each light:
    ///     CN3BaseFileAccess::Load  → int32 nameLen + name bytes
    ///     CN3Transform::Load       → Vector3 pos (12) + Quaternion rot (16) + Vector3 scale (12)
    ///                              → AnimKey(pos) + AnimKey(rot) + AnimKey(scale)
    ///     CN3Light::Load           → __Light struct (sizeof = 112 bytes)
    ///
    /// __Light struct (__D3DLight9 + bOn + nNumber):
    ///   D3DLIGHTTYPE Type        (4 bytes)  — 1=Point, 2=Spot, 3=Directional
    ///   ColorValue Diffuse       (16 bytes) — r,g,b,a float
    ///   ColorValue Specular      (16 bytes)
    ///   ColorValue Ambient       (16 bytes)
    ///   Vector3 Position         (12 bytes) — x,y,z float
    ///   Vector3 Direction        (12 bytes)
    ///   float Range              (4 bytes)
    ///   float Falloff            (4 bytes)
    ///   float Attenuation0       (4 bytes)
    ///   float Attenuation1       (4 bytes)
    ///   float Attenuation2       (4 bytes)
    ///   float Theta              (4 bytes)
    ///   float Phi                (4 bytes)
    ///   int32 bOn                (4 bytes)  — BOOL
    ///   int32 nNumber            (4 bytes)
    ///   = Total 112 bytes
    /// </summary>
    public static class GloLightImporter
    {
        /// <summary>
        /// Parse edilmiş ışık verisi.
        /// </summary>
        public class LightData
        {
            public string Name;

            // CN3Transform fields
            public Vector3 Position;    // m_vPos
            public Quaternion Rotation; // m_qRot
            public Vector3 Scale;       // m_vScale

            // __Light / __D3DLight9 fields
            public int Type;            // 1=Point, 2=Spot, 3=Directional
            public Color Diffuse;
            public Color Specular;
            public Color Ambient;
            public Vector3 LightPosition;  // __D3DLight9.Position
            public Vector3 Direction;
            public float Range;
            public float Falloff;
            public float Attenuation0;
            public float Attenuation1;
            public float Attenuation2;
            public float Theta;
            public float Phi;
            public bool IsOn;
            public int Number;
        }

        /// <summary>
        /// .glo dosyasını parse eder.
        /// Open-KO: LightMgr::LoadZoneLight (LightMgr.cpp:149-169)
        /// </summary>
        public static List<LightData> Load(string gloPath)
        {
            var lights = new List<LightData>();

            if (!KOBinaryProvider.Exists(gloPath))
            {
                Debug.LogWarning($"[GLO] Dosya bulunamadı: {gloPath}");
                return lights;
            }

            try
            {
                using var fs = File.OpenRead(gloPath);
                using var br = new BinaryReader(fs);

                // LightMgr.cpp:158-159 — int iVersion; file.Read(&iVersion, sizeof(int));
                int version = br.ReadInt32();

                // LightMgr.cpp:161-162 — int cnt; file.Read(&cnt, sizeof(int));
                int cnt = br.ReadInt32();

                for (int i = 0; i < cnt; i++)
                {
                    var light = ParseLight(br);
                    if (light != null)
                        lights.Add(light);
                }

            }
            catch (Exception ex)
            {
                Debug.LogError($"[GLO] Parse hatası: {gloPath} — {ex.Message}");
            }

            return lights;
        }

        /// <summary>
        /// Tek bir CN3Light nesnesini okur.
        /// CN3Light::Load çağrı zinciri:
        ///   CN3Transform::Load → CN3BaseFileAccess::Load → name
        ///                      → pos + rot + scale
        ///                      → AnimKey×3
        ///   CN3Light::Load     → __Light struct (112 bytes)
        /// </summary>
        private static LightData ParseLight(BinaryReader br)
        {
            var light = new LightData();

            // ─── CN3BaseFileAccess::Load (N3BaseFileAccess.cpp:49-68) ───
            int nameLen = br.ReadInt32();
            if (nameLen > 0 && nameLen <= 256)
                light.Name = new string(br.ReadChars(nameLen));
            else if (nameLen > 256)
                return null; // invalid data
            else
                light.Name = "";

            // ─── CN3Transform::Load (N3Transform.cpp:44-77) ───
            // file.Read(&m_vPos, sizeof(__Vector3));       — 12 bytes
            float px = br.ReadSingle();
            float py = br.ReadSingle();
            float pz = br.ReadSingle();
            light.Position = new Vector3(px, py, pz);

            // file.Read(&m_qRot, sizeof(__Quaternion));    — 16 bytes
            float qx = br.ReadSingle();
            float qy = br.ReadSingle();
            float qz = br.ReadSingle();
            float qw = br.ReadSingle();
            light.Rotation = new Quaternion(qx, qy, qz, qw);

            // file.Read(&m_vScale, sizeof(__Vector3));     — 12 bytes
            float sx = br.ReadSingle();
            float sy = br.ReadSingle();
            float sz = br.ReadSingle();
            light.Scale = new Vector3(sx, sy, sz);

            // ─── AnimKey×3 (N3AnimKey.cpp:70-100) ───
            SkipAnimKey(br); // m_KeyPos
            SkipAnimKey(br); // m_KeyRot
            SkipAnimKey(br); // m_KeyScale

            // ─── CN3Light::Load (N3Light.cpp:24-33) ───
            // file.Read(&m_Data, sizeof(m_Data));  — 112 bytes total

            // D3DLIGHTTYPE Type (4 bytes)
            light.Type = br.ReadInt32();

            // ColorValue Diffuse (r,g,b,a — 4×float = 16 bytes)
            light.Diffuse = ReadColorValue(br);

            // ColorValue Specular (16 bytes)
            light.Specular = ReadColorValue(br);

            // ColorValue Ambient (16 bytes)
            light.Ambient = ReadColorValue(br);

            // Vector3 Position (12 bytes) — D3DLight9.Position
            light.LightPosition = new Vector3(
                br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

            // Vector3 Direction (12 bytes)
            light.Direction = new Vector3(
                br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

            // float Range, Falloff, Atten0, Atten1, Atten2, Theta, Phi
            light.Range = br.ReadSingle();
            light.Falloff = br.ReadSingle();
            light.Attenuation0 = br.ReadSingle();
            light.Attenuation1 = br.ReadSingle();
            light.Attenuation2 = br.ReadSingle();
            light.Theta = br.ReadSingle();
            light.Phi = br.ReadSingle();

            // BOOL bOn (4 bytes) + int nNumber (4 bytes)
            light.IsOn = br.ReadInt32() != 0;
            light.Number = br.ReadInt32();

            return light;
        }

        /// <summary>
        /// CN3AnimKey::Load — animasyon key'lerini skip eder.
        /// Format:
        ///   int32 count
        ///   if count > 0:
        ///     int32 type (0=Vector3, 1=Quaternion)
        ///     float samplingRate
        ///     data[count] — Vector3(12) veya Quaternion(16) per key
        /// </summary>
        private static void SkipAnimKey(BinaryReader br)
        {
            int count = br.ReadInt32();
            if (count <= 0) return;

            int type = br.ReadInt32();  // KEY_VECTOR3=0, KEY_QUATERNION=1
            float _ = br.ReadSingle();  // samplingRate

            int bytesPerKey = type == 0 ? 12 : 16; // Vector3=12, Quaternion=16
            br.BaseStream.Seek(count * bytesPerKey, SeekOrigin.Current);
        }

        /// <summary>
        /// D3DCOLORVALUE okur (r,g,b,a — 4×float = 16 bytes).
        /// </summary>
        private static Color ReadColorValue(BinaryReader br)
        {
            float r = br.ReadSingle();
            float g = br.ReadSingle();
            float b = br.ReadSingle();
            float a = br.ReadSingle();
            return new Color(r, g, b, a);
        }
    }
}
