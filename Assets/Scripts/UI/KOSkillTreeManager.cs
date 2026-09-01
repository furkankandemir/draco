// ====================================================================
// KOSkillTreeManager.cs — UISkillTreeDlg birebir port (veri/logic katmanı)
// Open-KO v1.298: UISkillTreeDlg.h/cpp
//
// Sabitler (GameDef.h:1255-1259):
//   MAX_SKILL_FROM_SERVER = 9
//   MAX_SKILL_KIND_OF     = 5  (Basic, Special0-2, Master)
//   MAX_SKILL_IN_PAGE     = 6
//   MAX_SKILL_PAGE_NUM    = 7
//
// m_iSkillInfo[9]:
//   [0] = serbest puan, [1-4] = basic, [5-8] = pro skill branch puanları
//
// m_pMySkillTree[5][7][6]:
//   [kindOf][pageNum][slotInPage] → SkillIconEntry
// ====================================================================
using System;
using UnityEngine;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UISkillTreeDlg — veri/logic katmanı.
    /// UI render kısmı SkillTreeUI.cs'te yapılır.
    /// </summary>
    public class KOSkillTreeManager
    {
        // ============================
        // SABİTLER — GameDef.h:1255-1259 birebir
        // ============================

        /// <summary>Open-KO: MAX_SKILL_FROM_SERVER (GameDef.h:1255)</summary>
        public const int MAX_SKILL_FROM_SERVER = 9;

        /// <summary>Open-KO: MAX_SKILL_KIND_OF (GameDef.h:1257) — Basic + 3 Specialty + Master</summary>
        public const int MAX_SKILL_KIND_OF = 5;

        /// <summary>Open-KO: MAX_SKILL_IN_PAGE (GameDef.h:1258) — sayfa başı ikon sayısı</summary>
        public const int MAX_SKILL_IN_PAGE = 6;

        /// <summary>Open-KO: MAX_SKILL_PAGE_NUM (GameDef.h:1259) — kategori başı max sayfa</summary>
        public const int MAX_SKILL_PAGE_NUM = 7;

        // ============================
        // STATE — UISkillTreeDlg.h:43-44 birebir
        // ============================

        /// <summary>
        /// Open-KO: m_iSkillInfo[MAX_SKILL_FROM_SERVER] — sunucudan gelen slot bilgisi.
        /// [0]=serbest puan, [1-4]=basic stat, [5]=Special0, [6]=Special1, [7]=Special2, [8]=Master
        /// UISkillTreeDlg.h:43
        /// </summary>
        public int[] SkillInfo { get; } = new int[MAX_SKILL_FROM_SERVER];

        /// <summary>
        /// Open-KO: m_pMySkillTree[MAX_SKILL_KIND_OF][MAX_SKILL_PAGE_NUM][MAX_SKILL_IN_PAGE]
        /// UISkillTreeDlg.h:44
        /// </summary>
        public SkillIconEntry[,,] SkillTree { get; } = new SkillIconEntry[MAX_SKILL_KIND_OF, MAX_SKILL_PAGE_NUM, MAX_SKILL_IN_PAGE];

        /// <summary>Open-KO: m_iCurKindOf — aktif kategori (0-4). UISkillTreeDlg.cpp:31</summary>
        public int CurKindOf { get; set; }

        /// <summary>Open-KO: m_iCurSkillPage — aktif sayfa (0-6). UISkillTreeDlg.cpp:32</summary>
        public int CurSkillPage { get; set; }

        /// <summary>Skill tree değiştiğinde tetiklenir — UI render tarafı dinler.</summary>
        public event Action OnSkillTreeChanged;

        /// <summary>Sayfa/kategori değiştiğinde tetiklenir.</summary>
        public event Action OnPageChanged;

        // Singleton
        public static KOSkillTreeManager Instance { get; private set; }

        public KOSkillTreeManager()
        {
            Instance = this;
        }

        // ============================
        // INIT — UISkillTreeDlg constructor (cpp:23-55) birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::CUISkillTreeDlg() — cpp:23-55
        /// Tüm state'i sıfırlar.
        /// </summary>
        public void Init()
        {
            // cpp:31-32
            CurKindOf = 0;
            CurSkillPage = 0;

            // cpp:41-42: m_iSkillInfo sıfırla
            for (int i = 0; i < MAX_SKILL_FROM_SERVER; i++)
                SkillInfo[i] = 0;

            // cpp:44-51: m_pMySkillTree sıfırla
            for (int i = 0; i < MAX_SKILL_KIND_OF; i++)
                for (int j = 0; j < MAX_SKILL_PAGE_NUM; j++)
                    for (int k = 0; k < MAX_SKILL_IN_PAGE; k++)
                        SkillTree[i, j, k] = null;
        }

        // ============================
        // HasIDSkill — cpp:96-111 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::HasIDSkill(int iID) — cpp:96-111
        /// Skill tree'de belirtilen ID'ye sahip skill var mı kontrol eder.
        /// </summary>
        public bool HasIDSkill(int id)
        {
            for (int i = 0; i < MAX_SKILL_KIND_OF; i++)
                for (int j = 0; j < MAX_SKILL_PAGE_NUM; j++)
                    for (int k = 0; k < MAX_SKILL_IN_PAGE; k++)
                    {
                        if (SkillTree[i, j, k] != null && SkillTree[i, j, k].SkillId == id)
                            return true;
                    }
            return false;
        }

        // ============================
        // IsSkillUsable — cpp:1712-1737 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::IsSkillUsable(pUSkill) — cpp:1712-1737
        /// iNeedSkill % 10 → hangi branch.
        /// 0=Basic (level check), 5-8=Pro (SkillInfo[5-8] check).
        /// </summary>
        public bool IsSkillUsable(KOImport.SkillEntry skill)
        {
            if (skill == null) return false;

            var gm = GameManager.Instance;
            if (gm == null) return false;

            int iModulo = skill.NeedSkill % 10;
            switch (iModulo)
            {
                case 0: // Basic Skills — cpp:1717-1718
                    return skill.NeedLevel <= gm.Level;

                case 5: // First Skill Tab — cpp:1720-1721
                    return skill.NeedLevel <= SkillInfo[5];

                case 6: // Second Skill Tab — cpp:1723-1724
                    return skill.NeedLevel <= SkillInfo[6];

                case 7: // Third Skill Tab — cpp:1726-1727
                    return skill.NeedLevel <= SkillInfo[7];

                case 8: // Master Skill Tab — cpp:1729-1730
                    return skill.NeedLevel <= SkillInfo[8];

                default:
                    break;
            }
            return false;
        }

        // ============================
        // FindSlotForSkill — cpp:1651-1674 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::FindSlotForSkill(pUSkill, offset) — cpp:1651-1674
        /// Önce mevcut skill'i bul (güncelleme), yoksa boş slot bul.
        /// </summary>
        public (int page, int slot)? FindSlotForSkill(int skillId, int categoryOffset)
        {
            // cpp:1653-1661: Mevcut skill'i ara
            for (int i = 0; i < MAX_SKILL_PAGE_NUM; i++)
                for (int j = 0; j < MAX_SKILL_IN_PAGE; j++)
                {
                    if (SkillTree[categoryOffset, i, j] != null &&
                        SkillTree[categoryOffset, i, j].SkillId == skillId)
                        return (i, j);
                }

            // cpp:1663-1671: Boş slot ara
            for (int i = 0; i < MAX_SKILL_PAGE_NUM; i++)
                for (int j = 0; j < MAX_SKILL_IN_PAGE; j++)
                {
                    if (SkillTree[categoryOffset, i, j] == null)
                        return (i, j);
                }

            // cpp:1673: nullopt
            return null;
        }

        // ============================
        // AddSkillToPage — cpp:1676-1710 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::AddSkillToPage(pUSkill, offset, hasLevel) — cpp:1676-1710
        /// Skill'i tree'ye ekler. hasLevel=true ise gerçek ikon, false ise enigma ikon.
        /// İkon dosya adı: "UI\\skillicon_{id%100:02}_{id/100}.dxt"
        /// </summary>
        public void AddSkillToPage(KOImport.SkillEntry skill, int categoryOffset = 0, bool hasLevelToUse = true)
        {
            if (skill == null) return;

            var slot = FindSlotForSkill(skill.Id, categoryOffset);
            if (!slot.HasValue) return;

            var (page, slotIdx) = slot.Value;

            // cpp:1684-1691: SkillIconEntry oluştur
            var entry = new SkillIconEntry
            {
                SkillId = skill.Id,
                Skill = skill,
                HasLevel = hasLevelToUse,
                // cpp:1689: szIconFN = fmt::format("UI\\skillicon_{:02}_{}.dxt", pUSkill->dwID % 100, pUSkill->dwID / 100)
                // cpp:1691: enigma ikon
                IconFileName = hasLevelToUse
                    ? $"skillicon_{(skill.Id % 100):D2}_{skill.Id / 100}"
                    : "skillicon_enigma"
            };

            SkillTree[categoryOffset, page, slotIdx] = entry;
        }

        // ============================
        // InitIconUpdate — cpp:1195-1304 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::InitIconUpdate() — cpp:1195-1304
        /// Tüm skill ikonlarını temizler ve yeniden oluşturur.
        /// Skill tablosundan class'a göre filtreleyerek ekler.
        /// </summary>
        public void InitIconUpdate()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // cpp:1200-1224: Mevcut ikonları temizle
            for (int i = 0; i < MAX_SKILL_KIND_OF; i++)
                for (int j = 0; j < MAX_SKILL_PAGE_NUM; j++)
                    for (int k = 0; k < MAX_SKILL_IN_PAGE; k++)
                        SkillTree[i, j, k] = null;

            // cpp:1226-1229: Skill ID range hesapla
            // iSkillIDFirst = eClass * 1000 + 1
            int classCode = gm.CharClass;
            int skillIdFirst = classCode * 1000 + 1;

            // cpp:1231-1235: İlk skill'in index'ini bul
            var allSkills = KOImport.SkillTableParser.GetSkillsForClass(classCode);
            if (allSkills == null || allSkills.Length == 0)
            {
                PageButtonInitialize();
                return;
            }

            // cpp:1251-1301: Her skill'i iNeedSkill modülosuna göre kategoriye ekle
            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                // cpp:1256-1257: if (pUSkill->dwID >= UIITEM_TYPE_USABLE_ID_MIN) continue;
                // N3UIWndBase.h:106: UIITEM_TYPE_USABLE_ID_MIN = 450000
                if (skill.Id >= 450000) continue;

                int iModulo = skill.NeedSkill % 10;
                switch (iModulo)
                {
                    case 0: // Basic Skills — cpp:1263-1268
                        if (skill.NeedLevel <= gm.Level)
                            AddSkillToPage(skill, 0, true);
                        else
                            AddSkillToPage(skill, 0, false);
                        break;

                    case 5: // First Skill Tab — cpp:1270-1275
                        if (skill.NeedLevel <= SkillInfo[5])
                            AddSkillToPage(skill, 1, true);
                        else
                            AddSkillToPage(skill, 1, false);
                        break;

                    case 6: // Second Skill Tab — cpp:1277-1282
                        if (skill.NeedLevel <= SkillInfo[6])
                            AddSkillToPage(skill, 2, true);
                        else
                            AddSkillToPage(skill, 2, false);
                        break;

                    case 7: // Third Skill Tab — cpp:1284-1289
                        if (skill.NeedLevel <= SkillInfo[7])
                            AddSkillToPage(skill, 3, true);
                        else
                            AddSkillToPage(skill, 3, false);
                        break;

                    case 8: // Master Skill Tab — cpp:1291-1296
                        if (skill.NeedLevel <= SkillInfo[8])
                        {
                            AddSkillToPage(skill, 4, true);
                        }
                        else
                        {
                            AddSkillToPage(skill, 4, false);
                        }
                        break;

                    default:
                        break;
                }
            }

            // cpp:1303
            PageButtonInitialize();
        }

        // ============================
        // PageButtonInitialize — cpp:1306-1350 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::PageButtonInitialize() — cpp:1306-1350
        /// Sayfa navigasyonunu ilk sayfa/ilk kategoriye ayarlar ve skill info değerlerini UI'a yazar.
        /// </summary>
        private void PageButtonInitialize()
        {
            // cpp:1308
            SetPageInIconRegion(0, 0);

            // cpp:1313-1315: string_skillpoint = SkillInfo[0]
            // cpp:1317-1347: string_0..string_7 = SkillInfo[1..8]
            // → UI tarafı OnSkillTreeChanged event'ini dinler

            OnSkillTreeChanged?.Invoke();
        }

        // ============================
        // SetPageInIconRegion — cpp:1828-1900 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::SetPageInIconRegion(iKindOf, iPageNum) — cpp:1828-1900
        /// Aktif kategori ve sayfayı değiştirir. Diğer kategorileri/sayfaları gizler.
        /// </summary>
        public void SetPageInIconRegion(int kindOf, int pageNum)
        {
            // cpp:1830-1831
            if (kindOf >= MAX_SKILL_KIND_OF || pageNum >= MAX_SKILL_PAGE_NUM)
                return;

            // cpp:1833-1834
            CurKindOf = kindOf;
            CurSkillPage = pageNum;

            // cpp:1836-1871: visibility kontrolü
            // → UI tarafı OnPageChanged event'ini dinleyerek render yapar

            OnPageChanged?.Invoke();
        }

        // ============================
        // PageLeft / PageRight — cpp:437-451 birebir
        // ============================

        /// <summary>Open-KO: CUISkillTreeDlg::PageLeft() — cpp:437-443</summary>
        public void PageLeft()
        {
            if (CurSkillPage == 0)
                return;
            SetPageInIconRegion(CurKindOf, CurSkillPage - 1);
        }

        /// <summary>Open-KO: CUISkillTreeDlg::PageRight() — cpp:445-451</summary>
        public void PageRight()
        {
            if (CurSkillPage == MAX_SKILL_PAGE_NUM - 1)
                return;
            SetPageInIconRegion(CurKindOf, CurSkillPage + 1);
        }

        // ============================
        // PointPushUpButton — cpp:453-762 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::PointPushUpButton(int iValue) — cpp:453-762
        /// Skill point dağıtım butonu — sunucuya C2S_SKILL_POINT_CHANGE gönderir.
        /// iValue: 1-4=basic stat (devre dışı), 5=Special0, 6=Special1, 7=Special2, 8=Master
        /// </summary>
        public void PointPushUpButton(int iValue)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // cpp:455-457: backup
            int curKindOfBackup = CurKindOf;
            int curSkillPageBackup = CurSkillPage;

            // cpp:463-466: SkillInfo[0] = serbest puan kontrolü
            int skillExtra = SkillInfo[0];

            // cpp:467-472: serbest puan yok → hata mesajı
            if (skillExtra == 0)
            {
                return;
            }

            // cpp:474-479: iValue 1-4 → basic stat — devre dışı
            if (iValue >= 1 && iValue <= 4)
            {
                return;
            }

            // cpp:481-533: Sınıf değişimi kontrolü — 1. iş sınıfı (warrior/rogue/wizard/priest) ise pro skill eklenemez
            if (iValue >= 5 && iValue <= 8)
            {
                byte charClass = gm.CharClass;
                // Karus base classes
                if (charClass == GameConstants.CLASS_KA_WARRIOR ||
                    charClass == GameConstants.CLASS_KA_ROGUE ||
                    charClass == GameConstants.CLASS_KA_WIZARD ||
                    charClass == GameConstants.CLASS_KA_PRIEST)
                {
                    return;
                }
                // Elmorad base classes
                if (charClass == GameConstants.CLASS_EL_WARRIOR ||
                    charClass == GameConstants.CLASS_EL_ROGUE ||
                    charClass == GameConstants.CLASS_EL_WIZARD ||
                    charClass == GameConstants.CLASS_EL_PRIEST)
                {
                    return;
                }
            }

            // cpp:539-584: iValue==8 (Master) — sadece 2. iş sınıfları ekleyebilir
            if (iValue == 8)
            {
                byte charClass = gm.CharClass;
                // Karus: 1. iş sınıfları master ekleyemez
                if (charClass == GameConstants.CLASS_KA_SORCERER ||
                    charClass == GameConstants.CLASS_KA_HUNTER ||
                    charClass == GameConstants.CLASS_KA_SHAMAN ||
                    charClass == GameConstants.CLASS_KA_BERSERKER)
                {
                    return;
                }
                // Elmorad: 1. iş sınıfları master ekleyemez
                if (charClass == GameConstants.CLASS_EL_MAGE ||
                    charClass == GameConstants.CLASS_EL_RANGER ||
                    charClass == GameConstants.CLASS_EL_CLERIC ||
                    charClass == GameConstants.CLASS_EL_BLADE)
                {
                    return;
                }
            }

            // cpp:693-694: skill point'i oku
            int skillPoint = SkillInfo[iValue];

            // cpp:697-702: level kontrolü — kendi level'inden fazla olamaz
            if (skillPoint >= gm.Level)
            {
                return;
            }

            // cpp:704-709: Sunucuya gönder
            // Wire: [WIZ_SKILLPT_CHANGE: byte] [iValue: byte]
            SendSkillPointChange((byte)iValue);

            // cpp:711-717: Optimistik güncelleme (sunucudan yanıt beklenmez)
            skillExtra--;
            SkillInfo[0] = skillExtra;

            skillPoint++;
            SkillInfo[iValue] = skillPoint;

            // cpp:719-759: m_iSkillInfo güncelle + 5-8 için InitIconUpdate
            if (iValue >= 5 && iValue <= 8)
            {
                InitIconUpdate();
            }

            // Senkronize: GameManager'daki SkillPoints ve SkillTreePoints güncellemesi
            gm.SkillPoints = (short)SkillInfo[0];
            if (gm.SkillTreePoints != null && iValue >= 0 && iValue < gm.SkillTreePoints.Length)
                gm.SkillTreePoints[iValue] = SkillInfo[iValue];

            // cpp:761: önceki kategori/sayfaya dön
            SetPageInIconRegion(curKindOfBackup, curSkillPageBackup);
        }

        // ============================
        // MsgRecv_SkillChange — GameProcMain.cpp:5603-5611 birebir
        // (fail rollback — sunucudan gelir)
        // ============================

        /// <summary>
        /// Open-KO: GameProcMain.cpp:5603-5611 MsgRecv_SkillChange
        /// Sunucudan fail döndüğünde çağrılır.
        /// m_iSkillInfo[iType] = iValue, m_iSkillInfo[0]++, InitIconUpdate()
        /// </summary>
        public void MsgRecv_SkillChange(byte type, byte value)
        {
            // cpp:5608
            if (type < MAX_SKILL_FROM_SERVER)
                SkillInfo[type] = value;

            // cpp:5609
            SkillInfo[0]++;

            // cpp:5610
            InitIconUpdate();

            // GameManager senkronize
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SkillPoints = (short)SkillInfo[0];
                if (gm.SkillTreePoints != null && type < gm.SkillTreePoints.Length)
                    gm.SkillTreePoints[type] = value;
            }
        }

        // ============================
        // SetSkillInfo — sunucudan ilk yüklemede çağrılır
        // ============================

        /// <summary>
        /// Sunucudan gelen SkillInfo verisiyle skill tree'yi başlatır.
        /// Open-KO: MyInfo paketinde m_bstrSkill[0..8] okunur.
        /// </summary>
        public void SetSkillInfo(int[] skillInfoFromServer)
        {
            if (skillInfoFromServer == null) return;

            int count = Math.Min(skillInfoFromServer.Length, MAX_SKILL_FROM_SERVER);
            for (int i = 0; i < count; i++)
                SkillInfo[i] = skillInfoFromServer[i];

            // GameManager senkronize — SkillPoints = SkillInfo[0] (serbest puan)
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SkillPoints = (short)SkillInfo[0];
                if (gm.SkillTreePoints != null)
                {
                    for (int i = 0; i < Math.Min(count, gm.SkillTreePoints.Length); i++)
                        gm.SkillTreePoints[i] = SkillInfo[i];
                }
            }

            InitIconUpdate();
        }

        // ============================
        // GetHighlightIconItem — cpp:1786-1796 birebir
        // ============================

        /// <summary>
        /// Open-KO: CUISkillTreeDlg::GetHighlightIconItem(pUIIcon) — cpp:1786-1796
        /// Aktif sayfa/kategorideki skill icon entry'sini bulur.
        /// </summary>
        public SkillIconEntry GetSkillAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_SKILL_IN_PAGE) return null;
            return SkillTree[CurKindOf, CurSkillPage, slotIndex];
        }

        /// <summary>
        /// Aktif sayfadaki tüm skill icon'larını döndürür (6 slot).
        /// </summary>
        public SkillIconEntry[] GetCurrentPageSkills()
        {
            var result = new SkillIconEntry[MAX_SKILL_IN_PAGE];
            for (int k = 0; k < MAX_SKILL_IN_PAGE; k++)
                result[k] = SkillTree[CurKindOf, CurSkillPage, k];
            return result;
        }

        // ============================
        // NETWORK — C2S Paket
        // ============================

        /// <summary>
        /// Open-KO birebir: UISkillTreeDlg.cpp:707-709
        /// Wire: [WIZ_SKILLPT_CHANGE] [type: byte]
        /// </summary>
        private void SendSkillPointChange(byte type)
        {
            var netMgr = Network.KO.KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_SKILLPT_CHANGE + type
            using var pkt = new Network.KO.KOPacketWriter(
                Network.KO.WizOpcode.WIZ_SKILLPT_CHANGE);
            pkt.WriteByte(type);
            netMgr.SendPacket(pkt);

        }
    }

    // ============================
    // SkillIconEntry — __IconItemSkill birebir
    // ============================

    /// <summary>
    /// Open-KO: __IconItemSkill (GameDef.h)
    /// pSkill → SkillEntry ref, pUIIcon → Unity icon, szIconFN → ikon dosya adı.
    /// </summary>
    public class SkillIconEntry
    {
        /// <summary>Open-KO: pSkill->dwID</summary>
        public int SkillId { get; set; }

        /// <summary>Open-KO: pSkill — skill tablo referansı</summary>
        public KOImport.SkillEntry Skill { get; set; }

        /// <summary>Open-KO: szIconFN — ikon dosya adı (dxt uzantısız)</summary>
        public string IconFileName { get; set; }

        /// <summary>Level yeterli mi? true=gerçek ikon, false=enigma ikon</summary>
        public bool HasLevel { get; set; }
    }
}
