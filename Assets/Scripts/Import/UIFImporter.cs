using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO v1.298 .uif (UI File) Binary Parser
    /// 
    /// BIREBIR C++ kaynak kodundan portlanmıştır:
    ///   CN3BaseFileAccess::Load → int32 nameLen + char[] name
    ///   CN3UIBase::Load → childCount + recursive children + base info
    ///   derived::Load → type-specific extra data
    /// 
    /// Call chain per child node:
    ///   1. Parent reads uint32 eUI_TYPE
    ///   2. Child CN3BaseFileAccess::Load → nameLen + name
    ///   3. Child CN3UIBase::Load → children recursive + ID + region + movable + style + reserved + tooltip + openSnd + closeSnd
    ///   4. Child type-specific Load data (Image, Button, etc.)
    /// </summary>
    public static class UIFImporter
    {
        #region Data Structures

        public enum UIType : uint
        {
            Base = 0,
            Button = 1,
            Static = 2,
            Progress = 3,
            Image = 4,
            ScrollBar = 5,
            String = 6,
            TrackBar = 7,
            Edit = 8,
            Area = 9,
            Tooltip = 10,
            Icon = 11,
            IconManager = 12,
            IconSlot = 13,
            List = 14,
            Unknown = 0xFFFFFFFF
        }

        public struct Rect
        {
            public int Left, Top, Right, Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
            public override string ToString() => $"({Left},{Top},{Right},{Bottom})";
        }

        public struct FloatRect
        {
            public float Left, Top, Right, Bottom;
            public override string ToString() => $"({Left:F3},{Top:F3},{Right:F3},{Bottom:F3})";
        }

        /// <summary>Tüm UI element'ler için ortak veri.</summary>
        public class UIFNode
        {
            // Base data
            public string Name;           // CN3BaseFileAccess name
            public string ID;             // UI element ID (ör: "img_hp", "btn_zoom")
            public UIType Type;
            public Rect Region;           // Screen space bounding box
            public Rect Movable;          // Draggable area
            public uint Style;
            public uint Reserved;
            public string Tooltip;

            // Children
            public List<UIFNode> Children = new();

            // Image-specific
            public string TextureFileName;
            public FloatRect UVRect;
            public float AnimFrame;

            // String-specific  
            public string FontName;
            public int FontSize;
            public uint FontFlags;
            public uint FontColor;       // D3DCOLOR (ARGB)
            public string Text;

            // Area-specific (CN3UIArea::Load — N3UIArea.cpp:45)
            public int AreaType = -1;  // eUI_AREA_TYPE (N3UIArea.h:13-29)

            // Debug
            public int Depth;
        }

        #endregion


        #region Public API

        /// <summary>
        /// .uif dosyasını parse eder.
        /// </summary>
        public static UIFNode Load(string uifPath)
        {
            if (!KOBinaryProvider.Exists(uifPath))
            {
                Debug.LogError($"[UIFImporter] Dosya bulunamadı: {uifPath}");
                return null;
            }

            try
            {
                using var fs = File.OpenRead(uifPath);
                using var reader = new BinaryReader(fs, Encoding.ASCII);

                long fileSize = fs.Length;
                Trace($"=== UIF PARSE START: {Path.GetFileName(uifPath)} ({fileSize} bytes) ===");

                var root = new UIFNode();
                root.Type = UIType.Base;
                root.Depth = 0;

                // CN3BaseFileAccess::Load — root name
                root.Name = ReadName(reader, "root");

                // CN3UIBase::Load — recursive parse (name already read)
                ReadUIBase(reader, root, 0);

                // Root has NO type-specific data (it's always UIType.Base)

                // Diagnostic
                int totalNodes = CountNodes(root);

                LogNodeTree(root, 0, 8);

                return root;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIFImporter] Parse hatası '{uifPath}': {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        #endregion

        #region Binary Parsing — CN3UIBase::Load

        /// <summary>
        /// CN3UIBase::Load — v1264 format.
        /// Assumes CN3BaseFileAccess::Load (name) has already been called.
        /// Reads: childCount → recursive children → base info (ID, region, movable, style, reserved, tooltip, sounds)
        /// </summary>
        private static void ReadUIBase(BinaryReader reader, UIFNode node, int depth)
        {
            long posStart = reader.BaseStream.Position;

            // --- Child count (v1264: int16 + int16 unknown) ---
            short sCC = reader.ReadInt16();
            short sUnk = reader.ReadInt16();
            int childCount = sCC;

            Trace($"[d{depth}] ReadUIBase pos={posStart} childCount={childCount} unk={sUnk}");

            if (childCount < 0 || childCount > 4096)
                throw new Exception($"Geçersiz child count: {childCount} at pos={posStart}");

            // --- Recursive children ---
            for (int i = 0; i < childCount; i++)
            {
                long childStartPos = reader.BaseStream.Position;

                // 1. Read child UI type (uint32)
                uint typeVal = reader.ReadUInt32();
                var childType = (UIType)typeVal;

                Trace($"[d{depth}] child[{i}] type={childType}({typeVal}) at pos={childStartPos}");

                var child = new UIFNode();
                child.Type = childType;
                child.Depth = depth + 1;

                // 2. CN3BaseFileAccess::Load — child name
                child.Name = ReadName(reader, $"child[{i}]");

                // 3. CN3UIBase::Load — recursive (name already read)
                ReadUIBase(reader, child, depth + 1);

                // 4. Type-specific data (CN3UIImage::Load extra, CN3UIButton::Load extra, etc.)
                ReadTypeSpecificData(reader, child, depth + 1);

                node.Children.Add(child);

                Trace($"[d{depth}] child[{i}] done at pos={reader.BaseStream.Position}");
            }

            // --- Base info (after all children are loaded) ---
            long baseInfoStart = reader.BaseStream.Position;

            // ID string (int32 len + chars)
            node.ID = ReadString32(reader, "ID");

            // Region & Movable (RECT = 4 x int32 each)
            node.Region = ReadRect(reader);
            node.Movable = ReadRect(reader);

            // Style & Reserved
            node.Style = reader.ReadUInt32();
            node.Reserved = reader.ReadUInt32();

            // Tooltip
            node.Tooltip = ReadString32(reader, "tooltip");

            // Open sound filename
            ReadString32(reader, "openSnd"); // skip

            // Close sound filename
            ReadString32(reader, "closeSnd"); // skip

            Trace($"[d{depth}] baseInfo: id='{node.ID}' region={node.Region} style=0x{node.Style:X} reserved={node.Reserved} endPos={reader.BaseStream.Position}");
        }

        #endregion

        #region Type-Specific Data Readers

        /// <summary>Her UI type'ın CN3UIBase::Load'dan SONRA okunan ek verisini okur.</summary>
        private static void ReadTypeSpecificData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            switch (node.Type)
            {
                case UIType.Image:
                    // CN3UIImage::Load extra: texFN + UV rect + animFrame
                    ReadImageData(reader, node, depth);
                    break;

                case UIType.Button:
                    // CN3UIButton::Load extra: rcClick + sndOn + sndClick
                    ReadButtonData(reader, node, depth);
                    break;

                case UIType.Static:
                    // CN3UIStatic::Load extra: sndClick
                    ReadStaticData(reader, node, depth);
                    break;

                case UIType.String:
                    // CN3UIString::Load extra: font info + color + text + lineSpacing
                    ReadStringData(reader, node, depth);
                    break;

                case UIType.Edit:
                    // CN3UIEdit::Load → CN3UIStatic::Load extra + sndTyping
                    // Edit inherits from Static, so Static's Load is called first
                    ReadEditData(reader, node, depth);
                    break;

                case UIType.Area:
                    // CN3UIArea::Load extra: areaType (int32)
                    ReadAreaData(reader, node, depth);
                    break;

                case UIType.List:
                    // CN3UIList::Load extra: font info
                    ReadListData(reader, node, depth);
                    break;

                case UIType.Progress:
                    // CN3UIProgress::Load — NO extra data (just sets refs from children)
                    Trace($"[d{depth}] Progress: no extra data");
                    break;

                case UIType.ScrollBar:
                    // CN3UIScrollBar::Load — NO extra data (just sets refs from children)
                    Trace($"[d{depth}] ScrollBar: no extra data");
                    break;

                case UIType.TrackBar:
                    // CN3UITrackBar::Load — NO extra data (just sets refs from children)
                    Trace($"[d{depth}] TrackBar: no extra data");
                    break;

                case UIType.Tooltip:
                    // CN3UITooltip — NO extra data
                    Trace($"[d{depth}] Tooltip: no extra data");
                    break;

                case UIType.Base:
                    // Base — NO extra data
                    Trace($"[d{depth}] Base: no extra data");
                    break;

                default:
                    Debug.LogWarning($"[UIFImporter] Bilinmeyen UI type: {node.Type} at pos={pos}");
                    break;
            }
        }

        /// <summary>
        /// CN3UIImage::Load extra data.
        /// C++: texFNLen(int32) + texFN(string) + UV rect(4 floats) + animFrame(float)
        /// </summary>
        private static void ReadImageData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            // Texture filename
            node.TextureFileName = ReadString32(reader, "texFN");

            // UV rect (4 floats: left, top, right, bottom)
            node.UVRect = new FloatRect
            {
                Left = reader.ReadSingle(),
                Top = reader.ReadSingle(),
                Right = reader.ReadSingle(),
                Bottom = reader.ReadSingle()
            };

            // Anim frame
            node.AnimFrame = reader.ReadSingle();

            Trace($"[d{depth}] Image: tex='{node.TextureFileName}' uv={node.UVRect} animFrame={node.AnimFrame} endPos={reader.BaseStream.Position}");
        }

        /// <summary>
        /// CN3UIButton::Load extra data.
        /// C++: RECT rcClick(16b) + sndOn(int32+str) + sndClick(int32+str)
        /// </summary>
        private static void ReadButtonData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            // RECT m_rcClick (4 x int32 = 16 bytes)
            reader.ReadBytes(16); // skip click rect

            // On-hover sound filename
            ReadString32(reader, "btn_sndOn");

            // Click sound filename
            ReadString32(reader, "btn_sndClick");

            Trace($"[d{depth}] Button: endPos={reader.BaseStream.Position}");
        }

        /// <summary>
        /// CN3UIStatic::Load extra data.
        /// C++: sndClick(int32+str)
        /// </summary>
        private static void ReadStaticData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            // Click sound filename
            ReadString32(reader, "static_sndClick");

            Trace($"[d{depth}] Static: endPos={reader.BaseStream.Position}");
        }

        /// <summary>
        /// CN3UIString::Load extra data.
        /// C++:
        ///   fontNameLen(int32) 
        ///   if fontNameLen > 0: fontName(string) + fontHeight(uint32) + fontFlags(uint32)
        ///   color(uint32) — ALWAYS read
        ///   textLen(int32) + text(string) — ALWAYS read
        ///   if v1264: lineSpacing(int32) — ALWAYS read
        /// </summary>
        private static void ReadStringData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            // Font name length
            int fontNameLen = reader.ReadInt32();
            Trace($"[d{depth}] String: fontNameLen={fontNameLen} at pos={pos}");

            if (fontNameLen < 0 || fontNameLen > 32)
                throw new Exception($"String: invalid fontNameLen={fontNameLen} at pos={pos}");

            if (fontNameLen > 0)
            {
                // Font name
                node.FontName = Encoding.ASCII.GetString(reader.ReadBytes(fontNameLen));

                // Font height (uint32)
                uint fontHeight = reader.ReadUInt32();
                node.FontSize = (int)fontHeight;

                // Font flags (uint32 — bold/italic)
                node.FontFlags = reader.ReadUInt32();
            }
            else
            {
                node.FontName = "";
            }

            // Color (D3DCOLOR = uint32 ARGB) — ALWAYS read
            node.FontColor = reader.ReadUInt32();

            // Text string — ALWAYS read
            node.Text = ReadString32(reader, "string_text");

            // Line spacing (v1264 only) — ALWAYS read for v1264 format
            int lineSpacing = reader.ReadInt32();

            Trace($"[d{depth}] String: font='{node.FontName}' size={node.FontSize} color=0x{node.FontColor:X8} text='{node.Text}' lineSpacing={lineSpacing} endPos={reader.BaseStream.Position}");
        }

        /// <summary>
        /// CN3UIEdit::Load extra data.
        /// Edit inherits from Static. Its Load calls CN3UIStatic::Load first, then reads its own data.
        /// C++:
        ///   CN3UIStatic::Load extra → sndClick(int32+str)
        ///   sndTyping(int32+str)
        /// </summary>
        private static void ReadEditData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            // Static's extra data first (sndClick)
            ReadString32(reader, "edit_static_sndClick");

            // Edit's own extra data (sndTyping)
            ReadString32(reader, "edit_sndTyping");

            Trace($"[d{depth}] Edit: endPos={reader.BaseStream.Position}");
        }

        /// <summary>
        /// CN3UIArea::Load extra data.
        /// C++: areaType(int32)
        /// </summary>
        private static void ReadAreaData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            // Open-KO birebir: CN3UIArea::Load (N3UIArea.cpp:44-50)
            // file.Read(&iAreaType, sizeof(int));
            // m_eAreaType = (eUI_AREA_TYPE) iAreaType;
            int areaType = reader.ReadInt32();
            node.AreaType = areaType;

            Trace($"[d{depth}] Area: areaType={areaType} endPos={reader.BaseStream.Position}");
        }

        /// <summary>
        /// CN3UIList::Load extra data.
        /// C++:
        ///   fontNameLen(int32)
        ///   if fontNameLen > 0: fontName(string) + fontHeight(4b) + fontColor(4b) + fontBold(4b BOOL) + fontItalic(4b BOOL)
        /// </summary>
        private static void ReadListData(BinaryReader reader, UIFNode node, int depth)
        {
            long pos = reader.BaseStream.Position;

            int fontNameLen = reader.ReadInt32();
            Trace($"[d{depth}] List: fontNameLen={fontNameLen} at pos={pos}");

            if (fontNameLen < 0 || fontNameLen > 32)
                throw new Exception($"List: invalid fontNameLen={fontNameLen} at pos={pos}");

            if (fontNameLen > 0)
            {
                reader.ReadBytes(fontNameLen); // skip font name
                reader.ReadUInt32(); // fontHeight
                reader.ReadUInt32(); // fontColor
                reader.ReadUInt32(); // fontBold (BOOL = 4 bytes)
                reader.ReadUInt32(); // fontItalic (BOOL = 4 bytes)
            }

            Trace($"[d{depth}] List: endPos={reader.BaseStream.Position}");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// CN3BaseFileAccess::Load — reads int32 nameLen + name string.
        /// C++: nL = 0 → return immediately (no name). nL > 256 → error.
        /// </summary>
        private static string ReadName(BinaryReader reader, string context)
        {
            long pos = reader.BaseStream.Position;
            int len = reader.ReadInt32();

            if (len < 0 || len > 256)
                throw new Exception($"ReadName({context}): invalid len={len} at pos={pos}");

            if (len == 0)
            {
                Trace($"ReadName({context}): len=0 at pos={pos}");
                return "";
            }

            byte[] bytes = reader.ReadBytes(len);
            string name = Encoding.ASCII.GetString(bytes);
            Trace($"ReadName({context}): '{name}' (len={len}) at pos={pos}");
            return name;
        }

        /// <summary>
        /// Generic int32-prefixed string reader used for ID, tooltip, sound filenames, etc.
        /// C++: reads int32 len, if len > 0 reads len bytes as string.
        /// </summary>
        private static string ReadString32(BinaryReader reader, string context)
        {
            long pos = reader.BaseStream.Position;
            int len = reader.ReadInt32();

            if (len < 0 || len > 4096)
                throw new Exception($"ReadString32({context}): invalid len={len} at pos={pos}");

            if (len == 0)
                return "";

            byte[] bytes = reader.ReadBytes(len);
            return Encoding.ASCII.GetString(bytes);
        }

        private static Rect ReadRect(BinaryReader reader)
        {
            return new Rect
            {
                Left = reader.ReadInt32(),
                Top = reader.ReadInt32(),
                Right = reader.ReadInt32(),
                Bottom = reader.ReadInt32()
            };
        }

        private static int CountNodes(UIFNode node)
        {
            int count = 1;
            foreach (var child in node.Children)
                count += CountNodes(child);
            return count;
        }

        private static void LogNodeTree(UIFNode node, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;
            string indent = new string(' ', depth * 2);
            string texInfo = string.IsNullOrEmpty(node.TextureFileName) ? "" : $" tex='{node.TextureFileName}'";
            string uvInfo = (node.UVRect.Right > 0 || node.UVRect.Bottom > 0) ? $" uv={node.UVRect}" : "";
            foreach (var child in node.Children)
                LogNodeTree(child, depth + 1, maxDepth);
        }

        private static void Trace(string msg)
        {
        }

        #endregion
    }
}
