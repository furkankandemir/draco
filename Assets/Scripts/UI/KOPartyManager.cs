using System.Collections.Generic;
using UnityEngine;
using EntropyOnline.Network;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIPartyOrForce (UIPartyOrForce.h/cpp)
    /// 
    /// Parti üyelerini yönetir. MemberAdd/Remove/Destroy ve
    /// HP/MP/Level/Class/Status değişikliklerini işler.
    /// 
    /// MAX_PARTY_SIZE = 8 (globals.h:39)
    /// __InfoPartyOrForce struct = PartyMemberData (GameDef.h:574-606)
    /// e_PartyStatus: PARTY_STATUS_DOWN_HP=1, PARTY_STATUS_DOWN_ETC=2 (GameDef.h:608-612)
    /// </summary>
    public class KOPartyManager : MonoBehaviour
    {
        public static KOPartyManager Instance { get; private set; }

        public long LeaderTargetId { get; set; } = 0;
        public long LeaderId { get; set; } = -1;

        // Open-KO birebir: globals.h:39
        public const int MAX_PARTY_SIZE = 8;

        // Open-KO birebir: GameDef.h:608-612
        public const byte PARTY_STATUS_DOWN_HP  = 1;
        public const byte PARTY_STATUS_DOWN_ETC = 2;

        // Open-KO birebir: std::list<__InfoPartyOrForce> m_Members (UIPartyOrForce.h:26)
        private readonly List<PartyMemberData> _members = new();

        // Open-KO birebir: int m_iIndexSelected (UIPartyOrForce.h:27)
        private int _indexSelected = -1;

        /// <summary>Open-KO birebir: MemberCount() (UIPartyOrForce.h:40-43)</summary>
        public int MemberCount => _members.Count;

        /// <summary>Seçili üye indeksi. Open-KO: m_iIndexSelected</summary>
        public int IndexSelected => _indexSelected;

        /// <summary>Üye listesinin salt okunur kopyası.</summary>
        public IReadOnlyList<PartyMemberData> Members => _members;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==============================================================
        // Tick — Open-KO birebir: UIPartyOrForce.cpp:456-522
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::Tick()
        /// UIPartyOrForce.cpp:456-522
        ///
        /// Blink mantığı: GetTickCount64() / 1000 % 2 == 1 → bBlink = true
        /// Her frame'de üyelerin SufferDown_HP / SufferDown_Etc durumuna göre
        /// bar visibility'lerini hesaplar ve UI'a bildirir.
        ///
        /// NOT: C++ 519-520 satırı m_pProgress_MP'yi HER ZAMAN visible yapar
        /// (tüm koşullar sonrasında override eder) — birebir port edildi.
        /// </summary>
        private void Update()
        {
            if (_members.Count == 0) return;

            // Open-KO birebir: cpp:460-467
            // uint64_t dwTime = GetTickCount64();
            // dwTime = dwTime / 1000;
            // dwTime %= 2;
            // if (dwTime == 1) bBlink = true;
            bool bBlink = ((long)(Time.unscaledTime) % 2) == 1;

            for (int i = 0; i < _members.Count && i < MAX_PARTY_SIZE; i++)
            {
                var pIP = _members[i];

                // cpp:473-479: HP bar visibility
                bool hpBarVisible;
                if (pIP.SufferDownHP || pIP.SufferDownEtc)
                    hpBarVisible = false; // cpp:476
                else
                    hpBarVisible = true;  // cpp:478

                // cpp:481-517: HPReduce ve MP bar visibility
                bool hpReduceVisible;
                bool mpBarVisible;

                if (pIP.SufferDownHP && pIP.SufferDownEtc)
                {
                    // cpp:481-498: İkisi de true → bBlink'e göre toggle
                    if (bBlink)
                    {
                        hpReduceVisible = true;   // cpp:486
                        mpBarVisible = false;      // cpp:489
                    }
                    else
                    {
                        hpReduceVisible = false;   // cpp:494
                        mpBarVisible = true;        // cpp:497
                    }
                }
                else
                {
                    // cpp:500-517: Sadece biri true veya hiçbiri
                    hpReduceVisible = pIP.SufferDownHP;  // cpp:504-507
                    mpBarVisible = pIP.SufferDownEtc;     // cpp:512-515
                }

                // cpp:519-520: SON SATIR OVERRIDE — MP bar HER ZAMAN visible!
                // if (m_pProgress_MP[i] != nullptr) m_pProgress_MP[i]->SetVisible(true);
                mpBarVisible = true;

                // UI'a bildir — blink state değişikliği
                pIP.BlinkHpBarVisible = hpBarVisible;
                pIP.BlinkHpReduceVisible = hpReduceVisible;
                pIP.BlinkMpBarVisible = mpBarVisible;
            }
        }

        // ==============================================================
        // MemberAdd — Open-KO birebir: UIPartyOrForce.cpp:250-271
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberAdd()
        /// UIPartyOrForce.cpp:250-271
        /// 
        /// Yeni üyeyi listeye ekler ve MemberInfoReInit() çağırır.
        /// </summary>
        public PartyMemberData MemberAdd(
            long iID, string szID, int iLevel, byte eClass,
            int iHP, int iHPMax, int iMP, int iMPMax)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    _members[i].Name = szID;
                    _members[i].Level = (short)iLevel;
                    _members[i].Class = eClass;
                    _members[i].CurrentHp = iHP;
                    _members[i].MaxHp = iHPMax;
                    _members[i].CurrentMp = iMP;
                    _members[i].MaxMp = iMPMax;

                    MemberInfoReInit();
                    return _members[i];
                }
            }

            // Lider kimliği belirsizse, eklenen İLK üye lider kabul edilir (sunucu önce lideri gönderir).
            if (LeaderId == -1 && _members.Count == 0)
            {
                LeaderId = iID;
            }

            var info = new PartyMemberData
            {
                CharacterId  = iID,
                Name         = szID,
                Level        = (short)iLevel,
                Class        = eClass,
                CurrentHp    = iHP,
                MaxHp        = iHPMax,
                CurrentMp    = iMP,
                MaxMp        = iMPMax,
                SufferDownHP  = false,
                SufferDownEtc = false
            };

            _members.Add(info);

            MemberInfoReInit();

            return info;
        }

        // ==============================================================
        // MemberRemove — Open-KO birebir: UIPartyOrForce.cpp:273-290
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberRemove()
        /// UIPartyOrForce.cpp:273-290
        /// 
        /// ID'ye göre üyeyi listeden çıkarır ve MemberInfoReInit() çağırır.
        /// </summary>
        public bool MemberRemove(long iID)
        {
            if (_members.Count == 0)
                return false;

            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    _members.RemoveAt(i);
                    MemberInfoReInit();
                    return true;
                }
            }

            return false;
        }

        // ==============================================================
        // MemberDestroy — Open-KO birebir: UIPartyOrForce.cpp:292-319
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberDestroy()
        /// UIPartyOrForce.cpp:292-319
        /// 
        /// Tüm üyeleri siler ve UI'ı gizler.
        /// </summary>
        public void MemberDestroy()
        {
            // Open-KO birebir: UIPartyOrForce.cpp:292-319
            // C++ MemberDestroy() sadece m_Members.clear() + UI gizleme + MemberInfoReInit().
            // m_iIndexSelected sıfırlanmaz — sadece Release() ve constructor'da sıfırlanır.
            _members.Clear();
            LeaderId = -1;
            MemberInfoReInit();
        }

        // ==============================================================
        // MemberHPChange — Open-KO birebir: UIPartyOrForce.cpp:374-406
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberHPChange()
        /// UIPartyOrForce.cpp:374-406
        /// 
        /// ID'ye göre üyenin HP/MP değerlerini günceller.
        /// </summary>
        public void MemberHPChange(long iID, int iHP, int iHPMax, int iMP, int iMPMax)
        {
            for (int i = 0; i < _members.Count && i < MAX_PARTY_SIZE; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    _members[i].CurrentHp = iHP;
                    _members[i].MaxHp     = iHPMax;
                    _members[i].CurrentMp = iMP;
                    _members[i].MaxMp     = iMPMax;
                    break;
                }
            }
        }

        /// <summary>
        /// Yerel oyuncunun HP/MP değerleri değiştikçe parti listesindeki slotunu günceller.
        /// </summary>
        public void UpdateLocalPlayerStats(int curHp, int maxHp, int curMp, int maxMp)
        {
            if (EntropyOnline.Core.GameManager.Instance == null) return;
            long myId = EntropyOnline.Core.GameManager.Instance.CharacterId;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].CharacterId == myId)
                {
                    _members[i].CurrentHp = curHp;
                    _members[i].MaxHp     = maxHp;
                    _members[i].CurrentMp = curMp;
                    _members[i].MaxMp     = maxMp;
                    
                    // UI'ı tetikle
                    if (KOUIManager.Instance != null)
                    {
                        long leaderId = LeaderId;
                        if (leaderId == -1) leaderId = myId;
                        KOUIManager.Instance.PopulatePartyList(leaderId, _members.ToArray());
                    }
                    break;
                }
            }
        }

        // ==============================================================
        // MemberStatusChange — Open-KO birebir: UIPartyOrForce.cpp:408-424
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberStatusChange()
        /// UIPartyOrForce.cpp:408-424
        /// 
        /// Üyenin status flag'lerini günceller (HP debuff / curse).
        /// </summary>
        public void MemberStatusChange(long iID, byte ePS, bool bSuffer)
        {
            for (int i = 0; i < _members.Count && i < MAX_PARTY_SIZE; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    // Open-KO birebir: GameDef.h:610-611
                    if (ePS == PARTY_STATUS_DOWN_HP)
                        _members[i].SufferDownHP = bSuffer;
                    else if (ePS == PARTY_STATUS_DOWN_ETC)
                        _members[i].SufferDownEtc = bSuffer;
                    break;
                }
            }
        }

        // ==============================================================
        // MemberLevelChange — Open-KO birebir: UIPartyOrForce.cpp:426-439
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberLevelChange()
        /// UIPartyOrForce.cpp:426-439
        /// </summary>
        public void MemberLevelChange(long iID, int iLevel)
        {
            for (int i = 0; i < _members.Count && i < MAX_PARTY_SIZE; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    _members[i].Level = (short)iLevel;
                    break;
                }
            }
        }

        // ==============================================================
        // MemberClassChange — Open-KO birebir: UIPartyOrForce.cpp:441-454
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberClassChange()
        /// UIPartyOrForce.cpp:441-454
        /// </summary>
        public void MemberClassChange(long iID, byte eClass)
        {
            for (int i = 0; i < _members.Count && i < MAX_PARTY_SIZE; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    _members[i].Class = eClass;
                    break;
                }
            }
        }




        // ==============================================================
        // MemberInfoGetByID — Open-KO birebir: UIPartyOrForce.cpp:194-211
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberInfoGetByID()
        /// UIPartyOrForce.cpp:194-211
        /// 
        /// ID'ye göre üye bilgisini döndürür. iIndexResult çıkış parametresi.
        /// </summary>
        public PartyMemberData MemberInfoGetByID(long iID, out int iIndexResult)
        {
            iIndexResult = -1;

            if (_members.Count == 0)
                return null;

            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].CharacterId == iID)
                {
                    iIndexResult = i;
                    return _members[i];
                }
            }

            return null;
        }

        // ==============================================================
        // MemberInfoGetByIndex — Open-KO birebir: UIPartyOrForce.cpp:213-222
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberInfoGetByIndex()
        /// UIPartyOrForce.cpp:213-222
        /// </summary>
        public PartyMemberData MemberInfoGetByIndex(int iIndex)
        {
            if (iIndex < 0 || iIndex >= _members.Count)
                return null;

            return _members[iIndex];
        }

        // ==============================================================
        // MemberInfoGetSelected — Open-KO birebir: UIPartyOrForce.cpp:363-372
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberInfoGetSelected()
        /// UIPartyOrForce.cpp:363-372
        /// </summary>
        public PartyMemberData MemberInfoGetSelected()
        {
            if (_indexSelected < 0 || _indexSelected >= _members.Count)
                return null;

            return _members[_indexSelected];
        }

        // ==============================================================
        // MemberSelect — Open-KO birebir: UIPartyOrForce.h:54-60
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberSelect()
        /// UIPartyOrForce.h:54-60
        /// </summary>
        public void MemberSelect(int iMemberIndex)
        {
            if (iMemberIndex < 0 || iMemberIndex >= _members.Count)
                return;

            _indexSelected = iMemberIndex;
        }

        // ==============================================================
        // TargetByIndex — Open-KO birebir: UIPartyOrForce.cpp:177-192
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::TargetByIndex()
        /// UIPartyOrForce.cpp:177-192
        /// 
        /// İndekse göre üyeyi seçer ve hedef olarak atar.
        /// Open-KO birebir: cpp:188-189 — TargetSelect(pIP->iID, true) çağrısı yapılır.
        /// </summary>
        public bool TargetByIndex(int iIndex)
        {
            if (iIndex < 0 || iIndex >= _members.Count)
                return false;

            _indexSelected = iIndex;

            var pIP = _members[iIndex];
            if (pIP != null)
            {
                var gm = EntropyOnline.Core.GameManager.Instance;
                var ts = EntropyOnline.World.KOTargetSelector.Instance;

                if (gm != null && pIP.CharacterId == gm.CharacterId)
                {
                    if (ts != null)
                        ts.ClearTarget(); // Kendimizi hedef alamayız, hedefi temizle
                }
                else
                {
                    if (ts != null)
                        ts.SelectTargetByID(pIP.CharacterId, true);
                }
            }

            if (KOUIManager.Instance != null)
                KOUIManager.Instance.UpdatePartySelection();

            return true;
        }

        // ==============================================================
        // MemberInfoReInit — Open-KO birebir: UIPartyOrForce.cpp:321-361
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CUIPartyOrForce::MemberInfoReInit()
        /// UIPartyOrForce.cpp:321-361
        /// 
        /// Parti üye yapısı değiştiğinde çağrılır. UI güncellemesini tetikler.
        /// Open-KO'da bu metot UI widget'larını günceller — bizde event tetikler.
        /// </summary>
        private void MemberInfoReInit()
        {
            // Open-KO birebir: UIPartyOrForce.cpp:321-361

            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null && gm.CharacterId >= 0 && _members.Count > 0)
            {
                bool hasMe = false;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].CharacterId == gm.CharacterId)
                    {
                        hasMe = true;
                        break;
                    }
                }

                if (!hasMe)
                {
                    var myInfo = new PartyMemberData
                    {
                        CharacterId = gm.CharacterId,
                        Name = gm.CharacterName,
                        Level = gm.Level,
                        Class = gm.CharClass,
                        CurrentHp = gm.CurrentHp,
                        MaxHp = gm.MaxHp,
                        CurrentMp = gm.CurrentMp,
                        MaxMp = gm.MaxMp,
                        SufferDownHP = false,
                        SufferDownEtc = false
                    };

                    // Eğer index 0'da lider varsa ve o biz değilsek, kendimizi arkaya (üye olarak) ekleriz.
                    // Aksi takdirde kendimiz liderizdir, kendimizi başa (0. indexe) ekleriz.
                    if (_members.Count > 0 && _members[0].CharacterId != gm.CharacterId)
                    {
                        _members.Add(myInfo);
                    }
                    else
                    {
                        _members.Insert(0, myInfo);
                    }
                }
            }

            // Listeyi sırala: Lider en başta (index 0) olmalı, diğerleri arkasından gelmeli
            if (_members.Count > 1)
            {
                long leaderId = LeaderId;
                if (leaderId == -1 && gm != null) leaderId = gm.CharacterId;

                PartyMemberData leader = null;
                List<PartyMemberData> others = new List<PartyMemberData>();

                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].CharacterId == leaderId)
                        leader = _members[i];
                    else
                        others.Add(_members[i]);
                }

                _members.Clear();
                if (leader != null) _members.Add(leader);
                _members.AddRange(others);
            }

            // cpp:324-331: iHPMax <= 0 güvenlik kontrolü (__ASSERT)
            for (int i = 0; i < _members.Count && i < MAX_PARTY_SIZE; i++)
            {
                if (_members[i].MaxHp <= 0)
                {
                    // Open-KO birebir: cpp:329-330
                    // __ASSERT(0, "Invalid Party member HP");
                    Debug.LogWarning($"[PARTY] Invalid Party member HP (iHPMax <= 0): {_members[i].Name}");
                }
            }

            // Open-KO birebir: cpp:357-360
            // if (m_Members.empty()) SetVisible(false); else SetVisible(true);
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowParty(_members.Count > 0);

            // UI'ya güncel veri aktar
            if (KOUIManager.Instance != null && _members.Count > 0)
            {
                long leaderId = LeaderId;
                if (leaderId == -1 && _members.Count > 0) leaderId = _members[0].CharacterId;
                KOUIManager.Instance.PopulatePartyList(leaderId, _members.ToArray());
            }
        }

        public void UpdateLeader(long newLeaderId)
        {
            LeaderId = newLeaderId;
            MemberInfoReInit();
        }
    }
}
