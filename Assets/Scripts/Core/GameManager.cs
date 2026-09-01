using UnityEngine;

namespace EntropyOnline.Core
{
    /// <summary>
    /// Entropy Online — Ana Oyun Yöneticisi (Singleton)
    /// 
    /// Oyunun genel durumunu (Login, Karakter Seçim, Oyun İçi) yönetir.
    /// Tüm sahnelerde yaşar (DontDestroyOnLoad).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Oyun Durumu")]
        public GameState CurrentState = GameState.Login;

        // Aktif oyuncu bilgileri (login sonrası doldurulur)
        public long AccountId { get; set; }
        public long CharacterId { get; set; }
        public string CharacterName { get; set; }
        public byte Nation { get; set; }
        
        /// <summary>
        /// Open-KO birebir: __InfoPlayerBase::eRace (globals.h:164-176)
        /// Karakter ırkı — UPC_DefaultLooks.tbl'den model yüklemek için zorunlu.
        ///   1=KA_Ark, 2=KA_Tur, 3=KA_Wrt, 4=KA_Pri, 5=KA_Pla, 6=KA_Assa
        ///   11=EL_War, 12=EL_Rog, 13=EL_Mag, 14=EL_Pri
        /// Sunucudan S2C_MY_INFO ile set edilir.
        /// </summary>
        public byte Race { get; set; }
        
        /// <summary>
        /// Open-KO birebir: __InfoPlayerExt::iFace (GameDef.h:453)
        /// Karakter yüz index'i — InitFace() için kullanılır.
        /// Sunucudan S2C_MY_INFO ile set edilir.
        /// </summary>
        public byte PlayerFace { get; set; }
        
        /// <summary>
        /// Open-KO birebir: m_bHairColor (_USER_DATA.h:45)
        /// Karakter saç rengi — InitHair() için kullanılır.
        /// Sunucudan S2C_MY_INFO ile set edilir.
        /// </summary>
        public byte PlayerHairColor { get; set; }
        
        /// <summary>
        /// Open-KO birebir: __InfoPlayerBase::iAuthority (globals.h:57-67)
        /// 0 = AUTHORITY_MANAGER (GM), 1 = AUTHORITY_USER (normal),
        /// 11 = AUTHORITY_NOCHAT, 255 = AUTHORITY_BLOCK_USER
        /// Sunucudan S2C_MY_INFO ile set edilir.
        /// </summary>
        public byte Authority { get; set; } = 1; // Varsayılan: normal kullanıcı
        
        public byte CharClass { get; set; }
        public string JwtToken { get; set; }
        
        // Karakter statları (Zone Server'dan güncellenir)
        public short Level { get; set; } = 1;
        
        /// <summary>
        /// Open-KO birebir: __InfoPlayerMySelf::iLevelPrev (GameProcMain.cpp:3705,3716)
        /// MsgRecv_MyInfo_EXP'de seviye atlanmadığını kontrol etmek için kullanılır.
        /// C++ satır 3705: if (iLevelPrev == iLevel && iExp != iOldExp) → MsgOutput
        /// C++ satır 3716: iLevelPrev = iLevel
        /// </summary>
        public short _levelPrev = 1;
        public long Experience { get; set; }
        public int CurrentHp { get; set; } = 0;
        
        private int _maxHp = 0;
        public int MaxHp
        {
            get => _maxHp;
            set { if (value > 0) _maxHp = value; }
        }
        
        public int CurrentMp { get; set; } = 0;
        
        private int _maxMp = 0;
        public int MaxMp
        {
            get => _maxMp;
            set { if (value > 0) _maxMp = value; }
        }
        public long Gold { get; set; }
        
        // Aktif zone bilgisi
        public short CurrentZoneId { get; set; } = 0; // Karakter seçiminde set edilir
        
        // Klan bilgisi — Open-KO birebir: __InfoPlayerMySelf (GameDef.h:426-428)
        public string ClanName { get; set; } = string.Empty;       // szKnights
        public int KnightsGrade { get; set; }                      // iKnightsGrade (1-5)
        public byte KnightsDuty { get; set; }                      // eKnightsDuty
        public int KnightsRank { get; set; }                       // iKnightsRank
        public short CapeId { get; set; }
        public System.Collections.Generic.List<KnightsMemberInfo> KnightsMembers { get; set; } = new();
        public int KnightsMembersOnline { get; set; }
        public int KnightsMembersTotal { get; set; }
        
        // Karakter stat'ları
        public short StatStr { get; set; }
        public short StatSta { get; set; }
        public short StatDex { get; set; }
        public short StatInt { get; set; }
        public short StatCha { get; set; }
        private short _statPoints;
        private int _lastDecrementFrame = -1;
        public short StatPoints
        {
            get => _statPoints;
            set
            {
                if (value == _statPoints - 1)
                {
                    if (UnityEngine.Time.frameCount == _lastDecrementFrame)
                    {
                        return;
                    }
                    _lastDecrementFrame = UnityEngine.Time.frameCount;
                }
                _statPoints = value;
            }
        }
        public short SkillPoints { get; set; }
        
