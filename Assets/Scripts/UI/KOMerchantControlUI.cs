using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EntropyOnline.Trade;
using EntropyOnline.Core;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    public class KOMerchantControlUI : MonoBehaviour
    {
        private Transform _scrollContent;
        private Button _btnClose;
        private Button _btnCloseMerchant;

        // Auction House / Settings Color Scheme
        private Color _colorBg = new Color(0f, 0f, 0f, 185f / 255f);          // Dark brown/bronze charcoal
        private Color _colorBorder = new Color(0.43f, 0.36f, 0.26f, 1f);      // Gold/bronze border
        private Color _colorTextGold = new Color(0.9f, 0.8f, 0.6f, 1f);      // Golden text
        private Color _colorBtnNormal = new Color(0.06f, 0.05f, 0.04f, 0.95f); // Inner row bg
        private Color _colorBtnActive = new Color(0.48f, 0.38f, 0.22f, 1f);   // Sidebar button active
        private Color _colorInputBg = new Color(0.05f, 0.04f, 0.04f, 1f);    // Inner content dark input bg
        private Color _colorClaimBtn = new Color(0.12f, 0.28f, 0.12f, 1f);    // Dark green claim button
        private Color _colorClaimBorder = new Color(0.25f, 0.55f, 0.25f, 1f); // Bright green claim border
        private Color _colorCloseMerch = new Color(0.45f, 0.05f, 0.08f, 1f); // Dark red close merchant
        private Color _colorCloseMerchBorder = new Color(0.75f, 0.15f, 0.15f, 1f); // Bright red close border

        private void Awake()
        {
            BuildUIDynamically();
        }

        private void OnEnable()
        {
            KOMerchantManager.Instance?.SendMerchantControlListReq();
            RefreshUI();
            KOUIManager.Instance?.RepositionSkillBarForPanel();
        }

        private void OnDisable()
        {
            KOUIManager.Instance?.RepositionSkillBarForPanel();
        }
        private void Update()
        {
            // Cancel out Canvas scale factor to keep the pixel size completely constant
            var rt = GetComponent<RectTransform>();
            var canvas = GetComponentInParent<Canvas>();
            if (rt != null && canvas != null && canvas.scaleFactor > 0f)
            {
                float targetScale = 1f / canvas.scaleFactor;
                if (Mathf.Abs(rt.localScale.x - targetScale) > 0.001f)
                {
                    rt.localScale = new Vector3(targetScale, targetScale, 1f);
                }
            }
        }
        private GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            return go;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string text, int fontSize, TextAlignmentOptions alignment)
        {
            var go = CreateUIObject("Text", parent.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = _colorTextGold;
            txt.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return txt;
        }

        private void BuildUIDynamically()
        {
            // 1. Setup RectTransform for the whole window (anchored right center, compact height)
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-50f, 6.5f); // Shortened from bottom, keeping top aligned with inventory
            rt.sizeDelta = new Vector2(320f, 437f); // Shortened panel height to 437px

            // Add background panel using our premium generated skill theme sprite
            var mainImg = gameObject.AddComponent<Image>();
            mainImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("merchant_control_panel_bg", 320, 437, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f),
                    new Color(0.04f, 0.04f, 0.04f, 0.98f),
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),
                    2) : null;
            mainImg.color = Color.white;

            var cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = true;

            // 2. Header
            var headerGO = CreateUIObject("Header", transform);
            var headerRT = headerGO.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, -5f);
            headerRT.sizeDelta = new Vector2(-20f, 30f);

            var titleTxt = CreateText(headerGO, "Merchant Control", 14, TextAlignmentOptions.Center);
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Gold title color matching Skill Page

            var titleShadow = titleTxt.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = Color.black;
            titleShadow.effectDistance = new Vector2(1f, -1f);


            // Top-right Close button
            var closeGO = CreateUIObject("btn_close", headerGO.transform);
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 0.5f);
            closeRT.anchorMax = new Vector2(1, 0.5f);
            closeRT.pivot = new Vector2(1, 0.5f);
            closeRT.anchoredPosition = new Vector2(0f, 0);
            closeRT.sizeDelta = new Vector2(25f, 25f);

            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.1f, 0.1f, 1f); // Red exit button
            var closeTxt = CreateText(closeGO, "X", 10, TextAlignmentOptions.Center);
            closeTxt.color = Color.white;
            _btnClose = closeGO.AddComponent<Button>();
            _btnClose.onClick.AddListener(CloseMenu);

            // 2b. Banner Image ( Moradon stalls graphic )
            var bannerGO = CreateUIObject("Banner", transform);
            var bannerRT = bannerGO.GetComponent<RectTransform>();
            bannerRT.anchorMin = new Vector2(0.5f, 1f);
            bannerRT.anchorMax = new Vector2(0.5f, 1f);
            bannerRT.pivot = new Vector2(0.5f, 1f);
            bannerRT.anchoredPosition = new Vector2(0f, -35f);
            bannerRT.sizeDelta = new Vector2(300f, 60f);

            var bannerImg = bannerGO.AddComponent<Image>();
            var bannerTex = Resources.Load<Texture2D>("KOTextures/UI/merchant_banner");
            if (bannerTex != null)
            {
                float cropWidth = bannerTex.width;
                float cropHeight = cropWidth * 60f / 300f; // Keep 5:1 aspect ratio
                float cropY = (bannerTex.height - cropHeight) / 2f;
                bannerImg.sprite = Sprite.Create(bannerTex, new Rect(0f, cropY, cropWidth, cropHeight), new Vector2(0.5f, 0.5f));
            }
            bannerImg.color = Color.white;

            // 2bb. Banner Bottom Gradient Overlay (to fake bottom fadeout without modifying texture pixels)
            var overlayGO = CreateUIObject("BannerOverlay", bannerGO.transform);
            var overlayRT = overlayGO.GetComponent<RectTransform>();
            overlayRT.anchorMin = new Vector2(0f, 0f);
            overlayRT.anchorMax = new Vector2(1f, 0f);
            overlayRT.pivot = new Vector2(0.5f, 0f);
            overlayRT.anchoredPosition = new Vector2(0f, -1.5f); // Extend 1.5 pixels below to cover the outline border and prevent 1px gap
            overlayRT.sizeDelta = new Vector2(0f, 36.5f); // Total height 36.5px (covers bottom 35px of banner + 1.5px bleed)

            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemePanelBgSprite("banner_overlay_grad", 300, 37, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0f),       // Top: transparent warm charcoal
                    new Color(0.12f, 0.10f, 0.08f, 0.98f),    // Bottom: opaque panel background color
                    Color.clear, 0) : null;
            overlayImg.color = Color.white;

            var bannerOutline = bannerGO.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.6f, 0.48f, 0.22f, 0.4f);
            bannerOutline.effectDistance = new Vector2(1f, 1f);

            // 2c. Column Headers
            var headersGO = CreateUIObject("ColumnHeaders", transform);
            var headersRT = headersGO.GetComponent<RectTransform>();
            headersRT.anchorMin = new Vector2(0.5f, 1f);
            headersRT.anchorMax = new Vector2(0.5f, 1f);
            headersRT.pivot = new Vector2(0.5f, 1f);
            headersRT.anchoredPosition = new Vector2(0f, -95f); // Adjusted to prevent overlap from ListArea
            headersRT.sizeDelta = new Vector2(300f, 25f);

            // ITEM STATUS header (Left-aligned)
            var hStatusGO = CreateUIObject("Header_Status", headersGO.transform);
            var hStatusRT = hStatusGO.GetComponent<RectTransform>();
            hStatusRT.anchorMin = new Vector2(0f, 0f);
            hStatusRT.anchorMax = new Vector2(0.3f, 1f);
            hStatusRT.pivot = new Vector2(0f, 0.5f);
            hStatusRT.offsetMin = Vector2.zero;
            hStatusRT.offsetMax = Vector2.zero;
            var tStatus = CreateText(hStatusGO, "ITEM STATUS", 9, TextAlignmentOptions.MidlineLeft);
            tStatus.color = _colorTextGold;
            tStatus.fontStyle = FontStyles.Bold;
            tStatus.rectTransform.anchoredPosition += new Vector2(2f, 0f); // Shift 2px to the right

            // CLAIMABLE COINS header (Center-aligned)
            var hCoinsGO = CreateUIObject("Header_Coins", headersGO.transform);
            var hCoinsRT = hCoinsGO.GetComponent<RectTransform>();
            hCoinsRT.anchorMin = new Vector2(0.3f, 0f);
            hCoinsRT.anchorMax = new Vector2(0.7f, 1f);
            hCoinsRT.pivot = new Vector2(0.5f, 0.5f);
            hCoinsRT.offsetMin = Vector2.zero;
            hCoinsRT.offsetMax = Vector2.zero;
            var tCoins = CreateText(hCoinsGO, "CLAIMABLE COINS", 9, TextAlignmentOptions.Center);
            tCoins.color = _colorTextGold;
            tCoins.fontStyle = FontStyles.Bold;

            // CLAIM COINS header (Right-aligned)
            var hClaimGO = CreateUIObject("Header_Claim", headersGO.transform);
            var hClaimRT = hClaimGO.GetComponent<RectTransform>();
            hClaimRT.anchorMin = new Vector2(0.7f, 0f);
            hClaimRT.anchorMax = new Vector2(1f, 1f);
            hClaimRT.pivot = new Vector2(1f, 0.5f);
            hClaimRT.offsetMin = Vector2.zero;
            hClaimRT.offsetMax = Vector2.zero;
            var tClaim = CreateText(hClaimGO, "CLAIM COINS", 9, TextAlignmentOptions.MidlineRight);
            tClaim.color = _colorTextGold;
            tClaim.fontStyle = FontStyles.Bold;
            tClaim.rectTransform.anchoredPosition += new Vector2(-2f, 0f); // Shift 2px to the left

            // Underline divider for headers (matching skilltree panel's SKILL PAGE underline divider exactly)
            var dividerGO = CreateUIObject("Divider", headersGO.transform);
            var divRT = dividerGO.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0f, 0f);
            divRT.anchorMax = new Vector2(1f, 0f);
            divRT.pivot = new Vector2(0.5f, 0f);
            divRT.anchoredPosition = Vector2.zero;
            divRT.sizeDelta = new Vector2(0f, 2f); // Set to 2px height to match SKILL PAGE divider exactly
            var divImg = dividerGO.AddComponent<Image>();
            divImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemeFadingDividerSprite("headers_divider", 280, 2,
                    new Color(0.9f, 0.75f, 0.25f, 0.8f)) : null;
            divImg.color = Color.white;

            // 3. Scroll Content Area (Main pazar list area - Shifted downwards and shortened)
            var listAreaGO = CreateUIObject("ListArea", transform);
            var listAreaRT = listAreaGO.GetComponent<RectTransform>();
            listAreaRT.anchorMin = new Vector2(0f, 1f); // Top stretch anchor
            listAreaRT.anchorMax = new Vector2(1f, 1f);
            listAreaRT.pivot = new Vector2(0.5f, 1f); // Top center pivot
            listAreaRT.anchoredPosition = new Vector2(0f, -125f); // Top edge is exactly 125px from top of panel
            listAreaRT.sizeDelta = new Vector2(-20f, 270f); // Height 270px (Option A for perfect symmetry)

            var listImg = listAreaGO.AddComponent<Image>();
            listImg.color = _colorInputBg;
            var listOutline = listAreaGO.AddComponent<Outline>();
            listOutline.effectColor = _colorBorder * 0.5f;

            // Add ScrollRect
            var scrollRect = listAreaGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewportGO = CreateUIObject("Viewport", listAreaGO.transform);
            var viewportRT = viewportGO.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            var contentGO = CreateUIObject("Content", viewportGO.transform);
            var contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);
            scrollRect.content = contentRT;

            var layout = contentGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 0;
            layout.padding = new RectOffset(6, 6, 9, 9);
            layout.childControlHeight = false; // Respect row height
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;

            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollContent = contentRT.transform;

            // 4. Bottom Button: Close Merchant (shrunk, rounded corners with radius 10)
            var closeMerchGO = CreateUIObject("btn_close_merchant", transform);
            var cmRT = closeMerchGO.GetComponent<RectTransform>();
            cmRT.anchorMin = new Vector2(0.5f, 0);
            cmRT.anchorMax = new Vector2(0.5f, 0);
            cmRT.pivot = new Vector2(0.5f, 0);
            cmRT.anchoredPosition = new Vector2(0, 10f); // Shifted 2px down
            cmRT.sizeDelta = new Vector2(140f, 24f); // Shrunk to 140x24 size

            var cmImg = closeMerchGO.AddComponent<Image>();
            cmImg.sprite = KOUIManager.Instance != null ?
                KOUIManager.Instance.GetSkillThemeRoundedRectSprite("close_merchant_btn_bg", 140, 24, 10,
                    _colorCloseMerch, _colorCloseMerchBorder, 1) : null;
            cmImg.color = Color.white;

            var cmTxt = CreateText(closeMerchGO, "Close Merchant", 10, TextAlignmentOptions.Center); // Shrunk text font size to 10
            cmTxt.color = Color.white;
            cmTxt.fontStyle = FontStyles.Bold;

            _btnCloseMerchant = closeMerchGO.AddComponent<Button>();
            _btnCloseMerchant.onClick.AddListener(CloseMerchantStall);
        }

        private Sprite GetItemSprite(int itemId)
        {
            int iconId = itemId;
            if (itemId == 800085000) iconId = 800078000;
            else if (itemId > 0)
            {
                var pItem = KOInventory.s_pTbl_Items_Basic != null ? KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, itemId) : null;
                if (pItem != null) iconId = (int)pItem.dwIDIcon;
            }
            return KOItemIconLoader.LoadItemIcon(iconId);
        }

        public void RefreshUI()
        {
            if (_scrollContent == null) return;

            // Clear old list items
            foreach (Transform child in _scrollContent)
            {
                Destroy(child.gameObject);
            }

            var mgr = KOMerchantManager.Instance;
            if (mgr == null || !mgr.IsSelling)
            {
                // Draw empty state
                var emptyRow = CreateUIObject("EmptyRow", _scrollContent);
                var rowRT = emptyRow.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 60f);

                var txt = CreateText(emptyRow, "No active merchant found.", 11, TextAlignmentOptions.Center);
                txt.color = Color.gray;
                return;
            }

            int activeItemCount = 0;
            for (int i = 0; i < KOMerchantManager.MAX_MERCH_ITEMS; i++)
            {
                var item = mgr.SellingSetupItems[i];
                if (item == null || item.IsEmpty) continue;

                activeItemCount++;
                int itemIndex = i;
                int sold = item.SoldCount;
                int remaining = Math.Max(0, item.Count);

                // Create Row Container
                var rowGO = CreateUIObject($"ItemRow_{itemIndex}", _scrollContent);
                var rowRT = rowGO.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 42f);

                var rowImg = rowGO.AddComponent<Image>();
                rowImg.color = _colorBtnNormal;
                var rowOutline = rowGO.AddComponent<Outline>();
                rowOutline.effectColor = _colorBorder * 0.3f;

                // 1. Left: Item Icon slot
                var iconGO = CreateUIObject("Icon", rowGO.transform);
                var iconRT = iconGO.GetComponent<RectTransform>();
                iconRT.anchorMin = new Vector2(0, 0.5f);
                iconRT.anchorMax = new Vector2(0, 0.5f);
                iconRT.pivot = new Vector2(0, 0.5f);
                iconRT.anchoredPosition = new Vector2(5f, 0);
                iconRT.sizeDelta = new Vector2(32f, 32f);

                var iconImg = iconGO.AddComponent<Image>();
                iconImg.sprite = GetItemSprite(item.ItemId);
                var iconOutline = iconGO.AddComponent<Outline>();
                iconOutline.effectColor = _colorBorder * 0.4f;

                // 2. Column 1 (ITEM STATUS): count ratio next to icon
                var countGO = CreateUIObject("Count", rowGO.transform);
                var countRT = countGO.GetComponent<RectTransform>();
                countRT.anchorMin = new Vector2(0f, 0f);
                countRT.anchorMax = new Vector2(0.3f, 1f);
                countRT.pivot = new Vector2(0f, 0.5f);
                countRT.offsetMin = new Vector2(42f, 0f);
                countRT.offsetMax = new Vector2(0f, 0f);
                var countTxt = CreateText(countGO, $"{remaining}/{sold}", 10, TextAlignmentOptions.MidlineLeft);
                countTxt.color = Color.white;

                // 3. Column 2 (CLAIMABLE COINS): "Not sold yet." or the coin amount
                var coinsGO = CreateUIObject("Coins", rowGO.transform);
                var coinsRT = coinsGO.GetComponent<RectTransform>();
                coinsRT.anchorMin = new Vector2(0.3f, 0f);
                coinsRT.anchorMax = new Vector2(0.7f, 1f);
                coinsRT.pivot = new Vector2(0.5f, 0.5f);
                coinsRT.offsetMin = Vector2.zero;
                coinsRT.offsetMax = Vector2.zero;

                string coinText = sold == 0 ? "Not sold yet." : $"{item.ClaimableCoins:N0}";
                var coinsTxt = CreateText(coinsGO, coinText, 10, TextAlignmentOptions.Center);
                coinsTxt.color = sold == 0 ? Color.gray : new Color(0.2f, 0.9f, 0.2f, 1f); // Green for claimable coins

                // 4. Column 3 (CLAIM COINS button - only if claimable coins > 0)
                if (item.ClaimableCoins > 0)
                {
                    var claimGO = CreateUIObject("btn_claim", rowGO.transform);
                    var claimRT = claimGO.GetComponent<RectTransform>();
                    claimRT.anchorMin = new Vector2(0.85f, 0.5f);
                    claimRT.anchorMax = new Vector2(0.85f, 0.5f);
                    claimRT.pivot = new Vector2(0.5f, 0.5f);
                    claimRT.anchoredPosition = Vector2.zero;
                    claimRT.sizeDelta = new Vector2(50f, 22f);

                    var claimImg = claimGO.AddComponent<Image>();
                    claimImg.color = _colorClaimBtn;
                    var claimOutline = claimGO.AddComponent<Outline>();
                    claimOutline.effectColor = _colorClaimBorder;

                    var claimTxt = CreateText(claimGO, "CLAIM", 8, TextAlignmentOptions.Center);
                    claimTxt.color = Color.white;
                    claimTxt.fontStyle = FontStyles.Bold;

                    var btn = claimGO.AddComponent<Button>();
                    btn.onClick.AddListener(() => ClaimCoins(itemIndex));
                }
            }

            if (activeItemCount == 0)
            {
                var emptyRow = CreateUIObject("EmptyRow", _scrollContent);
                var rowRT = emptyRow.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, 60f);

                var txt = CreateText(emptyRow, "Pazarda ürün kalmadı.\n(All items sold out)", 11, TextAlignmentOptions.Center);
                txt.color = Color.gray;
            }
        }

        private void ClaimCoins(int merchantSlotIndex)
        {
            var mgr = KOMerchantManager.Instance;
            if (mgr == null) return;

            var item = mgr.SellingSetupItems[merchantSlotIndex];
            if (item == null || item.IsEmpty || item.ClaimableCoins <= 0) return;

            long currentGold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
            long claimAmount = item.ClaimableCoins;

            // Gold Limit Check (Max 2.1B / 21 GB)
            if (currentGold + claimAmount > 2100000000)
            {
                KOMessageBox.Instance?.ShowOk("You cannot exceed the maximum coin limit (21 GB)!", "Error");
                Debug.LogWarning("[MerchantControl] Claim rejected: Gold limit exceeded.");
                return;
            }

            // Send Claim Packet to Server
            mgr.SendMerchantClaimCoins((byte)merchantSlotIndex);
        }

        private void CloseMerchantStall()
        {
            var mgr = KOMerchantManager.Instance;
            if (mgr == null || !mgr.IsSelling) return;

            // 1. Count remaining unsold items and total claimable coins
            int unsoldItems = 0;
            long totalClaimableCoins = 0;

            for (int i = 0; i < KOMerchantManager.MAX_MERCH_ITEMS; i++)
            {
                var item = mgr.SellingSetupItems[i];
                if (item == null || item.IsEmpty) continue;

                if (item.Count > 0)
                {
                    unsoldItems++;
                }
                totalClaimableCoins += item.ClaimableCoins;
            }

            // 2. Envanter Slot Check (Checking empty slots for unsold items)
            int emptySlots = 0;
            if (KOInventory.Instance != null && KOInventory.Instance.m_pMyInvWnd != null)
            {
                for (int i = 0; i < KOInventory.Instance.m_pMyInvWnd.Length; i++)
                {
                    if (KOInventory.Instance.m_pMyInvWnd[i] == null)
                    {
                        emptySlots++;
                    }
                }
            }

            if (emptySlots < unsoldItems)
            {
                KOMessageBox.Instance?.ShowOk("There is not enough space in your inventory!", "Error");
                Debug.LogWarning($"[MerchantControl] Close shop rejected: Not enough inventory slots. Unsold: {unsoldItems}, Empty slots: {emptySlots}");
                return;
            }

            // 3. Gold Limit Check for claiming all remaining coins on close
            long currentGold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
            if (currentGold + totalClaimableCoins > 2100000000)
            {
                KOMessageBox.Instance?.ShowOk("You cannot exceed the maximum coin limit (21 GB)!", "Error");
                Debug.LogWarning("[MerchantControl] Close shop rejected: Gold limit exceeded on close.");
                return;
            }

            // Send Close Merchant request to server
            mgr.SendMerchantClose();
        }


        private void CloseMenu()
        {
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.CloseMerchantControl();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
