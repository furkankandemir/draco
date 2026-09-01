using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EntropyOnline.Network.KO;
using EntropyOnline.UI;

namespace EntropyOnline.UI
{
    public class KOPowerUpStoreUI : MonoBehaviour
    {
        private Transform _areaWeb;
        private Button _btnClose;
        private Text _txtKnightCash;
        private int _knightCash = 3000;

        // PUS Categories
        private enum PUSCategory
        {
            MainPage,
            PowerUp,
            Premiums,
            Scrolls,
            Extra,
            Bundle
        }

        private PUSCategory _currentCategory = PUSCategory.MainPage;

        // Featured items for Main Page
        private static readonly uint[] MAIN_PAGE_IDS = new uint[] {
            800080000, // Gold Premium
            800013000, // 1500 HP+ Scroll
            800010000, // 300 Defense+ Scroll
            700002000, // Trina's Piece
            800032000, // Name Change Scroll
            800014000  // Attack+ Scroll
        };

        // Authentic 1299 PUS Item IDs grouped by Category
        private static readonly uint[] PREMIUM_SPECIAL_IDS = new uint[] {
            389047000, // Shrinking Potion
            389047500, // Enlarging Potion
            800022000, // Duration Item
            800032000, // Name Change Scroll
            379079000, // Blessed Item Upgrade Scroll
            800037000, // Bronze Premum Service
            800038000, // Silver Premium Service
            800080000, // Gold Premium Service
            700001000, // Redistribution Item
            700002000, // Trina's Piece
            700004000, // Monster Summon Staff
            700006000  // Monster Summon Staff(Event)
        };

        private static readonly uint[] SCROLLS_BUFFS_IDS = new uint[] {
            800003000, // STR+ Scroll(Stat)(L)
            800004000, // HP+ Scroll(Stat)(L)
            800005000, // DEX+ Scroll(Stat)(L)
            800006000, // INT+ Scroll(Stat)(L)
            800007000, // MP+ Scroll(Stat)(L)
            800008000, // Power of Lion Scroll(Stat)(L)
            800009000, // 150 Defense+ Scroll(L)
            800010000, // 300 Defense+ Scroll(L)
            800011000, // 500 HP+ Scroll(L)
            800012000, // 1000 HP+ Scroll(L)
            800013000, // 1500 HP+ Scroll(L)
            800014000, // Attack+ Scroll
            800015000, // Speed+ Potion
            800023000, // STR+ Scroll(Stat)(S)
            800024000, // HP+ Scroll(Stat)(S)
            800025000, // DEX+ Scroll(Stat)(S)
            800026000, // INT+ Scroll(Stat)(S)
            800027000, // MP+ Scroll(Stat)(S)
            800028000, // Power of Lion Scroll(Stat)(S)
            800029000, // 150 Defense+ Scroll(S)
            800030000, // 500 HP+ Scroll(S)
            800031000, // 1000 HP+ Scroll(S)
            800085000  // Menicia's Official List
        };

        private static readonly uint[] UTILITY_EXP_IDS = new uint[] {
            389050000, // Calling Friend Scroll
            381001000, // Transformation Scroll
            800002000, // Re-Spawn Teleport Scroll
            800019000, // Clarity potion
            800021000, // Scroll of teleport friend
            800033000, // HP Rice Cake
            800034000, // MP Rice Cake
            800035000, // Speed Up Rice Cake
            800036000, // 60% re-spawn scroll
            800050000, // Mount Scroll
            800051000, // Ascent Scroll
            800052000, // Vegetable Dumplings
            800053000, // Normal Dumplings
            800054000, // Fish Dumplings
            800055000, // Cake
            800041000, // Resurrection Scroll(50)
            800056000, // Leader's Guardian [Karus]
            800057000, // Leader's Guardian [El Morad]
            800058000, // Co-Leader's Guardian [Karus]
            800059000, // Co-Leader's Guardian [El Morad]
            800060000, // Weight Scroll
            800061000, // Weapon Enchant Scroll
            800062000, // Armor Enchant Scroll
            800063000, // Stat Scroll
            800064000, // Leader's Guardian [Karus 5]
            800065000, // Leader's Guardian [El Morad 5]
            800066000, // Co-Leader's Guardian [Karus 5]
            800067000, // Co-Leader's Guardian [El Morad 5]
            800068000, // Leader's Guardian [Karus 5]
            800069000, // Leader's Guardian [El Morad 5]
            800070000, // Co-Leader's Guardian [Karus 5]
            800071000  // Co-Leader's Guardian [El Morad 5]
        };

        // PUS Item Data Model
        private struct PUSItem
        {
            public uint ItemID;
            public string Name;
            public string Description;
            public int Price;
            public int DefaultIconID;

            public PUSItem(uint itemId, string name, string description, int price, int defaultIconId)
            {
                ItemID = itemId;
                Name = name;
                Description = description;
                Price = price;
                DefaultIconID = defaultIconId;
            }
        }

