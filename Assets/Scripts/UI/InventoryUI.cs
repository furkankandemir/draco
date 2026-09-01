using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Inventory packet handler'ı.
    /// UI artık KOUIManager tarafından el_inventory_us.uif'den yükleniyor.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnInventoryData += HandleInventoryData_KO;
            KOPacketHandler.OnItemMove += HandleItemUseResult_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnInventoryData -= HandleInventoryData_KO;
            KOPacketHandler.OnItemMove -= HandleItemUseResult_KO;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>KO wrapper — WIZ_INVENTORY_DATA</summary>
        private void HandleInventoryData_KO(byte[] rawData)
        {
            // Route to KOInventory which handles the full Open-KO parse
            // InventoryUI doesn't need to parse — KOInventory.HandleInventoryData_KO handles the data model
        }

        /// <summary>KO wrapper — WIZ_ITEM_MOVE (item use result)</summary>
        private void HandleItemUseResult_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp MsgRecv_ItemMove
            // Wire: [opcode][result:byte] — same packet as item move, context determines meaning
            var r = new KOPacketReader(rawData);
            byte result = r.ReadByte();
        }

        /// <summary>
        /// Sunucudan gelen envanter verisini işler.
        /// C++ referans: GameProcMain.cpp MsgRecv_MyInfo → UIInventory InitIconUpdate
        /// 1. GameManager cache'e yaz
        /// 2. KOUIManager.PopulateInventory ile UIF paneline render et
        /// </summary>
        private void HandleInventoryData(InventoryItemData[] items)
        {

            // GameManager'da envanter cache'le
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
                gm.Inventory = items;

            // KOUIManager üzerinden UIF paneline doldur
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.PopulateInventory(items);
        }

        /// <summary>
        /// Item kullanım sonucu — stack güncelleme.
        /// C++ referans: UIInventory.cpp ItemCountChange() satır 2820-2830
        /// Stack azalınca veya item yok olunca inventory'yi yenile.
        /// </summary>
        private void HandleItemUseResult(long instanceId, bool success, byte healType, int healAmount, short remainingStack)
        {

            if (!success) return;

            // Cached inventory'de stack değerini güncelle
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm?.Inventory == null) return;

            bool needRefresh = false;
            for (int i = 0; i < gm.Inventory.Length; i++)
            {
                if (gm.Inventory[i].InstanceId == instanceId)
                {
                    if (remainingStack <= 0)
                    {
                        // C++ ItemCountChange: item tükendi → listeden kaldır
                        var list = new System.Collections.Generic.List<InventoryItemData>(gm.Inventory);
                        list.RemoveAt(i);
                        gm.Inventory = list.ToArray();
                    }
                    else
                    {
                        gm.Inventory[i].StackCount = remainingStack;
                    }
                    needRefresh = true;
                    break;
                }
            }

            // UI'ı yenile
            if (needRefresh && KOUIManager.Instance != null)
                KOUIManager.Instance.PopulateInventory(gm.Inventory);
        }

        public void SendEquipItem(long instanceId, byte slot)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_ITEM_MOVE + equip sub-opcode
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_MOVE);
            pkt.WriteByte(KOInventory.ITEM_MOVE_INV_TO_ARM);
            pkt.WriteInt64(instanceId);
            pkt.WriteByte(slot);
            netMgr.SendPacket(pkt);
        }

        public void SendUnequipItem(long instanceId, byte slot)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_ITEM_MOVE + unequip sub-opcode
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_MOVE);
            pkt.WriteByte(KOInventory.ITEM_MOVE_ARM_TO_INV);
            pkt.WriteInt64(instanceId);
            pkt.WriteByte(slot);
            netMgr.SendPacket(pkt);
        }

        public void SendUseItem(long instanceId)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_ITEM_MOVE (use consumable)
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_MOVE);
            pkt.WriteInt64(instanceId);
            netMgr.SendPacket(pkt);
        }
    }
}