        // Karakter Ağırlık ve Manner Puanları
        public short MaxWeight { get; set; }
        public short CurrentWeight { get; set; }
        public uint MannerPoints { get; set; }
        
        /// <summary>
        /// Open-KO: m_bstrSkill[0..8] / UISkillTreeDlg m_iSkillInfo[MAX_SKILL_FROM_SERVER=9]
        /// [0] = serbest puan (SkillPoints ile senkron), [1..4] = basic stat puanları,
        /// [5] = Special0, [6] = Special1, [7] = Special2, [8] = Master
        /// </summary>
        public int[] SkillTreePoints { get; set; } = new int[9];
        
        // Envanter
        public EntropyOnline.Network.InventoryItemData[] Inventory { get; set; }
        
        // Hedef bilgisi (saldırı/etkileşim)
        public long TargetId { get; set; } = -1;
        public bool TargetIsPlayer { get; set; }
        
        /// <summary>
        /// Open-KO birebir: szTargetID — hedef oyuncunun ismi.
        /// UPCGetByID ile alınır. Friend Btn_Add için kullanılır.
        /// </summary>
        public string TargetName { get; set; }
        
        // Öğrenilmiş skill'ler (sunucudan S2C_SKILL_LIST ile gelir)
        public int[] LearnedSkills { get; set; }
        
        // Savaş istatistikleri (sunucudan S2C_COMBAT_STATS ile güncellenir)
        // Open-KO: __InfoPlayerMySelf yapısı — GameDef.h satır 464-480
        public int TotalHit { get; set; }       // C++: iAttack
        public int TotalAc { get; set; }        // C++: iGuard
        public float TotalHitRate { get; set; }
        public float TotalEvasionRate { get; set; }
        
        // Toplam statlar (base + item + buff)
        public short TotalStr { get; set; }
        public short TotalSta { get; set; }
        public short TotalDex { get; set; }
        public short TotalInt { get; set; }
        
        // ================================================
        // Open-KO: __InfoPlayerMySelf — Stat Deltas
        // GameDef.h satır 468-480
        // MsgRecv_ItemMove (satır 3374-3393) tarafından set edilir
        // Weight sistemi mobilde devre dışı (ItemWeight, MaxWeight yok)
        // ================================================
        
        /// <summary>Open-KO: iStrength_Delta — Eşya+buff STR bonusu.</summary>
        public short StrDelta { get; set; }
        
        /// <summary>Open-KO: iStamina_Delta — Eşya+buff STA bonusu.</summary>
        public short StaDelta { get; set; }
        
        /// <summary>Open-KO: iDexterity_Delta — Eşya+buff DEX bonusu.</summary>
        public short DexDelta { get; set; }
        
        /// <summary>Open-KO: iIntelligence_Delta — Eşya+buff INT bonusu.</summary>
        public short IntDelta { get; set; }
        
        /// <summary>Open-KO: iMagicAttak_Delta — Eşya+buff CHA bonusu.</summary>
        public short ChaDelta { get; set; }
        
        /// <summary>Open-KO: iGuard_Delta — Eşya+buff AC bonusu. cpp:2377</summary>
        public int GuardDelta { get; set; }
        
        /// <summary>Open-KO: iAttack_Delta — Eşya+buff Attack bonusu. cpp:2385</summary>
        public int AttackDelta { get; set; }
        
        // ================================================
        // Open-KO: Elementel Direnç — GameDef.h satır 476-481
        // Base değerler (iRegistXxx) — sunucudan gelir
        // ================================================
        
        /// <summary>Open-KO: iRegistFire</summary>
        public short FireR { get; set; }
        
        /// <summary>Open-KO: iRegistCold</summary>
        public short ColdR { get; set; }
        
        /// <summary>Open-KO: iRegistLight</summary>
        public short LightningR { get; set; }
        
        /// <summary>Open-KO: iRegistMagic</summary>
        public short MagicR { get; set; }
        
        /// <summary>Open-KO: iRegistCurse</summary>
        public short DiseaseR { get; set; }
        
        /// <summary>Open-KO: iRegistPoison</summary>
        public short PoisonR { get; set; }
        
        // ================================================
        // Open-KO: Elementel Direnç Deltaları
        // EffectingType4 BUFFTYPE_RESIST tarafından set edilir
        // cpp:2432-2459
        // ================================================
        
        /// <summary>Open-KO: iRegistFire_Delta — cpp:2433</summary>
        public int FireRDelta { get; set; }
        
