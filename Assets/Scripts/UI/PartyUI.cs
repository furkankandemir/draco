using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: MsgRecv_PartyOrForce (GameProcMain.cpp:5151-5306)
    /// + MsgSend_PartyOrForcePermit (GameProcMain.cpp:1633-1643)
    /// + MsgSend_PartyOrForceCreate (GameProcMain.cpp:1645-1679)
    /// + MsgSend_PartyOrForceLeave (GameProcMain.cpp:1681-1716)
    /// + PartyOrForceConditionGet (GameProcMain.cpp:6127-6145)
    /// 
    /// Sunucudan gelen parti paketlerini KOPartyManager'a yönlendirir.
    /// Open-KO'daki CGameProcMain party handler'ının birebir karşılığı.
    /// </summary>
    public class PartyUI : MonoBehaviour
    {
        public static PartyUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnParty += HandleParty_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnParty -= HandleParty_KO;
        }

        /// <summary>KO wrapper — WIZ_PARTY</summary>
        private void HandleParty_KO(byte[] rawData)
        {
            // C++ birebir: GameProcMain.cpp:5151-5306 — MsgRecv_PartyOrForce dispatch
            // Wire: [opcode][sub:byte][...]
            var r = new KOPacketReader(rawData);
            byte sub = r.ReadByte();

            switch (sub)
            {
                case 0x02: // N3_SP_PARTY_OR_FORCE_PERMIT — invite
                {
                    // Wire: [id:int16][nameLen:int16][name:bytes]
                    short id = r.ReadInt16();
                    string name = r.ReadKOString();
                    HandlePartyInvite(id, name);
                    break;
                }
                case 0x03: // N3_SP_PARTY_OR_FORCE_INSERT
                {
                    // C++ birebir: GameProcMain.cpp:5175-5189
                    // ONE member per INSERT packet, NOT batch
                    // Wire: [idOrError:int16] then if positive:
                    //   [partyPosition:byte][nameLen:int16][name:string]
                    //   [hpMax:int16][hp:int16][level:byte][class:int16]
                    //   [mpMax:int16][mp:int16][nation:byte]
                    short idOrError = r.ReadInt16();

                    if (idOrError >= 0)
                    {
                        string name = r.ReadKOString();
                        short maxHp = r.ReadInt16();
                        short hp = r.ReadInt16();
                        byte level = r.ReadByte();
                        short charClass = r.ReadInt16();
                        short maxMp = r.ReadInt16();
                        short mp = r.ReadInt16();
                        byte nation = r.ReadByte(); // unused

                        if (KOPartyManager.Instance != null)
                        {
                            KOPartyManager.Instance.MemberAdd(
                                idOrError, name, level, (byte)charClass,
                                hp, maxHp, mp, maxMp);
                        }

                        // Open-KO birebir: cpp:5221 — UpdateUI_PartyOrForceButtons()
                        if (KOUIManager.Instance != null)
                        {
                            KOUIManager.Instance.ShowParty(true);
                            string msg = name + EntropyOnline.Services.StringTableService.Get(3405); // " has joined the party."
                            KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffff00));
                        }
                    }
                    else
                    {
                        // Error codes: -1=rejected, -2=level diff, -3=invalid nation (GameProcMain.cpp:5204-5216)
                        Debug.LogWarning($"[PARTY] INSERT error: {idOrError}");
                        if (KOUIManager.Instance != null)
                        {
                            string msg = idOrError switch
                            {
                                -1 => EntropyOnline.Services.StringTableService.Get(3406), // "The invitation to the party has been declined."
                                -2 => EntropyOnline.Services.StringTableService.Get(3408), // "You cannot form a party because of the Level difference."
                                -3 => EntropyOnline.Services.StringTableService.Get(3407), // "You cannot form a party with a user from the other nation."
                                _  => EntropyOnline.Services.StringTableService.Get(3406)
                            };
                            KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffffff));
                        }
                        if (KOPartyManager.Instance != null && KOPartyManager.Instance.MemberCount == 1)
                            KOPartyManager.Instance.MemberDestroy();
                    }
                    break;
                }
                case 0x04: // N3_SP_PARTY_OR_FORCE_REMOVE
                {
                    // Wire: [id:int16] — member removed
                    short removedId = r.ReadInt16();
                    // Refresh party by re-request or remove from manager
                    if (KOPartyManager.Instance != null)
                    {
                        var member = KOPartyManager.Instance.MemberInfoGetByID(removedId, out _);
                        string memberName = (member != null) ? member.Name : "";

                        KOPartyManager.Instance.MemberRemove(removedId);

                        if (KOUIManager.Instance != null)
                        {
                            long myCharId = GameManager.Instance != null ? GameManager.Instance.CharacterId : 0;
                            if (removedId == myCharId)
                            {
                                string msg = EntropyOnline.Services.StringTableService.Get(3414); // "You've quit the party."
                                KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffffff));
                            }
                            else if (!string.IsNullOrEmpty(memberName))
                            {
                                string msg = memberName + " left the party."; // Open-KO standard
                                KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffffff));
                            }
                        }
                    }
                    break;
                }
                case 0x05: // N3_SP_PARTY_OR_FORCE_DESTROY
                {
                    HandlePartyUpdate(0, null);
                    break;
                }
                case 0x06: // N3_SP_PARTY_OR_FORCE_HP_CHANGE
                {
                    // Wire: [id:int16][maxHp:int16][hp:int16][maxMp:int16][mp:int16]
                    short id = r.ReadInt16();
                    short maxHp = r.ReadInt16();
                    short hp = r.ReadInt16();
                    short maxMp = r.ReadInt16();
                    short mp = r.ReadInt16();
                    var updates = new[] { new PartyHpData
                    {
                        CharacterId = id,
                        CurrentHp = hp,
                        MaxHp = maxHp,
                        CurrentMp = mp,
                        MaxMp = maxMp
                    }};
                    HandlePartyHpUpdate(updates);
                    break;
                }
                case 0x07: // N3_SP_PARTY_OR_FORCE_LEVEL_CHANGE
                {
                    // Wire: [id:int16][level:byte]
                    short id = r.ReadInt16();
                    byte level = r.ReadByte();
                    HandlePartyLevelChange(id, level);
                    break;
                }
                case 0x08: // N3_SP_PARTY_OR_FORCE_CLASS_CHANGE
                {
                    // Wire: [id:int16][class:int16]
                    short id = r.ReadInt16();
                    short charClass = r.ReadInt16();
                    HandlePartyClassChange(id, charClass);
                    break;
                }
                case 0x09: // N3_SP_PARTY_OR_FORCE_STATUS_CHANGE
                {
                    // Wire: [id:int16][statusType:byte][bSuffer:byte]
                    short id = r.ReadInt16();
                    byte statusType = r.ReadByte();
                    bool bSuffer = r.ReadByte() != 0;
                    HandlePartyStatusChange(id, statusType, bSuffer);
                    break;
                }
                case 0x1C: // PARTY_PROMOTE
                {
                    short newLeaderId = r.ReadInt16();
                    if (KOPartyManager.Instance != null)
                    {
                        KOPartyManager.Instance.UpdateLeader(newLeaderId);
                    }
                    break;
                }
                default:
                {
                    Debug.LogWarning($"[PARTY] Unknown sub-opcode: 0x{sub:X2}");
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ==============================================================
        // HandlePartyUpdate — Open-KO birebir:
        // N3_SP_PARTY_OR_FORCE_INSERT (GameProcMain.cpp:5173-5222)
        // N3_SP_PARTY_OR_FORCE_REMOVE (GameProcMain.cpp:5225-5249)
        // N3_SP_PARTY_OR_FORCE_DESTROY (GameProcMain.cpp:5251-5260)
        //
        // Sunucu toplu güncelleme gönderiyor — üye listesini sıfırdan oluştur.
        // Open-KO'da her sub-command ayrı gelirdi, bizde sunucu toplu gönderir.
        // ==============================================================

        /// <summary>
        /// S2C_PARTY_UPDATE — Sunucu parti kompozisyonu değiştiğinde toplu gönderir.
        /// Open-KO birebir: MsgRecv_PartyOrForce INSERT/REMOVE/DESTROY sub-command'larının karşılığı.
        /// GameProcMain.cpp:5173-5260
        /// </summary>
        private void HandlePartyUpdate(long leaderId, PartyMemberData[] members)
        {
            if (KOPartyManager.Instance == null) return;

            // Open-KO birebir: N3_SP_PARTY_OR_FORCE_DESTROY (cpp:5251-5260)
            // Üye yoksa veya null ise → MemberDestroy()
            if (members == null || members.Length == 0)
            {
                // Open-KO birebir: m_pUIPartyOrForce->MemberDestroy();
                KOPartyManager.Instance.MemberDestroy();

                // Open-KO birebir: cpp:5255-5256
                if (KOUIManager.Instance != null)
                {
                    string msg = EntropyOnline.Services.StringTableService.Get(3404); // "The party has been disbanded."
                    KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffffff));
                }
                return;
            }

            // Mevcut listeyi temizle ve yeniden oluştur
            // Open-KO'da her INSERT ayrı gelir, bizde sunucu toplu gönderir
            KOPartyManager.Instance.MemberDestroy();
            KOPartyManager.Instance.LeaderId = leaderId;

            // Open-KO birebir: N3_SP_PARTY_OR_FORCE_INSERT (cpp:5173-5197)
            // m_pUIPartyOrForce->MemberAdd(iIDorErrorCode, szID, iLevel, eClass, iHP, iHPMax, iMP, iMPMax);
            foreach (var member in members)
            {
                KOPartyManager.Instance.MemberAdd(
                    member.CharacterId,
                    member.Name,
                    member.Level,
                    member.Class,
                    member.CurrentHp,
                    member.MaxHp,
                    member.CurrentMp,
                    member.MaxMp
                );
            }

            // Open-KO birebir: cpp:5221 — UpdateUI_PartyOrForceButtons()
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowParty(true);

        }

        // ==============================================================
        // HandlePartyHpUpdate — Open-KO birebir:
        // N3_SP_PARTY_OR_FORCE_HP_CHANGE (GameProcMain.cpp:5262-5272)
        // ==============================================================

        /// <summary>
        /// S2C_PARTY_HP_UPDATE — HP/MP değişikliği.
        /// Open-KO birebir: MsgRecv_PartyOrForce HP_CHANGE sub-command.
        /// GameProcMain.cpp:5262-5272
        /// 
        /// int iID    = pkt.read<int16_t>();
        /// int iHPMax = pkt.read<int16_t>();
        /// int iHP    = pkt.read<int16_t>();
        /// int iMPMax = pkt.read<int16_t>();
        /// int iMP    = pkt.read<int16_t>();
        /// m_pUIPartyOrForce->MemberHPChange(iID, iHP, iHPMax, iMP, iMPMax);
        /// </summary>
        private void HandlePartyHpUpdate(PartyHpData[] updates)
        {
            if (KOPartyManager.Instance == null) return;

            foreach (var update in updates)
            {
                // Open-KO birebir: cpp:5270
                // m_pUIPartyOrForce->MemberHPChange(iID, iHP, iHPMax, iMP, iMPMax);
                KOPartyManager.Instance.MemberHPChange(
                    update.CharacterId,
                    update.CurrentHp,
                    update.MaxHp,
                    update.CurrentMp,
                    update.MaxMp
                );
            }

            // UI güncelle
            if (KOUIManager.Instance != null && KOPartyManager.Instance.MemberCount > 0)
            {
                KOUIManager.Instance.UpdatePartyMemberHP(updates);
            }
        }

        // ==============================================================
        // HandlePartyInvite — Open-KO birebir:
        // N3_SP_PARTY_OR_FORCE_PERMIT (GameProcMain.cpp:5158-5171)
        // ==============================================================

        /// <summary>
        /// S2C_PARTY_INVITE — Parti daveti geldi.
        /// Open-KO birebir: MsgRecv_PartyOrForce PERMIT sub-command.
        /// GameProcMain.cpp:5158-5171
        /// 
        /// int iID     = pkt.read<int16_t>();
        /// int iStrLen = pkt.read<int16_t>();
        /// std::string szID; pkt.readString(szID, iStrLen);
        /// if (iID >= 0) {
        ///     std::string szMsg = fmt::format_text_resource(IDS_PARTY_PERMIT);
        ///     MessageBoxPost(szID + szMsg, "", MB_YESNO, BEHAVIOR_PARTY_PERMIT);
        /// }
        /// </summary>
        private void HandlePartyInvite(long inviterId, string inviterName)
        {

            if (KOPartyManager.Instance != null)
            {
                KOPartyManager.Instance.LeaderId = inviterId;
            }

            // Auto-decline if Block Party Requests option is active
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.Block_PartyRequests)
            {
                SendPartyResponse(inviterId, false);
                if (KOUIManager.Instance != null)
                {
                    string msg = Services.StringTableService.Get(3409); // "Parti davetiyesi reddedildi."
                    KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffff00));
                }
                return;
            }

            // Open-KO birebir: cpp:5165-5169
            // if (iID >= 0) { MessageBoxPost(szID + szMsg, "", MB_YESNO, BEHAVIOR_PARTY_PERMIT); }
            if (inviterId >= 0 && KOUIManager.Instance != null)
            {
                // Open-KO birebir: IDS_PARTY_PERMIT = "님이 파티에 참여하겠습니까?"
                KOUIManager.Instance.ShowPartyInviteDialog(inviterId, inviterName);
            }
        }

        // ==============================================================
        // MsgSend_PartyOrForcePermit — Open-KO birebir:
        // GameProcMain.cpp:1633-1643
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CGameProcMain::MsgSend_PartyOrForcePermit()
        /// GameProcMain.cpp:1633-1643
        /// 
        /// CAPISocket::MP_AddByte(byBuff, iOffset, WIZ_PARTY);
        /// CAPISocket::MP_AddByte(byBuff, iOffset, N3_SP_PARTY_OR_FORCE_PERMIT);
        /// CAPISocket::MP_AddByte(byBuff, iOffset, bYesNo);
        /// </summary>
        public void SendPartyResponse(long inviterId, bool accept)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_PARTY + N3_SP_PARTY_OR_FORCE_PERMIT
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY);
            pkt.WriteByte(0x02); // N3_SP_PARTY_OR_FORCE_PERMIT
            pkt.WriteByte(accept ? (byte)1 : (byte)0);
            netMgr.SendPacket(pkt);

        }

        // ==============================================================
        // MsgSend_PartyOrForceCreate — Open-KO birebir:
        // GameProcMain.cpp:1645-1679
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CGameProcMain::MsgSend_PartyOrForceCreate()
        /// GameProcMain.cpp:1645-1679
        /// 
        /// Hedef oyuncuya parti daveti gönderir.
        /// Open-KO: bIAmMember && !bIAmLeader → return false
        /// Üye yoksa CREATE, 2+ ise INSERT gönderilir.
        /// </summary>
        public void SendPartyInvite(string targetName)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null || string.IsNullOrEmpty(targetName)) return;

            bool bIAmLeader = false;
            bool bIAmMember = false;
            if (GameManager.Instance != null)
            {
                PartyOrForceConditionGet(
                    GameManager.Instance.CharacterId, out bIAmLeader, out bIAmMember);
            }

            if (bIAmMember && !bIAmLeader)
            {
                Debug.LogWarning("[PARTY] Sadece parti lideri davet gönderebilir.");
                return;
            }

            byte subOpcode = bIAmMember ? (byte)0x03 : (byte)0x01; // 0x03=INSERT, 0x01=CREATE

            if (subOpcode == 0x01 && KOPartyManager.Instance != null && GameManager.Instance != null)
            {
                KOPartyManager.Instance.LeaderId = GameManager.Instance.CharacterId;
            }

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY);
            pkt.WriteByte(subOpcode);
            pkt.WriteKOString(targetName);
            netMgr.SendPacket(pkt);

            if (KOUIManager.Instance != null)
            {
                string msg = EntropyOnline.Services.StringTableService.Get(3411); // "Player was invited into the party. Waiting for a response."
                KOUIManager.Instance.AddMsgOutput(msg, KOUIManager.D3DColorToUnity(0xffffff00));
            }

        }

        public void SendPartyInvite(long targetCharId)
        {
            var em = EntropyOnline.World.EntityManager.Instance;
            if (em != null)
            {
                var targetPlayer = em.GetRemotePlayer(targetCharId);
                if (targetPlayer != null && !string.IsNullOrEmpty(targetPlayer.Name))
                {
                    SendPartyInvite(targetPlayer.Name);
                    return;
                }
            }
            Debug.LogWarning($"[PARTY] Oyuncu ID {targetCharId} ismi bulunamadığı için davet gönderilemedi.");
        }

        // ==============================================================
        // MsgSend_PartyOrForceLeave — Open-KO birebir:
        // GameProcMain.cpp:1681-1716
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CGameProcMain::MsgSend_PartyOrForceLeave()
        /// GameProcMain.cpp:1681-1716
        /// 
        /// if (bIAmLeader) {
        ///     if (iMemberIndex > 0 && pTarget != nullptr)
        ///         → REMOVE (kick target)
        ///     else
        ///         → DESTROY (disband party)
        /// } else if (bIAmMember) {
        ///     → REMOVE (leave self)
        /// }
        /// </summary>
        public void SendPartyLeave()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;
            if (KOPartyManager.Instance == null) return;

            // Open-KO birebir: cpp:1683-1684
            if (KOPartyManager.Instance.MemberCount <= 0)
                return;

            long myCharId = GameManager.Instance != null ? GameManager.Instance.CharacterId : 0;
            bool bIAmLeader = false;
            bool bIAmMember = false;
            PartyOrForceConditionGet(myCharId, out bIAmLeader, out bIAmMember);

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY);
            if (bIAmLeader)
            {
                // 내가 리더일경우.. N3_SP_PARTY_OR_FORCE_DESTROY (0x05)
                pkt.WriteByte(0x05);
            }
            else if (bIAmMember)
            {
                // 리더가 아니면 N3_SP_PARTY_OR_FORCE_REMOVE (0x04) + s_pPlayer->IDNumber()
                pkt.WriteByte(0x04);
                pkt.WriteInt16((short)myCharId);
            }
            else
            {
                // Fallback for solo party
                pkt.WriteByte(0x05);
            }

            netMgr.SendPacket(pkt);
        }

        // ==============================================================
        // PartyOrForceConditionGet — Open-KO birebir:
        // GameProcMain.cpp:6127-6145
        // ==============================================================

        /// <summary>
        /// Open-KO birebir: CGameProcMain::PartyOrForceConditionGet()
        /// GameProcMain.cpp:6127-6145
        /// 
        /// Parti durumunu sorgular:
        ///   - bIAmLeader: İndeks 0'daki üye ben miyim?
        ///   - bIAmMember: Partide 2+ üye var mı?
        /// </summary>
        public static void PartyOrForceConditionGet(
            long myCharId, out bool bIAmLeader, out bool bIAmMember)
        {
            bIAmLeader = false;
            bIAmMember = false;

            if (KOPartyManager.Instance == null) return;

            // Open-KO birebir: cpp:6136
            // if (m_pUIPartyOrForce != nullptr && m_pUIPartyOrForce->MemberCount() >= 2)
            if (KOPartyManager.Instance.MemberCount >= 2)
            {
                bIAmMember = true;

                // Open-KO birebir: cpp:6139
                // if (m_pUIPartyOrForce->MemberInfoGetByIndex(0)->iID == s_pPlayer->IDNumber())
                var leader = KOPartyManager.Instance.MemberInfoGetByIndex(0);
                if (leader != null && leader.CharacterId == myCharId)
                    bIAmLeader = true;
            }
        }

        // ==============================================================
        // HandlePartyLevelChange — Open-KO birebir:
        // GameProcMain.cpp:5274-5281
        // m_pUIPartyOrForce->MemberLevelChange(iID, iLevel);
        // ==============================================================

        private void HandlePartyLevelChange(long charId, byte level)
        {
            if (KOPartyManager.Instance == null) return;
            KOPartyManager.Instance.MemberLevelChange(charId, level);
        }

        // ==============================================================
        // HandlePartyClassChange — Open-KO birebir:
        // GameProcMain.cpp:5283-5290
        // m_pUIPartyOrForce->MemberClassChange(iID, eClass);
        // ==============================================================

        private void HandlePartyClassChange(long charId, short charClass)
        {
            if (KOPartyManager.Instance == null) return;
            KOPartyManager.Instance.MemberClassChange(charId, (byte)charClass);
        }

        // ==============================================================
        // HandlePartyStatusChange — Open-KO birebir:
        // GameProcMain.cpp:5292-5301
        // m_pUIPartyOrForce->MemberStatusChange(iID, ePS, bSuffer);
        // ==============================================================

        private void HandlePartyStatusChange(long charId, byte statusType, bool bSuffer)
        {
            if (KOPartyManager.Instance == null) return;
            KOPartyManager.Instance.MemberStatusChange(charId, statusType, bSuffer);
        }

        public void SendPartyKick(long targetCharId)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY);
            pkt.WriteByte(0x04); // PARTY_REMOVE (Kick)
            pkt.WriteInt16((short)targetCharId);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        public void SendPartyPromote(long targetCharId)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY);
            pkt.WriteByte(0x1C); // PARTY_PROMOTE
            pkt.WriteInt16((short)targetCharId);
            KONetworkManager.Instance?.SendPacket(pkt);
        }
    }
}
