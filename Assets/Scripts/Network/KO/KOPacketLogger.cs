using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EntropyOnline.Network.KO
{
    /// <summary>
    /// Paket loglama sistemi — gelen/giden tüm paketleri Unity Console'a okunabilir formatta yazar.
    /// 
    /// Kullanım: Unity Console'da "[PKT-SEND]" ve "[PKT-RECV]" ile filtreleme yapılabilir.
    /// 
    /// Örnek çıktı:
    ///   [PKT-SEND] WIZ_NPC_EVENT (0x20) len=3 → [42 0E]
    ///   [PKT-RECV] WIZ_SELECT_MSG (0x55) len=28 → [42 0E 01 F5 ...]
    ///   [PKT-SEND] WIZ_SELECT_MSG (0x55) len=2 → [00]
    ///   [PKT-RECV] WIZ_TRADE_NPC (0x25) len=6 → [73 00 00 00]
    /// </summary>
    public static class KOPacketLogger
    {
        /// <summary>
        /// Loglama aktif mi? Runtime'da Inspector'dan veya koddan kapatılabilir.
        /// </summary>
        public static bool Enabled = false;

        /// <summary>
        /// Detaylı mod — paket payload'ını hex olarak gösterir.
        /// false ise sadece opcode adı ve uzunluk gösterilir (daha temiz).
        /// </summary>
        public static bool ShowHexPayload = true;

        /// <summary>
        /// Hex payload'da gösterilecek maksimum byte sayısı.
        /// </summary>
        public static int MaxHexBytes = 32;

        /// <summary>
        /// Bu opcode'lar loglanmaz (çok sık gelen hareket/bölge paketleri).
        /// </summary>
        private static readonly HashSet<byte> _mutedOpcodes = new HashSet<byte>
        {
            WizOpcode.WIZ_MOVE,             // Çok sık — hareket
            WizOpcode.WIZ_NPC_MOVE,         // Çok sık — NPC hareket
            WizOpcode.WIZ_ROTATE,           // Çok sık — yön
            WizOpcode.WIZ_REGIONCHANGE,     // Sık — bölge değişimi
            WizOpcode.WIZ_NPC_REGION,       // Sık — NPC bölge
            WizOpcode.WIZ_REQ_USERIN,       // Sık — bölge kullanıcı listesi
            WizOpcode.WIZ_REQ_NPCIN,        // Sık — bölge NPC listesi
            WizOpcode.WIZ_CONTINOUS_PACKET, // Çok sık — arka plan can ve mana güncellemeleri vb.
        };

        /// <summary>
        /// Belirli bir opcode'un loglanmasını kapat/aç.
        /// </summary>
        public static void MuteOpcode(byte opcode) => _mutedOpcodes.Add(opcode);
        public static void UnmuteOpcode(byte opcode) => _mutedOpcodes.Remove(opcode);

        /// <summary>
        /// Giden paketi logla.
        /// </summary>
        public static void LogSend(byte opcode, byte[] rawData)
        {
            if (!Enabled || _mutedOpcodes.Contains(opcode)) return;

            string name = GetOpcodeName(opcode);
            string detail = FormatPacketDetail(opcode, rawData, isSend: true);
            string hex = ShowHexPayload ? $" → {FormatHex(rawData, 1)}" : "";
            
            Debug.Log($"<color=#4FC3F7>[PKT-SEND]</color> <b>{name}</b> (0x{opcode:X2}) len={rawData.Length}{detail}{hex}");
        }

        /// <summary>
        /// Gelen paketi logla.
        /// </summary>
        public static void LogRecv(byte opcode, byte[] rawData)
        {
            if (!Enabled || _mutedOpcodes.Contains(opcode)) return;

            string name = GetOpcodeName(opcode);
            string detail = FormatPacketDetail(opcode, rawData, isSend: false);
            string hex = ShowHexPayload ? $" → {FormatHex(rawData, 1)}" : "";
            
            Debug.Log($"<color=#81C784>[PKT-RECV]</color> <b>{name}</b> (0x{opcode:X2}) len={rawData.Length}{detail}{hex}");
        }

        /// <summary>
        /// Önemli paketler için ek detay parse et.
        /// </summary>
        private static string FormatPacketDetail(byte opcode, byte[] data, bool isSend)
        {
            try
            {
                var r = new KOPacketReader(data);

                switch (opcode)
                {
                    case WizOpcode.WIZ_NPC_EVENT:
                        if (data.Length >= 3)
                        {
                            short npcSid = r.ReadInt16();
                            return $" npcSid={npcSid}";
                        }
                        break;

                    case WizOpcode.WIZ_SELECT_MSG:
                        if (isSend && data.Length >= 2)
                        {
                            byte idx = r.ReadByte();
                            return $" menuIndex={idx}";
                        }
                        else if (!isSend && data.Length >= 5)
                        {
                            short npcId = r.ReadInt16();
                            short talkId = r.ReadInt16();
                            return $" npcId={npcId} talkId={talkId}";
                        }
                        break;

                    case WizOpcode.WIZ_TRADE_NPC:
                        if (!isSend && data.Length >= 5)
                        {
                            int tradeId = r.ReadInt32();
                            return $" tradeId={tradeId}";
                        }
                        break;

                    case WizOpcode.WIZ_ATTACK:
                        if (isSend && data.Length >= 7)
                        {
                            byte atkType = r.ReadByte();
                            byte success = r.ReadByte();
                            short targetId = r.ReadInt16();
                            return $" type={atkType} target={targetId}";
                        }
                        break;

                    case WizOpcode.WIZ_DEAD:
                        if (data.Length >= 3)
                        {
                            short deadId = r.ReadInt16();
                            return $" entityId={deadId}";
                        }
                        break;

                    case WizOpcode.WIZ_HP_CHANGE:
                        if (data.Length >= 5)
                        {
                            short maxHp = r.ReadInt16();
                            short curHp = r.ReadInt16();
                            return $" hp={curHp}/{maxHp}";
                        }
                        break;

                    case WizOpcode.WIZ_MSP_CHANGE:
                        if (data.Length >= 5)
                        {
                            short maxMp = r.ReadInt16();
                            short curMp = r.ReadInt16();
                            return $" mp={curMp}/{maxMp}";
                        }
                        break;

                    case WizOpcode.WIZ_EXP_CHANGE:
                        if (data.Length >= 5)
                        {
                            // just show raw since format varies
                            return "";
                        }
                        break;

                    case WizOpcode.WIZ_WAREHOUSE:
                        if (data.Length >= 2)
                        {
                            byte subOp = r.ReadByte();
                            return $" subOp={subOp}";
                        }
                        break;

                    case WizOpcode.WIZ_ZONE_CHANGE:
                        if (data.Length >= 2)
                        {
                            byte sub = r.ReadByte();
                            return $" sub={sub}";
                        }
                        break;

                    case WizOpcode.WIZ_MAGIC_PROCESS:
                        if (data.Length >= 10)
                        {
                            byte sub = r.ReadByte();
                            string subName = sub switch { 1 => "CASTING", 2 => "FLYING", 3 => "EFFECTING", 4 => "FAIL", _ => $"0x{sub:X2}" };
                            int magicId = r.ReadInt32();
                            short src = r.ReadInt16();
                            short tgt = r.ReadInt16();
                            string extra = "";
                            if (sub == 4 && data.Length >= 18) // FAIL — Data[3] = fail reason
                            {
                                r.ReadInt16(); r.ReadInt16(); r.ReadInt16(); // d0,d1,d2
                                short failReason = r.ReadInt16(); // d3
                                string reasonName = failReason switch { -100 => "CASTING", -101 => "KILLFLYING", -103 => "NOEFFECT", -104 => "ATTACKZERO", _ => failReason.ToString() };
                                extra = $" reason={reasonName}({failReason})";
                            }
                            var pSkill = KOImport.SkillTableParser.Find(magicId);
                            string skillName = pSkill != null ? $" \"{pSkill.Name}\"" : "";
                            return $" sub={subName} magicId={magicId}{skillName} src={src} tgt={tgt}{extra}";
                        }
                        break;

                    case WizOpcode.WIZ_ITEM_MOVE:
                        if (data.Length >= 2)
                        {
                            byte sub = r.ReadByte();
                            return $" type={sub}";
                        }
                        break;

                    case WizOpcode.WIZ_CHAT:
                        if (data.Length >= 2)
                        {
                            byte chatType = r.ReadByte();
                            return $" chatType={chatType}";
                        }
                        break;

                    case WizOpcode.WIZ_WARP_LIST:
                        if (!isSend && data.Length >= 2)
                        {
                            byte sub = r.ReadByte();
                            return $" sub={sub}";
                        }
                        break;

                    case WizOpcode.WIZ_REPAIR_NPC:
                        if (data.Length >= 2)
                        {
                            byte sub = r.ReadByte();
                            return $" sub={sub}";
                        }
                        break;

                    case WizOpcode.WIZ_CLASS_CHANGE:
                        if (data.Length >= 2)
                        {
                            byte sub = r.ReadByte();
                            return $" sub={sub}";
                        }
                        break;
                }
            }
            catch
            {
                // Parse hatası — detay gösterme
            }

            return "";
        }

        /// <summary>
        /// rawData'yı hex string'e çevir (opcode byte'ını atla).
        /// </summary>
        private static string FormatHex(byte[] data, int skipBytes)
        {
            if (data == null || data.Length <= skipBytes) return "[]";

            int count = Math.Min(data.Length - skipBytes, MaxHexBytes);
            var sb = new StringBuilder(count * 3 + 4);
            sb.Append('[');
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[skipBytes + i].ToString("X2"));
            }
            if (data.Length - skipBytes > MaxHexBytes)
                sb.Append(" ...");
            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// Opcode → okunabilir isim.
        /// </summary>
        public static string GetOpcodeName(byte opcode)
        {
            return opcode switch
            {
                WizOpcode.WIZ_LOGIN            => "WIZ_LOGIN",
                WizOpcode.WIZ_NEW_CHAR         => "WIZ_NEW_CHAR",
                WizOpcode.WIZ_DEL_CHAR         => "WIZ_DEL_CHAR",
                WizOpcode.WIZ_SEL_CHAR         => "WIZ_SEL_CHAR",
                WizOpcode.WIZ_SEL_NATION       => "WIZ_SEL_NATION",
                WizOpcode.WIZ_MOVE             => "WIZ_MOVE",
                WizOpcode.WIZ_USER_INOUT       => "WIZ_USER_INOUT",
                WizOpcode.WIZ_ATTACK           => "WIZ_ATTACK",
                WizOpcode.WIZ_ROTATE           => "WIZ_ROTATE",
                WizOpcode.WIZ_NPC_INOUT        => "WIZ_NPC_INOUT",
                WizOpcode.WIZ_NPC_MOVE         => "WIZ_NPC_MOVE",
                WizOpcode.WIZ_ALLCHAR_INFO_REQ => "WIZ_ALLCHAR_INFO_REQ",
                WizOpcode.WIZ_GAMESTART        => "WIZ_GAMESTART",
                WizOpcode.WIZ_MYINFO           => "WIZ_MYINFO",
                WizOpcode.WIZ_LOGOUT           => "WIZ_LOGOUT",
                WizOpcode.WIZ_CHAT             => "WIZ_CHAT",
                WizOpcode.WIZ_DEAD             => "WIZ_DEAD",
                WizOpcode.WIZ_REGENE           => "WIZ_REGENE",
                WizOpcode.WIZ_TIME             => "WIZ_TIME",
                WizOpcode.WIZ_WEATHER          => "WIZ_WEATHER",
                WizOpcode.WIZ_REGIONCHANGE     => "WIZ_REGIONCHANGE",
                WizOpcode.WIZ_REQ_USERIN       => "WIZ_REQ_USERIN",
                WizOpcode.WIZ_HP_CHANGE        => "WIZ_HP_CHANGE",
                WizOpcode.WIZ_MSP_CHANGE       => "WIZ_MSP_CHANGE",
                WizOpcode.WIZ_EXP_CHANGE       => "WIZ_EXP_CHANGE",
                WizOpcode.WIZ_LEVEL_CHANGE     => "WIZ_LEVEL_CHANGE",
                WizOpcode.WIZ_NPC_REGION       => "WIZ_NPC_REGION",
                WizOpcode.WIZ_REQ_NPCIN        => "WIZ_REQ_NPCIN",
                WizOpcode.WIZ_WARP             => "WIZ_WARP",
                WizOpcode.WIZ_ITEM_MOVE        => "WIZ_ITEM_MOVE",
                WizOpcode.WIZ_NPC_EVENT        => "WIZ_NPC_EVENT",
                WizOpcode.WIZ_ITEM_TRADE       => "WIZ_ITEM_TRADE",
                WizOpcode.WIZ_TARGET_HP        => "WIZ_TARGET_HP",
                WizOpcode.WIZ_ITEM_DROP        => "WIZ_ITEM_DROP",
                WizOpcode.WIZ_BUNDLE_OPEN_REQ  => "WIZ_BUNDLE_OPEN_REQ",
                WizOpcode.WIZ_TRADE_NPC        => "WIZ_TRADE_NPC",
                WizOpcode.WIZ_ITEM_GET         => "WIZ_ITEM_GET",
                WizOpcode.WIZ_ZONE_CHANGE      => "WIZ_ZONE_CHANGE",
                WizOpcode.WIZ_POINT_CHANGE     => "WIZ_POINT_CHANGE",
                WizOpcode.WIZ_STATE_CHANGE     => "WIZ_STATE_CHANGE",
                WizOpcode.WIZ_LOYALTY_CHANGE   => "WIZ_LOYALTY_CHANGE",
                WizOpcode.WIZ_VERSION_CHECK    => "WIZ_VERSION_CHECK",
                WizOpcode.WIZ_CRYPTION         => "WIZ_CRYPTION",
                WizOpcode.WIZ_USERLOOK_CHANGE  => "WIZ_USERLOOK_CHANGE",
                WizOpcode.WIZ_NOTICE           => "WIZ_NOTICE",
                WizOpcode.WIZ_PARTY            => "WIZ_PARTY",
                WizOpcode.WIZ_EXCHANGE         => "WIZ_EXCHANGE",
                WizOpcode.WIZ_MAGIC_PROCESS    => "WIZ_MAGIC_PROCESS",
                WizOpcode.WIZ_SKILLPT_CHANGE   => "WIZ_SKILLPT_CHANGE",
                WizOpcode.WIZ_OBJECT_EVENT     => "WIZ_OBJECT_EVENT",
                WizOpcode.WIZ_CLASS_CHANGE     => "WIZ_CLASS_CHANGE",
                WizOpcode.WIZ_CHAT_TARGET      => "WIZ_CHAT_TARGET",
                WizOpcode.WIZ_CONCURRENTUSER   => "WIZ_CONCURRENTUSER",
                WizOpcode.WIZ_DURATION         => "WIZ_DURATION",
                WizOpcode.WIZ_REPAIR_NPC       => "WIZ_REPAIR_NPC",
                WizOpcode.WIZ_ITEM_REPAIR      => "WIZ_ITEM_REPAIR",
                WizOpcode.WIZ_KNIGHTS_PROCESS  => "WIZ_KNIGHTS_PROCESS",
                WizOpcode.WIZ_ITEM_COUNT_CHANGE => "WIZ_ITEM_COUNT_CHANGE",
                WizOpcode.WIZ_KNIGHTS_LIST     => "WIZ_KNIGHTS_LIST",
                WizOpcode.WIZ_ITEM_REMOVE      => "WIZ_ITEM_REMOVE",
                WizOpcode.WIZ_COMPRESS_PACKET  => "WIZ_COMPRESS_PACKET",
                WizOpcode.WIZ_CONTINOUS_PACKET => "WIZ_CONTINOUS_PACKET",
                WizOpcode.WIZ_WAREHOUSE        => "WIZ_WAREHOUSE",
                WizOpcode.WIZ_HOME             => "WIZ_HOME",
                WizOpcode.WIZ_FRIEND_PROCESS   => "WIZ_FRIEND_PROCESS",
                WizOpcode.WIZ_GOLD_CHANGE      => "WIZ_GOLD_CHANGE",
                WizOpcode.WIZ_WARP_LIST        => "WIZ_WARP_LIST",
                WizOpcode.WIZ_SELECT_MSG       => "WIZ_SELECT_MSG",
                WizOpcode.WIZ_NPC_SAY          => "WIZ_NPC_SAY",
                WizOpcode.WIZ_ITEM_UPGRADE     => "WIZ_ITEM_UPGRADE",
                WizOpcode.WIZ_ZONEABILITY      => "WIZ_ZONEABILITY",
                WizOpcode.WIZ_WEIGHT_CHANGE    => "WIZ_WEIGHT_CHANGE",
                WizOpcode.WIZ_QUEST            => "WIZ_QUEST",
                WizOpcode.WIZ_SKILLDATA        => "WIZ_SKILLDATA",
                WizOpcode.WIZ_CORPSE           => "WIZ_CORPSE",
                WizOpcode.WIZ_MERCHANT_INOUT   => "WIZ_MERCHANT_INOUT",
                _ => $"UNKNOWN_0x{opcode:X2}"
            };
        }
    }
}
