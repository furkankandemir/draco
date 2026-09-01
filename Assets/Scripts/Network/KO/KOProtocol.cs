using System;
using System.IO;
using System.Text;

namespace EntropyOnline.Network.KO
{
    /// <summary>
    /// Open-KO WIZ_* opcode'ları.
    /// Birebir: openko-ref/src/shared/packets.h — e_GameOpcode
    /// </summary>
    public static class WizOpcode
    {
        public const byte WIZ_LOGIN            = 0x01;
        public const byte WIZ_NEW_CHAR         = 0x02;
        public const byte WIZ_DEL_CHAR         = 0x03;
        public const byte WIZ_SEL_CHAR         = 0x04;
        public const byte WIZ_SEL_NATION       = 0x05;
        public const byte WIZ_MOVE             = 0x06;
        public const byte WIZ_USER_INOUT       = 0x07;
        public const byte WIZ_ATTACK           = 0x08;
        public const byte WIZ_ROTATE           = 0x09;
        public const byte WIZ_NPC_INOUT        = 0x0A;
        public const byte WIZ_NPC_MOVE         = 0x0B;
        public const byte WIZ_ALLCHAR_INFO_REQ = 0x0C;
        public const byte WIZ_GAMESTART        = 0x0D;
        public const byte WIZ_MYINFO           = 0x0E;
        public const byte WIZ_LOGOUT           = 0x0F;
        public const byte WIZ_CHAT             = 0x10;
        public const byte WIZ_DEAD             = 0x11;
        public const byte WIZ_REGENE           = 0x12;
        public const byte WIZ_TIME             = 0x13;
        public const byte WIZ_WEATHER          = 0x14;
        public const byte WIZ_REGIONCHANGE     = 0x15;
        public const byte WIZ_REQ_USERIN       = 0x16;
        public const byte WIZ_HP_CHANGE        = 0x17;
        public const byte WIZ_MSP_CHANGE       = 0x18;
        public const byte WIZ_EXP_CHANGE       = 0x1A;
        public const byte WIZ_LEVEL_CHANGE     = 0x1B;
        public const byte WIZ_NPC_REGION       = 0x1C;
        public const byte WIZ_REQ_NPCIN        = 0x1D;
        public const byte WIZ_WARP             = 0x1E;
        public const byte WIZ_ITEM_MOVE        = 0x1F;
        public const byte WIZ_NPC_EVENT        = 0x20;
        public const byte WIZ_ITEM_TRADE       = 0x21;
        public const byte WIZ_TARGET_HP        = 0x22;
        public const byte WIZ_ITEM_DROP        = 0x23;
        public const byte WIZ_BUNDLE_OPEN_REQ  = 0x24;
        public const byte WIZ_TRADE_NPC        = 0x25;
        public const byte WIZ_ITEM_GET         = 0x26;
        public const byte WIZ_ZONE_CHANGE      = 0x27;
        public const byte WIZ_POINT_CHANGE     = 0x28;
        public const byte WIZ_STATE_CHANGE     = 0x29;
        public const byte WIZ_LOYALTY_CHANGE   = 0x2A;
        public const byte WIZ_VERSION_CHECK    = 0x2B;
        public const byte WIZ_CRYPTION         = 0x2C;
        public const byte WIZ_USERLOOK_CHANGE  = 0x2D;
        public const byte WIZ_NOTICE           = 0x2E;
        public const byte WIZ_PARTY            = 0x2F;
        public const byte WIZ_EXCHANGE         = 0x30;
        public const byte WIZ_MAGIC_PROCESS    = 0x31;
        public const byte WIZ_SKILLPT_CHANGE   = 0x32;
        public const byte WIZ_OBJECT_EVENT     = 0x33;
        public const byte WIZ_CLASS_CHANGE     = 0x34;
        public const byte WIZ_CHAT_TARGET      = 0x35;
        public const byte WIZ_CONCURRENTUSER   = 0x36;
        public const byte WIZ_DATASAVE         = 0x37;
        public const byte WIZ_DURATION         = 0x38;
        public const byte WIZ_TIMENOTIFY       = 0x39;
        public const byte WIZ_REPAIR_NPC       = 0x3A;
        public const byte WIZ_ITEM_REPAIR      = 0x3B;
        public const byte WIZ_KNIGHTS_PROCESS  = 0x3C;
        public const byte WIZ_ITEM_COUNT_CHANGE = 0x3D;
        public const byte WIZ_KNIGHTS_LIST     = 0x3E;
        public const byte WIZ_ITEM_REMOVE      = 0x3F;
        public const byte WIZ_COMPRESS_PACKET  = 0x42;
        public const byte WIZ_CONTINOUS_PACKET = 0x44;
        public const byte WIZ_WAREHOUSE        = 0x45;
        public const byte WIZ_HOME             = 0x48;
        public const byte WIZ_FRIEND_PROCESS   = 0x49;
        public const byte WIZ_GOLD_CHANGE      = 0x4A;
        public const byte WIZ_WARP_LIST        = 0x4B;
        public const byte WIZ_SELECT_MSG       = 0x55;
        public const byte WIZ_NPC_SAY          = 0x56;
        public const byte WIZ_ITEM_UPGRADE     = 0x5B;
        public const byte WIZ_ZONEABILITY      = 0x5E;
        public const byte WIZ_WEIGHT_CHANGE    = 0x54;  // cpp packets.h — weight update after item move
        public const byte WIZ_QUEST            = 0x64;
        public const byte WIZ_MERCHANT         = 0x68;
        public const byte WIZ_SHOPPING_MALL    = 0x6A;
        public const byte WIZ_EFFECT           = 0x6C;
        public const byte WIZ_SKILLDATA        = 0x79;
        public const byte WIZ_CLIENT_EVENT     = 0x52; // packets.h:92 — Server-side only (ClientEvent fonksiyonu), S2C gönderilmez

