using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.World;
using EntropyOnline.Core;
using EntropyOnline.Import;
using EntropyOnline.Services;
using KOImport;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UIDroppedItemDlg (UIDroppedItemDlg.cpp)
    /// Loot/Bundle UI yöneticisi.
    ///
    /// Akış (Open-KO birebir):
    ///   1. S2C_ITEM_DROP → pCorpse->m_iDroppedItemID = iItemID
    ///      (GameProcMain.cpp:3542-3555)
    ///   2. Oyuncu cesete tıklar → MsgSend_RequestItemBundleOpen
    ///      (GameProcMain.cpp:1608-1631)
    ///      - m_pUIDroppedItemDlg->m_iItemBundleID = pCorpse->m_iDroppedItemID
    ///      - s_pOPMgr->CorpseRemove(pCorpse, false)
    ///      - WIZ_BUNDLE_OPEN_REQ + DWord(iItemBundleID) gönder
    ///   3. S2C_BUNDLE_OPEN → MsgRecv_ItemBundleOpen
    ///      (GameProcMain.cpp:3557-3578)
    ///      - EnterDroppedState → panel aç
    ///      - 6x AddToItemTable(itemId, count, i)
    ///      - InitIconUpdate → ikonları oluştur
    ///   4. Oyuncu item tıklar → WIZ_ITEM_GET
    ///      (UIDroppedItemDlg.cpp:432-441)
    ///      - WIZ_ITEM_GET + DWord(m_iItemBundleID) + DWord(itemId)
    /// </summary>
    public class LootDropUI : MonoBehaviour
    {
        public static LootDropUI Instance { get; private set; }

        /// <summary>
        /// Open-KO birebir: m_iItemBundleID (UIDroppedItemDlg.h:30)
        /// MsgSend_RequestItemBundleOpen (cpp:1618) ile set edilir.
        /// WIZ_ITEM_GET gönderirken bu ID kullanılır (cpp:433).
        /// </summary>
        private long _iItemBundleID = 0;

        private System.Collections.Generic.Dictionary<long, GameObject> _spawnedLootBoxes = new System.Collections.Generic.Dictionary<long, GameObject>();
        private RectTransform _modernLootContainerRT;

        public void RemoveLootBoxFromTracking(long bundleId)
        {
            if (_spawnedLootBoxes.ContainsKey(bundleId))
            {
                _spawnedLootBoxes.Remove(bundleId);
            }
        }

        private void DestroyLootBox(long bundleId)
        {
            if (_spawnedLootBoxes.TryGetValue(bundleId, out GameObject lootBoxGo))
            {
                if (lootBoxGo != null)
                {
                    Destroy(lootBoxGo);
                }
                _spawnedLootBoxes.Remove(bundleId);
            }
        }

        /// <summary>Açık bundle'daki 6 slot (Open-KO: m_pMyDroppedItem[6])</summary>
        private BundleLootSlot[] _currentItems;

        /// <summary>
        /// Open-KO birebir: m_bSendedIconArray[6] (UIDroppedItemDlg.h:32)
        /// Aynı slot iki kez WIZ_ITEM_GET göndermesin diye.
        /// </summary>
        private bool[] _bSendedIconArray = new bool[6];

        /// <summary>
        /// Open-KO birebir: s_sRecoveryJobInfo.UIWndSourceStart.iOrder (N3UIWndBase.h:126)
        /// Son tıklanan slot index'i — result=0x01, 0x04 geldiğinde bu index'teki ikon kaldırılır.
        /// UIDroppedItemDlg.cpp:447: s_sRecoveryJobInfo.UIWndSourceStart.iOrder = iOrder;
        /// </summary>
        private int _lastClickedOrder = -1;

        /// <summary>Loot panel slot UI objeleri</summary>
        private GameObject[] _slotObjects = new GameObject[6];

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnItemDrop += HandleItemDrop_KO;
            KOPacketHandler.OnBundleOpen += HandleBundleOpen_KO;
            KOPacketHandler.OnItemGet += HandleItemGet_KO;
            // Open-KO birebir: Bundle despawn ayrı opcode ile gelmez.
            // C++: UIDroppedItemDlg.cpp — LeaveDroppedState() oyuncu hareket edince veya
            // tüm slotlar boşalınca (CheckAllSlotsEmpty) çağrılır.
        }

        private void OnDisable()
        {
            KOPacketHandler.OnItemDrop -= HandleItemDrop_KO;
            KOPacketHandler.OnBundleOpen -= HandleBundleOpen_KO;
            KOPacketHandler.OnItemGet -= HandleItemGet_KO;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ItemBundleDrop (GameProcMain.cpp:3542-3555)
        ///   int16_t iID = pkt.read();
        ///   uint32_t iItemID = pkt.read();
        ///   pCorpse = NPCGetByID(iID, false);  // ölü NPC'yi bul
        ///   if (nullptr == pCorpse)
        ///     pCorpse = CorpseGetByID(iID);     // corpse listesinde ara
        ///   if (pCorpse)
        ///     pCorpse->m_iDroppedItemID = iItemID;
        /// </summary>
        /// <summary>KO wrapper — WIZ_ITEM_DROP</summary>
        private void HandleItemDrop_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp:3542-3555 — MsgRecv_ItemBundleDrop
            // Wire: [opcode][corpseId:int16][itemId:uint32]
            var r = new KOPacketReader(rawData);
            short corpseId = r.ReadInt16();
            uint itemId    = r.ReadUInt32();

            HandleItemDrop(corpseId, itemId);
        }
        /// <summary>KO wrapper — WIZ_BUNDLE_OPEN</summary>
        private void HandleBundleOpen_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp:3557-3578 — MsgRecv_ItemBundleOpen
            // Wire: [opcode][{itemId:uint32, count:int16} × MAX_ITEM_BUNDLE_DROP_PIECE(6)]
            var r = new KOPacketReader(rawData);
            var items = new BundleLootSlot[6];
            for (int i = 0; i < 6; i++)
            {
                uint itemId  = r.ReadUInt32();
                short count  = r.ReadInt16();
                items[i] = new BundleLootSlot { ItemId = (int)itemId, Count = count };
            }

            HandleBundleOpen(items);
        }
        /// <summary>KO wrapper — WIZ_ITEM_GET</summary>
        private void HandleItemGet_KO(byte[] rawData)
        {
            // C++ birebir: MsgRecv_ItemDroppedGetResult (GameProcMain.cpp:4935-4965)
            var r = new KOPacketReader(rawData);
            byte bResult = r.ReadByte();       // cpp:4944
            byte bPos = 0;
            int iItemID = 0;
            int iGoldID = 0;
            short sItemCount = 0;
            string szString = string.Empty;

            // cpp:4945-4954
            if (bResult == 0x01 || bResult == 0x02 || bResult == 0x05)
            {
                bPos = r.ReadByte();            // cpp:4947
                iItemID = (int)r.ReadUInt32();  // cpp:4948
                if (bResult == 0x01 || bResult == 0x05) // cpp:4949-4952
                    sItemCount = r.ReadInt16();
                iGoldID = (int)r.ReadUInt32();  // cpp:4953 — toplam gold
            }

            // cpp:4956-4961
            if (bResult == 0x03)
            {
                iItemID = (int)r.ReadUInt32();  // cpp:4958
                szString = r.ReadKOString();    // cpp:4959-4960
            }

            HandleItemGet(bResult, bPos, iItemID, sItemCount, iGoldID, szString);
        }
        /// <summary>KO wrapper — WIZ_BUNDLE_DESPAWN</summary>
        private void HandleDespawnBundle_KO(byte[] rawData)
        {
            // Mark bundle as gone — no additional fields needed
            var r = new KOPacketReader(rawData);
            HandleDespawnBundle(_iItemBundleID);
        }

        private void HandleItemDrop(long npcId, long bundleIndex)
        {

            // Open-KO birebir: GameProcMain.cpp:3542-3555
            //   pCorpse = NPCGetByID(iID, false);
            //   if (nullptr == pCorpse) pCorpse = CorpseGetByID(iID);
            //   if (pCorpse) pCorpse->m_iDroppedItemID = iItemID;
            var em = EntityManager.Instance;
            if (em != null)
            {
                var mv = em.GetMonster(npcId);
                if (mv != null)
                {
                    var koEntity = mv.Root?.GetComponent<KOEntity>();
                    if (koEntity != null)
                    {
                        koEntity.DroppedItemID = bundleIndex;

                        if (WorldBuilder.Instance != null)
                        {
                            DestroyLootBox(bundleIndex);
                            GameObject lootBoxObj = WorldBuilder.Instance.SpawnLootBox(koEntity.transform.position, bundleIndex, koEntity);
                            if (lootBoxObj != null)
                            {
                                _spawnedLootBoxes[bundleIndex] = lootBoxObj;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[LOOT] KOEntity bulunamadı: npcId={npcId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LOOT] Monster bulunamadı: npcId={npcId}");
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgSend_RequestItemBundleOpen (GameProcMain.cpp:1608-1631)
        ///
        /// Çağıran: KOTargetSelector — oyuncu cesete tıklayınca.
        ///
        ///   if (pCorpse == nullptr || pCorpse->m_iDroppedItemID <= 0) return false;
        ///   float fDistTmp = (pCorpse->Position() - s_pPlayer->Position()).Magnitude();
        ///   if (fDistTmp >= (pCorpse->Radius() * 2.0f + 6.0f)) return false;
        ///   int iItemBundleID = pCorpse->m_iDroppedItemID;
        ///   m_pUIDroppedItemDlg->m_iItemBundleID = pCorpse->m_iDroppedItemID;
        ///   s_pOPMgr->CorpseRemove(pCorpse, false);  // corpse fade başlat
        ///   WIZ_BUNDLE_OPEN_REQ + DWord(iItemBundleID)
        /// </summary>
        public void SendBundleOpen(long bundleId, KOEntity corpseEntity = null)
        {
            if (KONetworkManager.Instance == null) return;

            // Open-KO birebir: m_pUIDroppedItemDlg->m_iItemBundleID = pCorpse->m_iDroppedItemID
            _iItemBundleID = bundleId;

            // Open-KO birebir: s_pOPMgr->CorpseRemove(pCorpse, false) (GameProcMain.cpp:1620)
            if (corpseEntity != null)
            {
                corpseEntity.CorpseRemove(false);
            }

            // Open-KO birebir: WIZ_BUNDLE_OPEN_REQ
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_BUNDLE_OPEN_REQ);
            pkt.WriteInt32((int)bundleId);
            KONetworkManager.Instance.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ItemBundleOpen (GameProcMain.cpp:3557-3578)
        ///
        ///   m_pUIDroppedItemDlg->EnterDroppedState(ptCur.x, ptCur.y);
        ///   for (i = 0; i < MAX_ITEM_BUNDLE_DROP_PIECE; i++)
        ///     dwItemID = pkt.read<uint32>();
        ///     iItemCount = pkt.read<int16>();
        ///     if (dwItemID)
        ///       m_pUIDroppedItemDlg->AddToItemTable(dwItemID, iItemCount, i);
        ///   m_pUIDroppedItemDlg->InitIconUpdate();
        /// </summary>
        private void HandleBundleOpen(BundleLootSlot[] items)
        {
            // Clear and prepare state
            EnterDroppedState();

            // Collect active items/coins
            var activeItems = new System.Collections.Generic.List<BundleLootSlot>();
            for (int i = 0; i < 6; i++)
            {
                if (items[i].ItemId != 0)
                {
                    activeItems.Add(items[i]);
                }
            }

            // Pack _currentItems to the front of a padded array so slot index matches activeItems index
            _currentItems = new BundleLootSlot[6];
            for (int i = 0; i < activeItems.Count; i++)
            {
                _currentItems[i] = activeItems[i];
            }

            // Setup the modern container and slots
            SetupModernLootUI(activeItems);
        }

        /// <summary>
        /// Open-KO birebir: UIDroppedItemDlg.cpp:432-441
        /// WIZ_ITEM_GET gönder: DWord(m_iItemBundleID) + DWord(itemId)
        /// </summary>
        public void SendItemGet(long bundleId, int itemId)
        {
            if (KONetworkManager.Instance == null) return;

            // Open-KO birebir: WIZ_ITEM_GET
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ITEM_GET);
            pkt.WriteInt32((int)bundleId);
            pkt.WriteInt32(itemId);
            KONetworkManager.Instance.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: UIDroppedItemDlg::GetItemByIDToInventory (cpp:467-856)
        ///
        /// bResult değerleri:
        ///   0x00 = fail → "Envanter dolu" mesajı (cpp:481-498)
        ///   0x01 = solo success → gold veya item envantere eklendi (cpp:733-845)
        ///   0x02 = party gold → level-bazlı paylaşılmış gold (cpp:500-552)
        ///   0x03 = party item notify → "X, Y aldı" mesajı (cpp:554-614)
        ///   0x04 = party denied → başkasına gitti (cpp:618-643)
        ///   0x05 = party routed item → envantere eklendi (cpp:646-717)
        ///   0x06 = too heavy (cpp:719-724)
        ///   0x07 = inventory full (cpp:726-731)
        /// </summary>
        private void HandleItemGet(byte bResult, byte bPos, int iItemID, short sItemCount, long iGold, string charName)
        {
            // Open-KO birebir: cpp:481-498 — fail
            if (bResult == 0x00)
            {
                Debug.LogWarning("[LOOT] ItemGet başarısız — envanter dolu veya geçersiz");
                if (KOUIManager.Instance != null)
                {
                    string msg = StringTableService.Get(2301);
                    KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffff3b3b));
                }
                ResetAllSendFlags();
                return;
            }

            // Open-KO birebir: cpp:500-552 — party gold
            if (bResult == 0x02)
            {
                // Open-KO: cpp:507-511 — gold farkını göster, toplam gold güncelle
                if (GameManager.Instance != null)
                {
                    long diff = iGold - GameManager.Instance.Gold;

                    // C++ birebir: cpp:507-508 — IDS_DROPPED_NOAH_GET
                    // MsgOutput(szMsg, 0xff9b9bff)
                    if (diff > 0 && KOUIManager.Instance != null)
                        KOUIManager.Instance.AddMsgOutput(
                            $"Earned {diff} Coins.",
                            KOUIManager.D3DColorToUnity(0xff9b9bff));

                    GameManager.Instance.Gold = iGold;
                }

                // Open-KO: cpp:518-543 — loot panelinden gold ikonunu kaldır
                RemoveItemFromUI(GameConstants.ITEM_GOLD);
                return;
            }

            // Open-KO birebir: cpp:554-614 — parti üyesi item aldı
            if (bResult == 0x03)
            {
                if (KOUIManager.Instance != null)
                {
                    string itemName = GetItemName(iItemID);
                    KOUIManager.Instance.AddMsgOutput(
                        $"{charName} received {itemName}.",
                        KOUIManager.D3DColorToUnity(0xff9b9bff));
                }
                // Open-KO: cpp:577-606 — loot panelinden itemi kaldır
                RemoveItemFromUI(iItemID);
                return;
            }

            // Open-KO birebir: cpp:618-643 — başkasına gitti (denied)
            if (bResult == 0x04)
            {
                // Open-KO birebir: cpp:620
                // spItem = m_pMyDroppedItem[s_sRecoveryJobInfo.UIWndSourceStart.iOrder];
                // cpp:634: m_pMyDroppedItem[s_sRecoveryJobInfo.UIWndSourceStart.iOrder] = nullptr;
                RemoveItemFromUI(iItemID);
                return;
            }

            // Open-KO birebir: cpp:646-717 — routed item (parti routing)
            if (bResult == 0x05)
            {
                // Open-KO: cpp:648-653 — gold'a routing yapılmaz
                if (iItemID == GameConstants.ITEM_GOLD)
                {
                    Debug.LogError("[LOOT] Gold route edilemez!");
                    return;
                }

                // Add to inventory
                if (KOInventory.Instance != null && bPos < KOInventory.MAX_ITEM_INVENTORY)
                {
                    var slot = new KOInventory.ItemSlot
                    {
                        itemId = iItemID,
                        durability = 0,
                        count = sItemCount > 0 ? sItemCount : 1
                    };
                    if (KOInventory.s_pTbl_Items_Basic != null)
                    {
                        var pItem = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, iItemID);
                        if (pItem != null)
                        {
                            slot.attachPoint = pItem.byAttachPoint;
                            slot.itemClass = pItem.byClass;
                            slot.iconFN = pItem.dwIDIcon.ToString();
                        }
                    }
                    KOInventory.Instance.m_pMyInvWnd[bPos] = slot;
                    if (KOUIManager.Instance != null)
                        KOUIManager.Instance.RefreshInventoryUI();
                }

                // Show message output
                if (KOUIManager.Instance != null)
                {
                    string itemName = GetItemName(iItemID);
                    KOUIManager.Instance.AddMsgOutput(
                        $"You received {itemName}.",
                        KOUIManager.D3DColorToUnity(0xff9b9bff));
                }

                // Gold güncelle
                if (GameManager.Instance != null)
                    GameManager.Instance.Gold = iGold;

                return;
            }

            // Open-KO birebir: cpp:719-724 — çok ağır (0x06)
            if (bResult == 0x06)
            {
                Debug.LogWarning("[LOOT] Item çok ağır veya fazla!");
                if (KOUIManager.Instance != null)
                {
                    string msg = StringTableService.Get(2601);
                    KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffff3b3b));
                }
                ResetAllSendFlags();
                return;
            }

            // Open-KO birebir: cpp:726-731 — envanter dolu (0x07)
            if (bResult == 0x07)
            {
                Debug.LogWarning("[LOOT] Envanter dolu!");
                if (KOUIManager.Instance != null)
                {
                    string msg = StringTableService.Get(2301);
                    KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffff3b3b));
                }
                ResetAllSendFlags();
                return;
            }

            // Open-KO birebir: cpp:733-845 — solo success (0x01)
            if (bResult == 0x01)
            {
                // Open-KO: cpp:735 — gold mu item mı?
                if (iItemID != GameConstants.ITEM_GOLD)
                {
                    // Open-KO birebir: cpp:737-790 — envantere item ekle
                    // bPos = inventory slot index
                    if (KOInventory.Instance != null && bPos < KOInventory.MAX_ITEM_INVENTORY)
                    {
                        var slot = new KOInventory.ItemSlot
                        {
                            itemId = iItemID,
                            durability = 0,
                            count = sItemCount > 0 ? sItemCount : 1
                        };
                        if (KOInventory.s_pTbl_Items_Basic != null)
                        {
                            var pItem = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, iItemID);
                            if (pItem != null)
                            {
                                slot.attachPoint = pItem.byAttachPoint;
                                slot.itemClass = pItem.byClass;
                                slot.iconFN = pItem.dwIDIcon.ToString();
                            }
                        }
                        KOInventory.Instance.m_pMyInvWnd[bPos] = slot;
                        if (KOUIManager.Instance != null)
                            KOUIManager.Instance.RefreshInventoryUI();
                    }

                    // C++ birebir: cpp:792-793 — IDS_ITEM_GET_BY_RULE
                    // MsgOutput(szMsg, 0xff9b9bff)
                    if (KOUIManager.Instance != null)
                    {
                        string itemName = GetItemName(iItemID);
                        KOUIManager.Instance.AddMsgOutput(
                            $"You received {itemName}.",
                            KOUIManager.D3DColorToUnity(0xff9b9bff));
                    }

                    // Open-KO birebir: cpp:795-809
                    RemoveItemFromUI(iItemID);
                }
                else
                {
                    // Open-KO: cpp:811-838 — gold aldı
                    if (GameManager.Instance != null)
                    {
                        long diff = iGold - GameManager.Instance.Gold;

                        // C++ birebir: cpp:816-817 — IDS_DROPPED_NOAH_GET
                        // MsgOutput(szMsg, 0xff9b9bff)
                        if (diff > 0 && KOUIManager.Instance != null)
                            KOUIManager.Instance.AddMsgOutput(
                                $"Earned {diff} Coins.",
                                KOUIManager.D3DColorToUnity(0xff9b9bff));

                        GameManager.Instance.Gold = iGold;
                    }

                    // Open-KO birebir: cpp:822-835
                    RemoveItemFromUI(iItemID);
                }

                // Open-KO: cpp:840-844 — UpdateDisableCheck
            }

            // Open-KO birebir: cpp:847-855 — tüm slotlar boşsa panel kapat
            // Bu kontrol ALL result paths'ten sonra çalışır
            CheckAllSlotsEmpty();
        }

        /// <summary>
        /// Open-KO birebir: cpp:847-855
        /// Tüm slot'lar boşsa LeaveDroppedState çağır.
        /// </summary>
        private void CheckAllSlotsEmpty()
        {
            if (_currentItems == null) return;

            bool bFound = false;
            for (int i = 0; i < GameConstants.MAX_ITEM_BUNDLE_DROP_PIECE; i++)
            {
                if (_currentItems[i] != null && _currentItems[i].ItemId != 0)
                {
                    bFound = true;
                    break;
                }
            }

            if (!bFound)
            {
                DestroyLootBox(_iItemBundleID);
                LeaveDroppedState();
            }
        }

        /// <summary>
        /// Open-KO birebir: PlayerOtherMgr.cpp:444-450
        /// Loot kutusu tamamen boşaldığında sunucu gönderir.
        /// </summary>
        private void SetupModernLootUI(System.Collections.Generic.List<BundleLootSlot> activeItems)
        {
            if (KOUIManager.Instance == null) return;
            var panel = KOUIManager.Instance.GetDroppedItemPanel();
            if (panel == null) return;

            // 1. Hide all original prefab children of _uiDroppedItem
            foreach (Transform child in panel.transform)
            {
                if (child.name != "ModernLootContainer")
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 2. Destroy old ModernLootContainer and create a fresh one
            Transform oldContainer = panel.transform.Find("ModernLootContainer");
            if (oldContainer != null)
            {
                DestroyImmediate(oldContainer.gameObject);
            }

            GameObject containerGo = new GameObject("ModernLootContainer");
            containerGo.transform.SetParent(panel.transform, false);

            var containerRT = containerGo.AddComponent<RectTransform>();
            _modernLootContainerRT = containerRT;
            containerRT.anchorMin = new Vector2(0.5f, 0.5f);
            containerRT.anchorMax = new Vector2(0.5f, 0.5f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            containerRT.anchoredPosition = Vector2.zero;

            // Determine dimensions based on slot count
            int activeCount = activeItems.Count;
            int slotCount = Mathf.Max(2, ((activeCount + 1) / 2) * 2);
            int numRows = slotCount / 2;

            float slotsRegionHeight = numRows * 45f + (numRows - 1) * 3f;
            float containerHeight = slotsRegionHeight + 72f; 

            containerRT.sizeDelta = new Vector2(103f, containerHeight); // 93f grid/button width + 5px margin on left/right

            // Add background (no border/outline) - semi-transparent like merchant control panel
            var bgImg = containerGo.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.10f, 0.08f, 0.65f); // Semi-transparent merchant bg

            // 3. Create slots
            for (int i = 0; i < slotCount; i++)
            {
                int row = i / 2;
                int col = i % 2;

                float slotX = col == 0 ? -24f : 24f;
                float slotY = -27.5f - row * 48f; // Align top of slots at exactly -5f padding

                GameObject slotObj = new GameObject($"LootSlot_{i}");
                slotObj.transform.SetParent(containerGo.transform, false);
                var slotRT = slotObj.AddComponent<RectTransform>();
                slotRT.anchorMin = new Vector2(0.5f, 1f);
                slotRT.anchorMax = new Vector2(0.5f, 1f);
                slotRT.pivot = new Vector2(0.5f, 0.5f);
                slotRT.anchoredPosition = new Vector2(slotX, slotY);
                slotRT.sizeDelta = new Vector2(45f, 45f);

                var slotBg = slotObj.AddComponent<Image>();
                slotBg.color = new Color(0.02f, 0.02f, 0.04f, 0.8f); // dark background for slot
                var slotOutline = slotObj.AddComponent<Outline>();
                slotOutline.effectColor = new Color(0.2f, 0.2f, 0.22f, 0.7f);
                slotOutline.effectDistance = new Vector2(1f, -1f);

                _slotObjects[i] = slotObj;

                if (i < activeCount)
                {
                    var item = activeItems[i];

                    // Draw item icon
                    int iconId = ResolveIconIdForLoot(item.ItemId);
                    Sprite icon = KOItemIconLoader.LoadItemIcon(iconId);
                    if (icon != null)
                    {
                        GameObject iconObj = new GameObject("Icon");
                        iconObj.transform.SetParent(slotObj.transform, false);
                        var iconRT = iconObj.AddComponent<RectTransform>();
                        iconRT.anchorMin = Vector2.zero;
                        iconRT.anchorMax = Vector2.one;
                        iconRT.sizeDelta = Vector2.zero;

                        var iconImg = iconObj.AddComponent<Image>();
                        iconImg.sprite = icon;
                        iconImg.preserveAspect = true;
                        iconImg.raycastTarget = false;
                    }
                    else
                    {
                        var idObj = new GameObject("IdText");
                        idObj.transform.SetParent(slotObj.transform, false);
                        var idRT = idObj.AddComponent<RectTransform>();
                        idRT.anchorMin = Vector2.zero;
                        idRT.anchorMax = Vector2.one;
                        idRT.sizeDelta = Vector2.zero;
                        var idText = idObj.AddComponent<Text>();
                        if (item.ItemId == GameConstants.ITEM_GOLD)
                        {
                            idText.text = $"<b>{item.Count:N0}</b>\nGold";
                            idText.color = new Color(1f, 0.85f, 0f);
                        }
                        else
                        {
                            idText.text = item.ItemId.ToString();
                            idText.color = Color.white;
                        }
                        idText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        idText.fontSize = 9;
                        idText.alignment = TextAnchor.MiddleCenter;
                        idText.raycastTarget = false;
                    }

                    // Display count if > 1
                    if (item.Count > 1)
                    {
                        var countObj = new GameObject("CountText");
                        countObj.transform.SetParent(slotObj.transform, false);
                        var countRT = countObj.AddComponent<RectTransform>();
                        countRT.anchorMin = new Vector2(1, 0);
                        countRT.anchorMax = new Vector2(1, 0);
                        countRT.pivot = new Vector2(1, 0);
                        countRT.anchoredPosition = new Vector2(-2, 2);
                        countRT.sizeDelta = new Vector2(30, 12);
                        var countText = countObj.AddComponent<Text>();
                        countText.text = item.Count.ToString();
                        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        countText.fontSize = 9;
                        countText.color = Color.white;
                        countText.alignment = TextAnchor.LowerRight;
                        countText.raycastTarget = false;
                    }

                    // Button interaction
                    var btn = slotObj.AddComponent<Button>();
                    int capturedOrder = i;
                    int capturedItemId = item.ItemId;
                    btn.onClick.AddListener(() => OnSlotClicked(capturedOrder, capturedItemId));
                }
            }

            // 4. Create Loot All Button (Pazar Open button style)
            GameObject lootAllObj = new GameObject("LootAllButton");
            lootAllObj.transform.SetParent(containerGo.transform, false);
            var lootAllRT = lootAllObj.AddComponent<RectTransform>();
            lootAllRT.anchorMin = new Vector2(0.5f, 1f);
            lootAllRT.anchorMax = new Vector2(0.5f, 1f);
            lootAllRT.pivot = new Vector2(0.5f, 0.5f);
            lootAllRT.anchoredPosition = new Vector2(0f, -23f - slotsRegionHeight);
            lootAllRT.sizeDelta = new Vector2(93f, 26f); // 93f width matches exactly the 2-column grid

            var laImg = lootAllObj.AddComponent<Image>();
            laImg.color = new Color(0.06f, 0.24f, 0.44f, 1f);
            var laOutline = lootAllObj.AddComponent<Outline>();
            laOutline.effectColor = new Color(0.078f, 0.312f, 0.572f, 1f);
            laOutline.effectDistance = new Vector2(1f, -1f);

            var laBtn = lootAllObj.AddComponent<Button>();
            laBtn.transition = Selectable.Transition.ColorTint;
            var laColors = laBtn.colors;
            laColors.normalColor = Color.white;
            laColors.highlightedColor = new Color(1.1f, 1.2f, 1.3f, 1f);
            laColors.pressedColor = new Color(0.7f, 0.8f, 0.9f, 1f);
            laColors.selectedColor = new Color(1.1f, 1.2f, 1.3f, 1f);
            laBtn.colors = laColors;
            laBtn.onClick.AddListener(OnLootAllClicked);

            GameObject laTextObj = new GameObject("Text");
            laTextObj.transform.SetParent(lootAllObj.transform, false);
            var laTextRT = laTextObj.AddComponent<RectTransform>();
            laTextRT.anchorMin = Vector2.zero;
            laTextRT.anchorMax = Vector2.one;
            laTextRT.sizeDelta = Vector2.zero;
            var laText = laTextObj.AddComponent<Text>();
            laText.text = "Loot All";
            laText.fontSize = 12;
            laText.fontStyle = FontStyle.Bold;
            laText.color = Color.white;
            laText.alignment = TextAnchor.MiddleCenter;
            laText.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
            if (laText.font == null)
                laText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            laText.raycastTarget = false;

            // 5. Create Close Button (Question Dialog Cancel button style)
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(containerGo.transform, false);
            var closeRT = closeObj.AddComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(0.5f, 1f);
            closeRT.anchorMax = new Vector2(0.5f, 1f);
            closeRT.pivot = new Vector2(0.5f, 0.5f);
            closeRT.anchoredPosition = new Vector2(0f, -54f - slotsRegionHeight);
            closeRT.sizeDelta = new Vector2(93f, 26f); // 93f width matches exactly the 2-column grid

            var clImg = closeObj.AddComponent<Image>();
            clImg.color = new Color(0.45f, 0.05f, 0.08f, 0.95f);
            var clOutline = closeObj.AddComponent<Outline>();
            clOutline.effectColor = new Color(0.75f, 0.15f, 0.15f, 0.95f);
            clOutline.effectDistance = new Vector2(1f, -1f);

            var clBtn = closeObj.AddComponent<Button>();
            clBtn.transition = Selectable.Transition.ColorTint;
            var clColors = clBtn.colors;
            clColors.normalColor = Color.white;
            clColors.highlightedColor = new Color(1.1f, 1.2f, 1.3f, 1f);
            clColors.pressedColor = new Color(0.7f, 0.8f, 0.9f, 1f);
            clColors.selectedColor = new Color(1.1f, 1.2f, 1.3f, 1f);
            clBtn.colors = clColors;
            clBtn.onClick.AddListener(OnCloseClicked);

            GameObject clTextObj = new GameObject("Text");
            clTextObj.transform.SetParent(closeObj.transform, false);
            var clTextRT = clTextObj.AddComponent<RectTransform>();
            clTextRT.anchorMin = Vector2.zero;
            clTextRT.anchorMax = Vector2.one;
            clTextRT.sizeDelta = Vector2.zero;
            var clText = clTextObj.AddComponent<Text>();
            clText.text = "Close";
            clText.fontSize = 12;
            clText.fontStyle = FontStyle.Bold;
            clText.color = Color.white;
            clText.alignment = TextAnchor.MiddleCenter;
            clText.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
            if (clText.font == null)
                clText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clText.raycastTarget = false;
        }

        private void Update()
        {
            if (_modernLootContainerRT != null)
            {
                var canvas = _modernLootContainerRT.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.scaleFactor > 0f)
                {
                    float targetScale = 1f / canvas.scaleFactor;
                    if (Mathf.Abs(_modernLootContainerRT.localScale.x - targetScale) > 0.001f)
                    {
                        _modernLootContainerRT.localScale = new Vector3(targetScale, targetScale, 1f);
                    }
                }
            }
        }

        private void OnLootAllClicked()
        {
            if (_currentItems == null) return;
            for (int i = 0; i < _currentItems.Length; i++)
            {
                if (_currentItems[i] != null && _currentItems[i].ItemId != 0 && !_bSendedIconArray[i])
                {
                    _bSendedIconArray[i] = true;
                    SendItemGet(_iItemBundleID, _currentItems[i].ItemId);
                }
            }
        }

        private void OnCloseClicked()
        {
            LeaveDroppedState();
        }

        private void ResetAllSendFlags()
        {
            for (int i = 0; i < GameConstants.MAX_ITEM_BUNDLE_DROP_PIECE; i++)
            {
                _bSendedIconArray[i] = false;
            }
        }

        private void HandleDespawnBundle(long bundleIndex)
        {
            DestroyLootBox(bundleIndex);
            LeaveDroppedState();
        }

        // ============================
        // UI YÖNETİMİ — Open-KO birebir: UIDroppedItemDlg.cpp
        // ============================

        /// <summary>
        /// Open-KO birebir: EnterDroppedState (UIDroppedItemDlg.cpp:165-192)
        /// Panel aç, eski ikonları temizle, m_bSendedIconArray sıfırla.
        /// </summary>
        private void EnterDroppedState()
        {
            // Open-KO birebir: m_bSendedIconArray[i] = false (cpp:174)
            for (int i = 0; i < 6; i++)
            {
                _bSendedIconArray[i] = false;
                if (_slotObjects[i] != null)
                {
                    Destroy(_slotObjects[i]);
                    _slotObjects[i] = null;
                }
            }

            // Open-KO birebir: SetVisible(true) (cpp:168)
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowDroppedItem(true);
        }

        /// <summary>
        /// Open-KO birebir: LeaveDroppedState (UIDroppedItemDlg.cpp:194-201)
        /// Panel kapat.
        /// </summary>
        private void LeaveDroppedState()
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowDroppedItem(false);

            // Open-KO birebir: m_bSendedIconArray[i] = false (cpp:200)
            for (int i = 0; i < 6; i++)
            {
                _bSendedIconArray[i] = false;
                if (_slotObjects[i] != null)
                {
                    Destroy(_slotObjects[i]);
                    _slotObjects[i] = null;
                }
            }
            _iItemBundleID = 0;
        }

        /// <summary>
        /// Open-KO birebir: AddToItemTable (UIDroppedItemDlg.cpp:214-247)
        /// + InitIconUpdate (UIDroppedItemDlg.cpp:129-151)
        ///
        /// Item ikonunu loot paneline ekle.
        /// Pozisyon: GetChildAreaByiOrder(UI_AREA_TYPE_DROP_ITEM, i) (cpp:144)
        /// → Co_DroppedItem_us.uif'deki area elementlerinin bölgelerini kullanır.
        /// </summary>
        private void AddToItemTable(int itemId, int count, int order)
        {
            if (KOUIManager.Instance == null) return;
            var panel = KOUIManager.Instance.GetDroppedItemPanel();
            if (panel == null) return;

            // Open-KO birebir: InitIconUpdate (cpp:144)
            // CN3UIArea* pArea = GetChildAreaByiOrder(UI_AREA_TYPE_DROP_ITEM, i);
            // UI_AREA_TYPE_DROP_ITEM = 6 (N3UIArea.h:21)
            const int UI_AREA_TYPE_DROP_ITEM = 6;
            var areaRT = KOUIRenderer.FindChildAreaByiOrder(panel.transform, UI_AREA_TYPE_DROP_ITEM, order);

            var slotObj = new GameObject($"LootSlot_{order}");
            slotObj.transform.SetParent(panel.transform, false);

            var slotRT = slotObj.AddComponent<RectTransform>();

            if (areaRT != null)
            {
                // Open-KO birebir: m_pMyDroppedItem[i]->pUIIcon->SetRegion(pArea->GetRegion())
                // UIF'deki area pozisyonunu doğrudan kullan
                slotRT.anchorMin = areaRT.anchorMin;
                slotRT.anchorMax = areaRT.anchorMax;
                slotRT.offsetMin = areaRT.offsetMin;
                slotRT.offsetMax = areaRT.offsetMax;
            }
            else
            {
                // Area bulunamazsa fallback — bu olmamalı ama güvenlik için
                Debug.LogWarning($"[LOOT] Area bulunamadı: type=DROP_ITEM order={order}");
                float iconSize = 45f;
                float gap = 3f;
                int columns = 3;
                int col = order % columns;
                int row = order / columns;
                slotRT.anchorMin = new Vector2(0, 1);
                slotRT.anchorMax = new Vector2(0, 1);
                slotRT.pivot = new Vector2(0, 1);
                slotRT.anchoredPosition = new Vector2(10f + col * (iconSize + gap), -10f - row * (iconSize + gap));
                slotRT.sizeDelta = new Vector2(iconSize, iconSize);
            }

            // Open-KO birebir: InitIconUpdate (cpp:139)
            // m_pMyDroppedItem[i]->pUIIcon->SetTex(m_pMyDroppedItem[i]->szIconFN)
            // C++ AddToItemTable (UIDroppedItemDlg.cpp:221-237):
            //   pItem = s_pTbl_Items_Basic.Find(iItemID / 1000 * 1000);
            //   MakeResrcFileNameForUPC(pItem, pItemExt, nullptr, &szIconFN, ...);
            //   spItem->szIconFN = szIconFN;
            // Gold dahil HER item aynı akıştan geçer — özel durum yok.
            int iconId = ResolveIconIdForLoot(itemId);
            Sprite icon = KOItemIconLoader.LoadItemIcon(iconId);

            if (icon != null)
            {
                var img = slotObj.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = true;
            }
            else
            {
                // İkon yüklenemezse fallback — koyu arka plan + item ID
                var img = slotObj.AddComponent<Image>();
                img.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);

                var idObj = new GameObject("IdText");
                idObj.transform.SetParent(slotObj.transform, false);
                var idRT = idObj.AddComponent<RectTransform>();
                idRT.anchorMin = Vector2.zero;
                idRT.anchorMax = Vector2.one;
                idRT.sizeDelta = Vector2.zero;
                var idText = idObj.AddComponent<Text>();
                if (itemId == GameConstants.ITEM_GOLD)
                {
                    idText.text = $"<b>{count:N0}</b>\nGold";
                    idText.color = new Color(1f, 0.85f, 0f);
                }
                else
                {
                    idText.text = itemId.ToString();
                    idText.color = Color.white;
                }
                idText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                idText.fontSize = 9;
                idText.alignment = TextAnchor.MiddleCenter;
                idText.raycastTarget = false;
            }

            // Open-KO birebir: Render() (cpp:82-113) — Countable item count gösterimi
            // GetChildStringByiOrder(i) → pStr->SetStringAsInt(m_pMyDroppedItem[i]->iCount)
            if (count > 1)
            {
                var countObj = new GameObject("CountText");
                countObj.transform.SetParent(slotObj.transform, false);
                var countRT = countObj.AddComponent<RectTransform>();
                countRT.anchorMin = new Vector2(1, 0);
                countRT.anchorMax = new Vector2(1, 0);
                countRT.pivot = new Vector2(1, 0);
                countRT.anchoredPosition = new Vector2(-2, 2);
                countRT.sizeDelta = new Vector2(30, 12);
                var countText = countObj.AddComponent<Text>();
                countText.text = count.ToString();
                countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                countText.fontSize = 9;
                countText.color = Color.white;
                countText.alignment = TextAnchor.LowerRight;
                countText.raycastTarget = false;
            }

            // Open-KO birebir: ReceiveMessage → UIMSG_ICON_UP → WIZ_ITEM_GET (cpp:401-451)
            var btn = slotObj.AddComponent<Button>();
            int capturedOrder = order;
            int capturedItemId = itemId;
            btn.onClick.AddListener(() => OnSlotClicked(capturedOrder, capturedItemId));

            _slotObjects[order] = slotObj;
        }

        /// <summary>
        /// Open-KO birebir: ReceiveMessage → UIMSG_ICON_UP (UIDroppedItemDlg.cpp:401-451)
        ///   m_bSendedIconArray[iOrder] = true;
        ///   WIZ_ITEM_GET + DWord(m_iItemBundleID) + DWord(itemId)
        /// </summary>
        private void OnSlotClicked(int order, int itemId)
        {
            // Open-KO birebir: cpp:427-428 — zaten gönderilmişse tekrar gönderme
            if (_bSendedIconArray[order])
            {
                return;
            }

            // Open-KO birebir: m_bSendedIconArray[iOrder] = true (cpp:430)
            _bSendedIconArray[order] = true;

            // Open-KO birebir: s_sRecoveryJobInfo.UIWndSourceStart.iOrder = iOrder (cpp:447)
            _lastClickedOrder = order;

            // Open-KO birebir: WIZ_ITEM_GET + DWord(m_iItemBundleID) + DWord(itemId) (cpp:432-441)
            SendItemGet(_iItemBundleID, itemId);
        }

        /// <summary>
        /// UI'dan itemi kaldır — Open-KO birebir: GetItemByIDToInventory → RemoveChild + delete
        /// (cpp:534-543, cpp:597-606, cpp:795-809, cpp:822-835)
        /// </summary>
        private void RemoveItemFromUI(int itemId)
        {
            if (_currentItems == null) return;

            for (int i = 0; i < GameConstants.MAX_ITEM_BUNDLE_DROP_PIECE; i++)
            {
                if (_currentItems[i] != null && _currentItems[i].ItemId == itemId)
                {
                    _currentItems[i].ItemId = 0;
                    _currentItems[i].Count = 0;

                    if (_slotObjects[i] != null)
                    {
                        foreach (Transform child in _slotObjects[i].transform)
                        {
                            Destroy(child.gameObject);
                        }
                        var btn = _slotObjects[i].GetComponent<Button>();
                        if (btn != null) btn.enabled = false;
                    }

                    break;
                }
            }
        }

        private void RemoveItemByOrder(int order)
        {
            if (_currentItems == null) return;
            if (order < 0 || order >= GameConstants.MAX_ITEM_BUNDLE_DROP_PIECE) return;

            if (_currentItems[order] != null)
            {
                _currentItems[order].ItemId = 0;
                _currentItems[order].Count = 0;
            }

            if (_slotObjects[order] != null)
            {
                foreach (Transform child in _slotObjects[order].transform)
                {
                    Destroy(child.gameObject);
                }
                var btn = _slotObjects[order].GetComponent<Button>();
                if (btn != null) btn.enabled = false;
            }
        }

        /// <summary>
        /// Open-KO birebir: itemId → dwIDIcon lookup.
        /// C++ GameBase.cpp satır 617-621:
        ///   pItem = s_pTbl_Items_Basic.Find(iItemID / 1000 * 1000)
        ///   szIconFN = MakeIconFileName(pItem->dwIDIcon)
        /// </summary>
        private static int ResolveIconIdForLoot(int itemId)
        {
            return KOUIManager.ResolveIconId(itemId);
        }

        /// <summary>
        /// Open-KO birebir: CUITransactionDlg::GetItemName (UITransactionDlg.cpp:431-448)
        /// GetItemByIDToInventory'deki MsgOutput mesajı için item adı oluşturma.
        /// Unique ise ext.SzHeader ("Kekuri Ring"), upgrade ise basic.SzName + "(+X)", değilse basic.SzName.
        /// </summary>
        private static string GetItemName(int itemId)
        {
            var basic = ItemDataManager.GetItemBasic(itemId);
            if (basic == null)
            {
                basic = ItemDataManager.GetItemBasic(itemId / 1000 * 1000);
            }
            if (basic == null) return $"Item #{itemId}";

            var ext = ItemDataManager.GetItemExt(itemId);
            if (ext != null)
            {
                // Open-KO: (e_ItemAttrib)(spItem->pItemExt->byMagicOrRare) == ITEM_ATTRIB_UNIQUE (4)
                if (ext.ByMagicOrRare == 4 && !string.IsNullOrEmpty(ext.SzHeader))
                {
                    return ext.SzHeader;
                }

                // Open-KO: (spItem->pItemExt->dwID % 10) != 0 -> name += "(+X)"
                int extMod = (int)(ext.DwID % 10);
                if (extMod == 0)
                {
                    int lastTwo = (int)(ext.DwID % 100);
                    if (lastTwo == 10 || lastTwo == 20 || lastTwo == 30 || lastTwo == 40 || lastTwo == 50)
                        extMod = 10;
                }
                if (extMod != 0)
                {
                    return $"{basic.SzName}(+{extMod})";
                }
            }

            return !string.IsNullOrEmpty(basic.SzName) ? basic.SzName : $"Item #{itemId}";
        }
    }
}
