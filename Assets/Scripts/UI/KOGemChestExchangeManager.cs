using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;
using EntropyOnline.Services;
using KOImport;

namespace EntropyOnline.UI
{
    public class KOGemChestExchangeManager : MonoBehaviour
    {
        public static KOGemChestExchangeManager Instance { get; private set; }

        [Header("UI References")]
        private Transform _itemSlotArea;
        private Transform _giveSlotsContainer;
        private Transform[] _giveSlotAreas = new Transform[3];
        private Toggle _manualToggle;
        private Toggle _stockInnToggle;
        
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnShowDrop;

        private GameObject _placedItemIcon;
        private GameObject[] _giveItemIcons = new GameObject[3];

        // State & Logic
        private int _selectedInvPos = -1;
        public int SelectedInvPos => _selectedInvPos;
        private int _currentItemId = 0;
        private bool _isSpinning = false;
        private bool _isWaitingForServerResponse = false;
        private int _notifiedWarehouseItemId = 0;
        private Coroutine _exchangeRoutine;

        // Quest Option Mapping
        private short _npcId;
        private int _talkId;
        private readonly Dictionary<int, int> _optionMapping = new Dictionary<int, int>();

        // Inventory Snapshot
        private struct ItemSnapshot
        {
            public int ItemId;
            public int Count;
        }
        private readonly ItemSnapshot[] _inventorySnapshot = new ItemSnapshot[28];

        private static readonly Dictionary<string, int> KEYWORD_TO_ITEM_ID = new Dictionary<string, int>
        {
            { "abyss", 379106000 },
            { "abys", 379106000 },
            { "green", 379155000 },
            { "yeşil", 379155000 },
            { "blue", 379156000 },
            { "mavi", 379156000 },
            { "red", 379154000 },
            { "kırmızı", 379154000 }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnItemGet += HandleInventoryChange;
            KOPacketHandler.OnItemMove += HandleInventoryChange;
            KOPacketHandler.OnItemCountChange += HandleInventoryChange;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnItemGet -= HandleInventoryChange;
            KOPacketHandler.OnItemMove -= HandleInventoryChange;
            KOPacketHandler.OnItemCountChange -= HandleInventoryChange;
            
            if (_exchangeRoutine != null)
            {
                StopCoroutine(_exchangeRoutine);
                _exchangeRoutine = null;
            }
        }