        // Open-KO birebir: packets.h — ek opcode'lar
        public const byte WIZ_OPERATOR         = 0x40;  // GM komutları (arrest, forbid, permit)
        public const byte WIZ_SPEEDHACK_CHECK  = 0x41;  // cpp packets.h:74 — 0x41
        public const byte WIZ_PARTY_BBS        = 0x4F;  // Parti BBS kayıt/iptal
        public const byte WIZ_MARKET_BBS       = 0x50;  // Market BBS
        public const byte WIZ_CORPSE           = 0x4E;  // cpp packets.h:87 — 0x4e
        public const byte WIZ_MERCHANT_INOUT   = 0x69;  // cpp packets.h:115 — 0x69
        public const byte WIZ_STEALTH          = 0x60;  // stealth related (Cat's Eyes, Lupin Eyes)
        public const byte WIZ_EVENT            = 0x5F;  // cpp packets.h:105 — 0x5F
        public const byte WIZ_CAPE             = 0x70;
        public const byte WIZ_CAPTURE          = 0x85;
        public const byte WIZ_INSPECT          = 0xE1; // Custom Inspect Opcode
    }

    public static class TradeBBSSub
    {
        public const byte N3_SP_TYPE_REGISTER        = 0x01; // 물건 등록하기
        public const byte N3_SP_TYPE_REGISTER_CANCEL = 0x02; // 등록 해제하기
        public const byte N3_SP_TYPE_BBS_DATA        = 0x03; // 게시판 정보 요구
        public const byte N3_SP_TYPE_BBS_OPEN        = 0x04; // 상거래 게시판 열기
        public const byte N3_SP_TYPE_BBS_TRADE       = 0x05; // 게시판에서 거래 신청하기
    }

    public static class TradeBBSKind
    {
        public const byte N3_SP_TRADE_BBS_BUY  = 0x01; // 사는 물건 목록
        public const byte N3_SP_TRADE_BBS_SELL = 0x02; // 파는 물건 목록
    }


    public enum ShoppingMallOpcodes : byte
    {
        STORE_OPEN = 1,
        STORE_CLOSE = 2,
        STORE_BUY = 3,
        STORE_MINI = 4,
        STORE_PROCESS = 5,
        STORE_LETTER = 6
    }

    /// <summary>
    /// Open-KO wire format paket oluşturucu.
    /// Format: [0xAA][0x55][length:2 BE][payload][0x55][0xAA]
    /// </summary>
    public class KOPacketWriter : IDisposable
    {
        private readonly MemoryStream _stream;

        public KOPacketWriter(byte opcode)
        {
            _stream = new MemoryStream(256);
            _stream.WriteByte(opcode);
        }

