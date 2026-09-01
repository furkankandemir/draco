using UnityEngine;
using UnityEngine.SceneManagement;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.UI;
using KO;
using EntropyOnline.Trade;
using EntropyOnline.Character;

namespace EntropyOnline.World
{
    /// <summary>
    /// Entropy Online — Oyun Sahne Kontrolcüsü
    /// 
    /// GameScene yüklendiğinde çalışır:
    /// 1. Zone Server'a C2S_ZONE_AUTHENTICATE gönderir
    /// 2. Sunucudan gelen S2C_MY_INFO ile kendi karakter bilgilerini günceller
    /// 3. Spawn/despawn paketlerini işler
    /// 4. HUD barlarını yönetir (GameHUD)
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        private void Start()
        {
            // Open-KO: zone server yok — Ebenezer tek sunucu
            // Login olmadan bu sahne açıldıysa LoginScene'e yönlendir
            if (KONetworkManager.Instance == null || !KONetworkManager.Instance.IsConnected)
            {
                Debug.LogWarning("[GAME] Bağlantı yok — LoginScene'e yönlendiriliyor...");
                SceneManager.LoadScene("LoginScene");
                return;
            }



            // Durum güncelle
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.InGame);
            }

            // Dünya ortamını oluştur (Terrain, Skybox, Yapılar)
            if (WorldBuilder.Instance == null)
            {
                var worldObj = new GameObject("WorldBuilder");
                worldObj.AddComponent<WorldBuilder>();
            }

            // HUD oluştur
            CreateHUD();

            // Sunucu event'lerine abone ol — KOPacketHandler
            KOPacketHandler.OnMyInfo += HandleMyInfo_KO;
            // NOT: OnUserMove, OnAttackResult, OnUserInOut, OnNpcInOut, OnDead
            // EntityManager tarafından handle ediliyor — burada abone olursak
            // aynı paket iki kez işlenir (duplicate NPC spawn gibi sorunlara yol açar).
            // NOT: HP ve MSP event'leri GameHUD'da handle ediliyor.
            // C++ birebir: MsgRecv_MyInfo_HP/MSP tek fonksiyon — önce delta, sonra state günceller.
            // Burada ayrıca abone olmak race condition yaratır (delta = 0 olur).
            KOPacketHandler.OnExpChange += HandleExpChange_KO;
            KOPacketHandler.OnLevelChange += HandleLevelChange_KO;
            KOPacketHandler.OnZoneChange += HandleZoneChange_KO;
            KOPacketHandler.OnGoldChange += HandleGoldChange_KO;
            KOPacketHandler.OnWeightChange += HandleWeightChange_KO;
            KOPacketHandler.OnGameStart += HandleGameStart_KO;

            // ============================================================
            // RACE CONDITION FIX: WIZ_MYINFO paketi sahne yüklenirken
            // (Start() çağrılmadan ÖNCE) gelebilir. Cache'lenmiş veriyi kontrol et.
            // ============================================================
            if (KOPacketHandler.CachedMyInfoData != null)
            {
                HandleMyInfo_KO(KOPacketHandler.CachedMyInfoData);
            }

            // ============================================================
            // HEARTBEAT: C++ birebir — GameProcMain.cpp:616-621
            // fInterval3 > 10.0f → MsgSend_SpeedCheck()
            // Sunucu CheckAliveUser() ile m_sAliveCount > 3 olunca Close() çağırır.
            // WIZ_SPEEDHACK_CHECK alınca m_sAliveCount = 0 yapar.
            // ============================================================
            _speedCheckTimer = 0f;
            SendSpeedCheck(true); // C++ birebir: MsgSend_SpeedCheck(true) — ilk giriş
        }

        // C++ birebir: GameProcMain.cpp:601-621 — timer-like routine
        private float _speedCheckTimer;
        // C++ birebir: GameProcMain.cpp:606-614 — fInterval2 > 1200.0f → WIZ_DATASAVE
        private float _dataSaveTimer;
        private bool _isZoneChangeLoadedReceived = false;

        private void Update()
        {
            float dt = Time.deltaTime;
            _speedCheckTimer += dt;
            _dataSaveTimer += dt;

            // C++ birebir: fInterval3 > 10.0f → MsgSend_SpeedCheck()
            if (_speedCheckTimer > 10.0f)
            {
                SendSpeedCheck(false);
                _speedCheckTimer = 0f;
            }

            // C++ birebir: fInterval2 > 1200.0f → WIZ_DATASAVE (GameProcMain.cpp:607-614)
            // 저장 요청.. — Her 1200 saniyede (20 dk) bir sunucuya kaydetme isteği
            if (_dataSaveTimer > 1200.0f)
            {
                SendDataSave();
                _dataSaveTimer = 0f;
            }
        }

        /// <summary>
        /// C++ birebir: GameProcMain.cpp:609-612
        ///   WIZ_DATASAVE — sunucuya veri kaydetme isteği
        /// </summary>
        private void SendDataSave()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null || !netMgr.IsConnected) return;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_DATASAVE);
            netMgr.SendPacket(pkt);
        }

        /// <summary>
        /// C++ birebir: GameProcMain::MsgSend_SpeedCheck (GameProcMain.cpp:7972-7982)
        ///   WIZ_SPEEDHACK_CHECK + byte(bInit) + float(fTime)
        /// </summary>
        private void SendSpeedCheck(bool bInit)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null || !netMgr.IsConnected) return;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_SPEEDHACK_CHECK);
            pkt.WriteByte(bInit ? (byte)1 : (byte)0); // cpp:7979 — bInit
            pkt.WriteFloat(Time.time);                 // cpp:7980 — fTime (client time)
            netMgr.SendPacket(pkt);
        }

        private void OnDestroy()
        {
            KOPacketHandler.OnMyInfo -= HandleMyInfo_KO;
            // HP/MSP handler'ları GameHUD'da — burada abone değiliz
            KOPacketHandler.OnExpChange -= HandleExpChange_KO;
            KOPacketHandler.OnLevelChange -= HandleLevelChange_KO;
            KOPacketHandler.OnZoneChange -= HandleZoneChange_KO;
            KOPacketHandler.OnGoldChange -= HandleGoldChange_KO;
            KOPacketHandler.OnWeightChange -= HandleWeightChange_KO;
            KOPacketHandler.OnGameStart -= HandleGameStart_KO;
        }

        /// <summary>
        /// HUD ve EntityManager oluşturur.
        /// KO UI panelleri KOUIManager tarafından UIF'lerden yüklenir.
        /// </summary>
        private void CreateHUD()
        {
            // KO UI Manager oluştur — tüm UI panellerini UIF'lerden yükler
            if (KOUIManager.Instance == null)
            {
                var koUIMgrObj = new GameObject("KOUIManager");
                var koUIMgr = koUIMgrObj.AddComponent<KOUIManager>();
                // Open-KO birebir: InitUI(eNation) — nation seçimi login'den gelir
                byte nation = GameManager.Instance?.Nation ?? 2;
                koUIMgr.InitUI(nation);
            }

            // GameHUD oluştur (event handler'lar için — data binding)
            if (GameHUD.Instance == null)
            {
                var hudObj = new GameObject("GameHUD");
                hudObj.AddComponent<GameHUD>();
            }

            // Mobil Skill Bar (sağ taraf yuvarlak dokunmatik butonlar — Nova Online referansı)
            if (MobileSkillBar.Instance == null)
            {
                GameObject skillBarObj = null;
                var prefab = Resources.Load<GameObject>("ModernUI/MobileSkillBar");
                if (prefab != null)
                {
                    skillBarObj = Instantiate(prefab);
                    skillBarObj.name = "MobileSkillBar";
                }
                else
                {
                    skillBarObj = new GameObject("MobileSkillBar");
                }
                var skillBar = skillBarObj.GetComponent<MobileSkillBar>() ?? skillBarObj.AddComponent<MobileSkillBar>();
                skillBar.Initialize();
            }

            // Open-KO birebir: CGameProcedure::s_pMagicSkillMng = new CMagicSkillMng()
            // Skill casting state machine + cooldown tracking
            if (EntropyOnline.Combat.KOMagicSkillManager.Instance == null)
            {
                var magicObj = new GameObject("KOMagicSkillManager");
                magicObj.AddComponent<EntropyOnline.Combat.KOMagicSkillManager>();
            }
            
            // Open-KO birebir: CUIInventory veri modeli
            // m_pMySlot[14] + m_pMyInvWnd[28] + GetArmDestinationIndex + SendInvMsg
            if (KOInventory.Instance == null)
            {
                var invObj = new GameObject("KOInventory");
                invObj.AddComponent<KOInventory>();
            }

            // Entity Manager oluştur (yaratık/uzak oyuncu yönetimi)
            if (EntityManager.Instance == null)
            {
                var emObj = new GameObject("EntityManager");
                emObj.AddComponent<EntityManager>();
            }
            
            // Invented UI dosyaları — tüm packet handler'ları
            // bunlar artık KOUIManager'ın UIF panellerini kullanıyor
            // Packet handler mantığı burada oluşturulan Singleton'larda:
            if (DeathUI.Instance == null) new GameObject("DeathUI").AddComponent<DeathUI>();
            if (ShopUI.Instance == null) new GameObject("ShopUI").AddComponent<ShopUI>();
            // Open-KO birebir: CUIPartyOrForce data manager — PartyUI'dan önce oluşturulmalı
            if (KOPartyManager.Instance == null) new GameObject("KOPartyManager").AddComponent<KOPartyManager>();
            if (PartyUI.Instance == null) new GameObject("PartyUI").AddComponent<PartyUI>();
            // Open-KO birebir: UISkillTreeDlg veri katmanı — SkillTreeUI'dan önce oluşturulmalı
            if (KOSkillTreeManager.Instance == null) new KOSkillTreeManager();
            // SkillTree UI — KOUIManager tarafından El_SkillTree_us.uif'den yüklenir (programatik oluşturma kaldırıldı)
            if (InventoryUI.Instance == null) new GameObject("InventoryUI").AddComponent<InventoryUI>();
            if (UpgradeUI.Instance == null) new GameObject("UpgradeUI").AddComponent<UpgradeUI>();
            if (SkillTrainerUI.Instance == null) new GameObject("SkillTrainerUI").AddComponent<SkillTrainerUI>();
            if (LootDropUI.Instance == null) new GameObject("LootDropUI").AddComponent<LootDropUI>();
            if (CharacterInfoUI.Instance == null) new GameObject("CharacterInfoUI").AddComponent<CharacterInfoUI>();
            if (TargetInfoUI.Instance == null) new GameObject("TargetInfoUI").AddComponent<TargetInfoUI>();
            if (WarpUI.Instance == null) new GameObject("WarpUI").AddComponent<WarpUI>();
            if (KOTradeManager.Instance == null) new GameObject("KOTradeManager").AddComponent<KOTradeManager>();
            if (TradeUI.Instance == null) new GameObject("TradeUI").AddComponent<TradeUI>();
            if (ClanUI.Instance == null) new GameObject("ClanUI").AddComponent<ClanUI>();
            if (PlayerContextMenu.Instance == null) new GameObject("PlayerContextMenu").AddComponent<PlayerContextMenu>();
            if (KOWarehouseManager.Instance == null) new GameObject("KOWarehouseManager").AddComponent<KOWarehouseManager>();
            if (WarehouseUI.Instance == null) { var w = new GameObject("WarehouseUI").AddComponent<WarehouseUI>(); w.Initialize(); }
            // C++ birebir: m_pUIInn — Inn paneli (btn_warehouse, btn_makeclan, btn_sale)
            if (InnUI.Instance == null) new GameObject("InnUI").AddComponent<InnUI>();
            // QuestDialogUI — NPC diyalog + menü (WIZ_NPC_SAY, WIZ_SELECT_MSG) handler
            if (QuestDialogUI.Instance == null) new GameObject("QuestDialogUI").AddComponent<QuestDialogUI>();
            // Merchant Manager
            if (KOMerchantManager.Instance == null)
            {
                var merchantObj = new GameObject("KOMerchantManager");
                merchantObj.AddComponent<KOMerchantManager>();
            }

            // FX Manager oluştur — Open-KO birebir: CGameProcedure::s_pFX = new CN3FXMgr()
            if (KOFXManager.Instance == null)
            {
                var fxObj = new GameObject("KOFXManager");
                var fxMgr = fxObj.AddComponent<KOFXManager>();
                fxMgr.Initialize();
            }

            // Quest tabloları yükle — Open-KO birebir: GameBase.cpp:62-66
            // szFN = "Data\\Quest_Menu" + szLangTail; s_pTbl_QuestMenu.LoadFromFile(szFN);
            // szFN = "Data\\Quest_Talk" + szLangTail; s_pTbl_QuestTalk.LoadFromFile(szFN);
            // szFN = "Data\\Quest_Content" + szLangTail; s_pTbl_QuestContent.LoadFromFile(szFN);
            string koDataDir = "Data";

            if (!KOImport.QuestTableParser.IsLoaded)
            {
                KOImport.QuestTableParser.LoadAll(koDataDir);
            }

            // Open-KO birebir: GameBase.cpp:81-82 — HER tablo BAĞIMSIZ yüklenir
            // C++ GameBase constructor'ında quest ve skill tabloları ayrı ayrı LoadFromFile çağrılır.
            // szFN = "Data\\skill_magic_main" + szLangTail;
            // s_pTbl_Skill.LoadFromFile(szFN);
            if (!KOImport.SkillTableParser.IsLoaded)
            {
                string skillPath = System.IO.Path.Combine(koDataDir, "Skill_Magic_Main_us.tbl");
                KOImport.SkillTableParser.Load(skillPath);
            }

            // Open-KO birebir: GameBase.cpp — Type1/Type2/Type3/Type4 tabloları
            // MagicSkillMng.cpp:64-65: m_pTbl_Type_1->LoadFromFile("Data\\Skill_Magic_1.tbl")
            if (!KOImport.SkillType1Parser.IsLoaded)
            {
                string type1Path = System.IO.Path.Combine(koDataDir, "skill_magic_1.tbl");
                KOImport.SkillType1Parser.Load(type1Path);
            }

            // szFN = "Data\\skill_magic_2"; s_pTbl_Type_2.LoadFromFile(szFN);
            if (!KOImport.SkillType2Parser.IsLoaded)
            {
                string type2Path = System.IO.Path.Combine(koDataDir, "skill_magic_2.tbl");
                KOImport.SkillType2Parser.Load(type2Path);
            }

            // szFN = "Data\\skill_magic_3"; s_pTbl_Type_3.LoadFromFile(szFN);
            if (!KOImport.SkillType3Parser.IsLoaded)
            {
                string type3Path = System.IO.Path.Combine(koDataDir, "skill_magic_3.tbl");
                KOImport.SkillType3Parser.Load(type3Path);
            }

            // szFN = "Data\\skill_magic_4"; s_pTbl_Type_4.LoadFromFile(szFN);
            if (!KOImport.SkillType4Parser.IsLoaded)
            {
                string type4Path = System.IO.Path.Combine(koDataDir, "skill_magic_4.tbl");
                KOImport.SkillType4Parser.Load(type4Path);
            }
        }

        // ============================
        // Zone Giriş
        // ============================

        // ============================
        // KO Event Handlers — byte[] raw data parse
        // ============================

        /// <summary>
        /// Open-KO birebir: WIZ_GAMESTART response.
        /// Server WIZ_GAMESTART gönderdikten sonra WIZ_GAMESTART sub=2 (loading finished) gönder.
        /// </summary>
        private void HandleGameStart_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp:826-838
            // sub=2 yanıtı artık KOPacketHandler'da gönderiliyor (race condition fix)
            var r = new KOPacketReader(rawData);
            byte subOp = r.ReadByte();
        }

        /// <summary>
        /// Open-KO birebir: WIZ_MYINFO — full character data.
        /// User.cpp:1968-2079 birebir parse.
        /// </summary>
        private void HandleMyInfo_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);

            short socketId = r.ReadInt16();
            string name = r.ReadKOString1(); // SetString1 = 1-byte length prefix
            ushort xRaw = r.ReadUInt16();
            ushort zRaw = r.ReadUInt16();
            short yRaw = r.ReadInt16();
            float posX = xRaw / 10f;
            float posZ = zRaw / 10f;
            float posY = yRaw / 10f;
            byte nation = r.ReadByte();
            byte race = r.ReadByte();
            short classId = r.ReadInt16();
            byte face = r.ReadByte();
            byte hairColor = r.ReadByte();
            byte rank = r.ReadByte();
            byte title = r.ReadByte();
            byte level = r.ReadByte();
            short statPoints = r.ReadInt16();
            uint maxExp = r.ReadUInt32();
            uint exp = r.ReadUInt32();
            uint loyalty = r.ReadUInt32();
            uint loyaltyMonthly = r.ReadUInt32();
            byte city = r.ReadByte();
            short knightsId = r.ReadInt16();
            byte fame = r.ReadByte();
            short allianceKnights = r.ReadInt16();
            byte knightsFlag = r.ReadByte();
            string knightsName = r.ReadKOString1();
            byte knightsGrade = r.ReadByte();
            byte knightsRanking = r.ReadByte();
            short markVersion = r.ReadInt16();
            short cape = r.ReadInt16();
            short maxHp = r.ReadInt16();
            short hp = r.ReadInt16();
            short maxMp = r.ReadInt16();
            short mp = r.ReadInt16();
            ushort maxWeight = r.ReadUInt16();   // cpp:1910 — pkt.read<uint16_t>()
            ushort curWeight = r.ReadUInt16();   // cpp:1911 — pkt.read<uint16_t>()
            byte str = r.ReadByte();
            byte itemStr = r.ReadByte();
            byte sta = r.ReadByte();
            byte itemSta = r.ReadByte();
            byte dex = r.ReadByte();
            byte itemDex = r.ReadByte();
            byte intel = r.ReadByte();
            byte itemIntel = r.ReadByte();
            byte cha = r.ReadByte();
            byte itemCha = r.ReadByte();
            short totalHit = r.ReadInt16();
            short totalAc = r.ReadInt16();
            byte fireR = r.ReadByte();
            byte coldR = r.ReadByte();
            byte lightningR = r.ReadByte();
            byte magicR = r.ReadByte();
            byte diseaseR = r.ReadByte();
            byte poisonR = r.ReadByte();
            uint gold = r.ReadUInt32();
            byte authority = r.ReadByte();

            // === C++ birebir: cpp:1938-1939 — extra rank bytes ===
            byte bKnightsRank = r.ReadByte();   // cpp:1938
            byte bPersonalRank = r.ReadByte();   // cpp:1939

            // === C++ birebir: cpp:1942-1943 — skill tree info (9 bytes) ===
            byte[] skillInfo = new byte[9];
            for (int i = 0; i < 9; i++)
                skillInfo[i] = r.ReadByte();     // cpp:1943

            // === C++ birebir: cpp:1957-1965 — ITEM_SLOT_COUNT=14 equipped slots ===
            const int ITEM_SLOT_COUNT = 14;
            uint[] slotItemIds = new uint[ITEM_SLOT_COUNT];
            short[] slotDurabilities = new short[ITEM_SLOT_COUNT];
            short[] slotCounts = new short[ITEM_SLOT_COUNT];
            byte[] slotFlags = new byte[ITEM_SLOT_COUNT];
            short[] slotRemainingTimes = new short[ITEM_SLOT_COUNT];
            for (int i = 0; i < ITEM_SLOT_COUNT; i++)
            {
                slotItemIds[i] = r.ReadUInt32();     // cpp:1959
                slotDurabilities[i] = r.ReadInt16();  // cpp:1960
                slotCounts[i] = r.ReadInt16();        // cpp:1961
                slotFlags[i] = r.ReadByte();          // cpp:1963 — bRentFlag
                slotRemainingTimes[i] = r.ReadInt16(); // cpp:1964 — sRemainingRentalTime
            }

            // === C++ birebir: cpp:1997-2005 — MAX_ITEM_INVENTORY=28 inventory items ===
            const int MAX_ITEM_INVENTORY = 28;
            uint[] invItemIds = new uint[MAX_ITEM_INVENTORY];
            short[] invDurabilities = new short[MAX_ITEM_INVENTORY];
            short[] invCounts = new short[MAX_ITEM_INVENTORY];
            byte[] invFlags = new byte[MAX_ITEM_INVENTORY];
            short[] invRemainingTimes = new short[MAX_ITEM_INVENTORY];
            for (int i = 0; i < MAX_ITEM_INVENTORY; i++)
            {
                invItemIds[i] = r.ReadUInt32();       // cpp:1999
                invDurabilities[i] = r.ReadInt16();    // cpp:2000
                invCounts[i] = r.ReadInt16();          // cpp:2001
                invFlags[i] = r.ReadByte();            // cpp:2003 — bRentFlag
                invRemainingTimes[i] = r.ReadInt16();   // cpp:2004 — sRemainingRentalTime
            }

            // === C++ birebir: cpp:2007-2011 — trailing fields ===
            r.ReadByte();                             // cpp:2007 — unknown
            r.ReadByte();                             // cpp:2008 — unknown
            r.ReadInt16();                            // cpp:2009 — unknown
            byte isChicken = r.ReadByte();            // cpp:2010 — bIsChicken
            uint mannerPoints = r.ReadUInt32();       // cpp:2011 — iMannerPoints



            // === C++ birebir: s_pPlayer->m_InfoBase/m_InfoExt alanları ===
            // Sadece GameManager'da mevcut olan property'lere yazılır.
            if (GameManager.Instance != null)
            {
                var gm = GameManager.Instance;
                gm.CharacterName  = name;
                gm.CharacterId    = socketId;            // cpp:1844 iID
                gm.Nation         = nation;              // cpp:1855
                gm.Race           = race;                // cpp:1856
                gm.CharClass      = (byte)classId;       // cpp:1857
                gm.PlayerFace     = face;                // cpp:1858
                gm.PlayerHairColor = hairColor;           // cpp:1859
                gm.Level          = level;               // cpp:1872
                gm._levelPrev    = (short)level;         // cpp:1873 — iLevelPrev = iLevel
                gm.StatPoints     = statPoints;          // cpp:1874
                gm.Experience     = exp;                 // cpp:1877
                gm.Loyalty        = (int)loyalty;        // cpp:1878
                gm.LoyaltyMonthly = (int)loyaltyMonthly; // cpp:1881
                gm.MaxHp          = maxHp;               // cpp:1906
                gm.CurrentHp      = hp;                  // cpp:1907
                gm.MaxMp          = maxMp;               // cpp:1908
                gm.CurrentMp      = mp;                  // cpp:1909
                gm.StatStr        = str;                 // cpp:1913
                gm.StatSta        = sta;                 // cpp:1915
                gm.StatDex        = dex;                 // cpp:1917
                gm.StatInt        = intel;               // cpp:1919
                gm.StatCha        = cha;                 // cpp:1921
                gm.StrDelta       = itemStr;             // cpp:1914
                gm.StaDelta       = itemSta;             // cpp:1916
                gm.DexDelta       = itemDex;             // cpp:1918
                gm.IntDelta       = itemIntel;           // cpp:1920
                gm.ChaDelta       = itemCha;             // cpp:1922
                gm.TotalHit       = totalHit;            // cpp:1924 iAttack
                gm.TotalAc        = totalAc;             // cpp:1926 iGuard
                gm.FireR          = fireR;               // cpp:1928
                gm.ColdR          = coldR;               // cpp:1929
                gm.LightningR     = lightningR;          // cpp:1930
                gm.MagicR         = magicR;              // cpp:1931
                gm.DiseaseR       = diseaseR;            // cpp:1932
                gm.PoisonR        = poisonR;             // cpp:1933
                gm.Gold           = gold;                // cpp:1935
                gm.Authority      = authority;           // cpp:1936
                gm.MaxWeight      = (short)maxWeight;
                gm.CurrentWeight  = (short)curWeight;
                gm.MannerPoints   = mannerPoints;
                gm.PlayerPosX     = posX;
                gm.PlayerPosY     = posY;
                gm.PlayerPosZ     = posZ;

                // cpp:1942-1943 — skill tree info → SkillTreePoints
                for (int i = 0; i < 9; i++)
                    gm.SkillTreePoints[i] = skillInfo[i];

                // C++ birebir (cpp:1943-1945):
                //   m_pUISkillTreeDlg->m_iSkillInfo[i] = pkt.read<uint8_t>();
                //   m_pUISkillTreeDlg->InitIconUpdate();
                var skillMgr = EntropyOnline.UI.KOSkillTreeManager.Instance;
                if (skillMgr == null)
                    skillMgr = new EntropyOnline.UI.KOSkillTreeManager();
                int[] skillInfoInt = new int[9];
                for (int i = 0; i < 9; i++)
                    skillInfoInt[i] = skillInfo[i];
                skillMgr.SetSkillInfo(skillInfoInt);
            }

            // === C++ birebir: cpp:1886-1903 — Knights info ===
            // KnightsInfoSet(iKnightsID, szKnightsName, iKnightsGrade, iKnightsRank)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ClanName = knightsName ?? string.Empty;
                GameManager.Instance.CapeId = cape;
            }
            if (KOKnightsManager.Instance != null && knightsId > 0)
                KOKnightsManager.Instance.SetInitialState(knightsId, knightsName, fame, cape);

            // === C++ birebir: cpp:2013-2134 — Envanter slot/bag dolumu ===
            // m_pUIInventory->ReleaseItem() sonra m_pMySlot[i] ve m_pMyInvWnd[i] doldur
            if (KOInventory.Instance != null)
            {
                var inv = KOInventory.Instance;
                inv.ReleaseItem(); // cpp:2013

                // cpp:2016-2098 — equipped slots
                for (int i = 0; i < ITEM_SLOT_COUNT; i++)
                {
                    if (slotItemIds[i] == 0) continue;
                    inv.m_pMySlot[i] = new KOInventory.ItemSlot
                    {
                        itemId     = (int)slotItemIds[i],
                        durability = slotDurabilities[i],
                        count      = slotCounts[i],
                        byFlag     = slotFlags[i],
                        sTimeRemaining = slotRemainingTimes[i]
                    };
                    // C++ birebir: pItem->byAttachPoint item table'dan okunur
                    if (KOInventory.s_pTbl_Items_Basic != null
                        && KOInventory.s_pTbl_Items_Basic.TryGetValue(slotItemIds[i] / 1000 * 1000, out var basicSlot))
                    {
                        inv.m_pMySlot[i].pItemBasic = basicSlot;
                        inv.m_pMySlot[i].attachPoint = basicSlot.byAttachPoint;
                        inv.m_pMySlot[i].itemClass = basicSlot.byClass;
                        inv.m_pMySlot[i].iconFN = basicSlot.dwIDIcon.ToString();
                    }
                    // serverData oluştur — tooltip'te item bilgisi gösterilsin
                    inv.m_pMySlot[i].serverData = new InventoryItemData
                    {
                        ItemDefId = (int)slotItemIds[i],
                        StackCount = slotCounts[i],
                        Durability = slotDurabilities[i],
                        SlotType = 1, // EQUIPPED
                        SlotIndex = (byte)i,
                        IconId = inv.m_pMySlot[i].iconFN ?? "",
                        AttachPoint = (byte)inv.m_pMySlot[i].attachPoint,
                        Type = (byte)inv.m_pMySlot[i].itemClass,
                        byFlag = slotFlags[i],
                        sTimeRemaining = slotRemainingTimes[i]
                    };
                }

                // cpp:2126-2133 — inventory items
                for (int i = 0; i < MAX_ITEM_INVENTORY; i++)
                {
                    if (invItemIds[i] == 0) continue;
                    inv.m_pMyInvWnd[i] = new KOInventory.ItemSlot
                    {
                        itemId     = (int)invItemIds[i],
                        durability = invDurabilities[i],
                        count      = invCounts[i],
                        byFlag     = invFlags[i],
                        sTimeRemaining = invRemainingTimes[i]
                    };
                    // C++ birebir: pItem->byAttachPoint item table'dan okunur
                    if (KOInventory.s_pTbl_Items_Basic != null
                        && KOInventory.s_pTbl_Items_Basic.TryGetValue(invItemIds[i] / 1000 * 1000, out var basicInv))
                    {
                        inv.m_pMyInvWnd[i].pItemBasic = basicInv;
                        inv.m_pMyInvWnd[i].attachPoint = basicInv.byAttachPoint;
                        inv.m_pMyInvWnd[i].itemClass = basicInv.byClass;
                        inv.m_pMyInvWnd[i].iconFN = basicInv.dwIDIcon.ToString();
                    }
                    // serverData oluştur — tooltip'te item bilgisi gösterilsin
                    inv.m_pMyInvWnd[i].serverData = new InventoryItemData
                    {
                        ItemDefId = (int)invItemIds[i],
                        StackCount = invCounts[i],
                        Durability = invDurabilities[i],
                        SlotType = 0, // INVENTORY
                        SlotIndex = (byte)i,
                        IconId = inv.m_pMyInvWnd[i].iconFN ?? "",
                        AttachPoint = (byte)inv.m_pMyInvWnd[i].attachPoint,
                        Type = (byte)inv.m_pMyInvWnd[i].itemClass,
                        byFlag = invFlags[i],
                        sTimeRemaining = invRemainingTimes[i]
                    };
                }
            }

            // === C++ birebir: cpp:1868 — InitChr(pLooks) ===
            if (WorldBuilder.Instance != null)
            {
                try
                {
                    WorldBuilder.Instance.BuildPlayerModel(race);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MY_INFO] BuildPlayerModel EXCEPTION: {ex}");
                }
            }
            else
            {
                Debug.LogError("[MY_INFO] WorldBuilder.Instance == null — model oluşturulamadı!");
            }

            // === C++ birebir: InitPlayerPosition (GameProcMain.cpp:451-481) ===
            // fYTerrain = ACT_WORLD->GetHeightWithTerrain(vPos.x, vPos.z)
            // fYObject  = ACT_WORLD->GetHeightNearstPosWithShape(vPos)
            // if (!IsIndoor()) → abs(vPos.y - fYObject) < abs(vPos.y - fYTerrain) ? fYObject : fYTerrain
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float useX = posX;
                float useZ = posZ;
                float useY = posY; // server Y

                if (WorldBuilder.Instance != null)
                {
                    // C++ birebir: fYTerrain = ACT_WORLD->GetHeightWithTerrain(x, z)
                    float fYTerrain = float.MinValue;
                    if (!WorldBuilder.Instance.IsIndoor && WorldBuilder.Instance.Terrain != null)
                    {
                        var terrain = WorldBuilder.Instance.Terrain;
                        fYTerrain = terrain.transform.position.y + terrain.SampleHeight(new Vector3(useX, 0, useZ));
                    }

                    // C++ birebir: fYObject = ACT_WORLD->GetHeightNearstPosWithShape(vPos)
                    float fYObject = float.MinValue;
                    if (KOCollisionManager.Instance != null)
                        fYObject = KOCollisionManager.Instance.GetHeightNearstPos(new Vector3(useX, useY, useZ));

                    if (WorldBuilder.Instance.IsIndoor)
                    {
                        if (fYObject > float.MinValue)
                            useY = fYObject;
                    }
                    else
                    {
                        // C++ birebir: GameProcMain.cpp:456-461
                        // Outdoor: server Y'ye en yakın olanı seç
                        if (fYTerrain > float.MinValue && fYObject > float.MinValue)
                        {
                            if (Mathf.Abs(useY - fYObject) < Mathf.Abs(useY - fYTerrain))
                                useY = fYObject;
                            else
                                useY = fYTerrain;
                        }
                        else if (fYTerrain > float.MinValue)
                        {
                            useY = fYTerrain;
                        }
                        else if (fYObject > float.MinValue)
                        {
                            useY = fYObject;
                        }
                    }
                }

                // Negatif veya aşırı yüksek Y güvenlik kontrolü
                if (useY < -100f || useY > 1000f)
                {
                    Debug.LogWarning($"[MY_INFO] Bozuk Y değeri: {useY:F1} — 1.0'a clamp ediliyor");
                    useY = 1f;
                }

                // C++ birebir: CharacterController aktifken transform.position set edilemez
                // CharacterController geçici devre dışı bırak
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                player.transform.position = new Vector3(useX, useY, useZ);

                // C++ birebir: terrain yüklendikten sonra RepositionPlayer cc.enabled = true yapacak.
                // Eğer terrain henüz yüklenmediyse cc'yi disable bırakıyoruz ki oyuncu aşağı düşmesin.
                if (WorldBuilder.Instance != null && WorldBuilder.Instance.Terrain != null)
                {
                    if (cc != null) cc.enabled = true;
                }

                // Kamerayı anında snap et (ilk spawn'da dönme efektini önle)
                var camSpawn = FindAnyObjectByType<EntropyOnline.Camera.CameraController>();
                if (camSpawn != null) camSpawn.SnapToTarget();
                if (player.GetComponent<KOEquipmentVisualizer>() == null)
                    player.AddComponent<KOEquipmentVisualizer>();

                // C++ birebir: MsgRecv_MyInfo_All → InitChr(pLooks) → PlugSet/PartSet
                // GameProcMain.cpp:1868 — slot doldurulduktan hemen sonra senkron çağrılır.
                var equipVis = player.GetComponent<KOEquipmentVisualizer>();
                if (equipVis != null)
                    equipVis.InitEquipment();
            }

            // C++ GameProcMain::InitZone() satır 4478: ilk girişte minimap yükle
            if (KOUIManager.Instance != null)
            {
                string minimapFileName = KOUIManager.GetMiniMapFileName(city);
                float mapSize = 1024.0f;
                if (WorldBuilder.Instance != null && WorldBuilder.Instance.TerrainWorldSize > 0)
                {
                    mapSize = WorldBuilder.Instance.TerrainWorldSize;
                }
                KOUIManager.Instance.LoadMiniMap(minimapFileName, mapSize, mapSize);

                float fZoom = 6.0f;
                if (IsRepresentRogue((byte)classId))
                    fZoom = 3.0f;
                KOUIManager.Instance.SetMiniMapZoom(fZoom);
            }

            // === NPC Region tetikle: Player pozisyonu set edildikten hemen sonra ===
            // C++ birebir: MsgRecv_MyInfo_All sonrası sunucu WIZ_NPC_REGION bekler
            // Karakter konumu artık doğru — sunucuya move gönder, NPC'ler gelsin
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var pc = playerObj.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.SendInitialPositionReport();
                }
                else
                {
                    Debug.LogWarning("[GAME] PlayerController not found on Player GameObject in HandleMyInfo_KO!");
                }
            }
        }



        /// Open-KO birebir: MsgRecv_UserMove (GameProcMain.cpp:2327)

        /// GameSceneController sadece kendi oyuncumuzu günceller — diğer oyuncular EntityManager'da.
        /// </summary>
        private void HandleUserMove_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short id = r.ReadInt16();
            float x = r.ReadUInt16() / 10f;
            float z = r.ReadUInt16() / 10f;
            float y = r.ReadInt16() / 10f;

            // Kendi oyuncumuz ise GameManager pozisyonunu güncelle
            var gm = GameManager.Instance;
            if (gm != null && id == (short)gm.CharacterId)
            {
                gm.PlayerPosX = x;
                gm.PlayerPosZ = z;
            }
            // Entity movement → EntityManager handles
        }

        private void HandleHpChange_KO(short maxHp, short curHp)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.MaxHp = maxHp;
                GameManager.Instance.CurrentHp = curHp;
            }
        }

        private void HandleMSpChange_KO(short maxMp, short curMp)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.MaxMp = maxMp;
                GameManager.Instance.CurrentMp = curMp;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Attack (GameProcMain.cpp:3213)
        /// Wire: [opcode][type:byte][result:byte][attackerId:int16][targetId:int16]
        /// </summary>
        private void HandleAttackResult_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();
            byte result = r.ReadByte();
            short attackerId = r.ReadInt16();
            short targetId = r.ReadInt16();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_UserInOut (GameProcMain.cpp:2510)
        /// Wire: [opcode][type:byte]... — EntityManager handles full parse.
        /// </summary>
        private void HandleUserInOut_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_NPCInOut (GameProcMain.cpp:2838)
        /// Wire: [opcode][type:byte]... — EntityManager handles full parse.
        /// </summary>
        private void HandleNpcInOut_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_Dead (GameProcMain.cpp:3330)
        /// Wire: [opcode][targetId:int16]
        /// </summary>
        private void HandleDead_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short targetId = r.ReadInt16();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_EXP (GameProcMain.cpp:3696)
        /// Wire: [opcode][totalExp:uint32]
        /// </summary>
        private void HandleExpChange_KO(byte[] rawData)
        {
            // NOT: gm.Experience güncellemesi GameHUD.OnExpGained'de yapılır.
            // C++ birebir: MsgRecv_MyInfo_EXP (GameProcMain.cpp:3696-3717) tek handler'dır.
            // Burada duplicate set yapılırsa GameHUD delta hesaplayamaz.
            var r = new KOPacketReader(rawData);
            uint totalExp = r.ReadUInt32();
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_MyInfo_LevelChange (GameProcMain.cpp:3719)
        /// Wire: [opcode][id:int16][level:byte]...
        /// </summary>
        private void HandleLevelChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            short id = r.ReadInt16();
            byte level = r.ReadByte();
            var gm = GameManager.Instance;
            if (gm != null && id == (short)gm.CharacterId)
            {
                gm.Level = level;
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_ZoneChange (GameProcMain.cpp:4968-5030)
        /// Wire: [opcode][subOp:byte] if TELEPORT(1): [zone:byte][zoneSub:byte][x:uint16][z:uint16][y:int16][victoryNation:byte]
        /// NOT: GameHUD.OnZoneChange_KO da aynı paketi parse eder — ikisi tutarlı olmalı!
        /// openko-ref birebir: User.cpp:2751-2758
        ///   SetByte(zone), SetByte(subzone), SetShort(x*10), SetShort(z*10), SetShort(y*10), SetByte(victory)
        /// </summary>
        /// <summary>
        /// Open-KO birebir: MsgRecv_ZoneChange (GameProcMain.cpp:4968-5030)
        /// + InitZone (GameProcMain.cpp:4430-4520)
        /// + InitPlayerPosition (GameProcMain.cpp:451-481)
        /// </summary>
        private void HandleZoneChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte subOp = r.ReadByte();

            const byte ZONE_CHANGE_LOADING = 1;
            const byte ZONE_CHANGE_LOADED = 2;
            const byte ZONE_CHANGE_TELEPORT = 3;

            // Open-KO birebir: subOp=ZONE_CHANGE_TELEPORT (3) veya ZONE_CHANGE_LOADING (1) (eğer zone verisi içeriyorsa)
            bool hasZoneData = r.Remaining >= 8; // zone(1)+sub(1)+x(2)+z(2)+y(2)+victory(1) = 9 bytes min
            if (subOp == ZONE_CHANGE_TELEPORT || (subOp == ZONE_CHANGE_LOADING && hasZoneData))
            {
                // openko-ref birebir: User.cpp:2753-2758
                byte zoneId = r.ReadByte();       // SetByte(m_pUserData->m_bZone)
                byte zoneSub = r.ReadByte();      // SetByte(0) — subzone
                ushort x = r.ReadUInt16();        // SetShort(x*10)
                ushort z = r.ReadUInt16();        // SetShort(z*10)
                short y = r.ReadInt16();          // SetShort(y*10)
                byte victory = r.ReadByte();      // SetByte(victory)

                float posX = x / 10f;
                float posZ = z / 10f;
                float posY = y / 10f;

                StartCoroutine(DoZoneChangeSequence(zoneId, posX, posY, posZ));
            }
            else if (subOp == ZONE_CHANGE_LOADED) // 2
            {
                // C++ cpp:5020-5024 — WIZ_ZONE_CHANGE + ZONE_CHANGE_LOADED gönder
                var netMgr = KONetworkManager.Instance;
                if (netMgr != null && netMgr.IsConnected)
                {
                    using var pkt = new KOPacketWriter(WizOpcode.WIZ_ZONE_CHANGE);
                    pkt.WriteByte(ZONE_CHANGE_LOADED); // Sends 2
                    netMgr.SendPacket(pkt);
                }

                // Send initial position report (stop packet) after zone load completes to trigger NPC spawning in new zone
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj == null) playerObj = GameObject.Find("Player");
                if (playerObj != null)
                {
                    var pc = playerObj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.SendInitialPositionReport();
                    }
                    else
                    {
                        Debug.LogWarning("[GAME] PlayerController not found on Player GameObject in ZONE_CHANGE_LOADED!");
                    }
                }

                _isZoneChangeLoadedReceived = true;
            }
        }

        private System.Collections.IEnumerator DoZoneChangeSequence(byte zoneId, float posX, float posY, float posZ)
        {
            // C++ cpp:4450 — MsgSend_SpeedCheck(true) — speed hack timer reset
            SendSpeedCheck(true);

            _isZoneChangeLoadedReceived = false;

            short iZonePrev = GameManager.Instance?.CurrentZoneId ?? 0;

            // C++ cpp:4464 — s_pPlayer->m_InfoExt.iZoneCur = iZone
            if (GameManager.Instance != null)
                GameManager.Instance.CurrentZoneId = zoneId;

            bool zoneChanged = (iZonePrev != zoneId);

            if (zoneChanged)
            {
                // Show loading screen immediately!
                byte nation = GameManager.Instance?.Nation ?? 0;
                if (EntropyOnline.UI.KOLoadingScreen.Instance == null)
                {
                    var loadingObj = new GameObject("KOLoadingScreen");
                    loadingObj.AddComponent<EntropyOnline.UI.KOLoadingScreen>();
                }
                EntropyOnline.UI.KOLoadingScreen.Instance.Show(nation);

                // Let the progress bar climb smoothly to 90% BEFORE we freeze the thread!
                while (EntropyOnline.UI.KOLoadingScreen.Instance.CurrentDisplayPercentage < 90f)
                {
                    yield return null;
                }

                // C++ cpp:4475 — s_pOPMgr->Release()
                if (EntityManager.Instance != null)
                    EntityManager.Instance.ClearAll();

                // C++ cpp:4476 — s_pWorldMgr->InitWorld(iZone) — terrain/shape yeniden yükle
                if (WorldBuilder.Instance != null)
                {
                    WorldBuilder.Instance.ChangeZone(zoneId);
                }

                // C++ cpp:4458-4459 — Clear durational magic and FX
                EntropyOnline.Combat.KOMagicSkillManager.Instance?.ClearDurationalMagic();
                KO.KOFXManager.Instance?.ClearAll();
            }

            // === C++ birebir: InitPlayerPosition(vPos) — GameProcMain.cpp:451-481 ===
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                float useY = posY;

                if (WorldBuilder.Instance != null)
                {
                    // C++ cpp:455 — fYTerrain = ACT_WORLD->GetHeightWithTerrain(x, z)
                    float fYTerrain = float.MinValue;
                    if (!WorldBuilder.Instance.IsIndoor && WorldBuilder.Instance.Terrain != null)
                    {
                        var terrain = WorldBuilder.Instance.Terrain;
                        fYTerrain = terrain.transform.position.y + terrain.SampleHeight(new Vector3(posX, 0, posZ));
                    }

                    // C++ cpp:456 — fYObject = ACT_WORLD->GetHeightNearstPosWithShape(vPos)
                    float fYObject = float.MinValue;
                    if (KOCollisionManager.Instance != null)
                        fYObject = KOCollisionManager.Instance.GetHeightNearstPos(new Vector3(posX, posY, posZ));

                    if (WorldBuilder.Instance.IsIndoor)
                    {
                        if (fYObject > float.MinValue)
                            useY = fYObject;
                    }
                    else
                    {
                        // C++ cpp:457-468 — outdoor: abs(y-obj) < abs(y-terrain) ? obj : terrain
                        if (fYTerrain > float.MinValue && fYObject > float.MinValue)
                        {
                            if (Mathf.Abs(posY - fYObject) < Mathf.Abs(posY - fYTerrain))
                                useY = fYObject;
                            else
                                useY = fYTerrain;
                        }
                        else if (fYTerrain > float.MinValue)
                        {
                            useY = fYTerrain;
                        }
                        else if (fYObject > float.MinValue)
                        {
                            useY = fYObject;
                        }
                    }
                }

                player.transform.position = new Vector3(posX, useY, posZ);
                
                // C++ birebir: terrain yüklendikten sonra RepositionPlayer cc.enabled = true yapacak.
                if (WorldBuilder.Instance != null && WorldBuilder.Instance.Terrain != null)
                {
                    if (cc != null) cc.enabled = true;
                }

                // Kamerayı anında snap et (zone geçişinde dönme efektini önle)
                var cam = FindAnyObjectByType<EntropyOnline.Camera.CameraController>();
                if (cam != null) cam.SnapToTarget();

                // C++ cpp:477 — TargetSelect(-1, false)
                if (KOTargetSelector.Instance != null)
                    KOTargetSelector.Instance.ClearTarget();
            }

            // C++ GameProcMain::InitZone() satır 4478-4480 birebir:
            if (KOUIManager.Instance != null)
            {
                string minimapFileName = KOUIManager.GetMiniMapFileName(zoneId);
                float mapSize = 1024.0f;
                if (WorldBuilder.Instance != null && WorldBuilder.Instance.TerrainWorldSize > 0)
                {
                    mapSize = WorldBuilder.Instance.TerrainWorldSize;
                }
                KOUIManager.Instance.LoadMiniMap(minimapFileName, mapSize, mapSize);

                float fZoom = 6.0f;
                var gm = GameManager.Instance;
                if (gm != null && IsRepresentRogue(gm.CharClass))
                    fZoom = 3.0f;
                KOUIManager.Instance.SetMiniMapZoom(fZoom);
            }

            // C++ cpp:5009-5013 — WIZ_ZONE_CHANGE + ZONE_CHANGE_LOADING gönder
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                const byte ZONE_CHANGE_LOADING = 1;
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_ZONE_CHANGE);
                pkt.WriteByte(ZONE_CHANGE_LOADING);
                netMgr.SendPacket(pkt);
            }

            // Warp UI kapat
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowWarp(false, true);

            if (zoneChanged)
            {
                if (EntropyOnline.UI.KOLoadingScreen.Instance != null)
                {
                    // Tell loading screen that actual loading is done so progress bar can finish to 100%
                    EntropyOnline.UI.KOLoadingScreen.Instance.SetActualLoadingDone(true);

                    // Görsel progress bar'ın %100'e ulaşmasını bekle
                    while (EntropyOnline.UI.KOLoadingScreen.Instance.CurrentDisplayPercentage < 100f)
                    {
                        yield return null;
                    }

                    // Wait for the server to acknowledge zone change loaded (subOp == 2)
                    float timeoutTime = Time.time + 5f;
                    while (!_isZoneChangeLoadedReceived && Time.time < timeoutTime)
                    {
                        yield return null;
                    }

                    // Additional delay to allow NPC spawn packets to be processed and models instantiated
                    yield return new WaitForSeconds(1.5f);

                    EntropyOnline.UI.KOLoadingScreen.Instance.Hide();
                }
            }
        }


        private void HandleGoldChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();
            uint goldAmount = r.ReadUInt32();
            uint totalGold = r.ReadUInt32();

            // C++ birebir: MsgRecv_NoahChange (GameProcMain.cpp:6722-6750)
            if (KOUIManager.Instance != null)
            {
                switch (type)
                {
                    case 1: // GOLD_CHANGE_GAIN — C++ satır 6732-6734
                        // text_resources.h:446: IDS_NOAH_CHANGE_GET (6088) = "Earned %d Coins."
                        KOUIManager.Instance.AddMsgOutput(
                            $"Earned {goldAmount} Coins.",
                            KOUIManager.D3DColorToUnity(0xff6565ff)); // mavi
                        break;
                    case 2: // GOLD_CHANGE_LOSE — C++ satır 6737-6739
                        // text_resources.h:447: IDS_NOAH_CHANGE_LOST (6089) = "Lost %d Coins."
                        KOUIManager.Instance.AddMsgOutput(
                            $"Lost {goldAmount} Coins.",
                            KOUIManager.D3DColorToUnity(0xffff3b3b)); // kırmızı
                        break;
                    case 3: // GOLD_CHANGE_SPEND — C++ satır 6742-6744
                        // text_resources.h:448: IDS_NOAH_CHANGE_SPEND (6099) = "Used %d Coins."
                        KOUIManager.Instance.AddMsgOutput(
                            $"Used {goldAmount} Coins.",
                            KOUIManager.D3DColorToUnity(0xffff3b3b)); // kırmızı
                        break;
                }
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Gold = totalGold;

                // C++ birebir: gold UI güncelle (inventory, transaction, perTrade)
                KOUIManager.Instance?.UpdateGold(totalGold);
            }
        }

        private static bool IsRepresentRogue(byte charClass)
        {
            return charClass switch
            {
                102 => true,  // CLASS_KA_ROGUE
                107 => true,  // CLASS_KA_HUNTER
                108 => true,  // CLASS_KA_PENETRATOR
                202 => true,  // CLASS_EL_ROGUE
                207 => true,  // CLASS_EL_RANGER
                208 => true,  // CLASS_EL_ASSASSIN
                _ => false
            };
        }

        private void HandleWeightChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            ushort curWeight = r.ReadUInt16();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CurrentWeight = (short)curWeight;
            }
        }
    }
}
