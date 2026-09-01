using System;
using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using System.Collections.Generic;
using EntropyOnline.UI;
using EntropyOnline.World;
using KOImport;

namespace EntropyOnline.Trade
{
    public class MerchantItemInfo
    {
        public int ItemId;
        public int Count;
        public int Price;
        public byte InvPos;      // Inventory slot (0..27)
        public byte MerchantPos; // Merchant slot (0..11)
        public short Duration;
        public int SoldCount;
        public int ClaimableCoins;

        public bool IsEmpty => ItemId == 0;

        public void Clear()
        {
            ItemId = 0;
            Count = 0;
            Price = 0;
            InvPos = 0xFF;
            MerchantPos = 0xFF;
            Duration = 0;
            SoldCount = 0;
            ClaimableCoins = 0;
        }
    }

    public class KOMerchantManager : MonoBehaviour
    {
        public static KOMerchantManager Instance { get; private set; }

        public const byte MERCHANT_OPEN = 1;
        public const byte MERCHANT_CLOSE = 2;
        public const byte MERCHANT_ITEM_ADD = 3;
        public const byte MERCHANT_ITEM_CANCEL = 4;
        public const byte MERCHANT_ITEM_LIST = 5;
        public const byte MERCHANT_ITEM_BUY = 6;
        public const byte MERCHANT_INSERT = 7;
        public const byte MERCHANT_TRADE_CANCEL = 8;
        public const byte MERCHANT_ITEM_PURCHASED = 9;
        public const byte MERCHANT_CLAIM_COIN = 10;
        public const byte MERCHANT_CONTROL_LIST_REQ = 11;

        public const int MAX_MERCH_ITEMS = 12;

        // Player's own merchant setup state
        private bool _isSellingSetup = false;
        private bool _isSelling = false;
        private int _myMerchantSocketId = -1;
        private readonly MerchantItemInfo[] _sellingSetupItems = new MerchantItemInfo[MAX_MERCH_ITEMS];

        // Looking at another player's merchant stall
        private int _targetMerchantSocketId = -1;
        private readonly MerchantItemInfo[] _targetMerchantItems = new MerchantItemInfo[MAX_MERCH_ITEMS];

        // Public getters
        public bool IsSellingSetup => _isSellingSetup;
        public bool IsSelling => _isSelling;
        public MerchantItemInfo[] SellingSetupItems => _sellingSetupItems;
        public int TargetMerchantSocketId => _targetMerchantSocketId;
        public MerchantItemInfo[] TargetMerchantItems => _targetMerchantItems;
        public string TargetMerchantName { get; set; } = string.Empty;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeData();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Register network packet handlers
            KOPacketHandler.OnMerchant += HandleMerchantPacket;
            KOPacketHandler.OnMerchantInOut += HandleMerchantInOutPacket;
        }

        private void OnDestroy()
        {
            KOPacketHandler.OnMerchant -= HandleMerchantPacket;
            KOPacketHandler.OnMerchantInOut -= HandleMerchantInOutPacket;
        }

        private void InitializeData()
        {
            for (int i = 0; i < MAX_MERCH_ITEMS; i++)
            {
                _sellingSetupItems[i] = new MerchantItemInfo();
                _sellingSetupItems[i].Clear();

                _targetMerchantItems[i] = new MerchantItemInfo();
                _targetMerchantItems[i].Clear();
            }
        }

        // ==========================================
        // CLIENT PACKET WRITERS (SEND TO SERVER)
        // ==========================================