        public void InitializeUI(Transform root, short npcId, int talkId, int[] menuTextIds)
        {
            _npcId = npcId;
            _talkId = talkId;

            // Map Option IDs to Item IDs based on localized strings
            _optionMapping.Clear();
            for (int i = 0; i < menuTextIds.Length; i++)
            {
                int textId = menuTextIds[i];
                if (textId <= 0) continue;

                // Since these are menu options, resolve from Quest_Menu TBL first to prevent ID conflicts with Quest_Talk
                string menuText = KOImport.QuestTableParser.FindMenu(textId);
                string text = (menuText ?? StringTableService.Get(textId)).ToLower();
                foreach (var kv in KEYWORD_TO_ITEM_ID)
                {
                    if (text.Contains(kv.Key))
                    {
                        _optionMapping[kv.Value] = i;
                        break;
                    }
                }
            }

            if (KOUIManager.Instance != null)
            {
                // Hide old background images (ui_Image_*) to prevent double border/frame peaking
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (child.name.StartsWith("ui_Image_"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                // 1. Root Panel Background (Perfect replica)
                var bgImg = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
                bgImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite("gem_exchange_custom_bg", 368, 540, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f),
                    new Color(0.04f, 0.04f, 0.04f, 0.98f),
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),
                    2);
                bgImg.color = Color.white;

                var bgRt = root.GetComponent<RectTransform>();
                if (bgRt != null) bgRt.sizeDelta = new Vector2(368f, 540f);

                // 2. Title Text - GEM - FRAGMENT - CHEST
                var titleText = KOUIRenderer.FindChildText(root, "ModernTitleText");
                if (titleText != null)
                {
                    titleText.text = "GEM - FRAGMENT - CHEST";
                    titleText.alignment = TextAnchor.MiddleCenter;
                    titleText.color = new Color(0.95f, 0.85f, 0.35f, 1f);
                    titleText.fontSize = 14;
                    titleText.fontStyle = FontStyle.Bold;
                    titleText.font = KOUIManager.Instance.GetSafeFont(14);
                }

                // 3. Top Slots Background (ModernTopSlotsBg)
                var topBgGo = root.Find("ModernTopSlotsBg")?.gameObject;
                if (topBgGo == null)
                {
                    topBgGo = new GameObject("ModernTopSlotsBg", typeof(RectTransform));
                    topBgGo.transform.SetParent(root, false);
                }
                var topBgRt = topBgGo.GetComponent<RectTransform>();
                if (topBgRt != null)
                {
                    topBgRt.anchorMin = new Vector2(0, 1);
                    topBgRt.anchorMax = new Vector2(0, 1);
                    topBgRt.pivot = new Vector2(0, 1);
                    topBgRt.sizeDelta = new Vector2(325f, 143f);
                    topBgRt.anchoredPosition = new Vector2(22f, -50f);
                }
                var topBgImg = topBgGo.GetComponent<Image>() ?? topBgGo.AddComponent<Image>();
                // Set outer border to transparent to avoid double wide border look
                topBgImg.sprite = KOUIManager.Instance.GetSkillThemePanelBgSprite("item_upgrade_top_bg", 325, 143, 0,
                    new Color(0.08f, 0.07f, 0.06f, 0.95f),
                    new Color(0.03f, 0.03f, 0.03f, 0.95f),
                    new Color(0f, 0f, 0f, 0f),
                    0);
                topBgImg.color = Color.white;
                topBgGo.SetActive(true);

                // 4. Create Item Label directly under root (no container frame)
                var itemLabelGo = root.Find("ItemLabel")?.gameObject;
                if (itemLabelGo == null)
                {
                    itemLabelGo = new GameObject("ItemLabel", typeof(RectTransform));
                    itemLabelGo.transform.SetParent(root, false);
                }
                var itemLabelRT = itemLabelGo.GetComponent<RectTransform>();
                if (itemLabelRT != null)
                {
                    itemLabelRT.anchorMin = new Vector2(0, 1);
                    itemLabelRT.anchorMax = new Vector2(0, 1);
                    itemLabelRT.pivot = new Vector2(0.5f, 0.5f);
                    itemLabelRT.sizeDelta = new Vector2(60f, 15f);
                    itemLabelRT.anchoredPosition = new Vector2(73.5f, -94f);
                }
                var itemLabelText = itemLabelGo.GetComponent<Text>() ?? itemLabelGo.AddComponent<Text>();
                itemLabelText.text = "Item";
                itemLabelText.alignment = TextAnchor.MiddleCenter;
                itemLabelText.color = new Color(0.9f, 0.8f, 0.6f);
                itemLabelText.fontSize = 11;
                itemLabelText.fontStyle = FontStyle.Bold;
                itemLabelText.font = KOUIManager.Instance.GetSafeFont(11);
                itemLabelGo.SetActive(true);

                // 5. Create Give Label directly under root (no container frame)
                var giveLabelGo = root.Find("GiveLabel")?.gameObject;
                if (giveLabelGo == null)
                {
                    giveLabelGo = new GameObject("GiveLabel", typeof(RectTransform));
                    giveLabelGo.transform.SetParent(root, false);
                }
                var giveLabelRT = giveLabelGo.GetComponent<RectTransform>();
                if (giveLabelRT != null)
                {
                    giveLabelRT.anchorMin = new Vector2(0, 1);
                    giveLabelRT.anchorMax = new Vector2(0, 1);
                    giveLabelRT.pivot = new Vector2(0.5f, 0.5f);
                    giveLabelRT.sizeDelta = new Vector2(60f, 15f);
                    giveLabelRT.anchoredPosition = new Vector2(231.5f, -94f);
                }
                var giveLabelText = giveLabelGo.GetComponent<Text>() ?? giveLabelGo.AddComponent<Text>();
                giveLabelText.text = "Give";
                giveLabelText.alignment = TextAnchor.MiddleCenter;
                giveLabelText.color = new Color(0.9f, 0.8f, 0.6f);
                giveLabelText.fontSize = 11;
                giveLabelText.fontStyle = FontStyle.Bold;
                giveLabelText.font = KOUIManager.Instance.GetSafeFont(11);
                giveLabelGo.SetActive(true);

                // 6. Delete Need Coins, Upgrade Rate, and all unused slots completely from the hierarchy at runtime
                var rateContainerGo = root.Find("UpgradeRateContainer")?.gameObject;
                if (rateContainerGo != null) Destroy(rateContainerGo);

                var needCoinsContainerGo = root.Find("NeedCoinsContainer")?.gameObject;
                if (needCoinsContainerGo != null) Destroy(needCoinsContainerGo);

                string[] unusedSlots = { "a_m_0", "a_m_1", "a_m_2", "a_m_6", "a_m_7", "a_m_8", "a_result" };
                foreach (var slotName in unusedSlots)
                {
                    var slotGo = root.Find(slotName)?.gameObject;
                    if (slotGo != null) Destroy(slotGo);
                }

                // 7. Assign and position slot areas correctly
                _itemSlotArea = root.Find("a_upgrade");
                _giveSlotAreas[0] = root.Find("a_m_3");
                _giveSlotAreas[1] = root.Find("a_m_4");
                _giveSlotAreas[2] = root.Find("a_m_5");

                if (_itemSlotArea != null)
                {
                    var rt = _itemSlotArea.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(46f, -114f);
                        rt.sizeDelta = new Vector2(55f, 55f);
                    }
                }

                float[] giveXPositions = { 151f, 209f, 267f };
                for (int i = 0; i < 3; i++)
                {
                    var slot = _giveSlotAreas[i];
                    if (slot != null)
                    {
                        var rt = slot.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            rt.anchoredPosition = new Vector2(giveXPositions[i], -114f);
                            rt.sizeDelta = new Vector2(55f, 55f);
                        }
                    }
                }

                // Style active slots (restore glass slot sprite to fix white squares)
                Sprite slotSprite = KOUIManager.Instance.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45);
                
                if (_itemSlotArea != null)
                {
                    var img = _itemSlotArea.GetComponent<Image>() ?? _itemSlotArea.gameObject.AddComponent<Image>();
                    img.sprite = slotSprite;
                    img.color = Color.white;
                    _itemSlotArea.gameObject.SetActive(true);
                }

