using System;

namespace EntropyOnline.Network.KO
{
    /// <summary>
    /// LZF Decompression — Open-KO birebir port.
    /// Kaynak: openko-ref/src/shared/lzf.cpp lzf_decompress()
    /// WIZ_COMPRESS_PACKET ve AG_COMPRESSED_DATA paketlerinde kullanılır.
    /// </summary>
    public static class LZF
    {
        /// <summary>
        /// LZF sıkıştırılmış veriyi açar.
        /// </summary>
        /// <param name="input">Sıkıştırılmış veri</param>
        /// <param name="expectedOutputLength">Beklenen çıktı uzunluğu</param>
        /// <returns>Açılmış veri, başarısız olursa null</returns>
        public static byte[] Decompress(byte[] input, int expectedOutputLength)
        {
            if (input == null || input.Length == 0 || expectedOutputLength <= 0)
                return null;

            byte[] output = new byte[expectedOutputLength];
            int iidx = 0;
            int oidx = 0;

            int inLen = input.Length;

            while (iidx < inLen)
            {
                uint ctrl = input[iidx++];

                if (ctrl < (1 << 5)) // literal run
                {
                    ctrl++;

                    if (oidx + ctrl > expectedOutputLength)
                        return null; // output overflow

                    if (iidx + ctrl > inLen)
                        return null; // input underflow

                    do
                    {
                        output[oidx++] = input[iidx++];
                    }
                    while (--ctrl > 0);
                }
                else // back reference
                {
                    uint len = ctrl >> 5;
                    int refIdx = oidx - (int)((ctrl & 0x1f) << 8) - 1;

                    if (iidx >= inLen)
                        return null;

                    if (len == 7)
                    {
                        len += input[iidx++];
                        if (iidx >= inLen)
                            return null;
                    }

                    refIdx -= input[iidx++];

                    if (oidx + len + 2 > expectedOutputLength)
                        return null;

                    if (refIdx < 0)
                        return null;

                    // Copy from back reference (byte-by-byte, overlapping ok)
                    output[oidx++] = output[refIdx++];
                    output[oidx++] = output[refIdx++];

                    while (len-- > 0)
                    {
                        output[oidx++] = output[refIdx++];
                    }
                }
            }

            return output;
        }
    }
}
