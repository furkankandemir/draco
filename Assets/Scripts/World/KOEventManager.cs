using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Import;
using KO;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO birebir: RECT yapısı (left, top, right, bottom)
    /// </summary>
    public struct KOEventRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    /// <summary>
    /// Open-KO birebir: CEventCell (EventManager.h:12-21)
    /// </summary>
    public class KOEventCell
    {
        public short m_sEventType;
        public KOEventRect m_Rect;

        public KOEventCell()
        {
            m_sEventType = 1;
            m_Rect = default;
        }

        public void Load(BinaryReader reader)
        {
            // sizeof(RECT) = 16 bytes (4 x int32)
            m_Rect.left = reader.ReadInt32();
            m_Rect.top = reader.ReadInt32();
            m_Rect.right = reader.ReadInt32();
            m_Rect.bottom = reader.ReadInt32();

            // sizeof(int16_t) = 2 bytes
            m_sEventType = reader.ReadInt16();
        }
    }

    /// <summary>
    /// Open-KO birebir: CEventManager (EventManager.h:26-41)
    /// Haritada tanımlanmış etkinlik bölgelerini (GEV dosyaları) yönetir.
    /// Oyuncu bir bölgeye girdiğinde veya çıktığında davranış (Behavior) tetikler.
    /// </summary>
    public class KOEventManager : MonoBehaviour
    {
        public static KOEventManager Instance { get; private set; }

        private readonly List<KOEventCell> m_lstEvents = new List<KOEventCell>();
        private short m_sEventType = -1;
        private KOEventRect m_rcEvent;

        private const int EVENT_TYPE_POISON = 3;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Open-KO birebir: CEventManager::Release() (EventManager.cpp:71-79)
        /// </summary>
        public void Release()
        {
            m_sEventType = -1;
            m_rcEvent = default;
            m_lstEvents.Clear();
        }

        /// <summary>
        /// Open-KO birebir: CEventManager::LoadFromFile (EventManager.cpp:41-69)
        /// </summary>
        public bool LoadFromFile(string szFileName)
        {
            Release();

            using (BinaryReader reader = KOBinaryProvider.OpenReader(szFileName))
            {
                if (reader == null)
                {
                    Debug.LogWarning($"[KOEventManager] Failed to load GEV file (not found or failed to open): {szFileName}");
                    return false;
                }

                try
                {
                    int nEventCellCount = reader.ReadInt32(); // Read count of cells (sizeof(int))
                    for (int i = 0; i < nEventCellCount; i++)
                    {
                        var pEventCell = new KOEventCell();
                        pEventCell.Load(reader);
                        m_lstEvents.Add(pEventCell);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[KOEventManager] Error reading GEV file {szFileName}: {ex}");
                }
            }

            return false;
        }

        /// <summary>
        /// Open-KO birebir: CEventManager::SetPos (EventManager.cpp:81-113)
        /// </summary>
        public short SetPos(float fX, float fZ)
        {
            int x = (int)fX;
            int y = (int)fZ; // Note: C++ uses int y = (int)fZ;

            if (PtInRect(x, y, m_rcEvent))
                return m_sEventType;

            foreach (var pEventCell in m_lstEvents)
            {
                if (pEventCell == null) continue;

                if (!PtInRect(x, y, pEventCell.m_Rect))
                    continue;

                if (m_sEventType != pEventCell.m_sEventType)
                    Behavior(pEventCell.m_sEventType, m_sEventType);

                m_rcEvent = pEventCell.m_Rect;
                m_sEventType = pEventCell.m_sEventType;
                return pEventCell.m_sEventType;
            }

            if (m_sEventType != -1)
            {
                Behavior(-1, m_sEventType);
                m_sEventType = -1;
                m_rcEvent = default;
            }

            return m_sEventType;
        }

        /// <summary>
        /// Open-KO birebir: CEventManager::PtInRect (EventManager.cpp:115-127)
        /// </summary>
        public bool PtInRect(int x, int z, KOEventRect rc)
        {
            if (x < rc.left) return false;
            if (x > rc.right) return false;
            if (z < rc.top) return false;
            if (z > rc.bottom) return false;
            return true;
        }

        /// <summary>
        /// Open-KO birebir: CEventManager::Behavior (EventManager.cpp:129-158)
        /// </summary>
        public void Behavior(short sEventType, short sPreEventType)
        {
            int myId = GameManager.Instance != null ? (int)GameManager.Instance.CharacterId : -1;
            if (myId < 0) return;

            switch (sPreEventType)
            {
                case EVENT_TYPE_POISON:
                {
                    if (KOFXManager.Instance != null)
                    {
                        // Open-KO: CGameProcedure::s_pFX->Stop(iID, iID, iFX, -1, true);
                        KOFXManager.Instance.Stop(myId, myId, KOFXManager.FXID_REGION_POISON, -1, true);
                    }
                    break;
                }
            }

            switch (sEventType)
            {
                case EVENT_TYPE_POISON:
                {
                    if (KOFXManager.Instance != null)
                    {
                        // Open-KO: CGameProcedure::s_pFX->TriggerBundle(iID, 0, iFX, iID, -1, FX_BUNDLE_REGION_POISON);
                        // C++ passes FX_BUNDLE_REGION_POISON (5) as the 6th argument (idx).
                        // In our KOFXManager.cs wrapper, we trigger this properly by supplying it as moveType, 
                        // as we discovered the C++ client had a signature mismatch but mapped it internally.
                        KOFXManager.Instance.TriggerBundle(myId, 0, KOFXManager.FXID_REGION_POISON, myId, -1, 0, KOFXManager.FX_BUNDLE_REGION_POISON);
                    }
                    break;
                }
            }
        }
    }
}