                for (int i = 0; i < 3; i++)
                {
                    var slot = _giveSlotAreas[i];
                    if (slot != null)
                    {
                        var img = slot.GetComponent<Image>() ?? slot.gameObject.AddComponent<Image>();
                        img.sprite = slotSprite;
                        img.color = Color.white;
                        slot.gameObject.SetActive(true);
                    }
                }

                // 8. Inventory Slots (a_slot_0..a_slot_27)
                for (int i = 0; i < 28; i++)
                {
                    var slot = root.Find($"a_slot_{i}");
                    if (slot != null)
                    {
                        var img = slot.GetComponent<Image>() ?? slot.gameObject.AddComponent<Image>();
                        img.sprite = slotSprite;
                        img.color = Color.white;

                        var slotRT = slot.GetComponent<RectTransform>();
                        if (slotRT != null)
                        {
                            slotRT.anchorMin = new Vector2(0f, 1f);
                            slotRT.anchorMax = new Vector2(0f, 1f);
                            slotRT.pivot = new Vector2(0f, 1f);
                            slotRT.sizeDelta = new Vector2(45f, 45f);

                            float slotX = 16f + (i % 7) * 48.5f;
                            float slotY = -330f - (i / 7) * 49.5f;
                            slotRT.anchoredPosition = new Vector2(slotX, slotY);
                        }
                    }

                    var cnt = root.Find($"s_count_{i}");
                    if (cnt != null)
                    {
                        var txt = cnt.GetComponent<Text>();
                        if (txt != null)
                        {
                            txt.font = KOUIManager.Instance.GetSafeFont(10);
                            txt.fontStyle = FontStyle.Bold;
                        }

                        var cntRT = cnt.GetComponent<RectTransform>();
                        if (cntRT != null)
                        {
                            cntRT.anchorMin = new Vector2(0f, 1f);
                            cntRT.anchorMax = new Vector2(0f, 1f);
                            cntRT.pivot = new Vector2(1f, 0f);
                            cntRT.sizeDelta = new Vector2(40f, 15f);

                            float slotX = 16f + (i % 7) * 48.5f;
                            float slotY = -330f - (i / 7) * 49.5f;
                            cntRT.anchoredPosition = new Vector2(slotX + 45f, slotY - 45f);
                        }
                    }
                }

                // 9. Buttons Styling & Positioning (Sized to 84px for equal margin distribution)
                Color greenBg = new Color(0.12f, 0.28f, 0.12f, 0.95f);
                Color greenBorder = new Color(0.25f, 0.55f, 0.25f, 0.95f);

                Color redBg = new Color(0.45f, 0.05f, 0.08f, 0.95f);
                Color redBorder = new Color(0.75f, 0.15f, 0.15f, 0.95f);

                Color goldBg = new Color(0.55f, 0.42f, 0.12f, 0.95f);
                Color goldBorder = new Color(0.85f, 0.65f, 0.20f, 0.95f);

                // btn_ok is now START and placed above STOP on the right (121, -212) aligned with top bg right border
                StyleGemButton(root, "btn_ok", "ui_String_E4049600", greenBg, greenBorder, Color.white, "START", 84, 26, new Vector2(121f, -212f));
                // btn_cancel is now STOP and placed on the right (121, -248) aligned with top bg right border
                StyleGemButton(root, "btn_cancel", "ui_String_4BFD55E0", redBg, redBorder, Color.white, "STOP", 84, 26, new Vector2(121f, -248f));
                // btn_conversation is now SHOW DROPS, centered vertically at Y = -230f, colored golden yellow, and placed at X = 27f
                StyleGemButton(root, "btn_conversation", "ui_String_EFA7D40C", goldBg, goldBorder, Color.white, "SHOW DROPS", 84, 26, new Vector2(27f, -230f));

                // Style close button (red square with X)
                var btnClose = root.Find("btn_close");
                if (btnClose != null)
                {
                    var rtClose = btnClose.GetComponent<RectTransform>();
                    rtClose.anchoredPosition = new Vector2(338, -8);
                    rtClose.sizeDelta = new Vector2(22, 22);

                    Texture2D closeTex = new Texture2D(22, 22, TextureFormat.RGBA32, false);
                    Color redCol = new Color(0.45f, 0.05f, 0.08f, 0.85f);
                    Color closeBorder = new Color(0.65f, 0.15f, 0.15f, 0.90f);
                    for (int y = 0; y < 22; y++)
                    {
                        for (int x = 0; x < 22; x++)
                        {
                            if (x < 1 || x >= 22 - 1 || y < 1 || y >= 22 - 1)
                                closeTex.SetPixel(x, y, closeBorder);
                            else
                                closeTex.SetPixel(x, y, redCol);
                        }
                    }
                    closeTex.Apply();

                    var rawClose = btnClose.GetComponent<RawImage>();
                    if (rawClose != null)
                    {
                        rawClose.texture = closeTex;
                        rawClose.color = Color.white;
                    }
                    else
                    {
                        var imgClose = btnClose.GetComponent<Image>() ?? btnClose.gameObject.AddComponent<Image>();
                        imgClose.sprite = Sprite.Create(closeTex, new Rect(0, 0, 22, 22), new Vector2(0.5f, 0.5f));
                        imgClose.color = Color.white;
                    }

                    var xGo = btnClose.Find("XLabel")?.gameObject;
                    if (xGo == null)
                    {
                        xGo = new GameObject("XLabel", typeof(RectTransform));
                        xGo.transform.SetParent(btnClose, false);
                    }
                    var xRt = xGo.GetComponent<RectTransform>();
                    if (xRt != null)
                    {
                        xRt.anchorMin = Vector2.zero;
                        xRt.anchorMax = Vector2.one;
                        xRt.sizeDelta = Vector2.zero;
                    }
                    var xTxt = xGo.GetComponent<Text>() ?? xGo.AddComponent<Text>();
                    xTxt.text = "X";
                    xTxt.alignment = TextAnchor.MiddleCenter;
                    xTxt.color = Color.white;
                    xTxt.fontSize = 11;
                    xTxt.fontStyle = FontStyle.Bold;
                    xTxt.font = KOUIManager.Instance.GetSafeFont(11);
                    btnClose.gameObject.SetActive(true);
                }

