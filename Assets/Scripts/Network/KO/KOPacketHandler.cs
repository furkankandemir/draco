using System;
using UnityEngine;

namespace EntropyOnline.Network.KO
{
    /// <summary>
    /// Open-KO paket dispatcher — Ebenezer'dan gelen tüm paketleri ilgili handler'lara yönlendirir.
    /// Birebir: openko-ref/src/Server/Ebenezer/User.cpp Parsing() switch-case yapısının client karşılığı.
    /// </summary>
    public static class KOPacketHandler
    {
        // ============================================================
        // EVENTS — UI ve oyun sistemleri bu eventlere abone olur
        // ============================================================

        // Login/Character
        public static event Action<byte> OnLoginResult;              // nation (0xFF=fail, 1=Karus, 2=Elmo)
        public static event Action<byte> OnSelectNationResult;       // nation
        public static event Action<byte[]> OnAllCharInfo;            // raw char data
        public static event Action<bool> OnSelectCharResult;         // success
        public static event Action<byte> OnNewCharResult;            // result
        public static event Action<byte, byte> OnDeleteCharResult;   // result, charIndex

        // Game world
        public static event Action<byte[]> OnMyInfo;                 // WIZ_MYINFO full data
        /// <summary>
        /// WIZ_MYINFO paketi event handler bağlanmadan ÖNCE gelebilir (race condition).
        /// Bu cache sayesinde geç bağlanan handler'lar kaçırdıkları paketi işleyebilir.
        /// </summary>
        public static byte[] CachedMyInfoData { get; private set; }
        public static int MobileServerUserCount = 1;

        public static event Action<byte[]> OnGameStart;              // game start response
        public static event Action<byte[]> OnTimeInfo;               // game time
        public static event Action<byte[]> OnWeatherInfo;            // weather

        // Movement & region
        public static event Action<byte[]> OnUserInOut;              // user spawn/despawn
        public static event Action<byte[]> OnNpcInOut;               // npc spawn/despawn
        public static event Action<byte[]> OnNpcMove;                // npc movement
        public static event Action<byte[]> OnUserMove;               // other user movement
        public static event Action<byte[]> OnRegionChange;           // region user list
        public static event Action<byte[]> OnNpcRegion;              // region npc list
        public static event Action<byte[]> OnRequestUserIn;          // user detail for region
        public static event Action<byte[]> OnRequestNpcIn;           // npc detail for region

        // Combat
        public static event Action<byte[]> OnAttackResult;           // attack result
        public static event Action<byte[]> OnMagicProcess;           // magic/skill result
        public static event Action<byte[]> OnTargetHP;               // target HP response
        public static event Action<short, short> OnHPChange;         // maxHp, curHp
        public static event Action<short, short> OnMSPChange;        // maxMp, curMp
        public static event Action<byte[]> OnDead;                   // entity death
        public static event Action<byte[]> OnRegene;                 // respawn

        // Stats & Level
        public static event Action<byte[]> OnExpChange;              // exp change
        public static event Action<byte[]> OnLevelChange;            // level up
        public static event Action<byte[]> OnLoyaltyChange;          // national points

        // Items
        public static event Action<byte[]> OnItemMove;               // item move result
        public static event Action<byte[]> OnItemDrop;               // zone item drop
        public static event Action<byte[]> OnItemGet;                // item pickup
        public static event Action<byte[]> OnBundleOpen;             // loot bundle
        public static event Action<byte[]> OnItemCountChange;        // item count update
        public static event Action<byte[]> OnGoldChange;             // gold change
        public static event Action<byte[]> OnUserLookChange;         // appearance
        public static event Action<byte[]> OnDuration;               // durability

        /// <summary>
        /// Inventory data event — dispatched from WIZ_ITEM_COUNT_CHANGE (0x3D) alongside OnItemCountChange.
        /// Open-KO: GameProcMain.cpp MsgRecv_ItemCountChange — bulk inventory update.
        /// Subscribers: KOInventory.cs, InventoryUI.cs
        /// </summary>
        public static event Action<byte[]> OnInventoryData;          // inventory data (bulk item update)
        public static event Action<byte[]> OnInspectData;            // custom inspect player info data

        // Social
        public static event Action<byte[]> OnChat;                   // chat message
        public static event Action<byte[]> OnChatTargetResult;       // chat target response (whisper target)
        public static event Action<byte[]> OnParty;                  // party operations
        public static event Action<byte[]> OnExchange;               // trade
        public static event Action<byte[]> OnKnightsProcess;         // clan operations
        public static event Action<byte[]> OnKnightsList;            // clan list

