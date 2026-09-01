using System;
using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// GTT (Ground Tile Texture) archive dosyalarından tile texture okur.
    /// Open-KO v1.298: CN3Terrain::LoadTileInfo (N3Terrain.cpp:524-596)
    ///
    /// GTT = Birden fazla NTF (DXT) texture'ın sıralı birleşimi.
    /// Her tile texture = 128×128 pixel.
    ///
    /// Okuma akışı:
    ///   1. tileIndex kadar NTF entry atla (SkipNtfEntry — CN3Texture::SkipFileHandle portu)
    ///   2. Hedef NTF entry'yi oku → Texture2D
    /// </summary>
    public static class GttTextureImporter
    {
        // D3DFORMAT values (DxtTextureImporter ile aynı)
        private const int D3DFMT_DXT1 = 0x31545844;
        private const int D3DFMT_DXT2 = 0x32545844;
        private const int D3DFMT_DXT3 = 0x33545844;
        private const int D3DFMT_DXT4 = 0x34545844;
        private const int D3DFMT_DXT5 = 0x35545844;
        private const int D3DFMT_A1R5G5B5 = 25;
        private const int D3DFMT_A4R4G4B4 = 26;
        private const int D3DFMT_R8G8B8 = 20;

        public static Texture2D LoadTile(string gttPath, int tileIndex)
        {
            // 1. Try virtual KOBinaryProvider first
            try
            {
                using var br = KOBinaryProvider.OpenReader(gttPath);
                if (br != null)
                {
                    for (int i = 0; i < tileIndex; i++)
                    {
                        if (!SkipNtfEntry(br)) return null;
                    }
                    return ReadNtfTexture(br);
                }
            }
            catch {}

            // 2. Try raw file from disk (e.g. ko-assets/...)
            try
            {
                string cleanPath = gttPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string[] parentDirs = { 
                    Path.Combine(Application.dataPath, "../../ko-assets"), // C:\_dev\knightonline-mobil\ko-assets
                    Path.Combine(Application.dataPath, "../ko-assets"),    // C:\_dev\knightonline-mobil\Client\ko-assets
                    "ko-assets"
                };

                string diskPath = null;
                foreach (var parentDir in parentDirs)
                {
                    string p1 = Path.Combine(parentDir, cleanPath);
                    if (File.Exists(p1)) { diskPath = p1; break; }

                    // Ground tiles are placed under 'DTex' directory in KO assets, but GTDs might point to 'Terrain'.
                    // So we always check the DTex directory case-insensitively.
                    string file = Path.GetFileName(cleanPath);
                    string p2 = Path.Combine(Path.Combine(parentDir, "DTex"), file);
                    if (File.Exists(p2)) { diskPath = p2; break; }
                    string p3 = Path.Combine(Path.Combine(parentDir, "dtex"), file);
                    if (File.Exists(p3)) { diskPath = p3; break; }
                }

                if (!string.IsNullOrEmpty(diskPath) && File.Exists(diskPath))
                {
                    using var fs = File.OpenRead(diskPath);
                    using var brDisk = new BinaryReader(fs);

                    for (int i = 0; i < tileIndex; i++)
                    {
                        if (!SkipNtfEntry(brDisk)) return null;
                    }
                    return ReadNtfTexture(brDisk);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GTT Disk fallback] Hata: {gttPath} index={tileIndex} — {ex.Message}");
            }

            Debug.LogWarning($"[GTT] Dosya bulunamadı veya açılmadı: {gttPath}");
            return null;
        }

        /// <summary>
        /// Bir NTF entry'yi dosyada atlar.
        /// Open-KO: CN3Texture::SkipFileHandle (N3Texture.cpp:589-677) birebir portu.
        /// </summary>
        private static bool SkipNtfEntry(BinaryReader br)
        {
            // 1. N3BaseFileAccess header (int32 nameLen + name)
            int nameLen = br.ReadInt32();
            if (nameLen > 0 && nameLen <= 512)
                br.BaseStream.Seek(nameLen, SeekOrigin.Current);

            // 2. __DXT_HEADER (20 bytes)
            byte id0 = br.ReadByte(); // 'N'
            byte id1 = br.ReadByte(); // 'T'
            byte id2 = br.ReadByte(); // 'F'
            byte id3 = br.ReadByte(); // version
            if (id0 != 'N' || id1 != 'T' || id2 != 'F')
                return false;

            int width = br.ReadInt32();
            int height = br.ReadInt32();
            int format = br.ReadInt32();
            int bMipMap = br.ReadInt32();

            // 3. Skip boyutu hesapla
            bool isDXT = (format == D3DFMT_DXT1 || format == D3DFMT_DXT2 ||
                          format == D3DFMT_DXT3 || format == D3DFMT_DXT4 ||
                          format == D3DFMT_DXT5);

            int skipSize = 0;

            if (isDXT)
            {
                if (bMipMap != 0)
                {
                    // Compressed mip chain (N3Texture.cpp:614-622)
                    int w = width, h = height;
                    for (; w >= 4 && h >= 4; w /= 2, h /= 2)
                    {
                        if (format == D3DFMT_DXT1)
                            skipSize += w * h / 2;
                        else
                            skipSize += w * h;
                    }
                    // Fallback 16-bit mips (N3Texture.cpp:624-629)
                    w = width / 2;
                    h = height / 2;
                    for (; w >= 4 && h >= 4; w /= 2, h /= 2)
                        skipSize += w * h * 2;
                }
                else
                {
                    // No mipmap (N3Texture.cpp:631-643)
                    if (format == D3DFMT_DXT1)
                        skipSize += width * height / 2;
                    else
                        skipSize += width * height;
                    // Fallback
                    skipSize += width * height * 2;
                    if (width >= 1024)
                        skipSize += 256 * 256 * 2;
                }
            }
            else
            {
                // Uncompressed (N3Texture.cpp:648-675)
                int pixelSize;
                if (format == D3DFMT_A1R5G5B5 || format == D3DFMT_A4R4G4B4) pixelSize = 2;
                else if (format == D3DFMT_R8G8B8) pixelSize = 3;
                else pixelSize = 4;

                if (bMipMap != 0)
                {
                    int w = width, h = height;
                    for (; w >= 4 && h >= 4; w /= 2, h /= 2)
                        skipSize += w * h * pixelSize;
                }
                else
                {
                    skipSize += width * height * pixelSize;
                    if (width >= 512)
                        skipSize += 256 * 256 * 2;
                }
            }

            br.BaseStream.Seek(skipSize, SeekOrigin.Current);
            return true;
        }

        /// <summary>
        /// BinaryReader'ın mevcut konumundan bir NTF texture okur.
        /// DxtTextureImporter.Load() ile aynı mantık, ama dosya yerine reader'dan.
        /// </summary>
        private static Texture2D ReadNtfTexture(BinaryReader br)
        {
            // N3BaseFileAccess header
            int nameLen = br.ReadInt32();
            if (nameLen > 0 && nameLen <= 512)
                br.BaseStream.Seek(nameLen, SeekOrigin.Current);

            // __DXT_HEADER
            byte id0 = br.ReadByte();
            byte id1 = br.ReadByte();
            byte id2 = br.ReadByte();
            byte id3 = br.ReadByte();
            if (id0 != 'N' || id1 != 'T' || id2 != 'F')
                return null;

            int width = br.ReadInt32();
            int height = br.ReadInt32();
            int format = br.ReadInt32();
            int bMipMap = br.ReadInt32();

            if (width <= 0 || height <= 0 || width > 4096 || height > 4096)
                return null;

            bool isDXT = (format == D3DFMT_DXT1 || format == D3DFMT_DXT2 ||
                          format == D3DFMT_DXT3 || format == D3DFMT_DXT4 ||
                          format == D3DFMT_DXT5);

            if (!isDXT)
            {
                // Tile texture'lar genellikle DXT. Non-DXT desteklemiyoruz şimdilik.
                Debug.LogWarning($"[GTT] Non-DXT tile format: 0x{format:X}");
                return null;
            }

            // Level 0 boyutu
            int level0Size;
            if (format == D3DFMT_DXT1)
                level0Size = Math.Max(width * height / 2, 8);
            else
                level0Size = Math.Max(width * height, 16);

            byte[] data = br.ReadBytes(level0Size);

            // Mipmap varsa geri kalan mip level'ları atla
            if (bMipMap != 0)
            {
                int w = width / 2, h = height / 2;
                for (; w >= 4 && h >= 4; w /= 2, h /= 2)
                {
                    int mipSize = (format == D3DFMT_DXT1) ? w * h / 2 : w * h;
                    br.BaseStream.Seek(mipSize, SeekOrigin.Current);
                }
                // Fallback 16-bit mips
                w = width / 2; h = height / 2;
                for (; w >= 4 && h >= 4; w /= 2, h /= 2)
                    br.BaseStream.Seek(w * h * 2, SeekOrigin.Current);
            }
            else
            {
                // No mipmap — fallback data skip
                br.BaseStream.Seek(width * height * 2, SeekOrigin.Current);
                if (width >= 1024)
                    br.BaseStream.Seek(256 * 256 * 2, SeekOrigin.Current);
            }

            // DXT decode — DxtTextureImporter ile aynı decode mantığı
            Texture2D tex;
            if (format == D3DFMT_DXT1)
                tex = DecodeDXT1(data, width, height);
            else if (format == D3DFMT_DXT3 || format == D3DFMT_DXT2)
                tex = DecodeDXT3(data, width, height);
            else
                tex = DecodeDXT5(data, width, height);

            return tex;
        }

        // ============================================
        // DXT DECODE — DxtTextureImporter'dan kopyalanmış (flipY=false, world texture)
        // ============================================

        private static Texture2D DecodeDXT1(byte[] data, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            int blocksX = Math.Max(1, width / 4);
            int blocksY = Math.Max(1, height / 4);

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int off = (by * blocksX + bx) * 8;
                    if (off + 8 > data.Length) break;

                    ushort c0 = (ushort)(data[off] | (data[off + 1] << 8));
                    ushort c1 = (ushort)(data[off + 2] | (data[off + 3] << 8));
                    DecodeR5G6B5(c0, out byte r0, out byte g0, out byte b0);
                    DecodeR5G6B5(c1, out byte r1, out byte g1, out byte b1);

                    var pal = new Color32[4];
                    pal[0] = new Color32(r0, g0, b0, 255);
                    pal[1] = new Color32(r1, g1, b1, 255);
                    if (c0 > c1)
                    {
                        pal[2] = new Color32((byte)((2*r0+r1+1)/3), (byte)((2*g0+g1+1)/3), (byte)((2*b0+b1+1)/3), 255);
                        pal[3] = new Color32((byte)((r0+2*r1+1)/3), (byte)((g0+2*g1+1)/3), (byte)((b0+2*b1+1)/3), 255);
                    }
                    else
                    {
                        pal[2] = new Color32((byte)((r0+r1)/2), (byte)((g0+g1)/2), (byte)((b0+b1)/2), 255);
                        pal[3] = new Color32(0, 0, 0, 0);
                    }

                    uint lookup = (uint)(data[off+4] | (data[off+5]<<8) | (data[off+6]<<16) | (data[off+7]<<24));
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                        {
                            int px = bx*4+col, py = by*4+row;
                            if (px >= width || py >= height) continue;
                            int outY = py; // No flipY: TileDirU/V tablosu DirectX V-konvansiyonunda çalışır
                            int ci = (int)((lookup >> ((row*4+col)*2)) & 0x3);
                            pixels[outY * width + px] = pal[ci];
                        }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D DecodeDXT3(byte[] data, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            int blocksX = Math.Max(1, width / 4);
            int blocksY = Math.Max(1, height / 4);

            for (int by = 0; by < blocksY; by++)
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int off = (by * blocksX + bx) * 16;
                    if (off + 16 > data.Length) break;
                    ushort c0 = (ushort)(data[off+8] | (data[off+9]<<8));
                    ushort c1 = (ushort)(data[off+10] | (data[off+11]<<8));
                    DecodeR5G6B5(c0, out byte r0, out byte g0, out byte b0);
                    DecodeR5G6B5(c1, out byte r1, out byte g1, out byte b1);
                    var pal = new Color32[4];
                    pal[0] = new Color32(r0, g0, b0, 255);
                    pal[1] = new Color32(r1, g1, b1, 255);
                    pal[2] = new Color32((byte)((2*r0+r1+1)/3),(byte)((2*g0+g1+1)/3),(byte)((2*b0+b1+1)/3),255);
                    pal[3] = new Color32((byte)((r0+2*r1+1)/3),(byte)((g0+2*g1+1)/3),(byte)((b0+2*b1+1)/3),255);
                    uint lookup = (uint)(data[off+12]|(data[off+13]<<8)|(data[off+14]<<16)|(data[off+15]<<24));
                    for (int row = 0; row < 4; row++)
                    {
                        ushort alphaRow = (ushort)(data[off+row*2] | (data[off+row*2+1]<<8));
                        for (int col = 0; col < 4; col++)
                        {
                            int px = bx*4+col, py = by*4+row;
                            if (px >= width || py >= height) continue;
                            int outY = py; // No flipY: TileDirU/V tablosu DirectX V-konvansiyonunda çalışır
                            byte alpha = (byte)(((alphaRow >> (col*4)) & 0xF) * 255 / 15);
                            int ci = (int)((lookup >> ((row*4+col)*2)) & 0x3);
                            pixels[outY*width+px] = new Color32(pal[ci].r, pal[ci].g, pal[ci].b, alpha);
                        }
                    }
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D DecodeDXT5(byte[] data, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            int blocksX = Math.Max(1, width / 4);
            int blocksY = Math.Max(1, height / 4);

            for (int by = 0; by < blocksY; by++)
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int off = (by * blocksX + bx) * 16;
                    if (off + 16 > data.Length) break;
                    byte a0 = data[off], a1 = data[off+1];
                    var ap = new byte[8];
                    ap[0] = a0; ap[1] = a1;
                    if (a0 > a1)
                    { ap[2]=(byte)((6*a0+a1+3)/7); ap[3]=(byte)((5*a0+2*a1+3)/7); ap[4]=(byte)((4*a0+3*a1+3)/7); ap[5]=(byte)((3*a0+4*a1+3)/7); ap[6]=(byte)((2*a0+5*a1+3)/7); ap[7]=(byte)((a0+6*a1+3)/7); }
                    else
                    { ap[2]=(byte)((4*a0+a1+2)/5); ap[3]=(byte)((3*a0+2*a1+2)/5); ap[4]=(byte)((2*a0+3*a1+2)/5); ap[5]=(byte)((a0+4*a1+2)/5); ap[6]=0; ap[7]=255; }
                    ulong aLookup = 0;
                    for (int i = 0; i < 6; i++) aLookup |= (ulong)data[off+2+i] << (8*i);
                    ushort c0 = (ushort)(data[off+8]|(data[off+9]<<8));
                    ushort c1 = (ushort)(data[off+10]|(data[off+11]<<8));
                    DecodeR5G6B5(c0, out byte r0, out byte g0, out byte b0);
                    DecodeR5G6B5(c1, out byte r1, out byte g1, out byte b1);
                    var pal = new Color32[4];
                    pal[0] = new Color32(r0,g0,b0,255); pal[1] = new Color32(r1,g1,b1,255);
                    pal[2] = new Color32((byte)((2*r0+r1+1)/3),(byte)((2*g0+g1+1)/3),(byte)((2*b0+b1+1)/3),255);
                    pal[3] = new Color32((byte)((r0+2*r1+1)/3),(byte)((g0+2*g1+1)/3),(byte)((b0+2*b1+1)/3),255);
                    uint lookup = (uint)(data[off+12]|(data[off+13]<<8)|(data[off+14]<<16)|(data[off+15]<<24));
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                        {
                            int px = bx*4+col, py = by*4+row;
                            if (px >= width || py >= height) continue;
                            int outY = py; // No flipY: TileDirU/V tablosu DirectX V-konvansiyonunda çalışır
                            int ai = (int)((aLookup >> ((row*4+col)*3)) & 0x7);
                            int ci = (int)((lookup >> ((row*4+col)*2)) & 0x3);
                            pixels[outY*width+px] = new Color32(pal[ci].r, pal[ci].g, pal[ci].b, ap[ai]);
                        }
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static void DecodeR5G6B5(ushort c, out byte r, out byte g, out byte b)
        {
            r = (byte)(((c >> 11) & 0x1F) * 255 / 31);
            g = (byte)(((c >> 5) & 0x3F) * 255 / 63);
            b = (byte)((c & 0x1F) * 255 / 31);
        }
    }
}