                // 10. Border line separator (entire width of the panel, left-to-right, matching the outer border color)
                var dividerGo = root.Find("GoldSeparatorDivider")?.gameObject;
                if (dividerGo == null)
                {
                    dividerGo = new GameObject("GoldSeparatorDivider", typeof(RectTransform));
                    dividerGo.transform.SetParent(root, false);
                }
                var divRT = dividerGo.GetComponent<RectTransform>();
                if (divRT != null)
                {
                    divRT.anchorMin = new Vector2(0f, 1f); // Stretches horizontally across the entire panel
                    divRT.anchorMax = new Vector2(1f, 1f);
                    divRT.pivot = new Vector2(0.5f, 0.5f);
                    divRT.sizeDelta = new Vector2(0f, 2f); // 2px thickness
                    divRT.anchoredPosition = new Vector2(0f, -286.5f);
                }
                var divImg = dividerGo.GetComponent<Image>() ?? dividerGo.AddComponent<Image>();
                
                // Create a solid border-colored sprite
                Texture2D lineTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color borderColor = new Color(0.6f, 0.48f, 0.22f, 0.9f);
                lineTex.SetPixel(0, 0, borderColor);
                lineTex.SetPixel(0, 1, borderColor);
                lineTex.SetPixel(1, 0, borderColor);
                lineTex.SetPixel(1, 1, borderColor);
                lineTex.Apply();
                
                divImg.sprite = Sprite.Create(lineTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                divImg.color = Color.white;
                divImg.raycastTarget = false;
                dividerGo.SetActive(true);

                // 11. Create/Recreate Toggles dynamically on the left with updated "Stock Warehouse" text
                var oldManual = root.Find("Toggle_Manuel")?.gameObject;
                if (oldManual != null) Destroy(oldManual);

                var oldStock = root.Find("Toggle_StockInn")?.gameObject;
                if (oldStock != null) Destroy(oldStock);

                _manualToggle = CreateModernToggle(root, "Toggle_Manuel", "Manuel", new Vector2(-162f, -210f), true);
                _stockInnToggle = CreateModernToggle(root, "Toggle_StockInn", "Stock Warehouse", new Vector2(-162f, -246f), false);

                // Style text_gold and align with grid slot 6
                var goldTxt = root.Find("text_gold") ?? KOUIRenderer.FindChildByID(root, "text_gold");
                if (goldTxt != null)
                {
                    var txt = goldTxt.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.font = KOUIManager.Instance.GetSafeFont(12);
                        txt.fontStyle = FontStyle.Bold;
                        txt.color = new Color(0.92f, 0.80f, 0.52f, 1f);
                        txt.alignment = TextAnchor.MiddleCenter;
                    }

                    float slotX = 307f;
                    float slotY = -330f;
                    float capsuleX = slotX + 45f;
                    float capsuleY = slotY + 20f;

                    var capsuleObj = root.Find("GoldCapsule")?.gameObject;
                    if (capsuleObj == null)
                    {
                        capsuleObj = new GameObject("GoldCapsule", typeof(RectTransform));
                        capsuleObj.transform.SetParent(root, false);
                    }
                    capsuleObj.transform.SetSiblingIndex(goldTxt.GetSiblingIndex());

                    var capsuleImg = capsuleObj.GetComponent<Image>() ?? capsuleObj.AddComponent<Image>();
                    capsuleImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("upgrade_gold_capsule_130", 130, 22, 4,
                        new Color(0.08f, 0.08f, 0.08f, 0.6f),
                        new Color(0.45f, 0.35f, 0.15f, 0.8f),
                        1);
                    capsuleImg.color = Color.white;
                    capsuleImg.raycastTarget = false;

                    var capsuleRT = capsuleObj.GetComponent<RectTransform>();
                    if (capsuleRT != null)
                    {
                        capsuleRT.anchorMin = new Vector2(0f, 1f);
                        capsuleRT.anchorMax = new Vector2(0f, 1f);
                        capsuleRT.pivot = new Vector2(1f, 0.5f);
                        capsuleRT.sizeDelta = new Vector2(130f, 22f);
                        capsuleRT.anchoredPosition = new Vector2(capsuleX, capsuleY);
                    }

                    var iconObj = capsuleObj.transform.Find("GoldIcon")?.gameObject;
                    if (iconObj == null)
                    {
                        iconObj = new GameObject("GoldIcon", typeof(RectTransform));
                        iconObj.transform.SetParent(capsuleObj.transform, false);
                    }
                    var iconImg = iconObj.GetComponent<RawImage>() ?? iconObj.AddComponent<RawImage>();
                    KOUIManager.Instance.ConfigureGoldIcon(iconImg);
                    iconImg.raycastTarget = false;

                    var iconRT = iconObj.GetComponent<RectTransform>();
                    if (iconRT != null)
                    {
                        iconRT.anchorMin = new Vector2(0f, 0.5f);
                        iconRT.anchorMax = new Vector2(0f, 0.5f);
                        iconRT.pivot = new Vector2(0f, 0.5f);
                        iconRT.sizeDelta = new Vector2(14f, 14f);
                        iconRT.anchoredPosition = new Vector2(8f, 0f);
                    }

                    goldTxt.SetParent(capsuleObj.transform, false);
                    var textRT = goldTxt.GetComponent<RectTransform>();
                    if (textRT != null)
                    {
                        textRT.anchorMin = Vector2.zero;
                        textRT.anchorMax = Vector2.one;
                        textRT.pivot = new Vector2(0.5f, 0.5f);
                        textRT.sizeDelta = new Vector2(-28f, 0f);
                        textRT.anchoredPosition = new Vector2(10f, 0f);
                    }
                }

