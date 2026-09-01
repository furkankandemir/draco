using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 N3Chr (Character) Binary Parser
    /// 
    /// CN3Chr::Load() birebir portu.
    /// 
    /// .N3Chr dosya formatı:
    ///   CN3TransformCollision::Load:
    ///     CN3Transform::Load:
    ///       CN3BaseFileAccess::Load: int32 nameLen + char[] name
    ///       Vec3 position (12 bytes)
    ///       Quat rotation (16 bytes)
    ///       Vec3 scale (12 bytes)
    ///       AnimKey pos, rot, scale (3x)
    ///     int32 collisionMeshNameLen + char[]
    ///     int32 climbMeshNameLen + char[]
    ///   int32 jointFileNameLen + char[] (.N3Joint)
    ///   int32 partCount
    ///     For each: int32 nameLen + char[] (.N3CPart external file)
    ///   int32 plugCount
    ///     For each: int32 nameLen + char[] (.N3CPlug external file)
    ///   int32 aniCtrlNameLen + char[] (.N3AniCtrl)
    ///   int32[2] jointPartStarts
    ///   int32[2] jointPartEnds
    ///   int32 fxPlugNameLen + char[] (.N3FXPlug)
    /// 
    /// .N3Joint dosya formatı (rekürsif ağaç):
    ///   CN3BaseFileAccess::Load: version + name
    ///   CN3Joint::Load:
    ///     CN3Transform::Load (name + pos + rot + scale + 3x animkey)
    ///     AnimKey orient (4th key — joint-specific)
    ///     int32 childCount
    ///     For each child: CN3Joint::Load (recursive)
    /// </summary>
    public static class N3ChrImporter
    {
        #region Data Structures

        /// <summary>Animation key data (per-joint keyframes).</summary>
        public class AnimKeyData
        {
            public int Type; // 0=Vector3, 1=Quaternion
            public float SamplingRate;
            public Vector3[] VectorKeys;     // type=0
            public Quaternion[] QuatKeys;    // type=1

            public int Count => (Type == 0)
                ? (VectorKeys?.Length ?? 0)
                : (QuatKeys?.Length ?? 0);
        }

        /// <summary>Skeleton joint node.</summary>
        public class JointNode
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public List<JointNode> Children = new();
            public int Index; // Flat index for skinning

            // Animation keys (per-joint keyframe data)
            public AnimKeyData KeyPos;    // Position keyframes
            public AnimKeyData KeyRot;    // Rotation keyframes
            public AnimKeyData KeyScale;  // Scale keyframes
            public AnimKeyData KeyOrient; // Joint orient keyframes
        }

        /// <summary>Tam character verisi.</summary>
        public class N3ChrData
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;

            // Collision/Climb mesh referansları
            public string CollisionMeshName;
            public string ClimbMeshName;

            // Skeleton
            public string JointFileName;
            public JointNode RootJoint;
            public int TotalJointCount;

            // Part referansları (.N3CPart — skinned mesh parçaları)
            public List<string> PartFileNames = new();

            // Plug referansları (.N3CPlug — ekipman/silah attach noktaları)
            public List<string> PlugFileNames = new();

            // Animation controller referansı
            public string AniCtrlFileName;

            // Joint part ayrımı (üst/alt gövde animasyon)
            public int[] JointPartStarts = new int[2];
            public int[] JointPartEnds = new int[2];

            // FX plug referansı
            public string FXPlugFileName;
        }

        #endregion

        #region Main Parser

        /// <summary>
        /// .N3Chr dosyasını parse eder.
        /// </summary>
        public static N3ChrData Load(string chrPath)
        {
            try
            {
                using var reader = KOBinaryProvider.OpenReader(chrPath);
                if (reader == null)
                {
                    Debug.LogError($"[N3Chr] Dosya bulunamadı: {chrPath}");
                    return null;
                }
                return ParseChr(reader, chrPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3Chr] Parse hatası ({Path.GetFileName(chrPath)}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// CN3Chr::Load() birebir portu.
        /// </summary>
        private static N3ChrData ParseChr(BinaryReader reader, string path)
        {
            var data = new N3ChrData();

            // ============================================
            // CN3TransformCollision::Load
            //   → CN3Transform::Load → CN3BaseFileAccess::Load
            // ============================================

            // CN3BaseFileAccess::Load: name
            data.Name = ReadLenString(reader);

            // CN3Transform::Load: pos + rot + scale
            data.Position = ReadVector3(reader);
            data.Rotation = ReadQuaternion(reader);
            data.Scale = ReadVector3(reader);

            // Animation keys (3x: pos, rot, scale)
            SkipAnimKey(reader);
            SkipAnimKey(reader);
            SkipAnimKey(reader);

            // CN3TransformCollision: collision + climb mesh names
            data.CollisionMeshName = ReadLenString(reader);
            data.ClimbMeshName = ReadLenString(reader);

            // ============================================
            // Joint skeleton reference
            // ============================================
            data.JointFileName = ReadLenString(reader);

            // ============================================
            // Part references (skinned mesh parts)
            // ============================================
            int partCount = reader.ReadInt32();
            for (int i = 0; i < partCount; i++)
            {
                string partName = ReadLenString(reader);
                if (!string.IsNullOrEmpty(partName))
                    data.PartFileNames.Add(partName);
            }

            // ============================================
            // Plug references (equipment attach points)
            // ============================================
            int plugCount = reader.ReadInt32();
            for (int i = 0; i < plugCount; i++)
            {
                string plugName = ReadLenString(reader);
                if (!string.IsNullOrEmpty(plugName))
                    data.PlugFileNames.Add(plugName);
            }

            // ============================================
            // Animation Controller reference
            // ============================================
            data.AniCtrlFileName = ReadLenString(reader);

            // ============================================
            // Joint part indices (upper/lower body animation split)
            // ============================================
            for (int i = 0; i < 2; i++)
                data.JointPartStarts[i] = reader.ReadInt32();
            for (int i = 0; i < 2; i++)
                data.JointPartEnds[i] = reader.ReadInt32();

            // FX Plug reference (opsiyonel — ChrSelect dosyalarında olmayabilir)
            if (reader.BaseStream.Position < reader.BaseStream.Length - 4)
                data.FXPlugFileName = ReadLenString(reader);

            // ============================================
            // Joint skeleton dosyasını yükle (.N3Joint)
            // ============================================
            if (!string.IsNullOrEmpty(data.JointFileName))
            {
                string jointDir = Path.GetDirectoryName(path);
                string jointPath = Path.Combine(jointDir, 
                    Path.GetFileName(data.JointFileName));
                
                // Dosya adını normalize et
                if (!KOBinaryProvider.Exists(jointPath))
                {
                    // KO path'i düzelt (chr\xxx.n3joint → aynı klasörde ara)
                    string altPath = Path.Combine(jointDir,
                        data.JointFileName.Replace('\\', '/'));
                    if (KOBinaryProvider.Exists(altPath))
                        jointPath = altPath;
                }

                // ChrSelect joint dosyası keyframe içermeyebilir — Chr/ klasöründeki
                // büyük joint dosyasını tercih et (animasyon verileri için)
                string chrDir = Path.Combine(Path.GetDirectoryName(jointDir) ?? "", "Chr");
#if false // Resources-based
                string baseName = Path.GetFileNameWithoutExtension(data.JointFileName);
                // Sınıf sonekini kaldır: upc_el_rm_wa → upc_el_rm
                string[] classSuffixes = { "_wa", "_rog", "_pri", "_wiz", "_ma" };
                foreach (string suffix in classSuffixes)
                {
                    if (baseName.EndsWith(suffix))
                    {
                        baseName = baseName.Substring(0, baseName.Length - suffix.Length);
                        break;
                    }
                }
                string chrJointPath = Path.Combine(chrDir, baseName + ".n3joint");
                if (KOBinaryProvider.Exists(chrJointPath))
                {
                    var fi = new FileInfo(chrJointPath);
                    if (fi.Length > 100000) // 100KB+ = full keyframe data
                    {
                        jointPath = chrJointPath;
                    }
                }
#endif

                if (KOBinaryProvider.Exists(jointPath))
                {
                    data.RootJoint = LoadJointFile(jointPath);
                    if (data.RootJoint != null)
                    {
                        data.TotalJointCount = CountJoints(data.RootJoint);
                    }
                }
            }


            return data;
        }

        #endregion

        #region Joint Parser

        /// <summary>
        /// .N3Joint dosyasını parse eder.
        /// N3Joint header: CN3BaseFileAccess version+name → CN3Joint::Load rekürsif çağrı
        /// </summary>
        public static JointNode LoadJointFile(string jointPath)
        {
            try
            {
                using var reader = KOBinaryProvider.OpenReader(jointPath);
                if (reader == null) return null;

                // N3BaseFileAccess header (version + file name — dosya düzeyi)
                SkipN3FileHeader(reader);

                // CN3Joint::Load (rekürsif)
                int index = 0;
                return ParseJoint(reader, ref index);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[N3Joint] Parse hatası: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Standalone .n3joint dosyasından N3ChrData oluşturur.
        /// NPC'ler .n3chr kullanmaz — doğrudan joint dosyası referans edilir.
        /// </summary>
        public static N3ChrData LoadJointOnly(string jointPath)
        {
            var rootJoint = LoadJointFile(jointPath);
            if (rootJoint == null) return null;

            var data = new N3ChrData();
            data.Name = Path.GetFileNameWithoutExtension(jointPath);
            data.JointFileName = jointPath;
            data.RootJoint = rootJoint;
            data.TotalJointCount = CountJoints(rootJoint);
            data.Position = Vector3.zero;
            data.Rotation = Quaternion.identity;
            data.Scale = Vector3.one;

            return data;
        }

        /// <summary>
        /// CN3Joint::Load() birebir portu — rekürsif.
        /// Format:
        ///   CN3Transform::Load (name + pos + rot + scale + 3x animkey)
        ///   AnimKey orient (4th key)
        ///   int32 childCount
        ///   For each: CN3Joint::Load recursive
        /// </summary>
        private static JointNode ParseJoint(BinaryReader reader, ref int index)
        {
            var joint = new JointNode();
            joint.Index = index++;

            // CN3Transform::Load
            // CN3BaseFileAccess::Load: name
            joint.Name = ReadLenString(reader);

            // Position
            joint.Position = ReadVector3(reader);

            // Rotation (Quaternion)
            joint.Rotation = ReadQuaternion(reader);

            // Scale
            joint.Scale = ReadVector3(reader);

            // AnimKeys: pos, rot, scale (from CN3Transform)
            joint.KeyPos = ReadAnimKey(reader);
            joint.KeyRot = ReadAnimKey(reader);
            joint.KeyScale = ReadAnimKey(reader);

            // Joint-specific orient key (4th key)
            joint.KeyOrient = ReadAnimKey(reader);

            // Children
            int childCount = reader.ReadInt32();
            for (int i = 0; i < childCount; i++)
            {
                var child = ParseJoint(reader, ref index);
                if (child != null)
                    joint.Children.Add(child);
            }

            return joint;
        }

        private static int CountJoints(JointNode node)
        {
            int count = 1;
            foreach (var child in node.Children)
                count += CountJoints(child);
            return count;
        }

        #endregion

        #region Helpers

        private static string ReadLenString(BinaryReader reader)
        {
            int len = reader.ReadInt32();
            if (len <= 0) return string.Empty;
            if (len > 512)
            {
                Debug.LogWarning($"[N3Chr] String çok uzun: {len}");
                return string.Empty;
            }
            byte[] bytes = reader.ReadBytes(len);
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            return System.Text.Encoding.ASCII.GetString(
                bytes, 0, nullIdx >= 0 ? nullIdx : len).Trim();
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        private static Quaternion ReadQuaternion(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float w = reader.ReadSingle();
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// CN3AnimKey::Load() — skip (Chr root transform'daki key'ler için)
        /// </summary>
        private static void SkipAnimKey(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0) return;

            uint type = reader.ReadUInt32();
            reader.ReadSingle();

            int dataSize = (type == 0) ? (count * 12) : (count * 16);
            reader.BaseStream.Seek(dataSize, SeekOrigin.Current);
        }

        /// <summary>
        /// CN3AnimKey::Load() — full read (joint keyframe data)
        /// </summary>
        private static AnimKeyData ReadAnimKey(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0) return null;

            var key = new AnimKeyData();
            key.Type = (int)reader.ReadUInt32(); // KEY_VECTOR3=0, KEY_QUATERNION=1
            key.SamplingRate = reader.ReadSingle();

            if (key.Type == 0) // Vector3
            {
                key.VectorKeys = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    key.VectorKeys[i] = ReadVector3(reader);
                }
            }
            else // Quaternion
            {
                key.QuatKeys = new Quaternion[count];
                for (int i = 0; i < count; i++)
                {
                    key.QuatKeys[i] = ReadQuaternion(reader);
                }
            }

            return key;
        }

        /// <summary>
        /// N3BaseFileAccess dosya-düzeyi header'ı atla.
        /// LoadFromFile → LoadSupportedVersions → Load:
        ///   int32 nameLen + char[] name
        /// Ama dosya-düzeyi erişimlerde ayrıca FileFormatVersion okunuyor (v1264+).
        /// </summary>
        private static void SkipN3FileHeader(BinaryReader reader)
        {
            // Joint dosyaları basit: sadece nameLen + name
            // (FileFormatVersion yok — doğrudan CN3BaseFileAccess::Load çağrılıyor)
            // Bu zaten ParseJoint içinde okunuyor, burada bir şey okumaya gerek yok.
            // Joint dosyası doğrudan CN3Joint::Load ile başlıyor.
        }

        #endregion
    }
}
