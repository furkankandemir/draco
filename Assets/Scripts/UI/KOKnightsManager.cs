using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIKnightsOperation (UIKnightsOperation.h/cpp)
    ///                 + GameProcMain::MsgRecv_Knights dispatch (GameProcMain.cpp:6315-6386)
    ///
    /// Klan (Knights) sistemi istemci tarafı yöneticisi.
    /// Sunucudan gelen klan paketlerini işler, yerel durumu tutar,
    /// ve UI'a event'ler aracılığıyla bildirir.
    ///
    /// Open-KO referans: globals.h:186-196 — e_KnightsDuty enum
    ///                   UIKnightsOperation.h:14-30 — __KnightsInfoBase / __KnightsInfoExt
    ///                   UIKnightsOperation.cpp — tüm dosya
    /// </summary>
    public class KOKnightsManager : MonoBehaviour
    {
        public static KOKnightsManager Instance { get; private set; }

        // ==========================================================================
        // Open-KO birebir: e_KnightsDuty (globals.h:186-196)
        // ==========================================================================
        public const byte KNIGHTS_DUTY_UNKNOWN   = 0;   // Klansız / atılmış
        public const byte KNIGHTS_DUTY_CHIEF     = 1;   // Lider
        public const byte KNIGHTS_DUTY_VICECHIEF = 2;   // Yardımcı Lider
        public const byte KNIGHTS_DUTY_PUNISH    = 3;   // Cezalı
        public const byte KNIGHTS_DUTY_TRAINEE   = 4;   // Çaylak
        public const byte KNIGHTS_DUTY_KNIGHT    = 5;   // Normal Üye
        public const byte KNIGHTS_DUTY_OFFICER   = 6;   // Subay
        public const byte KNIGHTS_DUTY_CAPTAIN   = 100; // Savaş Komutanı

        // ==========================================================================
        // Open-KO birebir: CLAN_TYPE / KNIGHTS_TYPE (GameDefine.h:97-98)
        // ==========================================================================
        public const byte CLAN_TYPE    = 0x01;  // GameDefine.h:97
        public const byte KNIGHTS_TYPE = 0x02;  // GameDefine.h:98

        // ==========================================================================
        // Local State — Open-KO: CPlayerMySelf::m_InfoBase.iKnightsID,
        //                        CPlayerMySelf::m_InfoExt.eKnightsDuty
        // ==========================================================================
        private long  _clanId;
        private string _clanName = string.Empty;
        private byte  _duty;       // e_KnightsDuty
        private byte  _grade;      // m_byGrade (1-5)
        private byte  _ranking;    // m_byRanking
        private int   _points;     // m_nPoints
        private string _leaderName = string.Empty;
        private short _memberCount;
        private short _onlineCount;   // C++ iMemberCountOnline from packet header
        private short _maxMembers;
        private short _capeId;
        private byte  _capeR = 255;
        private byte  _capeG = 255;
        private byte  _capeB = 255;

        // Open-KO birebir: m_KnightsMapBase (UIKnightsOperation.h:30)
        // Tüm bilinen klanların temel bilgilerini tutar
        private readonly Dictionary<long, KnightsInfoBase> _knightsMapBase = new();

        // Open-KO birebir: m_KnightsListExt (UIKnightsOperation.h:32)
        // Klan listesi sayfası verisi
        private readonly List<KnightsInfoExt> _knightsListExt = new();
        private int _pageCurrent; // Open-KO: m_iPageCur

        // Üye listesi (S2C_CLAN_INFO'dan gelen son veri)
        private ClanMemberEntry[] _memberList = Array.Empty<ClanMemberEntry>();

        // ==========================================================================
        // Public Properties
        // ==========================================================================
        public long   ClanId       => _clanId;
        public string ClanName     => _clanName;
        public byte   Duty         => _duty;
        public byte   Grade        => _grade;
        public int    Points       => _points;
        public string LeaderName   => _leaderName;
        public short  MemberCount  => _memberCount;
        public short  OnlineCount  => _onlineCount;
        public short  MaxMembers   => _maxMembers;
        public bool   IsInClan     => _clanId > 0;
        public bool   IsChief      => _duty == KNIGHTS_DUTY_CHIEF;
        public bool   IsViceChief  => _duty == KNIGHTS_DUTY_VICECHIEF;
        public bool   IsOfficer    => _duty == KNIGHTS_DUTY_OFFICER;
        public int    PageCurrent  => _pageCurrent;
        public IReadOnlyList<KnightsInfoExt> KnightsList => _knightsListExt;
        public IReadOnlyList<ClanMemberEntry> Members => _memberList;
        public short  CapeId       => _capeId;
        public byte   CapeR        => _capeR;
        public byte   CapeG        => _capeG;
        public byte   CapeB        => _capeB;

        // ==========================================================================
        // Events — UI abone olur
        // ==========================================================================

        /// <summary>Klan bilgisi değiştiğinde (create, join, leave, duty change, info)</summary>
        public event Action OnKnightsInfoChanged;

        /// <summary>Klan üye listesi güncellendiğinde</summary>
        public event Action<ClanMemberEntry[]> OnMemberListUpdated;

        /// <summary>Klan listesi (sayfalı) alındığında</summary>
        public event Action<int, ClanListEntry[]> OnKnightsListReceived;

        /// <summary>Klan grade toplu güncelleme geldiğinde</summary>
        public event Action<ClanGradeEntry[]> OnGradeUpdateReceived;

        /// <summary>Klan davet geldiğinde</summary>
        public event Action<long, string, string> OnInviteReceived;

        /// <summary>Klan işlem sonucu (başarı/hata mesajı)</summary>
        public event Action<bool, string> OnClanResult;

        // ==========================================================================
        // Unity Lifecycle
        // ==========================================================================
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnKnights += HandleKnights_KO;
            KOPacketHandler.OnCapeChange += HandleCapeChange_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnKnights -= HandleKnights_KO;
            KOPacketHandler.OnCapeChange -= HandleCapeChange_KO;
        }

        /// <summary>KO wrapper — WIZ_KNIGHTS_PROCESS</summary>
        private void HandleKnights_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp:6315-6386 — MsgRecv_Knights dispatch
            // Wire: [opcode][sub:byte][...]
            var r = new KOPacketReader(rawData);
            byte sub = r.ReadByte();

            switch (sub)
            {
                case 0x01: // N3_SP_KNIGHTS_CREATE — cpp:GameProcMain.cpp:6858-6932
                {
                    // C++ birebir: MsgRecv_Knights_Create
                    // Wire: [sub:0x01][result:byte][...]
                    byte result = r.ReadByte();

                    switch (result)
                    {
                        case 0x01: // N3_SP_KNIGHTS_CREATE_SUCCESS (cpp:6864-6907)
                        {
                            short sid = r.ReadInt16();            // cpp:6866 — player ID
                            short knightsId = r.ReadInt16();      // cpp:6868 — klan ID
                            string knightsName = r.ReadKOString(); // cpp:6869-6870 — klan adı
                            byte grade = r.ReadByte();            // cpp:6871 — klan grade
                            byte rank = r.ReadByte();             // cpp:6872 — klan ranking
                            uint gold = r.ReadUInt32();           // cpp:6873 — kalan altın

                            var gm = EntropyOnline.Core.GameManager.Instance;
                            if (gm != null && sid == (short)gm.CharacterId)
                            {
                                // C++ birebir: cpp:6876-6900 — kendi karakterimiz
                                // cpp:6889: s_pPlayer->m_InfoExt.eKnightsDuty = KNIGHTS_DUTY_CHIEF
                                _duty = KNIGHTS_DUTY_CHIEF;
                                // cpp:6890: s_pPlayer->KnightsInfoSet(iID, szID, iGrade, iRank)
                                _clanId = knightsId;
                                _clanName = knightsName;
                                _grade = grade;
                                _ranking = rank;
                                // cpp:6880: s_pPlayer->m_InfoExt.iGold = dwGold
                                gm.Gold = gold;
                                // GameManager'ı da güncelle — nameplate ve diğer UI'lar bunu okur
                                gm.ClanName = knightsName;

                                OnKnightsInfoChanged?.Invoke();

                                // cpp:6895: MsgSend_MemberInfoAll() + ChangeUIByDuty()
                                MsgSend_MemberInfoAll();
                            }

                            HandleClanResult(0x01, true, "Knights created successfully!");
                            break;
                        }
                        case 0x02: // N3_SP_KNIGHTS_CREATE_FAIL_LOWLEVEL
                            HandleClanResult(0x01, false, "Your level is too low to create a Knights.");
                            break;
                        case 0x03: // N3_SP_KNIGHTS_CREATE_FAIL_DUPLICATEDNAME
                            HandleClanResult(0x01, false, "That Knights name already exists. Please choose another.");
                            break;
                        case 0x04: // N3_SP_KNIGHTS_CREATE_FAIL_LOWMONEY
                            HandleClanResult(0x01, false, "You don't have enough coins to create a Knights.");
                            break;
                        case 0x05: // N3_SP_KNIGHTS_CREATE_FAIL_ALREADYJOINED
                            HandleClanResult(0x01, false, "You are already a member of a Knights.");
                            break;
                        case 0x06: // N3_SP_KNIGHTS_CREATE_FAIL_UNKNOWN
                        case 0x00: // N3_SP_KNIGHTS_CREATE_FAIL_DBFAIL
                            HandleClanResult(0x01, false, "Failed to create Knights. Unknown error.");
                            break;
                        case 0x07: // N3_SP_KNIGHTS_CREATE_FAIL_INVALIDDAY
                            HandleClanResult(0x01, false, "You cannot create a Knights at this time.");
                            break;
                        case 0x08: // N3_SP_KNIGHTS_CREATE_FAIL_INVALIDSERVER
                            HandleClanResult(0x01, false, "Knights cannot be created on this server.");
                            break;
                        default:
                            HandleClanResult(0x01, false, $"Knights creation failed (code: {result}).");
                            break;
                    }
                    break;
                }
                case 0x02: // N3_SP_KNIGHTS_JOIN — cpp:GameProcMain.cpp:7011-7043
                {
                    byte result = r.ReadByte();
                    if (result == 0x01 && r.Remaining >= 4)
                    {
                        short sid = r.ReadInt16();            // cpp:7013 — player ID
                        short knightsId = r.ReadInt16();      // cpp:7014 — klan ID
                        byte fame = r.ReadByte();             // cpp:7015 — duty
                        string knightsName = r.ReadKOString(); // cpp:7016-7018 — klan adı
                        byte grade = r.ReadByte();            // cpp:7019 — grade
                        byte ranking = r.ReadByte();          // cpp:7020 — rank

                        var gm = EntropyOnline.Core.GameManager.Instance;
                        if (gm != null && sid == (short)gm.CharacterId)
                        {
                            // C++ birebir: cpp:7023-7036 — kendi karakterimiz
                            _clanId = knightsId;
                            _clanName = knightsName;
                            _grade = grade;
                            _ranking = ranking;
                            _duty = fame;
                            gm.ClanName = knightsName;

                            OnKnightsInfoChanged?.Invoke();
                        }

                        // C++ birebir: cpp:7039-7041 — CPlayerOther* pUPC = s_pOPMgr->UPCGetByID(sid, true); pUPC->KnightsInfoSet(iID, szKnightsName, iGrade, iRank);
                        if (EntropyOnline.World.EntityManager.Instance != null)
                        {
                            EntropyOnline.World.EntityManager.Instance.UpdatePlayerKnightsInfo(sid, knightsId, fame, knightsName, grade, ranking);
                        }
                    }
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x03: // N3_SP_KNIGHTS_WITHDRAW
                case 0x04: // N3_SP_KNIGHTS_MEMBER_REMOVE
                {
                    byte result = r.ReadByte();
                    if (result == 0x01 && r.Remaining >= 5)
                    {
                        short sid = r.ReadInt16();
                        short knightsId = r.ReadInt16();
                        byte fame = r.ReadByte();

                        var gm = EntropyOnline.Core.GameManager.Instance;
                        if (gm != null && sid == (short)gm.CharacterId)
                        {
                            ClearKnightsState();
                            HandleClanResult(sub, true, "");
                        }
                        else
                        {
                            if (EntropyOnline.World.EntityManager.Instance != null)
                            {
                                EntropyOnline.World.EntityManager.Instance.UpdatePlayerKnightsInfo(sid, 0, 0, "", 5, 0);
                            }
                            MsgSend_MemberInfoAll();
                        }
                    }
                    else
                    {
                        HandleClanResult(sub, result == 0x01, "");
                    }
                    break;
                }
                case 0x06: // N3_SP_KNIGHTS_MEMBER_JOIN_ADMIT
                case 0x09: // N3_SP_KNIGHTS_APPOINT_CHIEF
                {
                    byte result = r.ReadByte();
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x10: // N3_SP_KNIGHTS_DUTY_CHANGE
                {
                    byte result = r.ReadByte();
                    if (result == 0x01)
                    {
                        short sid = r.ReadInt16();
                        short knightsId = r.ReadInt16();
                        byte duty = r.ReadByte();

                        var gm = EntropyOnline.Core.GameManager.Instance;
                        if (gm != null && sid == (short)gm.CharacterId)
                        {
                            _clanId = knightsId;
                            _duty = duty;
                            if (knightsId == 0)
                            {
                                ClearKnightsState();
                            }
                            else
                            {
                                OnKnightsInfoChanged?.Invoke();
                            }
                        }
                        else
                        {
                            var em = World.EntityManager.Instance;
                            if (em != null)
                            {
                                em.UpdateRemotePlayerClan(sid, knightsId, duty);
                            }
                        }
                    }
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x05: // N3_SP_KNIGHTS_DESTROY — cpp:6349-6373
                {
                    // C++ birebir: sadece [result:byte] — string YOK
                    byte result = r.ReadByte();
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x07: // N3_SP_KNIGHTS_MEMBER_JOIN_REJECT — cpp:PacketDef.h:108
                {
                    byte result = r.ReadByte();
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x0A: // N3_SP_KNIGHTS_APPOINT_VICECHIEF — cpp:6337
                {
                    byte result = r.ReadByte();
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x0B: // N3_SP_KNIGHTS_APPOINT_OFFICER — cpp:PacketDef.h:112
                {
                    byte result = r.ReadByte();
                    HandleClanResult(sub, result == 0x01, "");
                    break;
                }
                case 0x0C: // N3_SP_KNIGHTS_GRADE_CHANGE_ALL — cpp:PacketDef.h:113
                {
                    // Wire: [count:int16][count × {clanId:int16, grade:byte, ranking:byte}]
                    short count = r.ReadInt16();
                    var entries = new ClanGradeEntry[count];
                    for (int i = 0; i < count; i++)
                    {
                        entries[i] = new ClanGradeEntry
                        {
                            ClanId  = r.ReadInt16(), // C++ int16_t
                            Grade   = r.ReadByte(),
                            Ranking = r.ReadByte()
                        };
                    }
                    HandleClanGradeUpdate(entries);
                    break;
                }
                case 0x0E: // N3_SP_KNIGHTS_MEMBER_INFO_ONLINE (KNIGHTS_CURRENT_REQ) — cpp:KnightsManager.cpp:789
                {
                    // Wire: [result:byte] then if success:
                    // [chiefName:string][page:int16][count:int16]
                    // count × [name:string][duty:byte][level:byte][class:int16]
                    byte result = r.ReadByte();
                    if (result != 0x01)
                    {
                        string errMsg = r.ReadKOString();
                        break;
                    }

                    string chiefName = r.ReadKOString();
                    short page = r.ReadInt16();
                    short count = r.ReadInt16();

                    if (count < 0 || count > 256)
                    {
                        Debug.LogError($"[KNIGHTS] CurrentMember: count={count} out of range, aborting parse");
                        break;
                    }

                    var onlineMembers = new ClanMemberEntry[count];
                    for (int i = 0; i < count; i++)
                    {
                        string name = r.ReadKOString();
                        byte rank = r.ReadByte();
                        byte level = r.ReadByte();
                        short charClass = r.ReadInt16();

                        onlineMembers[i] = new ClanMemberEntry
                        {
                            Name      = name,
                            Rank      = rank,
                            Level     = level,
                            CharClass = (byte)charClass,
                            IsOnline  = true // Bu liste zaten sadece online üyeler
                        };
                    }

                    // Online üye listesini güncelle
                    _onlineCount = count;
                    var sortedOnline = SortMemberList(onlineMembers);
                    _memberList = sortedOnline;
                    OnKnightsInfoChanged?.Invoke();
                    OnMemberListUpdated?.Invoke(sortedOnline);
                    break;
                }
                case 0x0D: // N3_SP_KNIGHTS_MEMBER_INFO_ALL — cpp:PacketDef.h:114
                {
                    // Wire (actual server format verified from hex dump):
                    // [result:byte][packetSize:int16][onlineCount:int16][totalCount:int16][listCount:int16]
                    // then listCount × [nameLen:int16][name:string][duty:byte][level:byte][class:int16][connected:byte]
                    // NOT: Sunucu sub-opcode'dan sonra 1 byte result gönderiyor.
                    // C++ client bunu dispatch katmanında okuyor, biz burada okuyoruz.
                    byte result = r.ReadByte();            // server result (0x01=success)
                    short packetSize = r.ReadInt16();       // cpp:916 — unused
                    short onlineCount = r.ReadInt16();      // cpp:917
                    short totalCount = r.ReadInt16();       // cpp:918
                    short listCount = r.ReadInt16();        // cpp:920


                    // Sanity check — listCount > 256 ise paket parsing hatası
                    if (listCount < 0 || listCount > 256)
                    {
                        Debug.LogError($"[KNIGHTS] MemberInfoAll: listCount={listCount} out of range, aborting parse");
                        break;
                    }

                    var members = new ClanMemberEntry[listCount];
                    for (int i = 0; i < listCount; i++)
                    {
                        string name = r.ReadKOString();     // cpp:926-928 — int16 len + string
                        byte rank = r.ReadByte();           // cpp:933 — duty (e_KnightsDuty)
                        byte level = r.ReadByte();          // cpp:934 — level
                        short charClass = r.ReadInt16();    // cpp:935 — class (e_Class)
                        byte connected = r.ReadByte();      // cpp:936 — connected

                        members[i] = new ClanMemberEntry
                        {
                            Name      = name,
                            Rank      = rank,
                            Level     = level,
                            CharClass = (byte)charClass,
                            IsOnline  = connected != 0
                        };
                    }
                    _memberCount = listCount;
                    _onlineCount = onlineCount;
                    var sortedAll = SortMemberList(members);
                    _memberList = sortedAll;
                    OnKnightsInfoChanged?.Invoke();
                    OnMemberListUpdated?.Invoke(sortedAll);
                    break;
                }
                case 0x11: // N3_SP_KNIGHTS_JOIN_REQ — cpp:6379 — davet geldi
                {
                    // C++ birebir: GameProcMain.cpp:MsgRecv_Knigts_Join_Req
                    // Wire: [result:byte] then if success: [requesterId:int16][clanId:int16][nameLen:int16][name:string]
                    byte result = r.ReadByte();
                    if (result == 0x01)
                    {
                        short requesterId = r.ReadInt16();
                        short clanId = r.ReadInt16();
                        string clanName = r.ReadKOString();
                        HandleClanInviteIncoming(requesterId, "", clanName, clanId);
                    }
                    break;
                }
                default:
                {
                    Debug.LogWarning($"[KNIGHTS] Unknown sub-opcode: 0x{sub:X2}");
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==========================================================================
        // Packet Handlers — Open-KO birebir dispatch
        // ==========================================================================

        /// <summary>
        /// Open-KO birebir: S2C_CLAN_INFO handler
        /// ClanProcessor.cs:612-634 wire:
        ///   [clanId: long] [clanName: string] [leaderName: string] [level: short]
        ///   [memberCount: short] [maxMembers: short] [grade: byte] [points: int]
        ///   [memberListCount: byte] [memberListCount × {charId, name, level, class, rank, isOnline}]
        /// </summary>
        private void HandleClanInfo(long clanId, string clanName, string leaderName, short level,
            short memberCount, short maxMembers, byte grade, int points, ClanMemberEntry[] members)
        {
            _clanId      = clanId;
            _clanName    = clanName;
            _leaderName  = leaderName;
            _memberCount = memberCount;
            _maxMembers  = maxMembers;
            _grade       = grade;
            _points      = points;
            _memberList  = members ?? Array.Empty<ClanMemberEntry>();

            // Duty'yi üye listesinden belirle — kendi karakterimizi bul
            var myName = EntropyOnline.Core.GameManager.Instance?.CharacterName;
            foreach (var m in _memberList)
            {
                if (m != null && !string.IsNullOrEmpty(m.Name) && m.Name.Equals(myName, System.StringComparison.OrdinalIgnoreCase))
                {
                    _duty = (byte)m.Rank;
                    break;
                }
            }

            OnKnightsInfoChanged?.Invoke();
            OnMemberListUpdated?.Invoke(_memberList);
        }

        /// <summary>
        /// Open-KO birebir: CUIKnights::MemberListSort() (UIVarious.cpp:856-895)
        /// CHIEF (Leader / Rank 1) ve VICECHIEF (Assistants / Rank 2) her zaman listenin en üstüne sıralanır.
        /// </summary>
        public static ClanMemberEntry[] SortMemberList(ClanMemberEntry[] list)
        {
            if (list == null || list.Length <= 1) return list ?? Array.Empty<ClanMemberEntry>();

            // C++ birebir: UIVarious.cpp:856-895 — MemberListSort()
            return list.OrderBy(m => m.Rank == 1 ? 0 : (m.Rank == 2 ? 1 : 2))
                       .ThenByDescending(m => m.IsOnline)
                       .ThenBy(m => m.Name)
                       .ToArray();
        }

        /// <summary>
        /// Open-KO birebir: S2C_CLAN_INVITE_INCOMING handler
        /// UIKnightsOperation.cpp:102-105 — MsgSend_KnightsJoin() → davet geldi
        /// </summary>
        private void HandleClanInviteIncoming(long inviterId, string inviterName, string clanName, int clanId = 0)
        {
            OnInviteReceived?.Invoke(inviterId, inviterName, clanName);

            // Open-KO birebir: GameProcMain.cpp:7001-7043 — MessageBoxPost(IDS_CLAN_JOIN_REQUEST, MB_YESNO, BEHAVIOR_KNIGHTS_JOIN)
            string displayName = !string.IsNullOrEmpty(clanName) ? clanName : (!string.IsNullOrEmpty(inviterName) ? inviterName : "Knights");
            string prompt = $"Do you want to join '{displayName}' Knights?";

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.AddMsgOutput($"You have been invited to join '{displayName}'.", KOUIManager.D3DColorToUnity(0xffffff00));
            }

            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    prompt,
                    "",
                    MsgBoxBehavior.BEHAVIOR_CLAN_JOIN,
                    onYes: () =>
                    {
                        MsgSend_KnightsJoinReq(true, (int)inviterId, clanId);
                    },
                    onNo: () =>
                    {
                        MsgSend_KnightsJoinReq(false, (int)inviterId, clanId);
                    }
                );
            }
            else
            {
                Debug.LogWarning("[KNIGHTS] ⚠️ KOMessageBox.Instance NULL — diyalog penceresi gösterilemedi!");
            }
        }

        /// <summary>
        /// Open-KO birebir: S2C_CLAN_RESULT handler
        /// subOpcode = e_SubPacket_Knights (PacketDef.h:100-119)
        ///
        /// State transition kararları subOpcode'a göre yapılır:
        ///   WITHDRAW (0x03) / DESTROY (0x05) / MEMBER_REMOVE (0x04) → ClearKnightsState()
        ///   CREATE (0x01) / JOIN (0x02) / CHIEF (0x09) / DUTY_CHANGE (0x10) → MsgSend_MemberInfoAll()
        ///   GENERAL (0xFF) / diğer → sadece mesaj göster
        /// </summary>
        private void HandleClanResult(byte subOpcode, bool success, string message)
        {
            if (success)
            {
                // Open-KO birebir: subOpcode'a göre state transition
                switch (subOpcode)
                {
                    // Klandan çıkaran operasyonlar → state sıfırla
                    case 0x03: // N3_SP_KNIGHTS_WITHDRAW
                    case 0x05: // N3_SP_KNIGHTS_DESTROY
                    case 0x04: // N3_SP_KNIGHTS_MEMBER_REMOVE
                        ClearKnightsState();
                        break;

                    // Klana giren / rank değişen operasyonlar → bilgi yenile
                    case 0x01: // N3_SP_KNIGHTS_CREATE
                    case 0x02: // N3_SP_KNIGHTS_JOIN
                    case 0x09: // N3_SP_KNIGHTS_CHIEF
                    case 0x10: // N3_SP_KNIGHTS_DUTY_CHANGE
                    case 0x06: // N3_SP_KNIGHTS_ADMIT
                        MsgSend_MemberInfoAll();
                        break;

                    // Genel mesajlar — state değişikliği yok
                    default:
                        break;
                }
            }

            OnClanResult?.Invoke(success, message);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:6389-6430 — MsgRecv_KnightsListBasic
        /// + UIKnightsOperation.cpp:219-243 — MsgRecv_KnightsList
        ///
        /// Klan listesi sayfasını alır ve UI'a bildirir.
        /// </summary>
        private void HandleClanList(short page, ClanListEntry[] clans)
        {
            _pageCurrent = page;

            // Open-KO birebir: KnightsListClear() + KnightsListAdd() + KnightsListUpdate()
            _knightsListExt.Clear();
            if (clans != null)
            {
                foreach (var c in clans)
                {
                    _knightsListExt.Add(new KnightsInfoExt
                    {
                        ClanId       = c.ClanId,
                        Name         = c.Name,
                        LeaderName   = c.LeaderName,
                        MemberCount  = c.MemberCount,
                        Points       = c.Points
                        // NOT: Grade bu pakette yok — GradeChangeAll'dan gelir
                    });
                }
            }

            OnKnightsListReceived?.Invoke(page, clans);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:7297-7334 — MsgRecv_Knights_GradeChangeAll
        ///
        /// Klanların grade değişikliklerini alır, yerel cache günceller.
        /// </summary>
        private void HandleClanGradeUpdate(ClanGradeEntry[] entries)
        {
            if (entries == null) return;

            // Open-KO birebir: cpp:7325-7332 — her klan için grade+ranking güncelle
            foreach (var e in entries)
            {
                // Kendi klanımızın grade/ranking'i değiştiyse güncelle
                // Open-KO birebir: cpp:7329 — KnightsInfoSet(iIDTmp, szKnights, iGrades[i], iRanks[i])
                if (e.ClanId == _clanId)
                {
                    _grade = e.Grade;
                    _ranking = e.Ranking;
                    OnKnightsInfoChanged?.Invoke();
                }

                // Genel map güncelle
                if (_knightsMapBase.TryGetValue(e.ClanId, out var info))
                {
                    info.Grade = e.Grade;
                }
            }

            OnGradeUpdateReceived?.Invoke(entries);
        }

        // ==========================================================================
        // State Management
        // ==========================================================================

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:6369-6371 — Destroy sonrası state temizleme
        ///   s_pPlayer->m_InfoExt.eKnightsDuty = KNIGHTS_DUTY_UNKNOWN;
        ///   s_pPlayer->KnightsInfoSet(0, "", 0, 0);
        /// </summary>
        private void ClearKnightsState()
        {
            _clanId      = 0;
            _clanName    = string.Empty;
            _duty        = KNIGHTS_DUTY_UNKNOWN;
            _grade       = 0;
            _ranking     = 0;
            _points      = 0;
            _leaderName  = string.Empty;
            _memberCount = 0;
            _maxMembers  = 0;
            _memberList  = Array.Empty<ClanMemberEntry>();

            OnKnightsInfoChanged?.Invoke();
            OnMemberListUpdated?.Invoke(_memberList);
        }

        /// <summary>
        /// Login sonrası klan bilgisini ayarlar (MyInfo paketinden).
        /// </summary>
        public void SetInitialState(long clanId, string clanName, byte duty, short capeId)
        {
            _clanId   = clanId;
            _clanName = clanName;
            _duty     = duty;
            _capeId   = capeId;

            if (clanId > 0)
            {
                OnKnightsInfoChanged?.Invoke();
            }
        }

        // ==========================================================================
        // KnightsInfoBase Map — Open-KO birebir: UIKnightsOperation.cpp:121-143
        // ==========================================================================

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::KnightsInfoInsert()
        /// UIKnightsOperation.cpp:128-135
        /// </summary>
        public void KnightsInfoInsert(long clanId, string name)
        {
            _knightsMapBase[clanId] = new KnightsInfoBase { ClanId = clanId, Name = name };
        }

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::KnightsInfoDelete()
        /// UIKnightsOperation.cpp:121-126
        /// </summary>
        public void KnightsInfoDelete(long clanId)
        {
            _knightsMapBase.Remove(clanId);
        }

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::KnightsInfoFind()
        /// UIKnightsOperation.cpp:137-143
        /// </summary>
        public KnightsInfoBase KnightsInfoFind(long clanId)
        {
            _knightsMapBase.TryGetValue(clanId, out var info);
            return info;
        }

        // ==========================================================================
        // ChangeUIByDuty — Open-KO birebir: UIKnightsOperation.cpp:179-199
        // ==========================================================================

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::ChangeUIByDuty()
        /// UIKnightsOperation.cpp:179-199
        ///
        /// SADECE 2 branch:
        ///   Chief → Destroy:NORMAL, Withdraw:DISABLE, Join:DISABLE
        ///   else  → Destroy:DISABLE, Withdraw:NORMAL, Join:NORMAL
        /// 
        /// NOT: Open-KO'da "klansız" ayrı branch yok.
        ///      Üye olan birisi Join'e basabilir — sunucu zaten reject eder.
        /// </summary>
        public (bool canDestroy, bool canWithdraw, bool canJoin) GetUIPermissions()
        {
            // Open-KO birebir: cpp:181-198 — sadece CHIEF vs else
            if (_duty == KNIGHTS_DUTY_CHIEF)
            {
                // cpp:183-188: Destroy=NORMAL, Withdraw=DISABLE, Join=DISABLE
                return (canDestroy: true, canWithdraw: false, canJoin: false);
            }
            else
            {
                // cpp:192-197: Destroy=DISABLE, Withdraw=NORMAL, Join=NORMAL
                return (canDestroy: false, canWithdraw: true, canJoin: true);
            }
        }

        // ==========================================================================
        // Send Helpers — Open-KO birebir: UIKnightsOperation.cpp MsgSend_* methods
        // ==========================================================================

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:245-267 — MsgSend_KnightsCreate()
        /// Wire: C2S_CLAN_CREATE [clanName: string]
        /// </summary>
        public void MsgSend_KnightsCreate(string clanName)
        {
            if (string.IsNullOrEmpty(clanName))
            {
                Debug.LogWarning("[KNIGHTS] Klan adı boş olamaz");
                return;
            }

            var netMgr = KONetworkManager.Instance;
            if (netMgr == null)
            {
                Debug.LogError("[KNIGHTS] ❌ KONetworkManager.Instance NULL — paket gönderilemiyor!");
                return;
            }
            if (!netMgr.IsConnected)
            {
                Debug.LogError("[KNIGHTS] ❌ Sunucuya bağlı değil — paket gönderilemiyor!");
                return;
            }

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x01); // N3_SP_KNIGHTS_CREATE
            pkt.WriteString(clanName);

            byte[] payload = pkt.GetPayload();
            string hex = System.BitConverter.ToString(payload);

            netMgr.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:269-278 — MsgSend_KnightsDestroy()
        /// Wire: C2S_CLAN_DISBAND (parametresiz)
        /// </summary>
        public void MsgSend_KnightsDestroy()
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x05); // N3_SP_KNIGHTS_DESTROY
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:303-312 — MsgSend_KnightsWithdraw()
        /// Wire: C2S_CLAN_LEAVE (parametresiz)
        /// </summary>
        public void MsgSend_KnightsWithdraw()
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x03); // N3_SP_KNIGHTS_WITHDRAW
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1792-1802 — MsgSend_KnightsJoin(int iTargetID)
        /// Hedeflenen oyuncuyu klana alma talebi.
        /// Wire: WIZ_KNIGHTS_PROCESS + N3_SP_KNIGHTS_JOIN(0x02) + int16(targetID)
        /// </summary>
        public void MsgSend_KnightsJoin(int targetID)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x02); // N3_SP_KNIGHTS_JOIN
            pkt.WriteInt16((short)targetID);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1778-1790 — MsgSend_KnightsJoinReq(bool bJoin)
        /// Klan davet kabul/red cevabı.
        /// Wire: WIZ_KNIGHTS_PROCESS + N3_SP_KNIGHTS_JOIN_REQ(0x11) + byte(bJoin) + int16(requierID) + int16(clanID)
        /// </summary>
        public void MsgSend_KnightsJoinReq(bool accept, int requierID, int clanID)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x11); // N3_SP_KNIGHTS_JOIN_REQ
            pkt.WriteByte((byte)(accept ? 1 : 0));
            pkt.WriteInt16((short)requierID);
            pkt.WriteInt16((short)clanID);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: UIVarious.cpp:750-758 — AdmitButtonHandler()
        /// Admit = Hedeflenen oyuncuyu klana almak. MsgSend_KnightsJoin(s_pPlayer->m_iIDTarget) çağırır.
        /// </summary>
        public void MsgSend_KnightsAdmit(int targetID)
        {
            MsgSend_KnightsJoin(targetID);
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:314-327 — MsgSend_KnightsList()
        /// Wire: C2S_CLAN_LIST_REQUEST [page: short]
        /// </summary>
        public void MsgSend_KnightsList(int page)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x0B); // N3_SP_KNIGHTS_LIST
            pkt.WriteInt16((short)page);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:6895 — MsgSend_MemberInfoAll()
        /// Kendi klanının tüm üye bilgisini ister.
        /// Wire: C2S_CLAN_MEMBER_LIST (parametresiz)
        /// </summary>
        public void MsgSend_MemberInfoAll()
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x0D); // N3_SP_KNIGHTS_MEMBER_INFO_ALL (0x0D, NOT 0x0C!)
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1804-1814 — MsgSend_KnightsLeave(string name)
        /// Üyeyi klandan çıkar (kick/remove). Listeden seçilen üye ismiyle.
        /// Wire: WIZ_KNIGHTS_PROCESS + N3_SP_KNIGHTS_MEMBER_REMOVE(0x04) + int16(nameLen) + string(name)
        /// </summary>
        public void MsgSend_KnightsLeave(string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return;
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x04); // N3_SP_KNIGHTS_MEMBER_REMOVE
            pkt.WriteString(memberName);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1826-1836 — MsgSend_KnightsAppointViceChief(string name)
        /// Üyeyi Vice Chief olarak ata. Listeden seçilen üye ismiyle.
        /// Wire: WIZ_KNIGHTS_PROCESS + N3_SP_KNIGHTS_APPOINT_VICECHIEF(0x0A) + int16(nameLen) + string(name)
        /// </summary>
        public void MsgSend_KnightsAppointViceChief(string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return;
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x0A); // N3_SP_KNIGHTS_APPOINT_VICECHIEF
            pkt.WriteString(memberName);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Üyeyi Chief (Lider) olarak ata / klanı devret.
        /// Wire: WIZ_KNIGHTS_PROCESS + N3_SP_KNIGHTS_APPOINT_CHIEF(0x09) + int16(nameLen) + string(name)
        /// </summary>
        public void MsgSend_KnightsAppointChief(string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return;
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(0x09); // N3_SP_KNIGHTS_APPOINT_CHIEF
            pkt.WriteString(memberName);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain.cpp:1571-1588 — MsgSend_ChatSelectTarget(string targetID)
        /// Whisper hedefini ayarlar (private mesaj gönderilecek kişi).
        /// </summary>
        public void MsgSend_ChatSelectTarget(string targetName)
        {
            KONetworkManager.Instance?.SendChatSelectTarget(targetName);
        }

        private void HandleCapeChange_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            
            // WIZ_CAPE: 0x70
            // Success: [uint16 1][allianceID: int16][clanID: int16][capeID: int16][r: byte][g: byte][b: byte][0: byte]
            // Error: [errorCode: int16]
            
            short code = r.ReadInt16();
            if (code <= 0)
            {
                Debug.LogError($"[KNIGHTS] Cape change failed: error code = {code}");
                OnClanResult?.Invoke(false, $"Cape change failed. Error: {code}");
                return;
            }

            short allianceId = r.ReadInt16();
            short clanId = r.ReadInt16();
            short capeId = r.ReadInt16();
            byte capeR = r.ReadByte();
            byte capeG = r.ReadByte();
            byte capeB = r.ReadByte();
            byte unused = r.ReadByte();

            if (clanId == _clanId)
            {
                _capeId = capeId;
                _capeR = capeR;
                _capeG = capeG;
                _capeB = capeB;

                var gm = EntropyOnline.Core.GameManager.Instance;
                if (gm != null)
                {
                    gm.CapeId = capeId;
                }

                OnKnightsInfoChanged?.Invoke();
            }

            var em = World.EntityManager.Instance;
            if (em != null)
            {
                em.UpdateRemotePlayersCapeByClan(clanId, capeId, capeR, capeG, capeB);
            }

            OnClanResult?.Invoke(true, "Cape changed successfully.");
        }

        public void MsgSend_KnightsCapeChange(short capeId, byte r, byte g, byte b)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_CAPE);
            pkt.WriteInt16(capeId);
            pkt.WriteByte(r);
            pkt.WriteByte(g);
            pkt.WriteByte(b);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        // ==========================================================================
        // Helpers
        // ==========================================================================

        /// <summary>Yerel oyuncunun karakter ID'sini döndürür.</summary>
        private long GetMyCharacterId()
        {
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
                return gm.CharacterId;
            return 0;
        }

        /// <summary>Duty ismi (Türkçe)</summary>
        public static string GetDutyName(byte duty) => duty switch
        {
            KNIGHTS_DUTY_UNKNOWN   => "Klansız",
            KNIGHTS_DUTY_CHIEF     => "Lider",
            KNIGHTS_DUTY_VICECHIEF => "Yrd. Lider",
            KNIGHTS_DUTY_PUNISH    => "Cezalı",
            KNIGHTS_DUTY_TRAINEE   => "Çaylak",
            KNIGHTS_DUTY_KNIGHT    => "Üye",
            KNIGHTS_DUTY_OFFICER   => "Subay",
            KNIGHTS_DUTY_CAPTAIN   => "Komutan",
            _ => "?"
        };
    }

    // ==========================================================================
    // Data Structures — Open-KO birebir
    // ==========================================================================

    /// <summary>
    /// Open-KO birebir: __KnightsInfoBase (UIKnightsOperation.h:14-18)
    /// Klanın temel bilgisi — ID + isim.
    /// </summary>
    public class KnightsInfoBase
    {
        public long ClanId;
        public string Name = string.Empty;
        public byte Grade;
    }

    /// <summary>
    /// Open-KO birebir: __KnightsInfoExt (UIKnightsOperation.h:20-28)
    /// Klan listesi detay bilgisi — lider ismi, üye sayısı, puan.
    /// </summary>
    public class KnightsInfoExt
    {
        public long ClanId;
        public string Name = string.Empty;
        public string LeaderName = string.Empty;
        public short MemberCount;
        public int Points;
        public byte Grade;
    }
}
