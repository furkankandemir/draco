using System;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 DXT (N3Texture) Binary Parser
    /// 
    /// CN3Texture::Load() birebir portu — .dxt (NTF) texture dosyalarını okur.
    /// 
    /// KO .dxt dosya formatı (NTF — Noah Texture File):
    ///   N3BaseFileAccess header (int32 nameLen + char[] name)
    ///   __DXT_HEADER:
    ///     char[4] szID  = "NTF\x03" (veya \x07 encrypted)
    ///     int32   nWidth
    ///     int32   nHeight
    ///     int32   Format (D3DFMT enum)
    ///     BOOL    bMipMap
    ///
    /// DXT compressed data layout (CN3Texture::Load, line 382-513):
    ///   [DXT compressed mip levels: level0 + level1 + ... down to 4x4]
    ///   [Fallback 16-bit mip levels: (w/2,h/2) down to (4,4)]
    ///
    /// Bu implementasyon DXT compressed data'yı software decode eder (tam çözünürlük).
    /// Eski implementasyon fallback 16-bit veri okuyordu (yarı çözünürlük, UV uyumsuzluğu).
    /// </summary>
    public static class DxtTextureImporter
    {
        // D3DFORMAT values
        private const int D3DFMT_DXT1 = 0x31545844;
        private const int D3DFMT_DXT2 = 0x32545844;
        private const int D3DFMT_DXT3 = 0x33545844;
        private const int D3DFMT_DXT4 = 0x34545844;
        private const int D3DFMT_DXT5 = 0x35545844;
        private const int D3DFMT_A1R5G5B5 = 25;
        private const int D3DFMT_A4R4G4B4 = 26;
        private const int D3DFMT_R8G8B8 = 20;
        private const int D3DFMT_A8R8G8B8 = 21;
        private const int D3DFMT_X8R8G8B8 = 22;

        /// <summary>
        /// .dxt (NTF) dosyasından Unity Texture2D oluştur.
        /// </summary>
        public static Texture2D Load(string dxtPath, bool flipY = true)
        {
            using var reader = KOBinaryProvider.OpenReader(dxtPath);
            if (reader == null)
            {
                Debug.LogError($"[DXT] Dosya bulunamadı veya okunamadı: {dxtPath}");
                return null;
            }

            // ============================================
            // N3BaseFileAccess header (int32 nameLen + char[] name)
            // ============================================
            SkipN3Header(reader);

            // ============================================
            // __DXT_HEADER (16 bytes)
            // ============================================
            byte id0 = reader.ReadByte(); // 'N'
            byte id1 = reader.ReadByte(); // 'T'
            byte id2 = reader.ReadByte(); // 'F'
            byte id3 = reader.ReadByte(); // version (3 or 7)

            if (id0 != (byte)'N' || id1 != (byte)'T' || id2 != (byte)'F')
            {
                Debug.LogError($"[DXT] Geçersiz NTF header: {(char)id0}{(char)id1}{(char)id2}");
                return null;
            }

            bool isEncrypted = (id3 >= 7);
            if (isEncrypted)
            {
                Debug.LogWarning($"[DXT] Encrypted NTF v{id3} — desteklenmiyor: {dxtPath}");
                return null;
            }

            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            int format = reader.ReadInt32();
            int bMipMap = reader.ReadInt32();

            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
            {
                Debug.LogError($"[DXT] Geçersiz boyut: {width}x{height}");
                return null;
            }

            // ============================================
            // Texture data okuma
            // ============================================
            bool isDXT = (format == D3DFMT_DXT1 || format == D3DFMT_DXT2 ||
                          format == D3DFMT_DXT3 || format == D3DFMT_DXT4 ||
                          format == D3DFMT_DXT5);

            Texture2D tex = null;
            if (isDXT)
            {
                // DXT compressed data'yı software decode et (tam çözünürlük)
                tex = LoadDXTDecode(reader, width, height, format, flipY);
            }
            else
            {
                tex = LoadUncompressed(reader, width, height, format, bMipMap != 0, flipY);
            }

            if (tex != null)
            {
                tex.name = Path.GetFileNameWithoutExtension(dxtPath);
            }
            return tex;
        }

        // ============================================
        // DXT SOFTWARE DECODE — CN3Texture::Load birebir
        // ============================================

        /// <summary>
        /// DXT compressed data'yı okuyup software decode ile tam çözünürlüklü RGBA32 texture oluşturur.
        /// C++ CN3Texture::Load — "bDXTSupport = TRUE" dalı, level 0 verisi okunur.
        /// 
        /// C++ GetTextureSize (N3Texture.cpp line 14-21):
        ///   DXT1: w * h / 2
        ///   DXT2-5: (w * h) & ~0xF  (power-of-2 texture'lar için w * h)
        /// </summary>
        private static Texture2D LoadDXTDecode(
            BinaryReader reader, int width, int height, int format, bool flipY)
        {
            // Level 0 compressed data boyutu — C++ GetTextureSize() birebir
            int level0Size;
            if (format == D3DFMT_DXT1)
                level0Size = Math.Max(width * height / 2, 8);
            else
                level0Size = Math.Max(width * height, 16);

            // Yeterli veri var mı kontrol et
            long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remaining < level0Size)
            {
                Debug.LogWarning($"[DXT] Yetersiz data: beklenen={level0Size}, kalan={remaining} " +
                                 $"({width}x{height} fmt=0x{format:X})");
                return null;
            }

            byte[] data = reader.ReadBytes(level0Size);

            // Format'a göre decode
            Texture2D tex;
            if (format == D3DFMT_DXT1)
                tex = DecodeDXT1(data, width, height, flipY);
            else if (format == D3DFMT_DXT3 || format == D3DFMT_DXT2)
                tex = DecodeDXT3(data, width, height, flipY);
            else // DXT5, DXT4
                tex = DecodeDXT5(data, width, height, flipY);

            return tex;
        }

        // ============================================
        // DXT1 DECODER
        // ============================================

        /// <summary>
        /// DXT1 (BC1) software decode.
        /// Her 4×4 blok = 8 byte: 2 byte color0 (R5G6B5) + 2 byte color1 (R5G6B5) + 4 byte lookup.
        /// color0 > color1: 4 renk interpolasyonu (opak)
        /// color0 <= color1: 3 renk + transparent
        /// </summary>
        private static Texture2D DecodeDXT1(byte[] data, int width, int height, bool flipY)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            int blocksX = Math.Max(1, width / 4);
            int blocksY = Math.Max(1, height / 4);

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int blockOffset = (by * blocksX + bx) * 8;
                    if (blockOffset + 8 > data.Length) break;

                    // Color endpoints (R5G6B5)
                    ushort c0 = (ushort)(data[blockOffset] | (data[blockOffset + 1] << 8));
                    ushort c1 = (ushort)(data[blockOffset + 2] | (data[blockOffset + 3] << 8));

                    DecodeR5G6B5(c0, out byte r0, out byte g0, out byte b0);
                    DecodeR5G6B5(c1, out byte r1, out byte g1, out byte b1);

                    // 4-color palette
                    var palette = new Color32[4];
                    palette[0] = new Color32(r0, g0, b0, 255);
                    palette[1] = new Color32(r1, g1, b1, 255);

                    if (c0 > c1)
                    {
                        // 4-color mode (opak)
                        palette[2] = new Color32(
                            (byte)((2 * r0 + r1 + 1) / 3),
                            (byte)((2 * g0 + g1 + 1) / 3),
                            (byte)((2 * b0 + b1 + 1) / 3), 255);
                        palette[3] = new Color32(
                            (byte)((r0 + 2 * r1 + 1) / 3),
                            (byte)((g0 + 2 * g1 + 1) / 3),
                            (byte)((b0 + 2 * b1 + 1) / 3), 255);
                    }
                    else
                    {
                        // 3-color + transparent mode
                        palette[2] = new Color32(
                            (byte)((r0 + r1) / 2),
                            (byte)((g0 + g1) / 2),
                            (byte)((b0 + b1) / 2), 255);
                        palette[3] = new Color32(0, 0, 0, 0); // transparent
                    }

                    // Lookup: 4 bytes, 2 bits per pixel
                    uint lookup = (uint)(data[blockOffset + 4] |
                                         (data[blockOffset + 5] << 8) |
                                         (data[blockOffset + 6] << 16) |
                                         (data[blockOffset + 7] << 24));

                    WriteBlockPixels(pixels, width, height, bx, by, palette, lookup, flipY);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ============================================
        // DXT3 DECODER
        // ============================================

        /// <summary>
        /// DXT3 (BC2) software decode.
        /// Her 4×4 blok = 16 byte: 8 byte explicit alpha (4 bit/pixel) + 8 byte DXT1 color block.
        /// DXT3 her zaman 4-color mode kullanır (color0/color1 karşılaştırması yok).
        /// </summary>
        private static Texture2D DecodeDXT3(byte[] data, int width, int height, bool flipY)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            int blocksX = Math.Max(1, width / 4);
            int blocksY = Math.Max(1, height / 4);

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int blockOffset = (by * blocksX + bx) * 16;
                    if (blockOffset + 16 > data.Length) break;

                    // Color endpoints (offset + 8)
                    ushort c0 = (ushort)(data[blockOffset + 8] | (data[blockOffset + 9] << 8));
                    ushort c1 = (ushort)(data[blockOffset + 10] | (data[blockOffset + 11] << 8));

                    DecodeR5G6B5(c0, out byte r0, out byte g0, out byte b0);
                    DecodeR5G6B5(c1, out byte r1, out byte g1, out byte b1);

                    // DXT3: her zaman 4-color interpolation
                    var palette = new Color32[4];
                    palette[0] = new Color32(r0, g0, b0, 255);
                    palette[1] = new Color32(r1, g1, b1, 255);
                    palette[2] = new Color32(
                        (byte)((2 * r0 + r1 + 1) / 3),
                        (byte)((2 * g0 + g1 + 1) / 3),
                        (byte)((2 * b0 + b1 + 1) / 3), 255);
                    palette[3] = new Color32(
                        (byte)((r0 + 2 * r1 + 1) / 3),
                        (byte)((g0 + 2 * g1 + 1) / 3),
                        (byte)((b0 + 2 * b1 + 1) / 3), 255);

                    // Color lookup (offset + 12)
                    uint lookup = (uint)(data[blockOffset + 12] |
                                         (data[blockOffset + 13] << 8) |
                                         (data[blockOffset + 14] << 16) |
                                         (data[blockOffset + 15] << 24));

                    // Alpha + color yazım
                    for (int row = 0; row < 4; row++)
                    {
                        // Alpha row: 2 bytes, 4 bits per pixel (explicit alpha)
                        ushort alphaRow = (ushort)(data[blockOffset + row * 2] |
                                                   (data[blockOffset + row * 2 + 1] << 8));

                        for (int col = 0; col < 4; col++)
                        {
                            int pixelX = bx * 4 + col;
                            int pixelY = by * 4 + row;
                            if (pixelX >= width || pixelY >= height) continue;

                            int outY = flipY ? (height - 1 - pixelY) : pixelY;
                            byte alpha = (byte)(((alphaRow >> (col * 4)) & 0xF) * 255 / 15);
                            int colorIdx = (int)((lookup >> ((row * 4 + col) * 2)) & 0x3);

                            pixels[outY * width + pixelX] = new Color32(
                                palette[colorIdx].r,
                                palette[colorIdx].g,
                                palette[colorIdx].b,
                                alpha);
                        }
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ============================================
        // DXT5 DECODER
        // ============================================

        /// <summary>
        /// DXT5 (BC3) software decode.
        /// Her 4×4 blok = 16 byte:
        ///   2 byte alpha endpoints (alpha0, alpha1)
        ///   6 byte alpha lookup (3 bit/pixel × 16 pixel = 48 bit)
        ///   8 byte DXT1 color block
        /// </summary>
        private static Texture2D DecodeDXT5(byte[] data, int width, int height, bool flipY)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            int blocksX = Math.Max(1, width / 4);
            int blocksY = Math.Max(1, height / 4);

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int blockOffset = (by * blocksX + bx) * 16;
                    if (blockOffset + 16 > data.Length) break;

                    // Alpha endpoints
                    byte alpha0 = data[blockOffset];
                    byte alpha1 = data[blockOffset + 1];

                    // Alpha palette (8 değer)
                    var alphaPalette = new byte[8];
                    alphaPalette[0] = alpha0;
                    alphaPalette[1] = alpha1;

                    if (alpha0 > alpha1)
                    {
                        // 8 interpolated alpha values
                        alphaPalette[2] = (byte)((6 * alpha0 + 1 * alpha1 + 3) / 7);
                        alphaPalette[3] = (byte)((5 * alpha0 + 2 * alpha1 + 3) / 7);
                        alphaPalette[4] = (byte)((4 * alpha0 + 3 * alpha1 + 3) / 7);
                        alphaPalette[5] = (byte)((3 * alpha0 + 4 * alpha1 + 3) / 7);
                        alphaPalette[6] = (byte)((2 * alpha0 + 5 * alpha1 + 3) / 7);
                        alphaPalette[7] = (byte)((1 * alpha0 + 6 * alpha1 + 3) / 7);
                    }
                    else
                    {
                        // 6 interpolated alpha values + 0 and 255
                        alphaPalette[2] = (byte)((4 * alpha0 + 1 * alpha1 + 2) / 5);
                        alphaPalette[3] = (byte)((3 * alpha0 + 2 * alpha1 + 2) / 5);
                        alphaPalette[4] = (byte)((2 * alpha0 + 3 * alpha1 + 2) / 5);
                        alphaPalette[5] = (byte)((1 * alpha0 + 4 * alpha1 + 2) / 5);
                        alphaPalette[6] = 0;
                        alphaPalette[7] = 255;
                    }

                    // Alpha lookup: 6 bytes = 48 bits, 3 bits per pixel
                    // Packed as: byte[2..7], LSB first
                    ulong alphaLookup = 0;
                    for (int i = 0; i < 6; i++)
                        alphaLookup |= (ulong)data[blockOffset + 2 + i] << (8 * i);

                    // Color endpoints (offset + 8)
                    ushort c0 = (ushort)(data[blockOffset + 8] | (data[blockOffset + 9] << 8));
                    ushort c1 = (ushort)(data[blockOffset + 10] | (data[blockOffset + 11] << 8));

                    DecodeR5G6B5(c0, out byte r0, out byte g0, out byte b0);
                    DecodeR5G6B5(c1, out byte r1, out byte g1, out byte b1);

                    // DXT5: her zaman 4-color interpolation
                    var palette = new Color32[4];
                    palette[0] = new Color32(r0, g0, b0, 255);
                    palette[1] = new Color32(r1, g1, b1, 255);
                    palette[2] = new Color32(
                        (byte)((2 * r0 + r1 + 1) / 3),
                        (byte)((2 * g0 + g1 + 1) / 3),
                        (byte)((2 * b0 + b1 + 1) / 3), 255);
                    palette[3] = new Color32(
                        (byte)((r0 + 2 * r1 + 1) / 3),
                        (byte)((g0 + 2 * g1 + 1) / 3),
                        (byte)((b0 + 2 * b1 + 1) / 3), 255);

                    // Color lookup (offset + 12)
                    uint lookup = (uint)(data[blockOffset + 12] |
                                         (data[blockOffset + 13] << 8) |
                                         (data[blockOffset + 14] << 16) |
                                         (data[blockOffset + 15] << 24));

                    // Alpha + color yazım
                    for (int row = 0; row < 4; row++)
                    {
                        for (int col = 0; col < 4; col++)
                        {
                            int pixelX = bx * 4 + col;
                            int pixelY = by * 4 + row;
                            if (pixelX >= width || pixelY >= height) continue;

                            int outY = flipY ? (height - 1 - pixelY) : pixelY;
                            // Alpha index: 3 bits, row-major order
                            int alphaIdx = (int)((alphaLookup >> ((row * 4 + col) * 3)) & 0x7);
                            byte alpha = alphaPalette[alphaIdx];

                            int colorIdx = (int)((lookup >> ((row * 4 + col) * 2)) & 0x3);

                            pixels[outY * width + pixelX] = new Color32(
                                palette[colorIdx].r,
                                palette[colorIdx].g,
                                palette[colorIdx].b,
                                alpha);
                        }
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ============================================
        // YARDIMCI METODLAR
        // ============================================

        /// <summary>R5G6B5 → RGB888 decode.</summary>
        private static void DecodeR5G6B5(ushort color, out byte r, out byte g, out byte b)
        {
            r = (byte)(((color >> 11) & 0x1F) * 255 / 31);
            g = (byte)(((color >> 5) & 0x3F) * 255 / 63);
            b = (byte)((color & 0x1F) * 255 / 31);
        }

        /// <summary>
        /// 4×4 block pixel'lerini output array'e yazar (DXT1 için).
        /// Unity Y-flip uygulanır (D3D top-to-bottom → Unity bottom-to-top).
        /// </summary>
        private static void WriteBlockPixels(
            Color32[] pixels, int width, int height,
            int bx, int by, Color32[] palette, uint lookup, bool flipY)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int pixelX = bx * 4 + col;
                    int pixelY = by * 4 + row;
                    if (pixelX >= width || pixelY >= height) continue;

                    int outY = flipY ? (height - 1 - pixelY) : pixelY;
                    int colorIdx = (int)((lookup >> ((row * 4 + col) * 2)) & 0x3);
                    pixels[outY * width + pixelX] = palette[colorIdx];
                }
            }
        }

        // ============================================
        // UNCOMPRESSED FORMAT OKUMA (değiştirilmedi)
        // ============================================

        /// <summary>
        /// Sıkıştırılmamış format okuma.
        /// </summary>
        private static Texture2D LoadUncompressed(
            BinaryReader reader, int width, int height, int format, bool hasMipMap, bool flipY)
        {
            int pixelSize;
            if (format == D3DFMT_A1R5G5B5 || format == D3DFMT_A4R4G4B4)
                pixelSize = 2;
            else if (format == D3DFMT_R8G8B8)
                pixelSize = 3;
            else if (format == D3DFMT_A8R8G8B8 || format == D3DFMT_X8R8G8B8)
                pixelSize = 4;
            else
            {
                Debug.LogWarning($"[DXT] Desteklenmeyen format: 0x{format:X}");
                return null;
            }

            if (pixelSize == 2)
                return Read16BitTexture(reader, width, height, format, flipY);
            else if (pixelSize == 4)
                return Read32BitTexture(reader, width, height, flipY);
            else
                return Read24BitTexture(reader, width, height, flipY);
        }

        /// <summary>
        /// 16-bit pixel verisinden Texture2D oluştur.
        /// A1R5G5B5 (fmt=25) veya A4R4G4B4 (fmt=26) format desteği.
        /// </summary>
        private static Texture2D Read16BitTexture(BinaryReader reader, int w, int h, int format = D3DFMT_A1R5G5B5, bool flipY = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];

            bool isA4R4G4B4 = (format == D3DFMT_A4R4G4B4);

            try
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        ushort pixel = reader.ReadUInt16();
                        byte r, g, b, a;

                        if (isA4R4G4B4)
                        {
                            // A4R4G4B4 decode: AAAA RRRR GGGG BBBB
                            a = (byte)(((pixel >> 12) & 0xF) * 255 / 15);
                            r = (byte)(((pixel >> 8) & 0xF) * 255 / 15);
                            g = (byte)(((pixel >> 4) & 0xF) * 255 / 15);
                            b = (byte)((pixel & 0xF) * 255 / 15);
                        }
                        else
                        {
                            // A1R5G5B5 decode
                            a = (byte)(((pixel >> 15) & 1) * 255);
                            r = (byte)(((pixel >> 10) & 0x1F) * 255 / 31);
                            g = (byte)(((pixel >> 5) & 0x1F) * 255 / 31);
                            b = (byte)((pixel & 0x1F) * 255 / 31);
                        }

                        int outY = flipY ? (h - 1 - y) : y;
                        pixels[outY * w + x] = new Color32(r, g, b, a);
                    }
                }
            }
            catch (EndOfStreamException)
            {
                Debug.LogWarning($"[DXT] 16-bit okuma erken sonlandı ({w}x{h})");
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            return tex;
        }

        private static Texture2D Read32BitTexture(BinaryReader reader, int w, int h, bool flipY = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];

            try
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        byte b = reader.ReadByte();
                        byte g = reader.ReadByte();
                        byte r = reader.ReadByte();
                        byte a = reader.ReadByte();

                        int outY = flipY ? (h - 1 - y) : y;
                        pixels[outY * w + x] = new Color32(r, g, b, a);
                    }
                }
            }
            catch (EndOfStreamException)
            {
                Debug.LogWarning($"[DXT] 32-bit okuma erken sonlandı");
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D Read24BitTexture(BinaryReader reader, int w, int h, bool flipY = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];

            try
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        byte b = reader.ReadByte();
                        byte g = reader.ReadByte();
                        byte r = reader.ReadByte();

                        int outY = flipY ? (h - 1 - y) : y;
                        pixels[outY * w + x] = new Color32(r, g, b, 255);
                    }
                }
            }
            catch (EndOfStreamException)
            {
                Debug.LogWarning($"[DXT] 24-bit okuma erken sonlandı");
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        /// <summary>
        /// CN3BaseFileAccess::Load() birebir portu.
        /// Format: int32 nameLen + char[nameLen] name
        /// nameLen=0 ise isim yok → hemen dön.
        /// </summary>
        private static void SkipN3Header(BinaryReader reader)
        {
            int nameLen = reader.ReadInt32();
            if (nameLen > 0 && nameLen <= 256)
            {
                reader.BaseStream.Seek(nameLen, SeekOrigin.Current);
            }
        }
    }
}
