using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.World
{
    public enum RenderType : byte
    {
        Unknown = 0,
        True = 1
    }

    public class KOPortalVolume
    {
        public class ShapeInfo
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Matrix4x4 Matrix;

            public int ID;
            public string ShapeFile;
            public int Belong;
            public int EventID;
            public int EventType;
            public int NPC_ID;
            public int NPC_Status;

            // Instantiated GameObjects in Unity
            public List<GameObject> SpawnedGameObjects = new List<GameObject>();
            public bool[] VisibleParts;

            public void RebuildMatrix()
            {
                Matrix = Matrix4x4.TRS(Position, Rotation, Scale);
            }

            public void Load(BinaryReader reader)
            {
                // CN3Transform::Load
                // CN3BaseFileAccess::Load
                int nameLen = reader.ReadInt32();
                if (nameLen > 0)
                {
                    byte[] nameBytes = reader.ReadBytes(nameLen);
                    int nullIdx = Array.IndexOf(nameBytes, (byte)0);
                    int length = nullIdx >= 0 ? nullIdx : nameLen;
                    Name = System.Text.Encoding.ASCII.GetString(nameBytes, 0, length).Trim();
                }
                else
                {
                    Name = "";
                }

                // Position
                float px = reader.ReadSingle();
                float py = reader.ReadSingle();
                float pz = reader.ReadSingle();
                Position = new Vector3(px, py, pz);

                // Rotation
                float qx = reader.ReadSingle();
                float qy = reader.ReadSingle();
                float qz = reader.ReadSingle();
                float qw = reader.ReadSingle();
                Rotation = new Quaternion(qx, qy, qz, qw);

                // Scale
                float sx = reader.ReadSingle();
                float sy = reader.ReadSingle();
                float sz = reader.ReadSingle();
                Scale = new Vector3(sx, sy, sz);

                // Skip 3 anim keys
                SkipAnimKey(reader);
                SkipAnimKey(reader);
                SkipAnimKey(reader);

                RebuildMatrix();
            }

            private void SkipAnimKey(BinaryReader reader)
            {
                int count = reader.ReadInt32();
                if (count <= 0) return;

                uint type = reader.ReadUInt32();
                float samplingRate = reader.ReadSingle();

                int dataSize = (type == 0) ? count * 12 : count * 16;
                reader.BaseStream.Seek(dataSize, SeekOrigin.Current);
            }
        }

        public class VPI
        {
            public int PartIndex;
            public List<int> Vector = new List<int>();
        }

        public class ShapePart
        {
            public int ID;
            public List<VPI> IndexList = new List<VPI>();
        }

        public class CollisionIndex
        {
            public int ID;
            public List<uint> Vector = new List<uint>();
        }

        public struct IDAndPriority
        {
            public int ID;
            public int Priority;
        }

        public struct VisPortalPriority
        {
            public KOPortalVolume Vol;
            public int Priority;
        }

        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Matrix4x4 Matrix;

        public int ID;
        public KOPvsManager Manager;
        public RenderType RenderTypeState = RenderType.Unknown;

        public const float BaseVolumnSize = 1.0f;
        public readonly float Offs = 0.001f;
        public readonly float HeightOffs = 0.01f;
        public readonly float VolOffs = 0.001f;
        public readonly float PickIncline = 0.6f;
        public readonly float CameraOffs = 0.4f;

        public List<ShapeInfo> ShapeInfoList = new List<ShapeInfo>();
        public List<IDAndPriority> VisibleIDList = new List<IDAndPriority>();
        public List<ShapePart> ShapePartList = new List<ShapePart>();
        public List<CollisionIndex> ShapeColPartList = new List<CollisionIndex>();
        public List<VisPortalPriority> VisiblePvsList = new List<VisPortalPriority>();

        public Vector3[] Vertex = new Vector3[8];
        public ushort[] Index = new ushort[36];
        public int Priority = 100;

        public KOPortalVolume()
        {
            Vertex[0] = new Vector3(-BaseVolumnSize, -BaseVolumnSize, BaseVolumnSize);
            Vertex[1] = new Vector3(BaseVolumnSize, -BaseVolumnSize, BaseVolumnSize);
            Vertex[2] = new Vector3(BaseVolumnSize, -BaseVolumnSize, -BaseVolumnSize);
            Vertex[3] = new Vector3(-BaseVolumnSize, -BaseVolumnSize, -BaseVolumnSize);
            Vertex[4] = new Vector3(-BaseVolumnSize, BaseVolumnSize, BaseVolumnSize);
            Vertex[5] = new Vector3(BaseVolumnSize, BaseVolumnSize, BaseVolumnSize);
            Vertex[6] = new Vector3(BaseVolumnSize, BaseVolumnSize, -BaseVolumnSize);
            Vertex[7] = new Vector3(-BaseVolumnSize, BaseVolumnSize, -BaseVolumnSize);

            ushort[] indexVal = {
                // Bottom
                0, 1, 3, 2, 3, 1,
                // Front
                7, 3, 6, 2, 6, 3,
                // Left
                4, 0, 7, 3, 7, 0,
                // Right
                6, 2, 5, 1, 5, 2,
                // Back
                5, 1, 4, 0, 4, 1,
                // Top
                4, 7, 5, 6, 5, 7
            };
            Array.Copy(indexVal, Index, 36);

            ID = -1;
            Manager = null;
            Priority = 100;
            RenderTypeState = RenderType.Unknown;
        }

        public bool IsInVolumn(Vector3 vec)
        {
            Vector3[] vec2 = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                vec2[i] = Matrix.MultiplyPoint3x4(Vertex[i]);
            }

            if (vec.x >= vec2[0].x && vec.x <= vec2[1].x &&
                vec.y >= vec2[0].y && vec.y <= vec2[4].y &&
                vec.z >= vec2[2].z && vec.z <= vec2[0].z)
            {
                return true;
            }

            return false;
        }

        public void RebuildMatrix()
        {
            Matrix = Matrix4x4.TRS(Position, Rotation, Scale);
        }

        public bool Load(BinaryReader reader)
        {
            // CN3Transform::Load
            // CN3BaseFileAccess::Load
            int nameLen = reader.ReadInt32();
            if (nameLen > 0)
            {
                byte[] nameBytes = reader.ReadBytes(nameLen);
                int nullIdx = Array.IndexOf(nameBytes, (byte)0);
                int length = nullIdx >= 0 ? nullIdx : nameLen;
                Name = System.Text.Encoding.ASCII.GetString(nameBytes, 0, length).Trim();
            }
            else
            {
                Name = "";
            }

            // Position
            float px = reader.ReadSingle();
            float py = reader.ReadSingle();
            float pz = reader.ReadSingle();
            Position = new Vector3(px, py, pz);

            // Rotation
            float qx = reader.ReadSingle();
            float qy = reader.ReadSingle();
            float qz = reader.ReadSingle();
            float qw = reader.ReadSingle();
            Rotation = new Quaternion(qx, qy, qz, qw);

            // Scale
            float sx = reader.ReadSingle();
            float sy = reader.ReadSingle();
            float sz = reader.ReadSingle();
            Scale = new Vector3(sx, sy, sz);

            // Skip 3 anim keys
            SkipAnimKey(reader);
            SkipAnimKey(reader);
            SkipAnimKey(reader);

            RebuildMatrix();

            // Linked count (skipped)
            int iLinkedCount = reader.ReadInt32();
            for (int i = 0; i < iLinkedCount; i++)
            {
                reader.ReadInt32(); // iTID
                reader.ReadInt32(); // iEWT
            }

            // Shape count
            int iCount = reader.ReadInt32();
            for (int i = 0; i < iCount; i++)
            {
                ShapeInfo pSI = new ShapeInfo();
                pSI.ID = reader.ReadInt32();

                // Decrypted string
                string strSrc = KOPvsManager.ReadDecryptString(reader);
                string strDest = Path.GetFileName(strSrc);
                pSI.ShapeFile = Path.Combine(KOPvsManager.IndoorFolder, strDest);

                pSI.Belong = reader.ReadInt32();
                pSI.EventID = reader.ReadInt32();
                pSI.EventType = reader.ReadInt32();
                pSI.NPC_ID = reader.ReadInt32();
                pSI.NPC_Status = reader.ReadInt32();

                pSI.Load(reader);
                ShapeInfoList.Add(pSI);
            }

            // Visible
            iCount = reader.ReadInt32();
            for (int i = 0; i < iCount; i++)
            {
                IDAndPriority IDAP;
                IDAP.ID = reader.ReadInt32();
                IDAP.Priority = reader.ReadInt32();
                VisibleIDList.Add(IDAP);
            }

            // ShapePart
            iCount = reader.ReadInt32();
            for (int i = 0; i < iCount; i++)
            {
                ShapePart pSP = new ShapePart();
                pSP.ID = reader.ReadInt32();

                int iSize_2 = reader.ReadInt32();
                for (int j = 0; j < iSize_2; j++)
                {
                    VPI vpi = new VPI();
                    vpi.PartIndex = reader.ReadInt32();

                    int iSize_3 = reader.ReadInt32();
                    for (int k = 0; k < iSize_3; k++)
                    {
                        vpi.Vector.Add(reader.ReadInt32());
                    }
                    pSP.IndexList.Add(vpi);
                }
                ShapePartList.Add(pSP);
            }

            // Collision index
            iCount = reader.ReadInt32();
            for (int i = 0; i < iCount; i++)
            {
                CollisionIndex pCI = new CollisionIndex();
                pCI.ID = reader.ReadInt32();

                int iSize_2 = reader.ReadInt32();
                for (int j = 0; j < iSize_2; j++)
                {
                    pCI.Vector.Add(reader.ReadUInt32());
                }
                ShapeColPartList.Add(pCI);
            }

            return true;
        }

        private void SkipAnimKey(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0) return;

            uint type = reader.ReadUInt32();
            float samplingRate = reader.ReadSingle();

            int dataSize = (type == 0) ? count * 12 : count * 16;
            reader.BaseStream.Seek(dataSize, SeekOrigin.Current);
        }
    }
}