        private List<PUSItem> _pusCatalog = new List<PUSItem>();

        // Dynamic UI References
        private Transform _scrollContent;
        private Text _txtCategoryTitle;

        private Dictionary<PUSCategory, Image> _tabImages = new Dictionary<PUSCategory, Image>();
        private Dictionary<PUSCategory, Outline> _tabOutlines = new Dictionary<PUSCategory, Outline>();
        private Dictionary<PUSCategory, Text> _tabTexts = new Dictionary<PUSCategory, Text>();

        public void Init()
        {
            // Catalog verisini tanımla
            PopulateCatalog();

            // Knight Cash bakiye simülasyonu
            if (!PlayerPrefs.HasKey("PUS_KnightCash"))
            {
                PlayerPrefs.SetInt("PUS_KnightCash", 3000);
            }
            _knightCash = PlayerPrefs.GetInt("PUS_KnightCash");

            // area_web'i dinamik olarak oluştur
            var areaWebObj = new GameObject("area_web", typeof(RectTransform));
            areaWebObj.transform.SetParent(transform, false);
            _areaWeb = areaWebObj.transform;

            var areaRT = areaWebObj.GetComponent<RectTransform>();
            if (areaRT != null)
            {
                areaRT.anchorMin = new Vector2(0.5f, 0.5f);
                areaRT.anchorMax = new Vector2(0.5f, 0.5f);
                areaRT.pivot = new Vector2(0.5f, 0.5f);
                areaRT.anchoredPosition = Vector2.zero;
                areaRT.sizeDelta = new Vector2(1024f, 600f);
            }

            // PUS açıldığında sağ taraftaki mobil skillbar'ı gizle
            if (MobileSkillBar.Instance != null)
            {
                MobileSkillBar.Instance.SetVisible(false);
            }

            BuildStaticPUSInterface();
            RefreshCatalogPage();

            if (KOUIManager.Instance != null)
            {
                UpdateScale(KOUIManager.Instance.CanvasScaleFactor);
            }
        }

        private void OnEnable()
        {
            KOPacketHandler.OnShoppingMall += HandleShoppingMallPacket;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnShoppingMall -= HandleShoppingMallPacket;
        }

        private void HandleShoppingMallPacket(byte[] rawData)
        {
            var reader = new KOPacketReader(rawData);
            byte subOpcode = reader.ReadByte();

            if (subOpcode == (byte)ShoppingMallOpcodes.STORE_CLOSE)
            {
                // Sunucudan gelen 28 slotluk envanter verisini oku
                for (byte i = 0; i < 28; i++)
                {
                    uint itemId = reader.ReadUInt32();
                    ushort duration = reader.ReadUInt16();
                    ushort count = reader.ReadUInt16();
                    byte flag = reader.ReadByte();
                    ushort timeRemaining = reader.ReadUInt16();
                    reader.ReadUInt32(); // unknown/align
                    reader.ReadUInt32(); // expiration time

                    if (KOInventory.Instance != null)
                    {
                        KOInventory.Instance.ItemCountChange(1, i, itemId, count, duration);
                    }
                }

                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.RefreshInventoryUI();
                    KOUIManager.Instance.CloseShoppingMallUI();
                }

                if (MobileSkillBar.Instance != null)
                {
                    MobileSkillBar.Instance.SetVisible(true);
                }
            }
        }

