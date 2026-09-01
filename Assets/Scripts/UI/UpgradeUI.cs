using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO v1.298: UIItemUpgrade.cpp event routing katmanÄ±.
    /// 
    /// KOPacketHandler event'lerini KOItemUpgradeManager'a yÃ¶nlendirir.
    /// C++ karÅŸÄ±lÄ±ÄŸÄ±: GameProcMain.cpp:1055-1056 â€” case WIZ_ITEM_UPGRADE: m_pUIItemUpgrade->MsgRecv_ItemUpgrade(pkt)
    /// </summary>
    public class UpgradeUI : MonoBehaviour
    {
        public static UpgradeUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (KOItemUpgradeManager.Instance == null)
            {
                gameObject.AddComponent<KOItemUpgradeManager>();
            }

            if (KOFastUpgradeManager.Instance == null)
            {
                gameObject.AddComponent<KOFastUpgradeManager>();
            }
        }

        private void OnEnable()
        {
            KOPacketHandler.OnItemUpgrade += HandleItemUpgrade_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnItemUpgrade -= HandleItemUpgrade_KO;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1055-1056
        /// case WIZ_ITEM_UPGRADE: m_pUIItemUpgrade->MsgRecv_ItemUpgrade(pkt)
        /// 
        /// KOPacketHandler'dan gelen upgrade sonucunu KOItemUpgradeManager'a yÃ¶nlendirir.
        /// </summary>
        private void HandleItemUpgrade_KO(byte[] rawData)
        {
            // Open-KO birebir: MsgRecv_ItemUpgrade (GameProcMain.cpp:8009-8041)
            // Wire: [opcode][subOpcode:byte][...]
            // subOpcode: ITEM_UPGRADE_REQ(npcId open), ITEM_UPGRADE_PROCESS(result), ITEM_UPGRADE_ACCESSORIES
            var r = new KOPacketReader(rawData);
            byte subOpcode = r.ReadByte();

            if (subOpcode == 1) // ITEM_UPGRADE_REQ
            {
                short npcId = r.ReadInt16();
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.ShowUpgradeSelect(true, npcId);
                }
            }
            else if (subOpcode == 3) // ITEM_UPGRADE_ACCESSORIES
            {
                if (KOAccessoryUpgradeManager.Instance != null)
                {
                    KOAccessoryUpgradeManager.Instance.HandleRawAccessoryUpgradePacket(rawData);
                }
                else
                {
                    Debug.LogWarning("[UPGRADE] KOAccessoryUpgradeManager bulunamadı – takı upgrade sonucu işlenemedi.");
                }
            }
            else
            {
                // Delegate full raw data to KOItemUpgradeManager/KOFastUpgradeManager for detailed processing
                bool isFast = KOUIManager.Instance != null && KOUIManager.Instance.IsFastUpgradeUIOpen;
                IKOUpgradeManager mgr = isFast ? (IKOUpgradeManager)KOFastUpgradeManager.Instance : (IKOUpgradeManager)KOItemUpgradeManager.Instance;
                if (mgr != null)
                {
                    mgr.HandleRawUpgradePacket(subOpcode, rawData);
                }
                else
                {
                    Debug.LogWarning($"[UPGRADE] UpgradeManager (isFast={isFast}) bulunamadı – sonuç işlenemedi.");
                }
            }
        }

        private void HandleUpgradeResult(long instanceId, bool success, byte resultType,
            short newLevel, short newAtkMin, short newAtkMax, short newDef)
        {
            bool isFast = KOUIManager.Instance != null && KOUIManager.Instance.IsFastUpgradeUIOpen;
            IKOUpgradeManager mgr = isFast ? (IKOUpgradeManager)KOFastUpgradeManager.Instance : (IKOUpgradeManager)KOItemUpgradeManager.Instance;
            if (mgr != null)
            {
                mgr.MsgRecv_ItemUpgrade(instanceId, success, resultType,
                    newLevel, newAtkMin, newAtkMax, newDef);
            }
            else
            {
                Debug.LogWarning("[UPGRADE] KOItemUpgradeManager bulunamadÄ± â€” sonuÃ§ iÅŸlenemedi.");
            }
        }

        /// <summary>
        /// NPC Event'ten upgrade paneli aÃ§Ä±lÄ±rken Ã§aÄŸrÄ±lÄ±r.
        /// C++ karÅŸÄ±lÄ±ÄŸÄ±: GameProcMain.cpp â€” SendItemUpgradeRequest(npcId)
        /// </summary>
        public void Open(int npcId)
        {
            var mgr = KOItemUpgradeManager.Instance;
            if (mgr != null)
            {
                mgr.SetNpcID(npcId);
                mgr.ResetUpgradeInventory();
            }

            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowItemUpgrade(true);

        }
    }
}
