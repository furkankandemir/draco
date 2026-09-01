using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EntropyOnline.World
{
    public class KOPvsManager
    {
        public const string IndoorFolder = "N3Indoor";
        public const float m_fVolumeOffs = 0.6f;

        public List<KOPortalVolume> m_pPvsList = new List<KOPortalVolume>();
        public KOPortalVolume m_pCurVol = null;

        // Global list of shapes loaded by the manager
        public static List<KOPortalVolume.ShapeInfo> s_plShapeInfoList = new List<KOPortalVolume.ShapeInfo>();

        private HashSet<GameObject> m_VisibleObjects = new HashSet<GameObject>();
        private List<GameObject> m_AllSpawnedObjects = new List<GameObject>();

        public static KOPortalVolume.ShapeInfo GetShapeInfoByManager(int id)
        {
            for (int i = 0; i < s_plShapeInfoList.Count; i++)
            {
                if (s_plShapeInfoList[i].ID == id)
                    return s_plShapeInfoList[i];
            }
            return null;
        }

        public KOPvsManager()
        {
            s_plShapeInfoList.Clear();
            m_pCurVol = null;
        }

        public void DeleteAllPvsObj()
        {
            m_pPvsList.Clear();
            s_plShapeInfoList.Clear();
            m_pCurVol = null;
            m_VisibleObjects.Clear();
            m_AllSpawnedObjects.Clear();
        }

        public static string ReadDecryptString(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0) return "";
            byte[] bytes = reader.ReadBytes(count);
            for (int i = 0; i < count; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ 0x16);
            }
            int nullIdx = Array.IndexOf(bytes, (byte)0);
            int length = nullIdx >= 0 ? nullIdx : count;
            return System.Text.Encoding.ASCII.GetString(bytes, 0, length).Trim();
        }

        public bool Load(BinaryReader reader)
        {
            DeleteAllPvsObj();

            if (reader.BaseStream.Length - reader.BaseStream.Position < 4)
            {
                Debug.LogWarning("[PVS] File too short to read version");
                return false;
            }

            int version = reader.ReadInt32();
            if (version != 1)
            {
                Debug.LogWarning($"[PVS] Version mismatch: expected 1, got {version}");
                return false;
            }

            // N3Scene file name - decrypt and ignore
            string strSrc = ReadDecryptString(reader);

            // Total offset - ignore
            reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();

            int shapeCount = reader.ReadInt32();
            for (int i = 0; i < shapeCount; i++)
            {
                var pSI = new KOPortalVolume.ShapeInfo();
                pSI.ID = reader.ReadInt32();

                string strSrcShape = ReadDecryptString(reader);
                string strDest = Path.GetFileName(strSrcShape);
                pSI.ShapeFile = Path.Combine(IndoorFolder, strDest).Replace('\\', '/');

                pSI.Belong = reader.ReadInt32();
                pSI.EventID = reader.ReadInt32();
                pSI.EventType = reader.ReadInt32();
                pSI.NPC_ID = reader.ReadInt32();
                pSI.NPC_Status = reader.ReadInt32();

                pSI.Load(reader);
                s_plShapeInfoList.Add(pSI);
            }

            int volumeCount = reader.ReadInt32();
            for (int i = 0; i < volumeCount; i++)
            {
                int id = reader.ReadInt32();
                var pVol = new KOPortalVolume();
                pVol.Manager = this;
                pVol.ID = id;
                pVol.Load(reader);
                m_pPvsList.Add(pVol);
            }

            // Link visible portal volumes
            for (int i = 0; i < m_pPvsList.Count; i++)
            {
                var pVol = m_pPvsList[i];
                for (int j = 0; j < pVol.VisibleIDList.Count; j++)
                {
                    var idap = pVol.VisibleIDList[j];
                    var pVolTo = GetPortalVolPointerByID(idap.ID);
                    if (pVolTo != null)
                    {
                        KOPortalVolume.VisPortalPriority vPP;
                        vPP.Vol = pVolTo;
                        vPP.Priority = idap.Priority;
                        pVol.VisiblePvsList.Add(vPP);
                    }
                }
                pVol.VisibleIDList.Clear();
            }

            return true;
        }

        public KOPortalVolume GetPortalVolPointerByID(int id)
        {
            for (int i = 0; i < m_pPvsList.Count; i++)
            {
                if (m_pPvsList[i].ID == id)
                    return m_pPvsList[i];
            }
            return null;
        }

        public void RegisterSpawnedObject(GameObject go)
        {
            if (go != null && !m_AllSpawnedObjects.Contains(go))
            {
                m_AllSpawnedObjects.Add(go);
            }
        }

        public void Tick(Vector3 playerPos, bool bWarp = false)
        {
            Vector3 vec = playerPos;
            vec.y += m_fVolumeOffs;

            m_pCurVol = null;
            for (int i = 0; i < m_pPvsList.Count; i++)
            {
                var pVol = m_pPvsList[i];
                if (pVol.IsInVolumn(vec))
                {
                    m_pCurVol = pVol;
                    break;
                }
            }

            // Set all render states to unknown
            for (int i = 0; i < m_pPvsList.Count; i++)
            {
                m_pPvsList[i].RenderTypeState = RenderType.Unknown;
            }

            if (m_pCurVol == null)
            {
                UpdateVisibility();
                return;
            }

            // Set current volume and its visible portal volumes to RenderType.True
            m_pCurVol.RenderTypeState = RenderType.True;
            for (int i = 0; i < m_pCurVol.VisiblePvsList.Count; i++)
            {
                m_pCurVol.VisiblePvsList[i].Vol.RenderTypeState = RenderType.True;
            }

            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            // 1. Reset visible parts array for all global shapes
            for (int i = 0; i < s_plShapeInfoList.Count; i++)
            {
                var pGlobalSI = s_plShapeInfoList[i];
                int partCount = pGlobalSI.SpawnedGameObjects.Count;
                if (pGlobalSI.VisibleParts == null || pGlobalSI.VisibleParts.Length != partCount)
                {
                    pGlobalSI.VisibleParts = new bool[partCount];
                }
                for (int k = 0; k < partCount; k++)
                {
                    pGlobalSI.VisibleParts[k] = false;
                }
            }

            // 2. Mark visible parts based on active volumes
            for (int i = 0; i < m_pPvsList.Count; i++)
            {
                var pVol = m_pPvsList[i];
                if (pVol.RenderTypeState == RenderType.True)
                {
                    // Fully visible shapes
                    for (int j = 0; j < pVol.ShapeInfoList.Count; j++)
                    {
                        var pSI = pVol.ShapeInfoList[j];
                        var pGlobalSI = GetShapeInfoByManager(pSI.ID);
                        if (pGlobalSI != null && pGlobalSI.VisibleParts != null)
                        {
                            for (int k = 0; k < pGlobalSI.VisibleParts.Length; k++)
                            {
                                pGlobalSI.VisibleParts[k] = true;
                            }
                        }
                    }

                    // Partially visible shapes (ShapeParts)
                    for (int j = 0; j < pVol.ShapePartList.Count; j++)
                    {
                        var pSP = pVol.ShapePartList[j];
                        var pGlobalSI = GetShapeInfoByManager(pSP.ID);
                        if (pGlobalSI != null && pGlobalSI.VisibleParts != null)
                        {
                            for (int k = 0; k < pSP.IndexList.Count; k++)
                            {
                                int partIndex = pSP.IndexList[k].PartIndex;
                                if (partIndex >= 0 && partIndex < pGlobalSI.VisibleParts.Length)
                                {
                                    pGlobalSI.VisibleParts[partIndex] = true;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Apply visibility to the renderers of all global shapes
            for (int i = 0; i < s_plShapeInfoList.Count; i++)
            {
                var pGlobalSI = s_plShapeInfoList[i];
                for (int k = 0; k < pGlobalSI.SpawnedGameObjects.Count; k++)
                {
                    var go = pGlobalSI.SpawnedGameObjects[k];
                    if (go == null) continue;

                    bool shouldBeVisible = pGlobalSI.VisibleParts[k];

                    var mr = go.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        if (mr.enabled != shouldBeVisible)
                        {
                            mr.enabled = shouldBeVisible;
                        }
                    }
                    else
                    {
                        var renderers = go.GetComponentsInChildren<Renderer>(true);
                        for (int r = 0; r < renderers.Length; r++)
                        {
                            if (renderers[r].enabled != shouldBeVisible)
                            {
                                renderers[r].enabled = shouldBeVisible;
                            }
                        }
                    }
                }
            }
        }
    }
}
