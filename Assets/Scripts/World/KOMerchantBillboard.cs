using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using EntropyOnline.UI;
using EntropyOnline.Trade;
using EntropyOnline.Import;
using EntropyOnline.Core;

namespace EntropyOnline.World
{
    /// <summary>
    /// Knight Online 1:1 Overhead Interactive Merchant Billboard.
    /// Spawns a floating billboard above the merchant character/stall,
    /// displaying the first 4 items with upgrade overlays (+N) and buttons to OPEN/WHISPER.
    /// </summary>
    public class KOMerchantBillboard : MonoBehaviour, KOProximityInteractRegistry.IProximityInteractable
    {
        private Canvas _canvas;
        private RectTransform _canvasRT;
        private int _socketId;
        private string _playerName;
        private int[] _activeItemIds = new int[4];
        private Transform _localPlayerTransform;
        private Material _customUiMat;

        private float _currentDistanceToPlayer = 999f;
        private bool _isInRange = false;

        public Transform GetTransform() { return transform; }
        public float GetCurrentDistance() { return _currentDistanceToPlayer; }
        public void SetCurrentDistance(float dist) { _currentDistanceToPlayer = dist; }
        public string PlayerName => _playerName;
        public void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.gameObject.activeSelf != visible)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }
        public bool IsInRange() { return _isInRange; }

        private void OnDisable()
        {
            KOProximityInteractRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            KOProximityInteractRegistry.Unregister(this);
        }

        public void Initialize(int socketId, string playerName, int[] itemIds)
        {
            _socketId = socketId;
            _playerName = playerName;
            
            // Extract the first 4 active (non-zero) items
            int count = 0;
            if (itemIds != null)
            {
                for (int i = 0; i < itemIds.Length; i++)
                {
                    if (itemIds[i] > 0)
                    {
                        _activeItemIds[count++] = itemIds[i];
                        if (count >= 4) break;
                    }
                }
            }

            CreateUI();
        }

        private void CreateUI()
        {
            bool isMyMerchant = (GameManager.Instance != null && (GameManager.Instance.CharacterName == _playerName || GameManager.Instance.CharacterId == _socketId || (short)GameManager.Instance.CharacterId == (short)_socketId));

            // Create ZTest Always material using GUI/Text Shader so it is never obscured by models
            var uiShader = Shader.Find("GUI/Text Shader");
            if (uiShader != null)
            {
                _customUiMat = new Material(uiShader);
            }

            // 1. Create child GameObject for Billboard Canvas
            GameObject canvasObj = new GameObject("MerchantBillboardCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 2.0f, 0f); // Floats above character
            canvasObj.transform.localRotation = Quaternion.identity;
            canvasObj.transform.localScale = Vector3.one * 0.014f; // World Space scaling

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 101; // Render above names

            _canvasRT = canvasObj.GetComponent<RectTransform>();
            _canvasRT.sizeDelta = isMyMerchant ? new Vector2(200f, 95f) : new Vector2(200f, 125f);

            // Enable mouse click/tap detection in world space
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Background Panel
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRT = bgObj.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.clear; // Invisible bounding box for raycast click safety

            // Create Name Text above the item grid
            GameObject nameObj = new GameObject("MerchantNameText");
            nameObj.transform.SetParent(canvasObj.transform, false);
            var nameRT = nameObj.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.5f, 1f);
            nameRT.anchorMax = new Vector2(0.5f, 1f);
            nameRT.pivot = new Vector2(0.5f, 1f);
            nameRT.anchoredPosition = new Vector2(0f, -8f);
            nameRT.sizeDelta = new Vector2(180f, 22f);

            var nameTxt = nameObj.AddComponent<Text>();
            nameTxt.text = _playerName;
            nameTxt.fontSize = 17;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = Color.white;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 17);
            if (nameTxt.font == null)
                nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameTxt.verticalOverflow = VerticalWrapMode.Overflow;
            nameTxt.raycastTarget = false;

            var nameOutline = nameObj.AddComponent<Outline>();
            nameOutline.effectColor = Color.black;
            nameOutline.effectDistance = new Vector2(1f, -1f);

            // 3. Row 1: Item Slots Grid (Horizontal Layout)
            GameObject gridObj = new GameObject("ItemGrid");
            gridObj.transform.SetParent(canvasObj.transform, false);
            var gridRT = gridObj.AddComponent<RectTransform>();
            
            gridRT.anchorMin = new Vector2(0.5f, 1f);
            gridRT.anchorMax = new Vector2(0.5f, 1f);
            gridRT.pivot = new Vector2(0.5f, 1f);
            gridRT.anchoredPosition = new Vector2(0f, -32f); // 32px from top
            gridRT.sizeDelta = new Vector2(193f, 55f); // Expanded size

            var gridImg = gridObj.AddComponent<Image>();
            gridImg.color = new Color(0.06f, 0.06f, 0.06f, 0.85f); // Anthracite background

            var gridOutline = gridObj.AddComponent<Outline>();
            gridOutline.effectColor = new Color(0.45f, 0.35f, 0.15f, 0.7f); // Bronze/Gold frame
            gridOutline.effectDistance = new Vector2(1f, -1f);

            var gridLayout = gridObj.AddComponent<HorizontalLayoutGroup>();
            gridLayout.padding = new RectOffset(5, 5, 5, 5);
            gridLayout.spacing = 1f;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
            gridLayout.childControlWidth = false;
            gridLayout.childControlHeight = false;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childForceExpandHeight = false;

            // Display up to 4 items
            for (int i = 0; i < 4; i++)
            {
                int itemId = _activeItemIds[i];

                GameObject slotObj = new GameObject($"Slot_{i}");
                slotObj.transform.SetParent(gridObj.transform, false);
                var slotRT = slotObj.AddComponent<RectTransform>();
                slotRT.sizeDelta = new Vector2(45f, 45f);

                Sprite slotSprite = null;
                if (KOUIManager.Instance != null)
                {
                    slotSprite = KOUIManager.Instance.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45);
                }

                var slotImg = slotObj.AddComponent<Image>();
                if (slotSprite != null)
                {
                    slotImg.sprite = slotSprite;
                    slotImg.color = Color.white;
                }
                else
                {
                    slotImg.color = new Color(0.04f, 0.04f, 0.06f, 1f); // Fallback solid dark color
                    var slotOutline = slotObj.AddComponent<Outline>();
                    slotOutline.effectColor = new Color(0.15f, 0.15f, 0.18f, 1f);
                    slotOutline.effectDistance = new Vector2(1f, -1f);
                }

                if (itemId > 0)
                {
                    var handler = slotObj.AddComponent<KOMerchantSlotHandler>();
                    handler.itemId = itemId;

                    // Resolve Icon ID from Item database
                    int iconId = ResolveIconId(itemId);
                    Sprite iconSprite = KOItemIconLoader.LoadItemIcon(iconId);

                    if (iconSprite != null)
                    {
                        GameObject iconObj = new GameObject("Icon");
                        iconObj.transform.SetParent(slotObj.transform, false);
                        var iconRT = iconObj.AddComponent<RectTransform>();
                        iconRT.anchorMin = Vector2.zero;
                        iconRT.anchorMax = Vector2.one;
                        iconRT.sizeDelta = Vector2.zero; // Stretch to fill slot

                        var iconImg = iconObj.AddComponent<Image>();
                        iconImg.sprite = iconSprite;
                        iconImg.raycastTarget = false;
                    }

                    // Check Upgrade Level
                    int upgradeLevel = GetItemUpgradeLevel(itemId);
                    if (upgradeLevel > 0)
                    {
                        GameObject txtObj = new GameObject("UpgradeText");
                        txtObj.transform.SetParent(slotObj.transform, false);
                        var txtRT = txtObj.AddComponent<RectTransform>();
                        txtRT.anchorMin = new Vector2(0.5f, 0.5f);
                        txtRT.anchorMax = Vector2.one;
                        txtRT.pivot = new Vector2(1f, 1f);
                        txtRT.anchoredPosition = new Vector2(0f, 0f);
                        txtRT.sizeDelta = new Vector2(30f, 20f);

                        var txt = txtObj.AddComponent<Text>();
                        txt.text = $"+{upgradeLevel}";
                        txt.fontSize = 14;
                        txt.fontStyle = FontStyle.Bold;
                        txt.color = new Color(0.9f, 0.4f, 0.9f, 1f); // Purple upgrade text
                        txt.alignment = TextAnchor.UpperRight;
                        txt.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                        if (txt.font == null)
                            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                        txt.verticalOverflow = VerticalWrapMode.Overflow;
                        txt.raycastTarget = false;
                    }
                }
            }

            if (!isMyMerchant)
            {
                // 4. Row 2: Control Buttons (Manual Layout)
                GameObject btnPanelObj = new GameObject("ButtonPanel");
                btnPanelObj.transform.SetParent(canvasObj.transform, false);
                var btnPanelRT = btnPanelObj.AddComponent<RectTransform>();
                btnPanelRT.anchorMin = new Vector2(0.5f, 1f);
                btnPanelRT.anchorMax = new Vector2(0.5f, 1f);
                btnPanelRT.pivot = new Vector2(0.5f, 1f);
                btnPanelRT.anchoredPosition = new Vector2(0f, -93f); // 93px from top (6px gap from grid bottom)
                btnPanelRT.sizeDelta = new Vector2(193f, 28f);

                // OPEN Button
                CreateMenuButton(btnPanelObj.transform, "Btn_Open", "👁 OPEN", new Color(0.06f, 0.24f, 0.44f, 1f), () =>
                {
                    if (KOMerchantManager.Instance != null)
                    {
                        KOMerchantManager.Instance.TargetMerchantName = _playerName;
                    }
                    KOMerchantManager.Instance?.SendMerchantItemList(_socketId);
                });

                // WHISPER Button
                CreateMenuButton(btnPanelObj.transform, "Btn_Whisper", "💬 WHISPER", new Color(0.43f, 0.27f, 0.1f, 1f), () =>
                {
                    KOUIManager.Instance?.OpenCmdEdit($"/w {_playerName} ");
                });

                // Apply manual spacing & offsets (OPEN: 1px up, 1px right; WHISPER: 1px up, 1px left)
                var openRT = btnPanelObj.transform.Find("Btn_Open")?.GetComponent<RectTransform>();
                if (openRT != null) openRT.anchoredPosition = new Vector2(-49f, 1f);

                var whisperRT = btnPanelObj.transform.Find("Btn_Whisper")?.GetComponent<RectTransform>();
                if (whisperRT != null) whisperRT.anchoredPosition = new Vector2(49f, 1f);
            }

            // Start as hidden until distance is evaluated
            _canvas.gameObject.SetActive(false);
        }

        private void CreateMenuButton(Transform parent, string name, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var btnRT = btnObj.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(93f, 28f); // Fit perfectly side-by-side

            var img = btnObj.AddComponent<Image>();
            img.color = bgColor;
            if (_customUiMat != null) img.material = _customUiMat;

            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            // Add thin outline to buttons
            var outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(bgColor.r * 1.3f, bgColor.g * 1.3f, bgColor.b * 1.3f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            var txt = textObj.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);
            if (txt.font == null)
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.raycastTarget = false;
            if (_customUiMat != null) txt.material = _customUiMat;
        }

        private int ResolveIconId(int itemId)
        {
            return KOUIManager.ResolveIconId(itemId);
        }

        private int GetItemUpgradeLevel(int itemId)
        {
            if (itemId <= 0) return 0;
            if (KOInventory.s_pTbl_Items_Basic == null) return 0;
            var basic = KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, itemId);
            if (basic == null || KOInventory.s_pTbl_Items_Exts == null) return 0;

            var ext = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, itemId);
            if (ext != null && ext.byMagicOrRare != 4) // Exclude Unique
            {
                return (int)(ext.dwID % 10);
            }
            return 0;
        }

        private void LateUpdate()
        {
            if (_canvas == null) return;

            // Find local player if not cached
            if (_localPlayerTransform == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
                if (playerObj != null)
                {
                    _localPlayerTransform = playerObj.transform;
                }
            }

            // Attempt to resolve character name dynamically if it was initially empty
            if (string.IsNullOrEmpty(_playerName))
            {
                if (GameManager.Instance != null && (GameManager.Instance.CharacterId == _socketId || (short)GameManager.Instance.CharacterId == (short)_socketId))
                {
                    _playerName = GameManager.Instance.CharacterName;
                }
                else
                {
                    var rp = EntityManager.Instance?.GetRemotePlayer(_socketId);
                    if (rp != null && !string.IsNullOrEmpty(rp.Name))
                    {
                        _playerName = rp.Name;
                    }
                }

                if (!string.IsNullOrEmpty(_playerName))
                {
                    var nameTxtObj = transform.Find("MerchantBillboardCanvas/MerchantNameText");
                    if (nameTxtObj != null)
                    {
                        var nameTxt = nameTxtObj.GetComponent<Text>();
                        if (nameTxt != null) nameTxt.text = _playerName;
                    }
                }
            }

            // Check distance to show/hide the billboard billboard
            bool showBillboard = false;
            if (_localPlayerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _localPlayerTransform.position);
                float playerRadius = 0.5f;
                var playerCol = _localPlayerTransform.GetComponent<CapsuleCollider>();
                if (playerCol != null)
                {
                    playerRadius = playerCol.radius * _localPlayerTransform.localScale.x;
                }

                float stallRadius = 0.5f; // Standard radius
                float distanceThreshold = (playerRadius + stallRadius) * 3.0f; // Birebir Sundries NPC etkileşim mesafesi

                if (dist <= distanceThreshold)
                {
                    showBillboard = true;
                    _currentDistanceToPlayer = dist;
                }
                else
                {
                    _currentDistanceToPlayer = 999f;
                }
            }
            else
            {
                _currentDistanceToPlayer = 999f;
            }

            _isInRange = showBillboard;

            if (_isInRange)
            {
                KOProximityInteractRegistry.Register(this);
            }
            else
            {
                KOProximityInteractRegistry.Unregister(this);
                if (_canvas.gameObject.activeSelf)
                {
                    _canvas.gameObject.SetActive(false);
                }
            }

            KOProximityInteractRegistry.UpdateRegistry();

            if (!_canvas.gameObject.activeSelf) return;

            // Billboard behavior: always align rotation to face the main camera
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                // Align rotation to camera
                _canvas.transform.rotation = cam.transform.rotation;

                // Restore standard position
                _canvas.transform.localPosition = new Vector3(0f, 2.0f, 0f);

                // Adjust scale dynamically based on distance to the camera to maintain constant screen size
                float distance = Vector3.Distance(transform.position + new Vector3(0f, 2.0f, 0f), cam.transform.position);
                // 9.0f is the reference camera distance where the base scale of 0.014f is applied
                float scale = 0.014f * (distance / 9.0f);
                _canvas.transform.localScale = Vector3.one * scale;
            }
        }
    }

    public class KOMerchantSlotHandler : MonoBehaviour, IPointerClickHandler
    {
        public int itemId;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (itemId <= 0) return;

            var tooltip = GetTooltip();
            if (tooltip != null)
            {
                tooltip.ShowByItemId(itemId, eventData.position, showPrice: false, isBuy: true);
            }
        }

        private KOItemTooltip GetTooltip()
        {
            if (KOUIManager.Instance == null || KOUIManager.Instance.Canvas == null)
            {
                return null;
            }
            Canvas canvas = KOUIManager.Instance.Canvas;
            var tooltip = canvas.GetComponentInChildren<KOItemTooltip>(true);
            if (tooltip == null)
            {
                var go = new GameObject("KOItemTooltip");
                go.transform.SetParent(canvas.transform, false);
                tooltip = go.AddComponent<KOItemTooltip>();
            }
            return tooltip;
        }
    }
}