        /// <summary>Open-KO: iRegistCold_Delta — cpp:2434</summary>
        public int ColdRDelta { get; set; }
        
        /// <summary>Open-KO: iRegistLight_Delta — cpp:2435</summary>
        public int LightningRDelta { get; set; }
        
        /// <summary>Open-KO: iRegistMagic_Delta — cpp:2436</summary>
        public int MagicRDelta { get; set; }
        
        /// <summary>Open-KO: iRegistCurse_Delta — cpp:2437</summary>
        public int DiseaseRDelta { get; set; }
        
        /// <summary>Open-KO: iRegistPoison_Delta — cpp:2438</summary>
        public int PoisonRDelta { get; set; }
        
        // PvP / NP (Loyalty) — sunucudan S2C_LOYALTY_CHANGE ile güncellenir
        public int Loyalty { get; set; }
        public int LoyaltyMonthly { get; set; }

        // C++ birebir: __InfoPlayerMySelf.iRank, .iTitle (GameDef.h)
        // Tooltip gereksinim renk kontrolü için kullanılır
        public int PersonalRank { get; set; }
        public int PersonalTitle { get; set; }
        
        // ================================================
        // Zone Ability — Open-KO birebir: __InfoPlayerMySelf (GameDef.h:506-509)
        // sunucudan S2C_ZONE_ABILITY ile güncellenir
        // ================================================
        
        /// <summary>Open-KO: eZoneAbilityType — e_ZoneAbilityType enum. GameDef.h:506</summary>
        public byte ZoneAbilityType { get; set; }
        
        /// <summary>Open-KO: bCanTradeWithOtherNation. GameDef.h:507</summary>
        public bool CanTradeWithOtherNation { get; set; }
        
        /// <summary>Open-KO: bCanTalkToOtherNation. GameDef.h:508</summary>
        public bool CanTalkToOtherNation { get; set; }
        
        /// <summary>Open-KO: m_bRecruitParty — parti BBS kaydı durumu. CPlayerMyself satır 182.</summary>
        public bool RecruitParty { get; set; }
        
        /// <summary>Open-KO: sZoneTariff. GameDef.h:509</summary>
        public short ZoneTariff { get; set; }
        
        // ================================================
        // Open-KO: PointChange wire fields — GameProcMain.cpp:3829-3837
        // MsgRecv_MyInfo_PointChange tarafından set edilir.
        // C++ int olarak saklar ama wire'dan int16_t/uint16_t olarak okunur.
        // ================================================
        
        /// <summary>
        /// Open-KO: __InfoPlayerBase::iHPMax (PlayerBase.h:40)
        /// Wire'dan int16_t olarak okunur (GameProcMain.cpp:3834).
        /// MaxHp ile aynı backing store — senkronizasyon sorunu yok.
        /// </summary>
        public short MaxHP
        {
            get => (short)MaxHp;
            set => MaxHp = value;
        }
        
        /// <summary>
        /// Open-KO: __InfoPlayerMySelf::iMSPMax (GameDef.h:461)
        /// Wire'dan int16_t olarak okunur (GameProcMain.cpp:3835).
        /// MaxMp ile aynı backing store — senkronizasyon sorunu yok.
        /// </summary>
        public short MaxMP
        {
            get => (short)MaxMp;
            set => MaxMp = value;
        }
        

        
        // duplicate property removed (MaxWeight is defined as ushort at line 98)
        
        // ================================================
        // Open-KO: Manner Point — GameProcMain.cpp:3817-3820
        // LOYALTY_CHANGE_MANNER sub-opcode ile güncellenir.
        // ================================================
        
        /// <summary>
        /// Open-KO: LOYALTY_CHANGE_MANNER (GameProcMain.cpp:3820)
        /// Karakter manner puanı. Wire'dan uint32_t olarak okunur, int olarak saklanır.
        /// </summary>
        public int MannerPoint { get; set; }
        
        // ================================================
        // Zone Ability kısa isim alias'ları
        // GameHUD.OnZoneAbility_KO tarafından kullanılır.
        // C++ kaynağı: GameProcMain.cpp:8043-8053
        // ================================================
        
        /// <summary>
        /// Open-KO: bCanTradeWithOtherNation (GameDef.h:507, GameProcMain.cpp:8048)
        /// Zone ability — diğer ulusla ticaret yapılabilir mi?
        /// CanTradeWithOtherNation ile aynı backing store'u paylaşır.
        /// </summary>
        public bool CanTradeOtherNation
        {
            get => CanTradeWithOtherNation;
            set => CanTradeWithOtherNation = value;
        }
        
        /// <summary>
        /// Open-KO: bCanTalkToOtherNation (GameDef.h:508, GameProcMain.cpp:8050)
        /// Zone ability — diğer ulusla konuşulabilir mi?
        /// CanTalkToOtherNation ile aynı backing store'u paylaşır.
        /// </summary>
        public bool CanTalkOtherNation
        {
            get => CanTalkToOtherNation;
            set => CanTalkToOtherNation = value;
        }
        