        private void OnCloseClicked()
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_SHOPPING_MALL);
            pkt.WriteByte((byte)ShoppingMallOpcodes.STORE_CLOSE);
            KONetworkManager.Instance.SendPacket(pkt);

            if (MobileSkillBar.Instance != null)
            {
                MobileSkillBar.Instance.SetVisible(true);
            }
        }

        private void PopulateCatalog()
        {
            _pusCatalog.Clear();

            // PUS Eşyaları ve Cash fiyatları
            var itemPrices = new Dictionary<uint, int>
            {
                { 379079000, 99 },
                { 381001000, 29 },
                { 389047000, 9 },
                { 389047500, 9 },
                { 389050000, 29 },
                { 700001000, 799 },
                { 700002000, 199 },
                { 700004000, 79 },
                { 700006000, 79 },
                { 800002000, 19 },
                { 800003000, 19 },
                { 800004000, 19 },
                { 800005000, 19 },
                { 800006000, 19 },
                { 800007000, 19 },
                { 800008000, 29 },
                { 800009000, 29 },
                { 800010000, 49 },
                { 800011000, 29 },
                { 800012000, 49 },
                { 800013000, 59 },
                { 800014000, 49 },
                { 800015000, 9 },
                { 800019000, 29 },
                { 800021000, 19 },
                { 800022000, 49 },
                { 800023000, 19 },
                { 800024000, 19 },
                { 800025000, 19 },
                { 800026000, 19 },
                { 800027000, 19 },
                { 800028000, 29 },
                { 800029000, 29 },
                { 800030000, 29 },
                { 800031000, 49 },
                { 800032000, 999 },
                { 800033000, 9 },
                { 800034000, 9 },
                { 800035000, 9 },
                { 800036000, 29 },
                { 800037000, 1000 },
                { 800038000, 1500 },
                { 800041000, 49 },
                { 800050000, 99 },
                { 800051000, 49 },
                { 800052000, 9 },
                { 800053000, 9 },
                { 800054000, 9 },
                { 800055000, 29 },
                { 800056000, 29 },
                { 800057000, 29 },
                { 800058000, 29 },
                { 800059000, 29 },
                { 800060000, 29 },
                { 800061000, 29 },
                { 800062000, 29 },
                { 800063000, 29 },
                { 800064000, 29 },
                { 800065000, 29 },
                { 800066000, 29 },
                { 800067000, 29 },
                { 800068000, 29 },
                { 800069000, 29 },
                { 800070000, 29 },
                { 800071000, 29 },
                { 800076000, 59 },
                { 800077000, 79 },
                { 800078000, 79 },
                { 800079000, 59 },
                { 800080000, 2000 },
                { 800085000, 99 }
            };

            foreach (var kvp in itemPrices)
            {
                uint itemId = kvp.Key;
                int price = kvp.Value;
                string name = "";
                string desc = "";

                // Item_Org_us.tbl verisinden ad ve açıklamayı yükle
                if (KOInventory.s_pTbl_Items_Basic != null && 
                    KOInventory.s_pTbl_Items_Basic.TryGetValue(itemId, out var basic))
                {
                    name = basic.szName;
                    desc = basic.szRemark;
                }
                else
                {
                    name = GetFallbackName(itemId);
                    desc = GetFallbackDesc(itemId);
                }

                _pusCatalog.Add(new PUSItem(itemId, name, desc, price, (int)itemId));
            }
        }

        private string GetFallbackName(uint itemId)
        {
            switch (itemId)
            {
                case 800037000: return "Bronze Premium Service";
                case 800038000: return "Silver Premium Service";
                case 800080000: return "Gold Premium Service";
                case 800032000: return "Name Change Scroll";
                case 700001000: return "Redistribution Item";
                case 700002000: return "Trina's Piece";
                case 700004000: return "Monster Summon Staff";
                case 800022000: return "Duration Item";
                case 800078000: return "HP Scroll 2000";
                case 800076000: return "Scroll of Armor 350";
                case 800085000: return "Menicia's Official List";
                default: return $"PUS Item {itemId}";
            }
        }

        private string GetFallbackDesc(uint itemId)
        {
            switch (itemId)
            {
                case 800037000: return "Use premium service";
                case 800038000: return "Use ultra premium service";
                case 700002000: return "Increases the success rate for Blessed Upgrade Scroll";
                case 800078000: return "Increase HP by 2000";
                case 800076000: return "Increase Defense by 350";
                case 800085000: return "Enables searching for items in active merchant stalls.";
                default: return "Authentic Power Up Store item";
            }
        }

        private PUSCategory GetItemCategory(uint itemId)
        {
            // Premiums
            if (itemId == 800037000 || itemId == 800038000 || itemId == 800080000)
                return PUSCategory.Premiums;

            // Power-UP
            if (itemId == 800014000 || itemId == 800015000 || itemId == 800022000 ||
                (itemId >= 800003000 && itemId <= 800008000) ||
                (itemId >= 800023000 && itemId <= 800028000))
                return PUSCategory.PowerUp;

            // Scrolls
            if (itemId == 800076000 || itemId == 800077000 || itemId == 800078000 || itemId == 800079000 ||
                itemId == 800009000 || itemId == 800010000 || itemId == 800011000 || itemId == 800012000 || itemId == 800013000 ||
                itemId == 800029000 || itemId == 800030000 || itemId == 800031000 ||
                itemId == 800061000 || itemId == 800062000 || itemId == 379079000 ||
                itemId == 800085000)
                return PUSCategory.Scrolls;

            // Bundle
            if (itemId == 800033000 || itemId == 800034000 || itemId == 800035000 ||
                (itemId >= 800052000 && itemId <= 800055000))
                return PUSCategory.Bundle;

            // Extra
            return PUSCategory.Extra;
        }

        private List<PUSItem> GetFilteredItems()
        {
            var filtered = new List<PUSItem>();
            if (_currentCategory == PUSCategory.MainPage)
            {
                foreach (var id in MAIN_PAGE_IDS)
                {
                    foreach (var item in _pusCatalog)
                    {
                        if (item.ItemID == id)
                        {
                            filtered.Add(item);
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var item in _pusCatalog)
                {
                    if (GetItemCategory(item.ItemID) == _currentCategory)
                    {
                        filtered.Add(item);
                    }
                }
            }
            return filtered;
        }

        private void BuildStaticPUSInterface()
        {
            if (_areaWeb == null) return;

            // Clear old children
            foreach (Transform child in _areaWeb)
            {
                Destroy(child.gameObject);
            }

            _tabImages.Clear();
            _tabOutlines.Clear();
            _tabTexts.Clear();

            // --- MAIN BACKGROUND PANEL ---
            var mainBg = new GameObject("PUS_MainPanel", typeof(RectTransform));
            mainBg.transform.SetParent(_areaWeb, false);
            var mainRT = mainBg.GetComponent<RectTransform>();
            mainRT.anchorMin = Vector2.zero;
            mainRT.anchorMax = Vector2.one;
            mainRT.offsetMin = Vector2.zero;
            mainRT.offsetMax = Vector2.zero;

            var mainImg = mainBg.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                // Procedural panel background matching the exact inventory/anvil style (gold border + dark gradient)
                mainImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite("pus_custom_bg", 1024, 600, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f),
                    new Color(0.04f, 0.04f, 0.04f, 0.98f),
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),
                    2);
                mainImg.color = Color.white;
            }
            else
            {
                mainImg.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
            }

            // --- TOP HEADER BAR ---
            var headerObj = new GameObject("Header", typeof(RectTransform));
            headerObj.transform.SetParent(mainBg.transform, false);
            var headerRT = headerObj.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 0.88f);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.offsetMin = Vector2.zero;
            headerRT.offsetMax = Vector2.zero;

            var headerImg = headerObj.AddComponent<Image>();
            headerImg.color = new Color(0f, 0f, 0f, 0.4f); // Transparent dark header, showing background gradient

            // Create close button dynamically inside the Header
            var btnCloseObj = new GameObject("btn_close", typeof(RectTransform));
            btnCloseObj.transform.SetParent(headerObj.transform, false);
            var btnCloseRT = btnCloseObj.GetComponent<RectTransform>();
            if (btnCloseRT != null)
            {
                btnCloseRT.anchorMin = new Vector2(1f, 0.5f);
                btnCloseRT.anchorMax = new Vector2(1f, 0.5f);
                btnCloseRT.pivot = new Vector2(1f, 0.5f);
                btnCloseRT.anchoredPosition = new Vector2(-15f, 0f);
                btnCloseRT.sizeDelta = new Vector2(28f, 28f);
            }

            var btnCloseImg = btnCloseObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                // Close button style matches the inventory close button style (dark gray fill + gold border)
                btnCloseImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_close_btn", 28, 28, 0,
                    new Color(0.18f, 0.18f, 0.18f, 1f),
                    new Color(0.45f, 0.35f, 0.15f, 1f),
                    1);
                btnCloseImg.color = Color.white;
            }
            else
            {
                btnCloseImg.color = new Color(0.85f, 0.25f, 0.25f, 0.8f);
            }

            var closeTextObj = new GameObject("Text", typeof(RectTransform));
            closeTextObj.transform.SetParent(btnCloseObj.transform, false);
            var closeTextRT = closeTextObj.GetComponent<RectTransform>();
            if (closeTextRT != null)
            {
                closeTextRT.anchorMin = Vector2.zero;
                closeTextRT.anchorMax = Vector2.one;
                closeTextRT.offsetMin = Vector2.zero;
                closeTextRT.offsetMax = Vector2.zero;
            }

            var closeTxt = closeTextObj.AddComponent<Text>();
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.text = "X";
            closeTxt.fontSize = 14;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Gold title text matching the rest
            closeTxt.alignment = TextAnchor.MiddleCenter;

            _btnClose = btnCloseObj.AddComponent<Button>();
            _btnClose.onClick.RemoveAllListeners();
            _btnClose.onClick.AddListener(OnCloseClicked);

            // Title Logo Text
            var titleObj = new GameObject("TitleText", typeof(RectTransform));
            titleObj.transform.SetParent(headerObj.transform, false);
            var titleRT = titleObj.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.05f, 0);
            titleRT.anchorMax = new Vector2(0.5f, 1);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            var titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleTxt.text = "DRACO STORE";
            titleTxt.fontSize = 22;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Bright gold
            titleTxt.alignment = TextAnchor.MiddleLeft;

            var shadow = titleObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
            shadow.effectDistance = new Vector2(1f, -1f);

            // Knight Cash Panel
            var kcPanel = new GameObject("KC_Panel", typeof(RectTransform));
            kcPanel.transform.SetParent(headerObj.transform, false);
            var kcRT = kcPanel.GetComponent<RectTransform>();
            kcRT.anchorMin = new Vector2(0.6f, 0.15f);
            kcRT.anchorMax = new Vector2(0.95f, 0.85f);
            kcRT.offsetMin = Vector2.zero;
            kcRT.offsetMax = Vector2.zero;

            var kcImg = kcPanel.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                kcImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_kc_panel_bg", 200, 36, 0,
                    new Color(0.08f, 0.08f, 0.08f, 0.8f),
                    new Color(0.6f, 0.48f, 0.22f, 0.8f),
                    1);
                kcImg.color = Color.white;
            }
            else
            {
                kcImg.color = new Color(0.18f, 0.15f, 0.22f, 1f);
            }

            var kcTextObj = new GameObject("KCText", typeof(RectTransform));
            kcTextObj.transform.SetParent(kcPanel.transform, false);
            var kcTextRT = kcTextObj.GetComponent<RectTransform>();
            kcTextRT.anchorMin = Vector2.zero;
            kcTextRT.anchorMax = Vector2.one;
            kcTextRT.offsetMin = new Vector2(10, 0);
            kcTextRT.offsetMax = new Vector2(-10, 0);

            _txtKnightCash = kcTextObj.AddComponent<Text>();
            _txtKnightCash.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _txtKnightCash.text = $"Knight Cash: <color=#FFD700>{_knightCash}</color> KC";
            _txtKnightCash.fontSize = 16;
            _txtKnightCash.fontStyle = FontStyle.Bold;
            _txtKnightCash.color = Color.white;
            _txtKnightCash.alignment = TextAnchor.MiddleCenter;
            _txtKnightCash.supportRichText = true;

            // --- LEFT SIDEBAR PANEL (MENU) ---
            var sidebarObj = new GameObject("SidebarPanel", typeof(RectTransform));
            sidebarObj.transform.SetParent(mainBg.transform, false);
            var sidebarRT = sidebarObj.GetComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0.02f, 0.32f);
            sidebarRT.anchorMax = new Vector2(0.18f, 0.85f);
            sidebarRT.offsetMin = Vector2.zero;
            sidebarRT.offsetMax = Vector2.zero;

            var sidebarImg = sidebarObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                sidebarImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_sidebar_bg", 160, 320, 0,
                    new Color(0.06f, 0.06f, 0.06f, 0.85f),
                    new Color(0.4f, 0.35f, 0.2f, 0.6f),
                    1);
                sidebarImg.color = Color.white;
            }
            else
            {
                sidebarImg.color = new Color(0.1f, 0.1f, 0.12f, 0.6f);
            }

            var sidebarLayout = sidebarObj.AddComponent<VerticalLayoutGroup>();
            sidebarLayout.spacing = 8;
            sidebarLayout.padding = new RectOffset(8, 8, 12, 12);
            sidebarLayout.childForceExpandWidth = true;
            sidebarLayout.childForceExpandHeight = false;
            sidebarLayout.childControlWidth = true;
            sidebarLayout.childControlHeight = true;

            // Create Sidebar buttons
            CreateMenuButton(sidebarObj.transform, "Main Page", PUSCategory.MainPage);
            CreateMenuButton(sidebarObj.transform, "Power-UP", PUSCategory.PowerUp);
            CreateMenuButton(sidebarObj.transform, "Premiums", PUSCategory.Premiums);
            CreateMenuButton(sidebarObj.transform, "Scrolls", PUSCategory.Scrolls);
            CreateMenuButton(sidebarObj.transform, "Extra", PUSCategory.Extra);
            CreateMenuButton(sidebarObj.transform, "Bundle", PUSCategory.Bundle);

            // --- RIGHT CONTENT SCROLL PANEL ---
            var rightContentObj = new GameObject("RightContentPanel", typeof(RectTransform));
            rightContentObj.transform.SetParent(mainBg.transform, false);
            var rightContentRT = rightContentObj.GetComponent<RectTransform>();
            rightContentRT.anchorMin = new Vector2(0.20f, 0.04f);
            rightContentRT.anchorMax = new Vector2(0.98f, 0.85f);
            rightContentRT.offsetMin = Vector2.zero;
            rightContentRT.offsetMax = Vector2.zero;

            // Content Area Header (Title of Category)
            var categoryTitleObj = new GameObject("CategoryTitle", typeof(RectTransform));
            categoryTitleObj.transform.SetParent(rightContentObj.transform, false);
            var categoryTitleRT = categoryTitleObj.GetComponent<RectTransform>();
            categoryTitleRT.anchorMin = new Vector2(0.05f, 0.92f);
            categoryTitleRT.anchorMax = new Vector2(0.95f, 1.0f);
            categoryTitleRT.offsetMin = Vector2.zero;
            categoryTitleRT.offsetMax = Vector2.zero;

            _txtCategoryTitle = categoryTitleObj.AddComponent<Text>();
            _txtCategoryTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _txtCategoryTitle.text = "Category Title";
            _txtCategoryTitle.fontSize = 18;
            _txtCategoryTitle.fontStyle = FontStyle.Bold;
            _txtCategoryTitle.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Gold title text
            _txtCategoryTitle.alignment = TextAnchor.MiddleCenter;

            // ScrollRect Viewport
            var scrollObj = new GameObject("CatalogScroll", typeof(RectTransform));
            scrollObj.transform.SetParent(rightContentObj.transform, false);
            var scrollRT = scrollObj.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(0.96f, 0.90f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 25f;

            var viewportObj = new GameObject("Viewport", typeof(RectTransform));
            viewportObj.transform.SetParent(scrollObj.transform, false);
            var viewRT = viewportObj.GetComponent<RectTransform>();
            viewRT.anchorMin = Vector2.zero;
            viewRT.anchorMax = Vector2.one;
            viewRT.offsetMin = Vector2.zero;
            viewRT.offsetMax = Vector2.zero;

            // Add Image as Raycast Target so dragging/scrolling works anywhere
            var vpImg = viewportObj.AddComponent<Image>();
            vpImg.color = Color.clear;
            vpImg.raycastTarget = true;

            viewportObj.AddComponent<RectMask2D>();
            scrollRect.viewport = viewRT;

            _scrollContent = new GameObject("Content", typeof(RectTransform)).transform;
            _scrollContent.SetParent(viewportObj.transform, false);
            var contentRT = _scrollContent.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 400);

            scrollRect.content = contentRT;

            // --- VERTICAL SCROLLBAR ---
            var scrollbarObj = new GameObject("VerticalScrollbar", typeof(RectTransform));
            scrollbarObj.transform.SetParent(rightContentObj.transform, false);
            var sbRT = scrollbarObj.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(0.97f, 0f);
            sbRT.anchorMax = new Vector2(0.99f, 0.90f);
            sbRT.offsetMin = Vector2.zero;
            sbRT.offsetMax = Vector2.zero;

            var sbImg = scrollbarObj.AddComponent<Image>();
            sbImg.color = new Color(0.05f, 0.05f, 0.07f, 0.5f); // Semi-transparent track

            var scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Sliding Area
            var slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarObj.transform, false);
            var saRT = slidingArea.GetComponent<RectTransform>();
            saRT.anchorMin = Vector2.zero;
            saRT.anchorMax = Vector2.one;
            saRT.offsetMin = Vector2.zero;
            saRT.offsetMax = Vector2.zero;

            // Handle
            var handleObj = new GameObject("Handle", typeof(RectTransform));
            handleObj.transform.SetParent(slidingArea.transform, false);
            var handleRT = handleObj.GetComponent<RectTransform>();
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = Vector2.one;
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;

            var handleImg = handleObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                handleImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_sb_handle", 16, 40, 0,
                    new Color(0.35f, 0.28f, 0.18f, 0.9f),
                    new Color(0.55f, 0.45f, 0.25f, 0.9f),
                    1);
                handleImg.color = Color.white;
            }
            else
            {
                handleImg.color = new Color(0.38f, 0.18f, 0.65f, 0.8f);
            }

            scrollbar.handleRect = handleRT;

            // Link to ScrollRect
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var grid = _scrollContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(140, 200);
            grid.spacing = new Vector2(10, 12);
            grid.padding = new RectOffset(10, 10, 15, 15);
            grid.childAlignment = TextAnchor.UpperLeft;

            var csf = _scrollContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void CreateMenuButton(Transform parent, string label, PUSCategory cat)
        {
            var btnObj = new GameObject($"MenuBtn_{cat}", typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredHeight = 40;
            le.minHeight = 40;

            var img = btnObj.AddComponent<Image>();

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(10, 0); // Padding left
            textRT.offsetMax = Vector2.zero;

            var txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label;
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => OnTabClicked(cat));

            _tabImages[cat] = img;
            _tabTexts[cat] = txt;
        }

        private void OnTabClicked(PUSCategory cat)
        {
            _currentCategory = cat;
            RefreshCatalogPage();
        }

        private void RefreshCatalogPage()
        {
            if (_scrollContent == null) return;

            // 1. Update Menu Button States (Active/Inactive styling)
            foreach (var cat in (PUSCategory[])Enum.GetValues(typeof(PUSCategory)))
            {
                if (_tabImages.TryGetValue(cat, out var img))
                {
                    bool isActive = (_currentCategory == cat);
                    if (KOUIManager.Instance != null)
                    {
                        var activeSprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_tab_active_" + cat, 120, 40, 0,
                            new Color(0.20f, 0.16f, 0.12f, 1f),
                            new Color(0.95f, 0.85f, 0.35f, 1f),
                            1);
                        var inactiveSprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_tab_inactive_" + cat, 120, 40, 0,
                            new Color(0.10f, 0.10f, 0.10f, 1f),
                            new Color(0.3f, 0.25f, 0.18f, 0.7f),
                            1);
                        img.sprite = isActive ? activeSprite : inactiveSprite;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.color = isActive ? new Color(0.25f, 0.20f, 0.15f, 1f) : new Color(0.14f, 0.14f, 0.16f, 1f);
                    }

                    if (_tabTexts.TryGetValue(cat, out var txt))
                    {
                        txt.color = isActive ? new Color(0.95f, 0.85f, 0.35f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
                    }
                }
            }

            // 2. Clear current content items
            foreach (Transform child in _scrollContent)
            {
                Destroy(child.gameObject);
            }

            // 3. Get filtered items
            var filtered = GetFilteredItems();
            foreach (var item in filtered)
            {
                CreateItemCard(_scrollContent, item);
            }

            // 4. Update Category Title Header
            if (_txtCategoryTitle != null)
            {
                _txtCategoryTitle.text = GetCategoryDisplayName(_currentCategory);
            }
        }

        private string GetCategoryDisplayName(PUSCategory cat)
        {
            switch (cat)
            {
                case PUSCategory.MainPage: return "Main Page";
                case PUSCategory.PowerUp: return "Power-UP";
                case PUSCategory.Premiums: return "Premiums";
                case PUSCategory.Scrolls: return "Scrolls";
                case PUSCategory.Extra: return "Extra";
                case PUSCategory.Bundle: return "Bundle";
                default: return "Store Items";
            }
        }

        private void CreateItemCard(Transform parent, PUSItem item)
        {
            var card = new GameObject($"Card_{item.ItemID}", typeof(RectTransform));
            card.transform.SetParent(parent, false);
            var cardRT = card.GetComponent<RectTransform>();

            var cardImg = card.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                cardImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_item_card_bg_" + item.ItemID, 140, 200, 0,
                    new Color(0.08f, 0.08f, 0.08f, 0.9f),
                    new Color(0.4f, 0.35f, 0.22f, 0.7f), // Antique gold/bronze border
                    1);
                cardImg.color = Color.white;
            }
            else
            {
                cardImg.color = new Color(0.09f, 0.09f, 0.11f, 1f);
                var cardOutline = card.AddComponent<Outline>();
                cardOutline.effectColor = new Color(0.22f, 0.16f, 0.3f, 0.7f);
                cardOutline.effectDistance = new Vector2(1f, 1f);
            }

            // --- EŞYA İKONU ALANI ---
            var iconBox = new GameObject("IconBox", typeof(RectTransform));
            iconBox.transform.SetParent(card.transform, false);
            var iconBoxRT = iconBox.GetComponent<RectTransform>();
            iconBoxRT.anchorMin = new Vector2(0.5f, 1f);
            iconBoxRT.anchorMax = new Vector2(0.5f, 1f);
            iconBoxRT.pivot = new Vector2(0.5f, 1f);
            iconBoxRT.anchoredPosition = new Vector2(0, -15);
            iconBoxRT.sizeDelta = new Vector2(45, 45);

            var iconBoxImg = iconBox.AddComponent<Image>();
            iconBoxImg.color = new Color(0.07f, 0.07f, 0.09f, 1f);

            var iconBoxOutline = iconBox.AddComponent<Outline>();
            iconBoxOutline.effectColor = new Color(0.3f, 0.3f, 0.35f, 1f);

            // İkonu yükle
            int iconId = KOUIManager.ResolveIconId((int)item.ItemID);
            Sprite itemIcon = KOItemIconLoader.LoadItemIcon(iconId);
            if (itemIcon != null)
            {
                var iconItemObj = new GameObject("ItemIcon", typeof(RectTransform));
                iconItemObj.transform.SetParent(iconBox.transform, false);
                var iconItemRT = iconItemObj.GetComponent<RectTransform>();
                iconItemRT.anchorMin = Vector2.zero;
                iconItemRT.anchorMax = Vector2.one;
                iconItemRT.offsetMin = Vector2.zero;
                iconItemRT.offsetMax = Vector2.zero;

                var iconImg = iconItemObj.AddComponent<Image>();
                iconImg.sprite = itemIcon;
                iconImg.preserveAspect = true;
            }

            // --- METİN ALANI (İsim + Açıklama) ---
            var nameObj = new GameObject("ItemName", typeof(RectTransform));
            nameObj.transform.SetParent(card.transform, false);
            var nameRT = nameObj.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.05f, 0.40f);
            nameRT.anchorMax = new Vector2(0.95f, 0.65f);
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;

            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.text = item.Name;
            nameTxt.fontSize = 10;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = new Color(0.9f, 0.9f, 0.95f, 1f);
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameTxt.verticalOverflow = VerticalWrapMode.Truncate;

            // --- FİYAT BÖLÜMÜ ---
            var priceObj = new GameObject("PriceText", typeof(RectTransform));
            priceObj.transform.SetParent(card.transform, false);
            var priceRT = priceObj.GetComponent<RectTransform>();
            priceRT.anchorMin = new Vector2(0.05f, 0.22f);
            priceRT.anchorMax = new Vector2(0.95f, 0.38f);
            priceRT.offsetMin = Vector2.zero;
            priceRT.offsetMax = Vector2.zero;

            var priceTxt = priceObj.AddComponent<Text>();
            priceTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            priceTxt.text = $"<color=#a449ff>♦</color> {item.Price} KC";
            priceTxt.fontSize = 12;
            priceTxt.fontStyle = FontStyle.Bold;
            priceTxt.color = new Color(0.98f, 0.74f, 0.02f, 1f); // Shiny gold
            priceTxt.alignment = TextAnchor.MiddleCenter;
            priceTxt.supportRichText = true;

            // Satın Al Butonu
            var buyBtnObj = new GameObject("BuyButton", typeof(RectTransform));
            buyBtnObj.transform.SetParent(card.transform, false);
            var buyBtnRT = buyBtnObj.GetComponent<RectTransform>();
            buyBtnRT.anchorMin = new Vector2(0.1f, 0.05f);
            buyBtnRT.anchorMax = new Vector2(0.9f, 0.20f);
            buyBtnRT.offsetMin = Vector2.zero;
            buyBtnRT.offsetMax = Vector2.zero;

            var buyBtnImg = buyBtnObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                buyBtnImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("pus_buy_btn_" + item.ItemID, 120, 30, 0,
                    new Color(0.18f, 0.14f, 0.10f, 1f),
                    new Color(0.5f, 0.4f, 0.18f, 1f),
                    1);
                buyBtnImg.color = Color.white;
            }
            else
            {
                buyBtnImg.color = new Color(0.42f, 0.22f, 0.72f, 1f);
            }

            var buyBtnTextObj = new GameObject("ButtonText", typeof(RectTransform));
            buyBtnTextObj.transform.SetParent(buyBtnObj.transform, false);
            var btnTextRT = buyBtnTextObj.GetComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;

            var btnText = buyBtnTextObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.text = "Purchase";
            btnText.fontSize = 10;
            btnText.fontStyle = FontStyle.Bold;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;

            var buyButton = buyBtnObj.AddComponent<Button>();
            buyButton.onClick.AddListener(() => OnBuyClicked(item));
        }

        private void OnBuyClicked(PUSItem item)
        {
            if (_knightCash < item.Price)
            {
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.ShowToast("<color=red>Insufficient Knight Cash!</color>");
                }
                return;
            }

            string msg = $"Do you want to buy {item.Name} for {item.Price} KC?";
            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(msg, "Draco Store", MsgBoxBehavior.BEHAVIOR_NOTHING,
                    onYes: () =>
                    {
                        // Deduct KC locally and update UI
                        _knightCash -= item.Price;
                        PlayerPrefs.SetInt("PUS_KnightCash", _knightCash);
                        if (_txtKnightCash != null)
                        {
                            _txtKnightCash.text = $"Knight Cash: <color=#FFD700>{_knightCash}</color> KC";
                        }

                        using var pkt = new KOPacketWriter(WizOpcode.WIZ_SHOPPING_MALL);
                        pkt.WriteByte((byte)ShoppingMallOpcodes.STORE_BUY);
                        pkt.WriteUInt32(item.ItemID);
                        pkt.WriteUInt16(1); // count

                        if (KONetworkManager.Instance != null)
                        {
                            KONetworkManager.Instance.SendPacket(pkt);
                        }

                        if (KOMessageBox.Instance != null)
                        {
                            string successMsg = $"Successfully purchased {item.Name}!";
                            KOMessageBox.Instance.Show(
                                successMsg,
                                "Draco Store",
                                MsgBoxStyle.MB_OK,
                                MsgBoxBehavior.BEHAVIOR_NOTHING,
                                callerPanel: null,
                                forceFixedCenter: true
                            );
                        }
                    },
                    callerPanel: null,
                    forceFixedCenter: true
                );
            }
            else
            {
                // Fallback if no messagebox
                _knightCash -= item.Price;
                PlayerPrefs.SetInt("PUS_KnightCash", _knightCash);
                if (_txtKnightCash != null)
                {
                    _txtKnightCash.text = $"Knight Cash: <color=#FFD700>{_knightCash}</color> KC";
                }

                using var pkt = new KOPacketWriter(WizOpcode.WIZ_SHOPPING_MALL);
                pkt.WriteByte((byte)ShoppingMallOpcodes.STORE_BUY);
                pkt.WriteUInt32(item.ItemID);
                pkt.WriteUInt16(1); // count

                if (KONetworkManager.Instance != null)
                {
                    KONetworkManager.Instance.SendPacket(pkt);
                }
            }
        }

        public void UpdateScale(float s)
        {
            if (_areaWeb != null && s > 0f)
            {
                _areaWeb.localScale = new Vector3(1f / s, 1f / s, 1f / s);
            }
        }
    }
}