                // Attach actions
                _btnStart = KOUIRenderer.FindChildButton(root.gameObject, "btn_ok");
                if (_btnStart != null)
                {
                    _btnStart.onClick.RemoveAllListeners();
                    _btnStart.onClick.AddListener(OnStartClicked);
                }

                _btnStop = KOUIRenderer.FindChildButton(root.gameObject, "btn_cancel");
                if (_btnStop != null)
                {
                    _btnStop.onClick.RemoveAllListeners();
                    _btnStop.onClick.AddListener(OnStopClicked);
                }

                _btnShowDrop = KOUIRenderer.FindChildButton(root.gameObject, "btn_conversation");
                if (_btnShowDrop != null)
                {
                    _btnShowDrop.onClick.RemoveAllListeners();
                    _btnShowDrop.onClick.AddListener(OnShowDropClicked);
                }

                if (_itemSlotArea != null)
                {
                    var oldDrop = _itemSlotArea.GetComponent<KOUpgradeDropTarget>();
                    if (oldDrop != null) Destroy(oldDrop);

                    if (_itemSlotArea.GetComponent<KOGemChestExchangeDropTarget>() == null)
                    {
                        _itemSlotArea.gameObject.AddComponent<KOGemChestExchangeDropTarget>();
                    }
                }
            }

            // Reset States
            ResetPanel();
        }

        private Toggle CreateModernToggle(Transform parent, string name, string labelText, Vector2 pos, bool defaultOn)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(170f, 30f);
            rt.anchoredPosition = pos;

            // Background box for checkbox
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(go.transform, false);
            var bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.5f);
            bgRT.anchorMax = new Vector2(0f, 0.5f);
            bgRT.pivot = new Vector2(0f, 0.5f);
            bgRT.sizeDelta = new Vector2(22f, 22f);
            bgRT.anchoredPosition = new Vector2(0f, 0f);

            var bgImg = bgGo.AddComponent<Image>();
            
            // Draw a themed checkbox texture
            Texture2D boxTex = new Texture2D(22, 22, TextureFormat.RGBA32, false);
            Color boxBorder = new Color(0.6f, 0.48f, 0.22f, 0.7f); // Bronze/Gold border
            Color boxBg = new Color(0.06f, 0.05f, 0.04f, 0.9f); // Dark fill
            for (int y = 0; y < 22; y++)
            {
                for (int x = 0; x < 22; x++)
                {
                    if (x < 1 || x >= 22 - 1 || y < 1 || y >= 22 - 1)
                        boxTex.SetPixel(x, y, boxBorder);
                    else
                        boxTex.SetPixel(x, y, boxBg);
                }
            }
            boxTex.Apply();
            bgImg.sprite = Sprite.Create(boxTex, new Rect(0, 0, 22, 22), new Vector2(0.5f, 0.5f));
            bgImg.color = Color.white;

            // Checkmark tick symbol
            var checkGo = new GameObject("Checkmark", typeof(RectTransform));
            checkGo.transform.SetParent(bgGo.transform, false);
            var checkRT = checkGo.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.5f, 0.5f);
            checkRT.anchorMax = new Vector2(0.5f, 0.5f);
            checkRT.pivot = new Vector2(0.5f, 0.5f);
            checkRT.sizeDelta = new Vector2(22f, 22f);
            checkRT.anchoredPosition = Vector2.zero;

            var checkTxt = checkGo.AddComponent<Text>();
            checkTxt.text = "✓";
            checkTxt.alignment = TextAnchor.MiddleCenter;
            checkTxt.color = new Color(0.85f, 0.70f, 0.25f, 1f); // Premium gold tick
            checkTxt.fontSize = 16;
            checkTxt.fontStyle = FontStyle.Bold;
            checkTxt.font = KOUIManager.Instance != null ? KOUIManager.Instance.GetSafeFont(16) : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Label text
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0.5f);
            labelRT.anchorMax = new Vector2(1f, 0.5f);
            labelRT.pivot = new Vector2(0f, 0.5f);
            labelRT.sizeDelta = new Vector2(-28f, 24f);
            labelRT.anchoredPosition = new Vector2(28f, 0f);

            var labelTxt = labelGo.AddComponent<Text>();
            labelTxt.text = labelText;
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = new Color(0.85f, 0.70f, 0.45f, 1f); // Warm premium text color matching screenshot
            labelTxt.fontSize = 12;
            labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.font = KOUIManager.Instance != null ? KOUIManager.Instance.GetSafeFont(12) : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Toggle component
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkTxt;
            toggle.isOn = defaultOn;

            return toggle;
        }

        private void StyleGemButton(Transform root, string btnName, string textName, Color normalColor, Color borderColor, Color textColor, string buttonText, float w, float h, Vector2 pos)
        {
            var btnTrans = root.Find(btnName);
            if (btnTrans == null) return;

            var txtTrans = btnTrans.Find(textName) ?? root.Find(textName) ?? KOUIRenderer.FindChildByID(btnTrans, textName) ?? KOUIRenderer.FindChildByID(root, textName);

            var rtBtn = btnTrans.GetComponent<RectTransform>();
            if (rtBtn != null)
            {
                rtBtn.anchorMin = new Vector2(0.5f, 1f);
                rtBtn.anchorMax = new Vector2(0.5f, 1f);
                rtBtn.pivot = new Vector2(0.5f, 1f);
                rtBtn.sizeDelta = new Vector2(w, h);
                rtBtn.anchoredPosition = pos;
            }

            var raw = btnTrans.GetComponent<RawImage>();
            if (raw != null) DestroyImmediate(raw);

            var img = btnTrans.GetComponent<Image>() ?? btnTrans.gameObject.AddComponent<Image>();

            int width = Mathf.RoundToInt(w);
            int height = Mathf.RoundToInt(h);
            Texture2D btnTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < 1 || x >= width - 1 || y < 1 || y >= height - 1)
                        btnTex.SetPixel(x, y, borderColor);
                    else
                        btnTex.SetPixel(x, y, normalColor);
                }
            }
            btnTex.Apply();

            img.sprite = Sprite.Create(btnTex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            img.color = Color.white;

            var btn = btnTrans.GetComponent<Button>();
            if (btn != null)
            {
                btn.transition = Selectable.Transition.ColorTint;
                btn.targetGraphic = img;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                btn.colors = colors;
            }

            if (txtTrans != null)
            {
                txtTrans.SetParent(btnTrans, false);
                txtTrans.localScale = Vector3.one;

                var shadow = txtTrans.GetComponent<UnityEngine.UI.Shadow>();
                if (shadow != null) DestroyImmediate(shadow);
                var outline = txtTrans.GetComponent<UnityEngine.UI.Outline>();
                if (outline != null) DestroyImmediate(outline);

                var rtTxt = txtTrans.GetComponent<RectTransform>();
                if (rtTxt != null)
                {
                    rtTxt.anchorMin = Vector2.zero;
                    rtTxt.anchorMax = Vector2.one;
                    rtTxt.pivot = new Vector2(0.5f, 0.5f);
                    rtTxt.sizeDelta = Vector2.zero;
                    rtTxt.anchoredPosition = Vector2.zero;
                }

                var textComp = txtTrans.GetComponent<Text>();
                if (textComp != null)
                {
                    textComp.text = buttonText;
                    textComp.alignment = TextAnchor.MiddleCenter;
                    textComp.color = textColor;
                    textComp.font = KOUIManager.Instance != null ? KOUIManager.Instance.GetSafeFont(11) : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    textComp.fontStyle = FontStyle.Bold;
                }
            }
        }

        public void SetExchangeItem(int invPos)
        {
            if (invPos < 0 || invPos >= 28) return;
            if (_isSpinning) return;

            var item = KOInventory.Instance.m_pMyInvWnd[invPos];
            if (item == null || item.itemId == 0) return;

            // Check if this item is a valid breakable gem/chest
            if (!_optionMapping.ContainsKey(item.itemId))
            {
                KOMessageBox.Instance?.ShowOk("This item cannot be exchanged at this NPC.", "Error");
                return;
            }

            _selectedInvPos = invPos;
            _currentItemId = item.itemId;

            // Draw Item Icon
            if (_placedItemIcon != null) Destroy(_placedItemIcon);
            _placedItemIcon = CreateSlotIcon(_itemSlotArea, item.itemId, "PlacedItemIcon");

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.PopulateUpgradeInventory();
            }
        }

        private void OnStartClicked()
        {
            if (_selectedInvPos == -1 || _currentItemId == 0)
            {
                KOMessageBox.Instance?.ShowOk("Please place a gem or chest in the slot.", "Error");
                return;
            }

            if (_isSpinning) return;

            // Capture inventory snapshot before sending packet
            CaptureInventorySnapshot();

            if (_manualToggle != null && _manualToggle.isOn)
            {
                // Manual Mode: Spin until Stop clicked
                StartSpinAnimation();
            }
            else
            {
                // Auto Mode: Start coroutine loop
                if (_exchangeRoutine != null) StopCoroutine(_exchangeRoutine);
                _exchangeRoutine = StartCoroutine(AutoExchangeLoop());
            }
        }

        private void OnStopClicked()
        {
            if (!_isSpinning)
            {
                // If not spinning, check if we need to stop the auto loop
                if (_exchangeRoutine != null)
                {
                    StopCoroutine(_exchangeRoutine);
                    _exchangeRoutine = null;
                    ResetPanel();
                }
                return;
            }

            if (_manualToggle != null && _manualToggle.isOn && _isSpinning && !_isWaitingForServerResponse)
            {
                // Send exchange request to server
                SendExchangePacket();
            }
        }

        private void OnShowDropClicked()
        {
            if (_currentItemId == 0)
            {
                KOMessageBox.Instance?.ShowOk("Please place the item in the slot to see its drop list.", "Information");
                return;
            }

            // Display a custom drop/rates summary based on the active item
            string info = GetDropRateInfo(_currentItemId);
            KOMessageBox.Instance?.ShowOk(info, "Drop Probabilities");
        }

        private string GetDropRateInfo(int itemId)
        {
            if (itemId == 379106000) // Abyss Gem
            {
                return "- Upgrade Scroll (+0): 11.16%\n- HP/MP/Res Potions: 7.44%\n- Chitin Armor (+1): 0.25% (per piece)\n- Full Plate Armor (+1): 1.00%\n- High-class Weapons (+1): 0.10%\n- Gold Bar: 0.01%";
            }
            else if (itemId == 379155000) // Green Chest
            {
                return "- High-class Weapons (+1) (Glave, Mirage, Elixir): 2.50%\n- Shard / Raptor / Graham (+1): 1.25%\n- Chitin Armor (+1): 1.25% (per piece)\n- Chitin Shell (+1): 0.50% (per piece)";
            }
            else if (itemId == 379156000) // Blue Chest
            {
                return "- Unique Weapons (Dark Vane, Hell Breaker, DL): 2.50%\n- Chitin Shell (+1): 1.00% (per piece)\n- High-class Weapons (+1): 2.50%\n- Shard / Raptor / II (+1): 1.25%";
            }
            return "Probability table not found.";
        }

        private void StartSpinAnimation()
        {
            _isSpinning = true;
            StartCoroutine(SpinSlotsCoroutine());
        }

        private void StopSpinAnimation(int resultItemId)
        {
            _isSpinning = false;
            
            // Set output icons: Middle is result, sides are random adjacent
            ClearGiveIcons();
            
            if (resultItemId > 0)
            {
                // Only show the won item in the middle slot (1), side slots (0 and 2) remain empty
                _giveItemIcons[1] = CreateSlotIcon(_giveSlotAreas[1], resultItemId, "ResultIcon");
            }
        }

        public void OnWarehouseItemWon(int itemId)
        {
            _notifiedWarehouseItemId = itemId;
        }

        private void SendExchangePacket()
        {
            if (!_optionMapping.TryGetValue(_currentItemId, out int optionIndex)) return;

            _isWaitingForServerResponse = true;
            
            // Send selected menu option index and stock warehouse toggle to the server
            using var p = new KOPacketWriter(WizOpcode.WIZ_SELECT_MSG);
            p.WriteByte((byte)optionIndex);
            p.WriteByte((byte)(_stockInnToggle != null && _stockInnToggle.isOn ? 1 : 0));
            
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null)
            {
                netMgr.SendPacket(p);
            }
        }

        private IEnumerator AutoExchangeLoop()
        {
            while (true)
            {
                if (_selectedInvPos == -1 || KOInventory.Instance.m_pMyInvWnd[_selectedInvPos].itemId != _currentItemId)
                {
                    // Find next gem/chest of the same type in the inventory
                    int nextPos = FindNextItemOfSameType(_currentItemId);
                    if (nextPos != -1)
                    {
                        SetExchangeItem(nextPos);
                    }
                    else
                    {
                        // No more items, end loop
                        ResetPanel();
                        yield break;
                    }
                }

                // Capture snapshot
                CaptureInventorySnapshot();

                // Start spin
                StartSpinAnimation();
                yield return new WaitForSeconds(0.6f);

                // Send request
                SendExchangePacket();

                // Wait for network response (which triggers HandleInventoryChange)
                float timeout = 2.0f;
                while (_isWaitingForServerResponse && timeout > 0)
                {
                    timeout -= 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }

                // Pause to let player see the result
                yield return new WaitForSeconds(0.8f);
            }
        }

        private IEnumerator SpinSlotsCoroutine()
        {
            while (_isSpinning)
            {
                ClearGiveIcons();
                for (int i = 0; i < 3; i++)
                {
                    int sampleId = GetRandomSampleDrop(_currentItemId);
                    _giveItemIcons[i] = CreateSlotIcon(_giveSlotAreas[i], sampleId, $"SpinIcon_{i}");
                }
                yield return new WaitForSeconds(0.06f);
            }
        }

        private int GetRandomSampleDrop(int sourceId)
        {
            // Simple subset of drops for visual spinning shuffler
            if (sourceId == 379106000) // Abyss
            {
                int[] list = { 379016000, 389020000, 389019000, 389013000, 204001001, 244001001, 205001001 };
                return list[UnityEngine.Random.Range(0, list.Length)];
            }
            else // Chests
            {
                int[] list = { 156110001, 111110001, 126310001, 149101103, 119101101, 206001001, 246001001 };
                return list[UnityEngine.Random.Range(0, list.Length)];
            }
        }

        private int FindNextItemOfSameType(int itemId)
        {
            if (KOInventory.Instance == null) return -1;
            for (int i = 0; i < 28; i++)
            {
                var item = KOInventory.Instance.m_pMyInvWnd[i];
                if (item != null && item.itemId == itemId)
                {
                    return i;
                }
            }
            return -1;
        }

        private void CaptureInventorySnapshot()
        {
            if (KOInventory.Instance == null) return;
            for (int i = 0; i < 28; i++)
            {
                var item = KOInventory.Instance.m_pMyInvWnd[i];
                _inventorySnapshot[i] = new ItemSnapshot
                {
                    ItemId = item != null ? item.itemId : 0,
                    Count = item != null ? item.count : 0
                };
            }
        }

        private void HandleInventoryChange(byte[] rawData)
        {
            if (_isWaitingForServerResponse)
            {
                StartCoroutine(DetectDroppedItemCoroutine());
            }
        }

        private IEnumerator DetectDroppedItemCoroutine()
        {
            _isWaitingForServerResponse = false;
            
            // Wait 100ms for local inventory cache to fully update from network packets
            yield return new WaitForSeconds(0.1f);

            int droppedItemId = 0;
            int droppedSlotIndex = -1;

            if (_stockInnToggle != null && _stockInnToggle.isOn && _notifiedWarehouseItemId > 0)
            {
                droppedItemId = _notifiedWarehouseItemId;
                _notifiedWarehouseItemId = 0;
            }
            else if (KOInventory.Instance != null)
            {
                for (int i = 0; i < 28; i++)
                {
                    var currentItem = KOInventory.Instance.m_pMyInvWnd[i];
                    var snapshot = _inventorySnapshot[i];

                    if (currentItem.itemId != snapshot.ItemId)
                    {
                        if (currentItem.itemId > 0 && currentItem.itemId != _currentItemId)
                        {
                            droppedItemId = currentItem.itemId;
                            droppedSlotIndex = i;
                            break;
                        }
                    }
                    else if (currentItem.itemId == snapshot.ItemId && currentItem.count > snapshot.Count)
                    {
                        if (currentItem.itemId > 0 && currentItem.itemId != _currentItemId)
                        {
                            droppedItemId = currentItem.itemId;
                            droppedSlotIndex = i;
                            break;
                        }
                    }
                }
            }

            if (droppedItemId > 0)
            {
                // Stop spin on this item
                StopSpinAnimation(droppedItemId);
            }
            else
            {
                // Fallback / fail
                _isSpinning = false;
            }

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.PopulateUpgradeInventory();
            }
        }

        private void DepositToWarehouse(int invSlot, int itemId)
        {
            var item = KOInventory.Instance.m_pMyInvWnd[invSlot];
            if (item == null || item.itemId != itemId) return;

            // Find first empty warehouse slot
            int page, pos;
            if (FindFirstEmptyWarehouseSlot(out page, out pos))
            {
                KOWarehouseManager.Instance.SendToServerToWareMsg(itemId, (byte)page, (byte)invSlot, (byte)pos, item.count);
            }
            else
            {
                Debug.LogWarning("[AUTO-BANK] Failed to auto-deposit: Warehouse is full!");
            }
        }

        private bool FindFirstEmptyWarehouseSlot(out int page, out int pos)
        {
            page = 0;
            pos = 0;
            var ware = KOWarehouseManager.Instance;
            if (ware == null) return false;

            for (int p = 0; p < KOWarehouseManager.MAX_ITEM_WARE_PAGE; p++)
            {
                for (int s = 0; s < KOWarehouseManager.MAX_ITEM_TRADE; s++)
                {
                    var slot = ware.GetSlot(p, s);
                    if (slot == null || slot.ItemId == 0)
                    {
                        page = p;
                        pos = s;
                        return true;
                    }
                }
            }
            return false;
        }

        private GameObject CreateSlotIcon(Transform parent, int itemId, string name)
        {
            var iconObj = new GameObject(name);
            iconObj.transform.SetParent(parent, false);
            var iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.sizeDelta = Vector2.zero;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;

            var icon = KOItemIconLoader.LoadItemIcon(ResolveIconId(itemId));
            var img = iconObj.AddComponent<Image>();
            if (icon != null)
            {
                img.sprite = icon;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            }
            img.raycastTarget = true;

            // Add tooltip click handler so players can click to view item stats
            var handler = iconObj.AddComponent<KOItemSlotHandler>();
            handler.tooltipItemDefId = itemId;
            handler.tooltipShowPrice = false;

            return iconObj;
        }

        private int ResolveIconId(int itemId)
        {
            var itemDef = ItemDataManager.GetItemBasic(itemId);
            if (itemDef != null)
            {
                return (int)itemDef.DwIDIcon;
            }
            return itemId / 1000 * 1000;
        }

        private void ResetPanel()
        {
            _selectedInvPos = -1;
            _currentItemId = 0;
            _isSpinning = false;
            _isWaitingForServerResponse = false;

            if (_placedItemIcon != null) Destroy(_placedItemIcon);
            ClearGiveIcons();

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.PopulateUpgradeInventory();
            }
        }

        private void ClearGiveIcons()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_giveItemIcons[i] != null)
                {
                    Destroy(_giveItemIcons[i]);
                    _giveItemIcons[i] = null;
                }
            }
        }
    }

    public class KOGemChestExchangeDropTarget : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            var dragSource = KOItemDragHandler.CurrentDragSource;
            if (dragSource == null) return;
            if (dragSource.district != KOItemDragHandler.SlotDistrict.BagSlot) return;

            int invPos = dragSource.slotIndex;
            if (KOGemChestExchangeManager.Instance != null)
            {
                KOGemChestExchangeManager.Instance.SetExchangeItem(invPos);
            }
        }
    }
}