        public KOPacketWriter WriteByte(byte value)
        {
            _stream.WriteByte(value);
            return this;
        }

        public KOPacketWriter WriteInt16(short value)
        {
            _stream.WriteByte((byte)(value & 0xFF));
            _stream.WriteByte((byte)((value >> 8) & 0xFF));
            return this;
        }

        public KOPacketWriter WriteUInt16(ushort value)
        {
            _stream.WriteByte((byte)(value & 0xFF));
            _stream.WriteByte((byte)((value >> 8) & 0xFF));
            return this;
        }

        public KOPacketWriter WriteInt32(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            _stream.Write(bytes, 0, 4);
            return this;
        }

        public KOPacketWriter WriteUInt32(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            _stream.Write(bytes, 0, 4);
            return this;
        }

        public KOPacketWriter WriteInt64(long value)
        {
            var bytes = BitConverter.GetBytes(value);
            _stream.Write(bytes, 0, 8);
            return this;
        }

        public KOPacketWriter WriteUInt64(ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            _stream.Write(bytes, 0, 8);
            return this;
        }

        public KOPacketWriter WriteFloat(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            _stream.Write(bytes, 0, 4);
            return this;
        }

        /// <summary>
        /// Open-KO string format: [length:2 LE][string bytes]
        /// Birebir: SetString2() — shared/globals.h
        /// </summary>
        public KOPacketWriter WriteKOString(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            WriteInt16((short)bytes.Length);
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        /// <summary>
        /// Raw byte dizisi yaz (SetString gibi).
        /// </summary>
        public KOPacketWriter WriteBytes(byte[] data, int offset, int count)
        {
            _stream.Write(data, offset, count);
            return this;
        }

        /// <summary>
        /// WriteKOString kısayolu — migrasyon uyumluluğu.
        /// </summary>
        public KOPacketWriter WriteString(string value) => WriteKOString(value);

        /// <summary>
        /// Boolean yaz — true=1, false=0.
        /// </summary>
        public KOPacketWriter WriteBool(bool value)
        {
            _stream.WriteByte(value ? (byte)1 : (byte)0);
            return this;
        }

        /// <summary>
        /// Loglama için raw payload'ı döndür (opcode + data, şifrelenmemiş).
        /// </summary>
        public byte[] GetPayload() => _stream.ToArray();

        /// <summary>
        /// Payload'ı Open-KO wire frame'e sar.
        /// Şifreleme kapalıysa: [AA][55][len:2][payload][55][AA]
        /// Şifreleme açıksa: [AA][55][len:2][encrypted(0xFC 0x1E seq[3] payload + CRC32)][55][AA]
        /// </summary>
        public byte[] Build(JvCryption crypto, bool encrypted, ref uint sendValue)
        {
            byte[] payload = _stream.ToArray();

            using var output = new MemoryStream(payload.Length + 16);

            // Header
            output.WriteByte(0xAA); // PACKET_START1
            output.WriteByte(0x55); // PACKET_START2

            if (encrypted && crypto != null)
            {
                sendValue++;
                sendValue &= 0x00FFFFFF; // C++ birebir: _sendValue &= 0x00ffffff

                // C++ PullOutCore format: [sendValue:4 DWORD LE][payload]
                // Sunucu GetDWORD ile 4 byte sequence okur, ardından opcode+data
                int clearLen = 4 + payload.Length;
                byte[] cleartext = new byte[clearLen];
                cleartext[0] = (byte)(sendValue & 0xFF);
                cleartext[1] = (byte)((sendValue >> 8) & 0xFF);
                cleartext[2] = (byte)((sendValue >> 16) & 0xFF);
                cleartext[3] = (byte)((sendValue >> 24) & 0xFF);
                Buffer.BlockCopy(payload, 0, cleartext, 4, payload.Length);

                // Encrypt with CRC32
                byte[] encryptedData = crypto.EncryptWithCRC32(cleartext, 0, clearLen);

                // Length (2 bytes LE) — encrypted data length
                ushort encLen = (ushort)encryptedData.Length;
                output.WriteByte((byte)(encLen & 0xFF));
                output.WriteByte((byte)((encLen >> 8) & 0xFF));

                output.Write(encryptedData, 0, encryptedData.Length);
            }
            else
            {
                // Length (2 bytes LE)
                ushort len = (ushort)payload.Length;
                output.WriteByte((byte)(len & 0xFF));
                output.WriteByte((byte)((len >> 8) & 0xFF));

                output.Write(payload, 0, payload.Length);
            }

            // Tail
            output.WriteByte(0x55); // PACKET_END1
            output.WriteByte(0xAA); // PACKET_END2

            return output.ToArray();
        }

        /// <summary>
        /// Şifresiz paket oluştur (handshake aşaması için).
        /// </summary>
        public byte[] BuildUnencrypted()
        {
            uint dummy = 0;
            return Build(null, false, ref dummy);
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }

    /// <summary>
    /// Open-KO wire format paket okuyucu.
    /// Payload'ın zaten çözülmüş (decrypted) olduğunu varsayar.
    /// </summary>
    public class KOPacketReader
    {
        private readonly byte[] _data;
        private int _pos;

        public byte Opcode { get; }
        public int Length => _data.Length;
        public int Remaining => _data.Length - _pos;

        public KOPacketReader(byte[] payload)
        {
            _data = payload;
            _pos = 0;
            Opcode = ReadByte();
        }

        /// <summary>
        /// Belirli bir offset'ten başlayan reader oluştur (opcode zaten okunmuş).
        /// </summary>
        public KOPacketReader(byte[] payload, byte opcode)
        {
            _data = payload;
            _pos = 0;
            Opcode = opcode;
        }

        public byte ReadByte()
        {
            if (_pos >= _data.Length) return 0;
            return _data[_pos++];
        }

        public short ReadInt16()
        {
            if (_pos + 2 > _data.Length) return 0;
            short v = BitConverter.ToInt16(_data, _pos);
            _pos += 2;
            return v;
        }

        public ushort ReadUInt16()
        {
            if (_pos + 2 > _data.Length) return 0;
            ushort v = BitConverter.ToUInt16(_data, _pos);
            _pos += 2;
            return v;
        }

        public int ReadInt32()
        {
            if (_pos + 4 > _data.Length) return 0;
            int v = BitConverter.ToInt32(_data, _pos);
            _pos += 4;
            return v;
        }

        public uint ReadUInt32()
        {
            if (_pos + 4 > _data.Length) return 0;
            uint v = BitConverter.ToUInt32(_data, _pos);
            _pos += 4;
            return v;
        }

        public long ReadInt64()
        {
            if (_pos + 8 > _data.Length) return 0;
            long v = BitConverter.ToInt64(_data, _pos);
            _pos += 8;
            return v;
        }

        public ulong ReadUInt64()
        {
            if (_pos + 8 > _data.Length) return 0;
            ulong v = BitConverter.ToUInt64(_data, _pos);
            _pos += 8;
            return v;
        }

        public float ReadFloat()
        {
            if (_pos + 4 > _data.Length) return 0;
            float v = BitConverter.ToSingle(_data, _pos);
            _pos += 4;
            return v;
        }

        /// <summary>
        /// Open-KO string format: [length:2 LE][string bytes]
        /// </summary>
        public string ReadKOString()
        {
            ushort len = ReadUInt16();
            if (len == 0 || _pos + len > _data.Length) return string.Empty;
            string v = Encoding.ASCII.GetString(_data, _pos, len);
            _pos += len;
            return v;
        }

        /// <summary>
        /// Open-KO string format (1-byte prefix): [length:1][string bytes]
        /// C++ birebir: SetString1 / GetVarString(1)
        /// </summary>
        public string ReadKOString1()
        {
            byte len = ReadByte();
            if (len == 0 || _pos + len > _data.Length) return string.Empty;
            string v = Encoding.ASCII.GetString(_data, _pos, len);
            _pos += len;
            return v;
        }

        public byte[] ReadBytes(int count)
        {
            if (_pos + count > _data.Length) count = _data.Length - _pos;
            byte[] result = new byte[count];
            Buffer.BlockCopy(_data, _pos, result, 0, count);
            _pos += count;
            return result;
        }

        public void Skip(int count)
        {
            _pos += count;
        }
    }

    public enum e_QuestOpcode : byte
    {
        QUEST_LIST = 1,
        QUEST_UPDATE = 2
    }
}
