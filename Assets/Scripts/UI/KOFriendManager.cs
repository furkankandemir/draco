using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIFriends — UIVarious.cpp:1037-1403
    /// 
    /// Arkadaş listesi yönetimi:
    ///   - MemberAdd: arkadaş ekle (lokal map)
    ///   - MemberDelete: arkadaş sil (lokal map)
    ///   - MsgSend_MemberInfo: sunucuya durumları sor (FRIEND_REPORT)
    ///   - MsgSend_FriendAdd: sunucuya arkadaş ekle (FRIEND_ADD)
    ///   - MsgSend_FriendRemove: sunucuya arkadaş sil (FRIEND_REMOVE)
    ///   - MsgRecv handlers: sunucudan gelen yanıtları işle
    /// 
    /// C++ m_MapFriends → Dictionary _friends
    /// C++ MAX_FRIEND_COUNT = 24 (FriendHandler.cpp:29)
    /// </summary>
    public class KOFriendManager : MonoBehaviour
    {
        public static KOFriendManager Instance { get; private set; }

        // Open-KO birebir: FriendHandler.cpp:29
        private const int MAX_FRIEND_COUNT = 24;
        private const int MAX_ID_SIZE = 20;

        // Open-KO birebir: sub-opcodes — FriendHandler.cpp packets.h:520-526
        private const byte FRIEND_REQUEST = 1;
        private const byte FRIEND_REPORT  = 2;
        private const byte FRIEND_ADD     = 3;
        private const byte FRIEND_REMOVE  = 4;

        // Open-KO birebir: FriendAddResult — DBAgent.cpp:880-900
        private const byte FRIEND_ADD_SUCCESS = 0;
        private const byte FRIEND_ADD_ALREADY = 1;
        private const byte FRIEND_ADD_FULL    = 2;

        // Open-KO birebir: m_MapFriends — UIVarious.h:49
        private readonly Dictionary<string, FriendInfo> _friends = new Dictionary<string, FriendInfo>();

        // Open-KO birebir: 3 saniye spam koruması — UIVarious.cpp:1326-1329
        private float _lastRefreshTime = 0f;
        private const float REFRESH_INTERVAL = 3.0f;

        /// <summary>Arkadaş listesi değiştiğinde tetiklenir (UI güncelleme için).</summary>
        public event Action OnFriendListChanged;

        /// <summary>Arkadaş ekleme sonucu — UI'ye bildirim için.</summary>
        public event Action<byte, string> OnAddResult; // result, name

        /// <summary>Arkadaş silme sonucu — UI'ye bildirim için.</summary>
        public event Action<byte, string> OnRemoveResult; // result, name

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnFriendProcess += HandleFriendProcess_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnFriendProcess -= HandleFriendProcess_KO;
        }

        /// <summary>KO wrapper — WIZ_FRIEND_PROCESS</summary>
        private void HandleFriendProcess_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte sub = r.ReadByte();

            switch (sub)
            {
                case FRIEND_REQUEST:
                case FRIEND_REPORT:
                {
                    // TBTKO birebir: FriendHandler.cpp:54-74 — FriendReport S2C
                    // Wire: [count:uint16][count × {name:string, sid:int16, status:byte}]
                    // status: 0=offline, 1=online, 3=online+party (cpp:84-92)
                    short count = r.ReadInt16();
                    var friends = new FriendInfoData[count];
                    for (int i = 0; i < count; i++)
                    {
                        string name = r.ReadKOString();
                        short sid = r.ReadInt16();
                        byte status = r.ReadByte(); // cpp:71 — tek byte! (0/1/3)
                        friends[i] = new FriendInfoData
                        {
                            Name    = name,
                            Sid     = sid,
                            OnLine  = (status & 0x01) != 0,  // bit 0 = online
                            IsParty = (status & 0x02) != 0   // bit 1 = party
                        };
                    }
                    HandleFriendReport(friends);
                    break;
                }
                case FRIEND_ADD:
                {
                    // Wire: [result:byte][name:string2][sid:int16][status:byte]
                    byte result = r.ReadByte();
                    string name = r.ReadKOString();
                    short sid   = r.ReadInt16();
                    byte status = r.ReadByte();
                    HandleFriendAddResult(result, name, sid, status);
                    break;
                }
                case FRIEND_REMOVE:
                {
                    // Wire: [result:byte][name:string2]
                    byte result = r.ReadByte();
                    string name = r.ReadKOString();
                    HandleFriendRemoveResult(result, name);
                    break;
                }
                default:
                {
                    Debug.LogWarning($"[FRIEND] Unknown sub-opcode: 0x{sub:X2}");
                    break;
                }
            }
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Arkadaş listesini al (salt okunur kopya).
        /// </summary>
        public IReadOnlyDictionary<string, FriendInfo> GetFriends() => _friends;

        /// <summary>
        /// Arkadaş sayısı.
        /// </summary>
        public int Count => _friends.Count;

        /// <summary>
        /// Open-KO birebir: MemberAdd — UIVarious.cpp:1245-1260
        /// Lokal listeye ekle (sunucuya henüz göndermez).
        /// </summary>
        public bool MemberAdd(string name, short sid, bool onLine, bool isParty)
        {
            // UIVarious.cpp:1247-1250
            if (string.IsNullOrEmpty(name))
                return false;
            if (_friends.ContainsKey(name))
                return false;

            _friends[name] = new FriendInfo
            {
                Name = name,
                Sid = sid,
                OnLine = onLine,
                IsParty = isParty
            };
            return true;
        }

        /// <summary>
        /// Open-KO birebir: MemberDelete — UIVarious.cpp:1262-1271
        /// Lokal listeden sil.
        /// </summary>
        public bool MemberDelete(string name)
        {
            return _friends.Remove(name);
        }

        /// <summary>
        /// Open-KO birebir: MsgSend_MemberInfo(bool bDisableInterval) — UIVarious.cpp:1323-1356
        /// Mevcut arkadaş listesinin online durumlarını sunucudan sor.
        /// C++ wire: [WIZ_FRIEND_PROCESS] [count: short] + N × [nameLen: short] [name: string]
        /// Bizim server wire: [C2S_FRIEND_PROCESS] [FRIEND_REPORT: byte] [count: short] + N × [name: string]
        /// </summary>
        public void MsgSend_MemberInfo(bool disableInterval = false)
        {
            // UIVarious.cpp:1325-1329 — spam koruması
            float time = Time.time;
            if (disableInterval && time < _lastRefreshTime + REFRESH_INTERVAL)
                return;
            _lastRefreshTime = time;

            // UIVarious.cpp:1332-1335
            if (_friends.Count == 0)
                return;

            var names = _friends.Keys.ToArray();
            int count = names.Length;

            // Open-KO birebir: WIZ_FRIEND_PROCESS + FRIEND_REPORT
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_FRIEND_PROCESS);
            pkt.WriteByte(FRIEND_REPORT);
            pkt.WriteInt16((short)count);
            for (int i = 0; i < count; i++)
            {
                pkt.WriteString(names[i]);
            }

            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: Btn_Add handler — UIVarious.cpp:1190-1200
        /// Hedef oyuncuyu arkadaş listesine ekle.
        /// Lokal listeye ekler + sunucuya bildirir.
        /// </summary>
        public void MsgSend_FriendAdd(string targetName)
        {
            if (string.IsNullOrEmpty(targetName) || targetName.Length > MAX_ID_SIZE)
                return;

            if (_friends.Count >= MAX_FRIEND_COUNT)
            {
                Debug.LogWarning("[FRIEND] Friend list full!");
                return;
            }

            if (_friends.ContainsKey(targetName))
            {
                Debug.LogWarning($"[FRIEND] {targetName} already in friend list");
                return;
            }

            // Open-KO birebir: WIZ_FRIEND_PROCESS + FRIEND_ADD
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_FRIEND_PROCESS);
            pkt.WriteByte(FRIEND_ADD);
            pkt.WriteString(targetName);

            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>
        /// Open-KO birebir: Btn_Delete handler — UIVarious.cpp:1202-1213
        /// Arkadaşı listeden sil + sunucuya bildir.
        /// </summary>
        public void MsgSend_FriendRemove(string targetName)
        {
            if (string.IsNullOrEmpty(targetName) || targetName.Length > MAX_ID_SIZE)
                return;

            // Open-KO birebir: WIZ_FRIEND_PROCESS + FRIEND_REMOVE
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_FRIEND_PROCESS);
            pkt.WriteByte(FRIEND_REMOVE);
            pkt.WriteString(targetName);

            KONetworkManager.Instance?.SendPacket(pkt);
        }

        // ========================================
        // PACKET HANDLERS (MsgRecv)
        // ========================================

        /// <summary>
        /// Open-KO birebir: MsgRecv_MemberInfo — UIVarious.cpp:1377-1403
        /// Sunucudan gelen arkadaş durumlarını güncelle.
        /// </summary>
        private void HandleFriendReport(FriendInfoData[] friends)
        {
            foreach (var fi in friends)
            {
                // UIVarious.cpp:1393-1400 birebir
                if (_friends.TryGetValue(fi.Name, out var existing))
                {
                    existing.Sid = fi.Sid;
                    existing.OnLine = fi.OnLine;
                    existing.IsParty = fi.IsParty;
                }
                else
                {
                    // Server'dan dönen ama lokal listede olmayan — ekle
                    _friends[fi.Name] = new FriendInfo
                    {
                        Name = fi.Name,
                        Sid = fi.Sid,
                        OnLine = fi.OnLine,
                        IsParty = fi.IsParty
                    };
                }
            }

            // UIVarious.cpp:1402 — this->UpdateList()
            OnFriendListChanged?.Invoke();
        }

        /// <summary>
        /// Open-KO birebir: RecvFriendModify(FRIEND_ADD) — FriendHandler.cpp:95-112
        /// </summary>
        private void HandleFriendAddResult(byte result, string name, short sid, byte status)
        {
            if (result == FRIEND_ADD_SUCCESS)
            {
                bool added = MemberAdd(name, sid, (status & 0x01) != 0, (status & 0x02) != 0);
                OnFriendListChanged?.Invoke();
            }

            OnAddResult?.Invoke(result, name);
        }

        /// <summary>
        /// Open-KO birebir: RecvFriendModify(FRIEND_REMOVE) — FriendHandler.cpp:95-112
        /// </summary>
        private void HandleFriendRemoveResult(byte result, string name)
        {
            if (result == 0) // FRIEND_REMOVE_SUCCESS
            {
                MemberDelete(name);
                OnFriendListChanged?.Invoke();
            }

            OnRemoveResult?.Invoke(result, name);
        }

        public void MsgSend_FriendRequest()
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_FRIEND_PROCESS);
            pkt.WriteByte(FRIEND_REQUEST);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        // ========================================
        // DATA
        // ========================================

        /// <summary>
        /// Open-KO birebir: __FriendsInfo — UIVarious.h:37-43
        /// </summary>
        public class FriendInfo
        {
            public string Name;     // UIVarious.h:39 — szName
            public short Sid;       // UIVarious.h:40 — iID (-1 = offline)
            public bool OnLine;    // UIVarious.h:41 — bOnLine
            public bool IsParty;   // UIVarious.h:42 — bIsParty
        }
    }
}