        public void SendMerchantOpen(byte mode = 1) // 1 = Selling mode
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_OPEN);
            writer.WriteByte(mode);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantItemAdd(int itemId, int count, int price, byte srcPos, byte dstPos, byte mode = 0)
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_ITEM_ADD);
            writer.WriteInt32(itemId);
            writer.WriteUInt16((ushort)count);
            writer.WriteInt32(price); // unit price
            writer.WriteByte(srcPos);
            writer.WriteByte(dstPos);
            writer.WriteByte(mode);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantItemCancel(byte srcPos) // srcPos is the merchant slot index (0..11)
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_ITEM_CANCEL);
            writer.WriteByte(srcPos);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantInsert(string message)
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_INSERT);
            writer.WriteKOString(message ?? string.Empty);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantClose()
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_CLOSE);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantItemList(int uid)
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_ITEM_LIST);
            writer.WriteUInt16((ushort)uid);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantItemBuy(int itemId, int count, byte itemSlot, byte destSlot)
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_ITEM_BUY);
            writer.WriteInt32(itemId);
            writer.WriteUInt16((ushort)count);
            writer.WriteByte(itemSlot); // Merchant slot (0..11)
            writer.WriteByte(destSlot); // Inventory slot (0..27)

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantTradeCancel()
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_TRADE_CANCEL);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantClaimCoins(byte merchantSlot)
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_CLAIM_COIN);
            writer.WriteByte(merchantSlot);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        public void SendMerchantControlListReq()
        {
            var writer = new KOPacketWriter(WizOpcode.WIZ_MERCHANT);
            writer.WriteByte(MERCHANT_CONTROL_LIST_REQ);

            KONetworkManager.Instance?.SendPacket(writer);
        }

        // ==========================================
        // CLIENT PACKET READERS (RECV FROM SERVER)
        // ==========================================

        private void HandleMerchantPacket(byte[] rawData)
        {
            // rawData[0] is opcode (WIZ_MERCHANT), rawData[1] is sub-opcode
            var reader = new KOPacketReader(rawData);
            byte subOpcode = reader.ReadByte(); // reads sub-opcode

            switch (subOpcode)
            {
                case MERCHANT_OPEN:
                    HandleMerchantOpen(reader);
                    break;

                case MERCHANT_ITEM_ADD:
                    HandleMerchantItemAdd(reader);
                    break;

                case MERCHANT_ITEM_CANCEL:
                    HandleMerchantItemCancel(reader);
                    break;

                case MERCHANT_INSERT:
                    HandleMerchantInsert(reader);
                    break;

                case MERCHANT_CLOSE:
                    HandleMerchantClose(reader);
                    break;

                case MERCHANT_ITEM_LIST:
                    HandleMerchantItemList(reader);
                    break;

                case MERCHANT_ITEM_BUY:
                    HandleMerchantItemBuy(reader);
                    break;

                case MERCHANT_ITEM_PURCHASED:
                    HandleMerchantItemPurchased(reader);
                    break;

                case MERCHANT_TRADE_CANCEL:
                    HandleMerchantTradeCancel(reader);
                    break;

                case MERCHANT_CLAIM_COIN:
                    HandleMerchantClaimCoinRsp(reader);
                    break;

                case MERCHANT_CONTROL_LIST_REQ:
                    HandleMerchantControlListRsp(reader);
                    break;
            }
        }

        private void HandleMerchantOpen(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            if (result == 1)
            {
                _isSellingSetup = true;
                InitializeData();
                KOUIManager.Instance?.ShowMerchantSetup(true);
            }
            else
            {
                Debug.LogWarning("[MERCHANT] Open request rejected by server.");
            }
        }

        private void HandleMerchantItemAdd(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            int itemId = reader.ReadInt32();
            int count = reader.ReadUInt16();
            short duration = reader.ReadInt16();
            int gold = reader.ReadInt32();
            byte srcPos = reader.ReadByte(); // Inventory pos (with server offset)
            byte dstPos = reader.ReadByte(); // Merchant pos

            // Convert server inventory pos (14..41) back to client (0..27)
            byte clientInvPos = (byte)(srcPos - 14);

            if (result == 1)
            {
                if (dstPos < MAX_MERCH_ITEMS)
                {
                    _sellingSetupItems[dstPos].ItemId = itemId;
                    _sellingSetupItems[dstPos].Count = count;
                    _sellingSetupItems[dstPos].Price = gold;
                    _sellingSetupItems[dstPos].InvPos = clientInvPos;
                    _sellingSetupItems[dstPos].MerchantPos = dstPos;
                    _sellingSetupItems[dstPos].Duration = duration;

                    KOUIManager.Instance?.RefreshMerchantSetupUI();
                }
            }
            else
            {
                Debug.LogWarning("[MERCHANT] Item add failed.");
            }
        }

        private void HandleMerchantItemCancel(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success, negative values are error codes
            if (result == 1)
            {
                byte merchantPos = reader.ReadByte();
                if (merchantPos < MAX_MERCH_ITEMS)
                {
                    _sellingSetupItems[merchantPos].Clear();
                    KOUIManager.Instance?.RefreshMerchantSetupUI();
                }
            }
            else
            {
                Debug.LogWarning($"[MERCHANT] Item cancel failed with error code: {result}");
            }
        }

        private void HandleMerchantInsert(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            if (result == 1)
            {
                string advertMsg = reader.ReadKOString();
                int socketId = reader.ReadUInt16(); // Transient socket ID
                byte isPremium = reader.ReadByte();
                float x = reader.ReadFloat();
                float y = reader.ReadFloat();
                float z = reader.ReadFloat();

                // Read item list (12 item IDs)
                int[] items = new int[MAX_MERCH_ITEMS];
                for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                {
                    items[i] = reader.ReadInt32();
                }

                // If this is us, finalize setup
                _isSellingSetup = false;
                _isSelling = true;
                _myMerchantSocketId = socketId;

                for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                {
                    _sellingSetupItems[i].Clear();
                }

                KOUIManager.Instance?.ShowMerchantSetup(false);
                KOUIManager.Instance?.SetMerchantState(true, advertMsg);
                KOUIManager.Instance?.RefreshInventoryUI();

                // Spawn shop model at the exact coordinates
                string myName = GameManager.Instance != null ? GameManager.Instance.CharacterName : "";
                WorldBuilder.Instance?.SpawnMerchantStall(socketId, advertMsg, items, isPremium == 1, myName, x, y, z);
            }
        }

        private void HandleMerchantClose(KOPacketReader reader)
        {
            int socketId = reader.ReadUInt16();

            if (GameManager.Instance != null && (GameManager.Instance.CharacterId == socketId || (short)GameManager.Instance.CharacterId == (short)socketId || socketId == _myMerchantSocketId))
            {
                _isSelling = false;
                _myMerchantSocketId = -1;
                KOUIManager.Instance?.SetMerchantState(false, string.Empty);

                for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                {
                    _sellingSetupItems[i].Clear();
                }

                KOUIManager.Instance?.RefreshInventoryUI();
            }

            // Remove world model / sign
            WorldBuilder.Instance?.DespawnMerchantStall(socketId);
        }

        private void HandleMerchantItemList(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            int uid = reader.ReadUInt16();

            if (result == 1)
            {
                _targetMerchantSocketId = uid;
                for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                {
                    _targetMerchantItems[i].ItemId = reader.ReadInt32();
                    _targetMerchantItems[i].Count = reader.ReadUInt16();
                    _targetMerchantItems[i].Duration = reader.ReadInt16();
                    _targetMerchantItems[i].Price = reader.ReadInt32();
                    reader.ReadInt32(); // skip dummy 0
                    _targetMerchantItems[i].MerchantPos = (byte)i;
                }

                // Open readable shop UI
                KOUIManager.Instance?.ShowStallView(true);
            }
        }

        private void HandleMerchantItemBuy(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            int itemId = reader.ReadInt32();
            int leftoverCount = reader.ReadUInt16();
            byte itemSlot = reader.ReadByte();
            byte destSlot = reader.ReadByte();


            if (result == 1)
            {
                if (itemSlot < MAX_MERCH_ITEMS)
                {
                    _targetMerchantItems[itemSlot].Count = leftoverCount;
                    if (leftoverCount == 0)
                    {
                        _targetMerchantItems[itemSlot].Clear();
                    }
                }
                KOUIManager.Instance?.RefreshStallViewUI();
            }
        }

        private void HandleMerchantItemPurchased(KOPacketReader reader)
        {
            int itemId = reader.ReadInt32();
            string buyerName = reader.ReadKOString();

            var itemTbl = ItemDataManager.GetItemBasic(itemId);
            string itemName = itemTbl != null ? itemTbl.SzName : "Item";
            KOUIManager.Instance?.AddChatMessage(1, $"[Stall] The player named {buyerName} purchased '{itemName}' from you.");

            // Request latest merchant status and refresh Merchant Control UI
            SendMerchantControlListReq();
        }

        private void HandleMerchantTradeCancel(KOPacketReader reader)
        {
            short result = reader.ReadInt16();
            if (result == 1)
            {
                _targetMerchantSocketId = -1;
                KOUIManager.Instance?.ShowStallView(false);
            }
        }

        // ==========================================
        // MERCHANT VISUAL SPAWN/DESPAWN
        // ==========================================

        private void HandleMerchantInOutPacket(byte[] rawData)
        {
            // WIZ_MERCHANT_INOUT (0x69) packet structure:
            // S2C: [sub-opcode:1][socketId:2][isPremium:1][message:string1][sellerName:string1][x:float][y:float][z:float][item1..item4:int32]
            var reader = new KOPacketReader(rawData);
            byte mode = reader.ReadByte(); // 1 = Spawn, 2 = Despawn/Sold out update

            if (mode == 1)
            {
                int socketId = reader.ReadUInt16();
                byte isPremium = reader.ReadByte();
                string advertMsg = reader.ReadKOString1(); // 1-byte length prefix
                string sellerName = reader.ReadKOString1(); // 1-byte length prefix for owner name
                float x = reader.ReadFloat();
                float y = reader.ReadFloat();
                float z = reader.ReadFloat();
                
                int[] items = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    items[i] = reader.ReadInt32();
                }

                // If this is our own merchant stall, automatically sync our selling state
                string myName = GameManager.Instance != null ? GameManager.Instance.CharacterName : "";
                if (!string.IsNullOrEmpty(myName) && string.Equals(sellerName, myName, StringComparison.OrdinalIgnoreCase))
                {
                    _isSelling = true;
                    _myMerchantSocketId = socketId;
                }

                // Spawn shop model / sign in the world at the correct coordinates
                WorldBuilder.Instance?.SpawnMerchantStall(socketId, advertMsg, items, isPremium == 1, sellerName, x, y, z);
            }
            else if (mode == 2)
            {
                int socketId = reader.ReadUInt16();

                // If this is our own merchant stall, reset our selling state
                if (_isSelling && (socketId == _myMerchantSocketId || (GameManager.Instance != null && (short)GameManager.Instance.CharacterId == (short)socketId)))
                {
                    _isSelling = false;
                    _myMerchantSocketId = -1;
                    for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                    {
                        _sellingSetupItems[i].Clear();
                    }
                    KOUIManager.Instance?.RefreshInventoryUI();
                    KOUIManager.Instance?.RefreshMerchantControlUI();
                }

                // Despawn / Sold out
                WorldBuilder.Instance?.DespawnMerchantStall(socketId);
            }
        }

        public void SyncSellingItemCount(byte clientInvPos, int newCount)
        {
            if (!_isSelling) return;
            for (int i = 0; i < MAX_MERCH_ITEMS; i++)
            {
                var item = _sellingSetupItems[i];
                if (item != null && !item.IsEmpty && item.InvPos == clientInvPos)
                {
                    item.Count = newCount;
                    if (newCount <= 0)
                    {
                        item.Clear();
                    }
                    break;
                }
            }
        }

        private void HandleMerchantClaimCoinRsp(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            byte merchantSlot = reader.ReadByte();
            int remainingClaimable = reader.ReadInt32();
            long newGold = reader.ReadInt64();

            if (result == 1)
            {
                if (merchantSlot < MAX_MERCH_ITEMS)
                {
                    _sellingSetupItems[merchantSlot].ClaimableCoins = remainingClaimable;
                    if (_sellingSetupItems[merchantSlot].Count <= 0 && remainingClaimable <= 0)
                    {
                        _sellingSetupItems[merchantSlot].Clear();
                    }
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.Gold = newGold;
                    }
                    KOUIManager.Instance?.UpdateGold(newGold);
                    KOUIManager.Instance?.RefreshMerchantControlUI();
                }
            }
            else
            {
                Debug.LogWarning($"[MerchantManager] Claim coin failed with result code: {result}");
            }
        }

        private void HandleMerchantControlListRsp(KOPacketReader reader)
        {
            short result = reader.ReadInt16(); // 1 = Success
            if (result == 1)
            {
                _isSelling = true;
                _myMerchantSocketId = reader.ReadUInt16(); // Read transient socket ID!
                for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                {
                    _sellingSetupItems[i].Clear();
                }
                byte itemCount = reader.ReadByte();
                for (int i = 0; i < itemCount; i++)
                {
                    byte merchantSlot = reader.ReadByte();
                    int itemId = reader.ReadInt32();
                    int remainingCount = reader.ReadInt32();
                    int soldCount = reader.ReadInt32();
                    int claimableCoins = reader.ReadInt32();

                    if (merchantSlot < MAX_MERCH_ITEMS)
                    {
                        _sellingSetupItems[merchantSlot].ItemId = itemId;
                        _sellingSetupItems[merchantSlot].Count = remainingCount;
                        _sellingSetupItems[merchantSlot].SoldCount = soldCount;
                        _sellingSetupItems[merchantSlot].ClaimableCoins = claimableCoins;
                        _sellingSetupItems[merchantSlot].MerchantPos = merchantSlot;
                    }
                }
                KOUIManager.Instance?.RefreshMerchantControlUI();
            }
            else
            {
                _isSelling = false;
                _myMerchantSocketId = -1;
                for (int i = 0; i < MAX_MERCH_ITEMS; i++)
                {
                    _sellingSetupItems[i].Clear();
                }
                Debug.LogWarning($"[MerchantManager] Merchant Control List request failed.");
                KOUIManager.Instance?.RefreshMerchantControlUI();
            }
        }
    }
}