        /// <summary>
        /// Knights alias event — dispatched from WIZ_KNIGHTS_PROCESS (0x3C) alongside OnKnightsProcess.
        /// Open-KO: GameProcMain.cpp:6315-6386 — MsgRecv_Knights dispatch.
        /// Subscriber: KOKnightsManager.cs (subscribes to OnKnights, not OnKnightsProcess).
        /// </summary>
        public static event Action<byte[]> OnKnights;                // clan operations (alias)

        // World
        public static event Action<byte[]> OnZoneChange;             // zone teleport
        public static event Action<byte[]> OnWarpList;               // warp list
        public static event Action<byte[]> OnObjectEvent;            // bind point etc.
        public static event Action<byte[]> OnNotice;                 // server notice
        public static event Action<byte[]> OnZoneAbility;            // zone restrictions

        // NPC interaction
        public static event Action<byte[]> OnNpcEvent;               // NPC shop/dialog
        public static event Action<byte[]> OnTradeNpc;               // NPC trade panel
        public static event Action<byte[]> OnNpcSay;                 // NPC dialog text
        public static event Action<byte[]> OnSelectMsg;              // NPC dialog options
        public static event Action<byte[]> OnWarehouse;              // warehouse ops
        public static event Action<byte[]> OnItemUpgrade;            // item upgrade result
        public static event Action<byte[]> OnClassChange;            // class change
        public static event Action<byte[]> OnItemRepair;             // item repair
        public static event Action<byte[]> OnItemTradeResult;        // cpp:941 MsgRecv_ItemTradeResult — NPC alış/satış sonucu
        public static event Action<byte[]> OnRepairNpc;              // cpp:947 MsgRecv_RepairNpc — tamir NPC

        // State
        public static event Action<byte[]> OnStateChange;            // sit/stand/stealth
        public static event Action<byte, short> OnStealthInfo;       // isEnabled, radius
        public static event Action<byte[]> OnPointChange;            // stat point result

        /// <summary>
        /// Stat change event — dispatched from WIZ_POINT_CHANGE (0x28) alongside OnPointChange.
        /// Open-KO: GameProcMain.cpp MsgRecv_PointChange — Str/Sta/Dex/Int/Cha point allocation result.
        /// Subscriber: KOInventory.cs (updates local player stats for equipment requirement checks).
        /// </summary>
        public static event Action<byte[]> OnStatChange;             // stat point change (alias)

        // Quest
        public static event Action<byte[]> OnQuest;                  // quest update
        public static event Action<byte[]> OnEvent;                  // WIZ_EVENT event messages
        public static event Action<byte[]> OnCapture;                // WIZ_CAPTURE altar capture

        // Misc
        public static event Action<byte[]> OnConcurrentUser;         // user count
        // WIZ_COMPRESS_PACKET ve WIZ_CONTINOUS_PACKET event olarak yayılmaz —
        // C++ birebir: User.cpp SendCompressingPacket/RegionPacketClear
        // Bunlar HandleCompressedPacket/HandleContinuousPacket içeride işlenir,
        // alt-paketler tekrar HandlePacket'e gönderilir.
        public static event Action<byte[]> OnSkillData;              // skillbar
        public static event Action<byte[]> OnWarp;                   // instant warp / GM warp
        public static event Action<byte[]> OnFriend;                 // friend system

        /// <summary>
        /// Friend process event — dispatched from WIZ_FRIEND_PROCESS (0x49) alongside OnFriend.
        /// Open-KO: FriendHandler.cpp — RecvFriendModify dispatch.
        /// </summary>
        public static event Action<byte[]> OnFriendProcess;          // friend system (alias)

        // C++ birebir eksik olan event'ler — audit'te eklendi:
        public static event Action<byte[]> OnRotate;                 // cpp:2381 — MsgRecv_Rotation [id:int16][yaw:int16]
        public static event Action<byte[]> OnSkillPtChange;          // cpp:5603 — MsgRecv_SkillChange [type:uint8][value:uint8]
        public static event Action<byte[]> OnItemRemoveResult;       // cpp:3619 — MsgRecv_ItemDestroy [result:uint8]
        public static event Action<byte[]> OnCorpse;                 // cpp:MsgRecv_Corpse — tombstone
        public static event Action<byte[]> OnMerchantInOut;          // cpp:personal shop spawn/despawn
        public static event Action<byte[]> OnMerchant;               // WIZ_MERCHANT personal shop actions
        public static event Action<byte[]> OnMarketBBS;
        public static event Action<byte[]> OnShoppingMall;
        public static event Action<byte[]> OnCapeChange;
        public static event Action<byte[]> OnWeightChange;