        /// <summary>
        /// Convenience getter: Bu zone'da oyuncular arası saldırı mümkün mü?
        /// NOT: C++'da böyle bir computed property yok — IsHostileTarget() switch case ile kontrol eder.
        /// Tam doğruluk için IsHostileTarget() kullanılmalı (KOFXManager.cs:740-797).
        /// Bu property basitleştirilmiş bir tahminidir.
        /// </summary>
        public bool IsPvPZone => ZoneAbilityType != 0; // NEUTRAL hariç hepsi potansiyel PvP
        
        // Oyuncu pozisyonu — hareket ile güncellenir
        public float PlayerPosX { get; set; }
        public float PlayerPosY { get; set; }
        public float PlayerPosZ { get; set; }

        /// <summary>
        /// Karakter N3ChrData cache — üst gövde animasyonu (JointPartStarts) için gerekli.
        /// Karakter yüklenirken (EntityManager/CharLoader) set edilir.
        /// C++ karşılığı: CN3Chr sınıfında m_nJointPartStarts/Ends tutulur.
        /// </summary>
        public Import.N3ChrImporter.N3ChrData PlayerChrData { get; set; }

        /// <summary>
        /// N3CharBuilder'dan gelen sıralı AnimationClip listesi.
        /// KOAnimResolver bu listeyi kullanarak doğru index→clip eşlemesi yapar.
        /// Unity AnimationState foreach sırası garanti DEĞİLDİR —
        /// bu yüzden orijinal ekleme sırasını korumak gerekli.
        /// </summary>
        public System.Collections.Generic.List<AnimationClip> PlayerAnimClips { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Open-KO style: Load database tables at startup
            KOImport.ItemDataManager.LoadAll("Data");

            // Hedef FPS (mobilde pil tasarrufu için)
            Application.targetFrameRate = 60;
            
            // Ekran uyumama (idle sırasında ekran kararmasın)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // C++ birebir: GameProcedure::Init() — tüm prosedürler başlangıçta oluşturulur
            // cpp:198-213 — s_pProcLogIn, s_pProcCharacterSelect, s_pProcCharacterCreate, ...
            EnsureUIProcs();

        }

        /// <summary>
        /// C++ birebir: GameProcedure::Init() satır 198-213
        /// Tüm UI prosedürlerinin sahnede mevcut olmasını sağlar.
        /// LoginUI sahneye Inspector'dan eklenmiş olmalı (aktif).
        /// CharacterSelectUI ve CharacterCreateUI yoksa otomatik oluşturulur (inactive).
        /// </summary>
        private void EnsureUIProcs()
        {
            // CharacterSelectUI — cpp:206 — s_pProcCharacterSelect = new CGameProcCharacterSelect
            if (FindAnyObjectByType<UI.CharacterSelectUI>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("CharacterSelectUI");
                go.AddComponent<UI.CharacterSelectUI>();
                go.SetActive(false); // LoginUI aktif → bu inactive başlar
            }

            // CharacterCreateUI — cpp:208 — s_pProcCharacterCreate = new CGameProcCharacterCreate
            if (FindAnyObjectByType<UI.CharacterCreateUI>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("CharacterCreateUI");
                go.AddComponent<UI.CharacterCreateUI>();
                go.SetActive(false); // CharacterSelectUI'dan açılır
            }
        }

        /// <summary>
        /// Oyun durumunu değiştirir.
        /// </summary>
        public void SetState(GameState newState)
        {
            CurrentState = newState;
        }
    }

    /// <summary>
    /// Oyunun genel durum makinesi.
    /// </summary>
    public enum GameState
    {
        Login,              // Giriş ekranında
        CharacterSelect,    // Karakter seçim ekranında
        Loading,            // Harita yükleniyor
        InGame,             // Oyun içinde (aktif oynanış)
        Disconnected        // Bağlantı kesildi
    }
}

namespace EntropyOnline
{
    public static class SafeLogger
    {
        private const string LOG_SYMBOL = "FALSE";

        [System.Diagnostics.Conditional(LOG_SYMBOL)]
        public static void Log(object message)
        {
            UnityEngine.Debug.Log(message);
        }

        [System.Diagnostics.Conditional(LOG_SYMBOL)]
        public static void Log(object message, UnityEngine.Object context)
        {
            UnityEngine.Debug.Log(message, context);
        }

        [System.Diagnostics.Conditional(LOG_SYMBOL)]
        public static void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        [System.Diagnostics.Conditional(LOG_SYMBOL)]
        public static void LogWarning(object message, UnityEngine.Object context)
        {
            UnityEngine.Debug.LogWarning(message, context);
        }
    }
}
