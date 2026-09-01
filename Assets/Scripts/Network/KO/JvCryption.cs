using System;

namespace EntropyOnline.Network.KO
{
    /// <summary>
    /// Open-KO JvCryption — C# port.
    /// Birebir: openko-ref/src/shared/JvCryption.cpp
    /// XOR-based packet encryption/decryption.
    /// </summary>
    public class JvCryption
    {
        private const ulong PRIVATE_KEY = 0x1234567890123456UL;

        private ulong _publicKey;
        private ulong _tkey;

        public ulong PublicKey => _publicKey;

        public void SetPublicKey(ulong key)
        {
            _publicKey = key;
        }

        public void Init()
        {
            _tkey = _publicKey ^ PRIVATE_KEY;
        }

        /// <summary>
        /// XOR-based fast encryption (identical to decryption).
        /// Birebir: JvCryption.cpp satır 31-51
        /// </summary>
        public void JvEncryptionFast(byte[] dataIn, byte[] dataOut, int len)
        {
            byte[] pkey = BitConverter.GetBytes(_tkey);
            byte lkey = (byte)((len * 157) & 0xff);
            int rkey = 2157;

            for (int i = 0; i < len; i++)
            {
                byte rsk = (byte)((rkey >> 8) & 0xff);
                dataOut[i] = (byte)(((dataIn[i] ^ rsk) ^ pkey[i % 8]) ^ lkey);
                rkey *= 2171;
            }
        }

        /// <summary>
        /// Decrypt and verify CRC32.
        /// Returns decrypted length (excluding 4-byte CRC), or -1 on failure.
        /// Birebir: JvCryption.cpp satır 53-67
        /// </summary>
        public int JvDecryptionWithCRC32(byte[] dataIn, byte[] dataOut, int len)
        {
            JvEncryptionFast(dataIn, dataOut, len);

            // Son 4 byte CRC32
            uint expectedCrc = BitConverter.ToUInt32(dataOut, len - 4);
            uint actualCrc = CRC32.Compute(dataOut, 0, len - 4);

            if (actualCrc == expectedCrc)
                return len - 4;

            return -1;
        }

        /// <summary>
        /// Encrypt payload and append CRC32.
        /// </summary>
        public byte[] EncryptWithCRC32(byte[] payload, int offset, int length)
        {
            // payload + 4 byte CRC32
            byte[] withCrc = new byte[length + 4];
            Buffer.BlockCopy(payload, offset, withCrc, 0, length);

            uint crc = CRC32.Compute(withCrc, 0, length);
            byte[] crcBytes = BitConverter.GetBytes(crc);
            Buffer.BlockCopy(crcBytes, 0, withCrc, length, 4);

            byte[] encrypted = new byte[withCrc.Length];
            JvEncryptionFast(withCrc, encrypted, withCrc.Length);

            return encrypted;
        }
    }

    /// <summary>
    /// CRC32 — Open-KO birebir: crc32.h/crc32.cpp
    /// </summary>
    public static class CRC32
    {
        private static readonly uint[] Table = new uint[256];

        static CRC32()
        {
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320U;
                    else
                        crc >>= 1;
                }
                Table[i] = crc;
            }
        }

        public static uint Compute(byte[] data, int offset, int length, uint init = 0xFFFFFFFF)
        {
            uint crc = init;
            for (int i = offset; i < offset + length; i++)
            {
                crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc;
        }
    }
}