        // ============================================================
        // MAIN DISPATCHER
        // ============================================================

        public static void HandlePacket(byte opcode, byte[] rawData)
        {
            // Paket loglama — her gelen paket loglanır
            KOPacketLogger.LogRecv(opcode, rawData);

            // rawData[0] == opcode, actual data starts at [1]
            var reader = new KOPacketReader(rawData);

            switch (opcode)
            {
                // ---- Handshake & Login ----
                case WizOpcode.WIZ_VERSION_CHECK:
                    KONetworkManager.Instance?.HandleVersionCheckResponse(reader);
                    break;

                case WizOpcode.WIZ_LOGIN:
                    byte loginResult = reader.ReadByte();
                    int activeUsers = 1;
                    if (loginResult != 0xFF && reader.Remaining >= 2)
                    {
                        activeUsers = reader.ReadInt16();
                    }
                    MobileServerUserCount = activeUsers;
                    OnLoginResult?.Invoke(loginResult);
                    break;

                case WizOpcode.WIZ_SEL_NATION:
                    OnSelectNationResult?.Invoke(reader.ReadByte());
                    break;

                case WizOpcode.WIZ_ALLCHAR_INFO_REQ:
                    OnAllCharInfo?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_NEW_CHAR:
                    OnNewCharResult?.Invoke(reader.ReadByte());
                    break;

                case WizOpcode.WIZ_DEL_CHAR:
                    byte delResult = reader.ReadByte();
                    byte charIdx = reader.ReadByte();
                    OnDeleteCharResult?.Invoke(delResult, charIdx);
                    break;

                case WizOpcode.WIZ_SEL_CHAR:
                    // C++ birebir: MsgRecv_CharacterSelect (GameProcedure.cpp:946-973)
                    // Wire: [result:uint8] if result==1: [zone:uint8][x:uint16][z:uint16][y:int16][victoryNation:uint8]
                    byte selCharResult = reader.ReadByte();
                    if (selCharResult == 0x01)
                    {
                        byte selZone = reader.ReadByte();           // cpp:951 — iZoneCur
                        float selX = reader.ReadUInt16() / 10f;     // cpp:952 — fX
                        float selZ = reader.ReadUInt16() / 10f;     // cpp:953 — fZ
                        float selY = reader.ReadInt16() / 10f;      // cpp:954 — fY
                        byte selVictory = reader.ReadByte();        // cpp:956 — iVictoryNation

                        // cpp:963-964 — s_pPlayer->m_InfoExt.iZoneCur = iZoneCur; PositionSet()
                        var gm = EntropyOnline.Core.GameManager.Instance;
                        if (gm != null)
                        {
                            gm.CurrentZoneId = selZone;
                            gm.PlayerPosX = selX;
                            gm.PlayerPosZ = selZ;
                            gm.PlayerPosY = selY;
                        }
                    }
                    OnSelectCharResult?.Invoke(selCharResult == 0x01);
                    break;

                case WizOpcode.WIZ_GAMESTART:
                    // C++ birebir: GameProcMain.cpp:826-838
                    // Server WIZ_GAMESTART gönderir → client loading bittiğinde sub=2 (loading finished) gönderir.
                    // Bu yanıt artık KOLoadingScreen veya sahne yüklemesi bittiğinde gönderilir (race condition fix).
                    OnGameStart?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_MYINFO:
                    CachedMyInfoData = rawData;  // Race condition fix: cache for late subscribers
                    OnMyInfo?.Invoke(rawData);
                    break;

                // ---- Movement & Region ----
                case WizOpcode.WIZ_MOVE:
                    OnUserMove?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_USER_INOUT:
                    OnUserInOut?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_NPC_INOUT:
                    OnNpcInOut?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_NPC_MOVE:
                    OnNpcMove?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_REGIONCHANGE:
                    OnRegionChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_NPC_REGION:
                    OnNpcRegion?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_REQ_USERIN:
                    OnRequestUserIn?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_REQ_NPCIN:
                    OnRequestNpcIn?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ROTATE:
                    // C++ birebir: MsgRecv_Rotation (GameProcMain.cpp:2381)
                    // Wire: [opcode][id:int16][yaw:int16]
                    OnRotate?.Invoke(rawData);
                    break;

                // ---- Combat ----
                case WizOpcode.WIZ_ATTACK:
                    OnAttackResult?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_MAGIC_PROCESS:
                    OnMagicProcess?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_TARGET_HP:
                    OnTargetHP?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_HP_CHANGE:
                    short maxHp = reader.ReadInt16();
                    short curHp = reader.ReadInt16();
                    OnHPChange?.Invoke(maxHp, curHp);
                    break;

                case WizOpcode.WIZ_MSP_CHANGE:
                    short maxMp = reader.ReadInt16();
                    short curMp = reader.ReadInt16();
                    OnMSPChange?.Invoke(maxMp, curMp);
                    break;

                case WizOpcode.WIZ_DEAD:
                    OnDead?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_REGENE:
                    OnRegene?.Invoke(rawData);
                    break;

                // ---- Stats ----
                case WizOpcode.WIZ_EXP_CHANGE:
                    OnExpChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_LEVEL_CHANGE:
                    OnLevelChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_LOYALTY_CHANGE:
                    OnLoyaltyChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_POINT_CHANGE:
                    OnPointChange?.Invoke(rawData);
                    OnStatChange?.Invoke(rawData);
                    break;

                // ---- Items ----
                case WizOpcode.WIZ_ITEM_MOVE:
                    OnItemMove?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ITEM_DROP:
                    OnItemDrop?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ITEM_GET:
                    OnItemGet?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_BUNDLE_OPEN_REQ:
                    OnBundleOpen?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ITEM_COUNT_CHANGE:
                    OnItemCountChange?.Invoke(rawData);
                    OnInventoryData?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_GOLD_CHANGE:
                    OnGoldChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_USERLOOK_CHANGE:
                    OnUserLookChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_INSPECT:
                    OnInspectData?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_DURATION:
                    OnDuration?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ITEM_UPGRADE:
                    OnItemUpgrade?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ITEM_REPAIR:
                    OnItemRepair?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ITEM_REMOVE:
                    // C++ birebir: MsgRecv_ItemDestroy (GameProcMain.cpp:3619)
                    // Wire: [opcode][result:uint8]
                    OnItemRemoveResult?.Invoke(rawData);
                    break;

                // ---- Social ----
                case WizOpcode.WIZ_CHAT:
                    OnChat?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_CHAT_TARGET:
                    OnChatTargetResult?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_PARTY:
                    OnParty?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_EXCHANGE:
                    OnExchange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_KNIGHTS_PROCESS:
                    OnKnightsProcess?.Invoke(rawData);
                    OnKnights?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_KNIGHTS_LIST:
                    OnKnightsList?.Invoke(rawData);
                    break;

                // ---- World ----
                case WizOpcode.WIZ_TIME:
                    OnTimeInfo?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_WEATHER:
                    OnWeatherInfo?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ZONE_CHANGE:
                    OnZoneChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_STEALTH:
                    byte isEnabled = reader.ReadByte();
                    short radius = reader.ReadInt16();
                    OnStealthInfo?.Invoke(isEnabled, radius);
                    break;

                case WizOpcode.WIZ_WARP_LIST:
                    OnWarpList?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_OBJECT_EVENT:
                    OnObjectEvent?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_NOTICE:
                    OnNotice?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_ZONEABILITY:
                    OnZoneAbility?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_CONCURRENTUSER:
                    OnConcurrentUser?.Invoke(rawData);
                    break;

                // ---- NPC Interaction ----
                case WizOpcode.WIZ_NPC_EVENT:
                    OnNpcEvent?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_TRADE_NPC:
                    OnTradeNpc?.Invoke(rawData);
                    break;

                // C++ birebir: GameProcMain.cpp:941 — MsgRecv_ItemTradeResult
                case WizOpcode.WIZ_ITEM_TRADE:
                    OnItemTradeResult?.Invoke(rawData);
                    break;

                // C++ birebir: GameProcMain.cpp:947 — MsgRecv_RepairNpc
                case WizOpcode.WIZ_REPAIR_NPC:
                    OnRepairNpc?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_NPC_SAY:
                    OnNpcSay?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_SELECT_MSG:
                    OnSelectMsg?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_WAREHOUSE:
                    OnWarehouse?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_CLASS_CHANGE:
                    OnClassChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_STATE_CHANGE:
                    OnStateChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_SKILLPT_CHANGE:
                    // C++ birebir: MsgRecv_SkillChange (GameProcMain.cpp:5603)
                    // Wire: [opcode][type:uint8][value:uint8]
                    OnSkillPtChange?.Invoke(rawData);
                    break;

                // ---- Quest ----
                case WizOpcode.WIZ_QUEST:
                    OnQuest?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_EVENT:
                    OnEvent?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_CAPTURE:
                    OnCapture?.Invoke(rawData);
                    break;

                // ---- Compressed / Batch ----
                case WizOpcode.WIZ_COMPRESS_PACKET:
                    HandleCompressedPacket(rawData);
                    break;

                case WizOpcode.WIZ_CONTINOUS_PACKET:
                    HandleContinuousPacket(rawData);
                    break;

                // ---- Misc ----
                case WizOpcode.WIZ_SKILLDATA:
                    OnSkillData?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_EFFECT:
                    // visual effect
                    break;

                case WizOpcode.WIZ_WARP:
                    OnWarp?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_FRIEND_PROCESS:
                    OnFriend?.Invoke(rawData);
                    OnFriendProcess?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_PARTY_BBS:
                    if (EntropyOnline.UI.KOPartyBBS.Instance != null)
                    {
                        EntropyOnline.UI.KOPartyBBS.Instance.MsgRecv_RefreshData(reader);
                    }
                    break;

                case WizOpcode.WIZ_HOME:
                    // home teleport
                    break;

                // ---- C++ birebir: Audit'te eklenen eksik opcode'lar ----
                case WizOpcode.WIZ_CORPSE:
                    // C++ birebir: MsgRecv_Corpse — tombstone effect
                    OnCorpse?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_MERCHANT_INOUT:
                    // C++ birebir: personal shop spawn/despawn
                    OnMerchantInOut?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_MERCHANT:
                    // WIZ_MERCHANT pazar işlemleri
                    OnMerchant?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_MARKET_BBS:
                    OnMarketBBS?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_SHOPPING_MALL:
                    OnShoppingMall?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_CAPE:
                    OnCapeChange?.Invoke(rawData);
                    break;

                case WizOpcode.WIZ_WEIGHT_CHANGE:
                    // C++ birebir: MsgRecv_WeightChange — ağırlık güncellemesi
                    OnWeightChange?.Invoke(rawData);
                    break;


                default:
                    Debug.LogWarning($"[KO-PKT] Unhandled opcode: 0x{opcode:X2} (len={rawData.Length})");
                    break;
            }
        }

