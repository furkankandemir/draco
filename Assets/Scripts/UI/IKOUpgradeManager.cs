using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    public interface IKOUpgradeManager
    {
        bool IsUpgradeInProgress { get; set; }
        bool IsPreviewActive { get; set; }
        int PreviewResultItemId { get; set; }
        int UpgradeItemSlotInvPos { get; }
        int GetRequirementSlotPos(int slotIndex);
        bool RequestUpgradePreview(out int previewResultId);
        void SendToServerUpgradeMsg();
        void HandleRawUpgradePacket(byte subOpcode, byte[] rawData);
        void MsgRecv_ItemUpgrade(long instanceId, bool success, byte resultType, short newLevel, short newAtkMin, short newAtkMax, short newDef);
        void SetNpcID(int npcId);
        void ResetUpgradeInventory();
        bool IsUpgradeSucceeded { get; }
        void RemoveRequirementItem(int slotIndex);
        float CalculateUpgradeRate();
        int CalculateUpgradeCost();
        bool SetUpgradeItem(int invPos);
        bool SetRequirementItem(int invPos, int slotIndex = -1);
    }
}