        // ============================================================
        // COMPRESSED / BATCH PACKET HANDLING
        // ============================================================

        /// <summary>
        /// WIZ_COMPRESS_PACKET — LZF sıkıştırılmış paket.
        /// Wire: [opcode][compLen:2][origLen:2][crc:4][compData]
        /// </summary>
        private static void HandleCompressedPacket(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short compLen = r.ReadInt16();
            short origLen = r.ReadInt16();
            uint crc = r.ReadUInt32();
            byte[] compData = r.ReadBytes(compLen);

            // LZF decompress
            byte[] decompressed = LZF.Decompress(compData, origLen);
            if (decompressed == null)
            {
                Debug.LogError("[KO-PKT] LZF decompression failed!");
                return;
            }

            // Decompress sonucu normal paket gibi işle
            if (decompressed.Length > 0)
            {
                byte innerOpcode = decompressed[0];
                HandlePacket(innerOpcode, decompressed);
            }
        }

        /// <summary>
        /// WIZ_CONTINOUS_PACKET — birden fazla paket tek frame'de.
        /// Wire: [opcode][totalLen:2][{pktLen:2, pktData}*]
        /// </summary>
        private static void HandleContinuousPacket(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short totalLen = r.ReadInt16();

            int bytesRead = 0;
            while (bytesRead < totalLen && r.Remaining > 2)
            {
                short pktLen = r.ReadInt16();
                bytesRead += 2;

                if (pktLen <= 0 || pktLen > r.Remaining) break;

                byte[] subPacket = r.ReadBytes(pktLen);
                bytesRead += pktLen;

                if (subPacket.Length > 0)
                {
                    byte innerOpcode = subPacket[0];
                    HandlePacket(innerOpcode, subPacket);
                }
            }
        }
    }
}
