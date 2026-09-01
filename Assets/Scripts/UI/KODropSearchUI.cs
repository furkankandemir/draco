using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.IO;
using EntropyOnline.Core;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    public class KODropSearchUI : MonoBehaviour
    {
        public static KODropSearchUI Instance { get; private set; }

        // UI references (generated dynamically on Awake)
        private Toggle toggleMonster;
        private Toggle toggleItem;
        private InputField searchInput;
        private Transform resultListContainer;
        private GameObject resultItemPrefab;
        private Text txtResultsPage;
        private Button btnPrevResultsPage;
        private Button btnNextResultsPage;
        private GameObject[] dropSlots = new GameObject[15];
        private Text txtDropsPage;
        private Button btnPrevDropsPage;
        private Button btnNextDropsPage;
        private Button btnClose;
        private Text txtSelectedName;
        private Text txtSelectedInfo;
        private Button btnBack;

        // Search state
        private List<JsonMonsterDrop> _matchingMonsters = new List<JsonMonsterDrop>();
        private List<KOTableReader.TableItemBasic> _matchingItems = new List<KOTableReader.TableItemBasic>();
        
        private int _currentResultsPage = 1;
        private int _maxResultsPage = 1;
        private const int ResultsPerPage = 5;

        private JsonMonsterDrop _selectedMonster = null;
        private KOTableReader.TableItemBasic _selectedItem = null;

        private int _currentDropsPage = 1;
        private int _maxDropsPage = 1;
        private const int DropsPerPage = 15;
        private int _currentGroupViewing = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildUIHierarchy();
            ConfigureListeners();

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            // Close Character Info (VariousPanel) if it is open on the left side
            if (KOUIManager.Instance != null && KOUIManager.Instance.VariousPanel != null && KOUIManager.Instance.VariousPanel.activeSelf)
            {
                KOUIManager.Instance.ToggleCharacterInfo();
            }

            gameObject.SetActive(true);
            searchInput.text = "";
            OnSearchTypeChanged();
        }

        public void OpenForMonster(int monsterId)
        {
            // Close Character Info (VariousPanel) if it is open on the left side
            if (KOUIManager.Instance != null && KOUIManager.Instance.VariousPanel != null && KOUIManager.Instance.VariousPanel.activeSelf)
            {
                KOUIManager.Instance.ToggleCharacterInfo();
            }

            gameObject.SetActive(true);
            toggleMonster.isOn = true;
            toggleItem.isOn = false;

            var m = KODropDataManager.GetMonsterDrops(monsterId);
            if (m != null)
            {
                searchInput.text = m.name;
                SelectMonster(m);
            }
            else
            {
                searchInput.text = "";
                OnSearchTypeChanged();
            }
        }

        public void Close()
        {
            var tooltip = GetTooltip();
            if (tooltip != null) tooltip.Hide();

            var slide = GetComponent<KOUIPanelSlideIn>();
            if (slide != null)
            {
                slide.SlideOut(() => gameObject.SetActive(false));
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnSearchTypeChanged()
        {
            searchInput.text = "";
            _currentResultsPage = 1;
            _selectedMonster = null;
            _selectedItem = null;
            if (txtSelectedName != null) txtSelectedName.text = "Select Monster or Item";
            if (txtSelectedInfo != null) txtSelectedInfo.text = "";
            ClearDropSlots();
            DoSearch();
        }

        private void OnSearchInputChanged()
        {
            _currentResultsPage = 1;
            DoSearch();
        }

        private void DoSearch()
        {
            string query = searchInput.text.Trim();
            _matchingMonsters.Clear();
            _matchingItems.Clear();



            if (query.Length < 2)
            {
                _maxResultsPage = 1;
                RenderMonsterResults();
            }
            else
            {
                if (toggleMonster.isOn)
                {
                    _matchingMonsters = KODropDataManager.SearchMonsters(query);
                }
                else
                {
                    // Search items matching the query, then collect all monsters dropping them
                    var matchingItems = KODropDataManager.SearchItems(query);
                    var monsterIdSet = new HashSet<int>();
                    foreach (var item in matchingItems)
                    {
                        var mIds = KODropDataManager.GetMonstersByItem((int)item.dwID);
                        foreach (var mId in mIds)
                        {
                            monsterIdSet.Add(mId);
                        }
                    }

                    // Map monster IDs to actual JsonMonsterDrop entries
                    foreach (int mId in monsterIdSet)
                    {
                        var monster = KODropDataManager.GetMonsterDrops(mId);
                        if (monster != null)
                        {
                            _matchingMonsters.Add(monster);
                        }
                    }

                    // Sort matching monsters by level for clean presentation
                    _matchingMonsters.Sort((a, b) => a.level.CompareTo(b.level));
                }

                _maxResultsPage = Mathf.Max(1, Mathf.CeilToInt((float)_matchingMonsters.Count / ResultsPerPage));
                RenderMonsterResults();
            }
        }

        private void RenderMonsterResults()
        {
            foreach (Transform child in resultListContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            int startIndex = (_currentResultsPage - 1) * ResultsPerPage;
            int count = Mathf.Min(ResultsPerPage, _matchingMonsters.Count - startIndex);

            for (int i = 0; i < count; i++)
            {
                var monster = _matchingMonsters[startIndex + i];
                var go = Instantiate(resultItemPrefab);
                go.transform.SetParent(resultListContainer, false);
                go.SetActive(true);
                
                var txt = go.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = $"Lv.{monster.level} {monster.name}";
                }
                
                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    bool isSelected = (_selectedMonster != null && _selectedMonster.id == monster.id);
                    img.color = isSelected ? new Color(0.4f, 0.25f, 0.1f, 0.9f) : new Color(0.12f, 0.12f, 0.12f, 0.8f);
                }

                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => {
                        SelectMonster(monster);
                        RefreshResultHighlights();
                    });
                }
            }

            // Pad with empty rows to always show 5 rows (Excel style grid lines)
            for (int i = count; i < ResultsPerPage; i++)
            {
                var go = Instantiate(resultItemPrefab);
                go.transform.SetParent(resultListContainer, false);
                go.SetActive(true);

                var txt = go.GetComponentInChildren<Text>();
                if (txt != null) txt.text = "";

                var img = go.GetComponent<Image>();
                if (img != null) img.color = new Color(0.12f, 0.12f, 0.12f, 0.4f); // Fainter empty row background

                var btn = go.GetComponent<Button>();
                if (btn != null) btn.enabled = false;
            }

            txtResultsPage.text = $"{_currentResultsPage}/{_maxResultsPage}";
        }

        private void RenderItemResults()
        {
            foreach (Transform child in resultListContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            int startIndex = (_currentResultsPage - 1) * ResultsPerPage;
            int count = Mathf.Min(ResultsPerPage, _matchingItems.Count - startIndex);

            for (int i = 0; i < count; i++)
            {
                var item = _matchingItems[startIndex + i];
                var go = Instantiate(resultItemPrefab);
                go.transform.SetParent(resultListContainer, false);
                go.SetActive(true);
                
                var txt = go.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.text = item.szName;
                }
                
                var img = go.GetComponent<Image>();
                if (img != null)
                {
                    bool isSelected = (_selectedItem != null && _selectedItem.dwID == item.dwID);
                    img.color = isSelected ? new Color(0.4f, 0.25f, 0.1f, 0.9f) : new Color(0.12f, 0.12f, 0.12f, 0.8f);
                }

                 var btn = go.GetComponent<Button>();
                 if (btn != null)
                 {
                     btn.onClick.AddListener(() => {
                         SelectItem(item, go.transform.position);
                         RefreshResultHighlights();
                     });
                 }
            }

            // Pad with empty rows to always show 5 rows (Excel style grid lines)
            for (int i = count; i < ResultsPerPage; i++)
            {
                var go = Instantiate(resultItemPrefab);
                go.transform.SetParent(resultListContainer, false);
                go.SetActive(true);

                var txt = go.GetComponentInChildren<Text>();
                if (txt != null) txt.text = "";

                var img = go.GetComponent<Image>();
                if (img != null) img.color = new Color(0.12f, 0.12f, 0.12f, 0.4f); // Fainter empty row background

                var btn = go.GetComponent<Button>();
                if (btn != null) btn.enabled = false;
            }

            txtResultsPage.text = $"{_currentResultsPage}/{_maxResultsPage}";
        }

        private List<JsonDropItem> GetValidItemDrops(JsonMonsterDrop monster)
        {
            var list = new List<JsonDropItem>();
            if (monster != null && monster.drops != null)
            {
                var basic = KOInventory.s_pTbl_Items_Basic;
                foreach (var d in monster.drops)
                {
                    if (d.itemId == 110 || d.itemId == 120) continue;

                    // If it is a group ID, it is a valid drop!
                    if (KODropDataManager.IsGroupId(d.itemId))
                    {
                        list.Add(d);
                    }
                    else if (basic != null)
                    {
                        uint baseId = (uint)(d.itemId / 1000 * 1000);
                        if (basic.ContainsKey(baseId))
                        {
                            list.Add(d);
                        }
                    }
                }
            }
            return list;
        }

        private void SelectMonster(JsonMonsterDrop monster)
        {
            _selectedMonster = monster;
            _selectedItem = null;
            _currentGroupViewing = 0; // Reset group view
            if (btnBack != null) btnBack.gameObject.SetActive(false); // Hide back button

            if (txtSelectedName != null) txtSelectedName.text = monster.name;
            
            var nonCoinDrops = GetValidItemDrops(monster);
             if (txtSelectedInfo != null) txtSelectedInfo.text = "";

            _currentDropsPage = 1;
            _maxDropsPage = Mathf.Max(1, Mathf.CeilToInt((float)nonCoinDrops.Count / DropsPerPage));

            RenderMonsterDrops();
        }

        private void SelectItem(KOTableReader.TableItemBasic item, Vector3 worldPos)
        {
            _selectedMonster = null;
            _selectedItem = item;

            if (txtSelectedName != null) txtSelectedName.text = item.szName;

            var monsterIds = KODropDataManager.GetMonstersByItem((int)item.dwID);
             if (txtSelectedInfo != null) txtSelectedInfo.text = "";

            _currentDropsPage = 1;
            _maxDropsPage = Mathf.Max(1, Mathf.CeilToInt((float)monsterIds.Count / DropsPerPage));

            RenderItemDrops(monsterIds);

            // Open item tooltip at results row position using clean game-standard conversion
            ShowTooltip((int)item.dwID, worldPos);
        }

        private void RenderMonsterDrops()
        {
            ClearDropSlots();
            if (_selectedMonster == null) return;

            var nonCoinDrops = GetValidItemDrops(_selectedMonster);
            int startIndex = (_currentDropsPage - 1) * DropsPerPage;
            int count = Mathf.Min(DropsPerPage, nonCoinDrops.Count - startIndex);

            for (int i = 0; i < count; i++)
            {
                var drop = nonCoinDrops[startIndex + i];
                ConfigureSlot(i, drop.itemId, KODropDataManager.GetDropRateCategory(drop.rate));
            }

            txtDropsPage.text = $"{_currentDropsPage}/{_maxDropsPage}";
        }

        private void RenderItemDrops(List<int> monsterIds)
        {
            ClearDropSlots();
            if (_selectedItem == null) return;

            int startIndex = (_currentDropsPage - 1) * DropsPerPage;
            int count = Mathf.Min(DropsPerPage, monsterIds.Count - startIndex);

            for (int i = 0; i < count; i++)
            {
                int monsterId = monsterIds[startIndex + i];
                var monster = KODropDataManager.GetMonsterDrops(monsterId);
                if (monster != null)
                {
                    string rateText = "Low";
                    foreach (var d in monster.drops)
                    {
                        if (d.itemId == (int)_selectedItem.dwID)
                        {
                            rateText = KODropDataManager.GetDropRateCategory(d.rate);
                            break;
                        }
                    }
                    ConfigureSlotAsMonster(i, monsterId, monster.name, rateText);
                }
            }

            txtDropsPage.text = $"{_currentDropsPage}/{_maxDropsPage}";
        }

        private void ConfigureSlot(int slotIndex, int itemId, string rateText)
        {
            if (slotIndex < 0 || slotIndex >= dropSlots.Length) return;

            var slot = dropSlots[slotIndex];
            slot.SetActive(true);

            // Check if this is a group ID
            bool isGroup = KODropDataManager.IsGroupId(itemId);
            var qmarkTrans = slot.transform.Find("QMark");
            if (qmarkTrans != null)
            {
                qmarkTrans.gameObject.SetActive(isGroup);
            }

            var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                if (isGroup)
                {
                    iconImg.gameObject.SetActive(false);
                }
                else
                {
                    var basic = KOInventory.s_pTbl_Items_Basic;
                    uint baseId = (uint)(itemId / 1000 * 1000); // KO Item base ID formula (removes upgrade/ext suffix)
                    if (basic != null && basic.TryGetValue(baseId, out var itemBasic))
                    {
                        iconImg.sprite = KOItemIconLoader.LoadItemIcon((int)itemBasic.dwIDIcon);
                        iconImg.gameObject.SetActive(iconImg.sprite != null); // Set active only if sprite loaded successfully to avoid white squares
                    }
                    else
                    {
                        iconImg.gameObject.SetActive(false);
                    }
                }
            }

            var nameTxt = slot.transform.Find("Name")?.GetComponent<Text>();
            if (nameTxt != null)
            {
                nameTxt.text = ""; // Clear name inside slot for items to match inventory clean style
            }

            var rateTxt = slot.transform.Find("Rate")?.GetComponent<Text>();
            if (rateTxt != null)
            {
                rateTxt.text = rateText;
                rateTxt.gameObject.SetActive(!string.IsNullOrEmpty(rateText));
                if (!string.IsNullOrEmpty(rateText))
                {
                    rateTxt.transform.SetAsLastSibling(); // Force draw rate count in front of Icon
                }
            }

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                if (isGroup)
                {
                    btn.onClick.AddListener(() => ShowGroupDrops(itemId));
                }
                else
                {
                    btn.onClick.AddListener(() => ShowTooltip(itemId, slot.transform.position));
                }
            }
        }

        private void ConfigureSlotAsMonster(int slotIndex, int monsterId, string monsterName, string rateText)
        {
            if (slotIndex < 0 || slotIndex >= dropSlots.Length) return;

            var slot = dropSlots[slotIndex];
            slot.SetActive(true);

            var qmarkTrans = slot.transform.Find("QMark");
            if (qmarkTrans != null) qmarkTrans.gameObject.SetActive(false);

            var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.sprite = Resources.Load<Sprite>("UI/target_icon_monster");
                iconImg.gameObject.SetActive(iconImg.sprite != null);
            }

            var nameTxt = slot.transform.Find("Name")?.GetComponent<Text>();
            if (nameTxt != null)
            {
                nameTxt.text = monsterName.Length > 8 ? monsterName.Substring(0, 7) + "." : monsterName;
            }

            var rateTxt = slot.transform.Find("Rate")?.GetComponent<Text>();
            if (rateTxt != null)
            {
                rateTxt.text = rateText;
                rateTxt.gameObject.SetActive(true);
                rateTxt.transform.SetAsLastSibling(); // Force draw rate count in front of Icon
            }

            var btn = slot.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    var monster = KODropDataManager.GetMonsterDrops(monsterId);
                    if (monster != null)
                    {
                        toggleMonster.isOn = true;
                        SelectMonster(monster);
                    }
                });
            }
        }

        private void ClearDropSlots()
        {
            foreach (var slot in dropSlots)
            {
                if (slot != null)
                {
                    slot.SetActive(true); // Keep slot frames always visible

                    var qmarkTrans = slot.transform.Find("QMark");
                    if (qmarkTrans != null) qmarkTrans.gameObject.SetActive(false);

                    var iconImg = slot.transform.Find("Icon")?.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.sprite = null;
                        iconImg.gameObject.SetActive(false);
                    }

                    var nameTxt = slot.transform.Find("Name")?.GetComponent<Text>();
                    if (nameTxt != null) nameTxt.text = "";

                    var rateTxt = slot.transform.Find("Rate")?.GetComponent<Text>();
                    if (rateTxt != null)
                    {
                        rateTxt.text = "";
                        rateTxt.gameObject.SetActive(false);
                    }

                    var btn = slot.GetComponent<Button>();
                    if (btn != null) btn.onClick.RemoveAllListeners();
                }
            }
            txtDropsPage.text = "0/0";
        }

        private void ShowTooltip(int itemId, Vector3 worldPosition)
        {
            var tooltip = GetTooltip();
            if (tooltip != null)
            {
                // Convert World Position to Screen Point so tooltip aligns correctly in overlay canvas
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
                tooltip.ShowByItemId(itemId, screenPos, false, false);
            }
        }

        private void ShowGroupDrops(int groupId)
        {
            _currentGroupViewing = groupId;
            if (btnBack != null) btnBack.gameObject.SetActive(true);

            // Fetch items in this group and setup paging
            var groupItems = KODropDataManager.GetGroupItems(groupId);
            _currentDropsPage = 1;
            _maxDropsPage = Mathf.Max(1, Mathf.CeilToInt((float)groupItems.Count / DropsPerPage));

            RenderGroupDrops();
        }

        private void RenderGroupDrops()
        {
            if (_currentGroupViewing == 0) return;

            var groupItems = KODropDataManager.GetGroupItems(_currentGroupViewing);
            ClearDropSlots();

            int startIndex = (_currentDropsPage - 1) * DropsPerPage;
            int count = Mathf.Min(DropsPerPage, groupItems.Count - startIndex);

            for (int i = 0; i < count; i++)
            {
                // Group items don't have individual rates inside the group, they inherit parent rate
                ConfigureSlot(i, groupItems[startIndex + i], "");
            }

            txtDropsPage.text = $"{_currentDropsPage}/{_maxDropsPage}";
            if (btnPrevDropsPage != null) btnPrevDropsPage.interactable = (_currentDropsPage > 1);
            if (btnNextDropsPage != null) btnNextDropsPage.interactable = (_currentDropsPage < _maxDropsPage);
        }

        private void OnBackButtonClicked()
        {
            _currentGroupViewing = 0;
            if (btnBack != null) btnBack.gameObject.SetActive(false);
            
            // Recalculate page bounds for the selected monster before rendering
            if (_selectedMonster != null)
            {
                var nonCoinDrops = GetValidItemDrops(_selectedMonster);
                _currentDropsPage = 1;
                _maxDropsPage = Mathf.Max(1, Mathf.CeilToInt((float)nonCoinDrops.Count / DropsPerPage));
                RenderMonsterDrops();
            }
        }

        private KOItemTooltip GetTooltip()
        {
            if (KOUIManager.Instance == null || KOUIManager.Instance.Canvas == null) return null;
            return KOUIManager.Instance.Canvas.GetComponentInChildren<KOItemTooltip>(true);
        }

        private void OnPrevResultsPage()
        {
            if (_currentResultsPage > 1)
            {
                _currentResultsPage--;
                DoSearch();
            }
        }

        private void OnNextResultsPage()
        {
            if (_currentResultsPage < _maxResultsPage)
            {
                _currentResultsPage++;
                DoSearch();
            }
        }

        private void OnPrevDropsPage()
        {
            if (_currentDropsPage > 1)
            {
                _currentDropsPage--;
                if (_currentGroupViewing != 0) RenderGroupDrops();
                else if (_selectedMonster != null) RenderMonsterDrops();
            }
        }

        private void OnNextDropsPage()
        {
            if (_currentDropsPage < _maxDropsPage)
            {
                _currentDropsPage++;
                if (_currentGroupViewing != 0) RenderGroupDrops();
                else if (_selectedMonster != null) RenderMonsterDrops();
            }
        }

        private void ConfigureListeners()
        {
            toggleMonster.onValueChanged.AddListener(delegate { OnSearchTypeChanged(); });
            toggleItem.onValueChanged.AddListener(delegate { OnSearchTypeChanged(); });
            searchInput.onValueChanged.AddListener(delegate { OnSearchInputChanged(); });

            btnPrevResultsPage.onClick.AddListener(OnPrevResultsPage);
            btnNextResultsPage.onClick.AddListener(OnNextResultsPage);

            btnPrevDropsPage.onClick.AddListener(OnPrevDropsPage);
            btnNextDropsPage.onClick.AddListener(OnNextDropsPage);
            
            if (btnBack != null) btnBack.onClick.AddListener(OnBackButtonClicked);

            btnClose.onClick.AddListener(Close);
        }

        // =========================================================================
        // SELF-BUILDING UI HIERARCHY (Runtime UI Generation)
        // =========================================================================

        private void BuildUIHierarchy()
        {
            // Colors
            Color colorBg = new Color(0.12f, 0.10f, 0.08f, 0.98f);
            Color colorBorder = new Color(0.6f, 0.48f, 0.22f, 0.9f);
            Color colorTextGold = new Color(0.9f, 0.75f, 0.25f, 1f);
            Color colorTextWhite = Color.white;
            Color colorInputBg = new Color(0.05f, 0.04f, 0.04f, 1f);
            Color colorBtnBg = new Color(0.06f, 0.05f, 0.04f, 0.95f);

            // Sprites loaded from KO UI Manager Theme system
            Sprite spBg = null;
            Sprite spInput = null;
            Sprite spBtn = null;
            Sprite spSlot = null;

            if (KOUIManager.Instance != null)
            {
                spBg = KOUIManager.Instance.GetSkillThemePanelBgSprite("drop_search_panel_bg", 322, 519, 0,
                    new Color(0.12f, 0.10f, 0.08f, 0.98f),
                    new Color(0.04f, 0.04f, 0.04f, 0.98f),
                    new Color(0.6f, 0.48f, 0.22f, 0.9f),
                    2);
                spInput = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("drop_search_input_bg", 302, 29, 10,
                    new Color(0.08f, 0.07f, 0.06f, 0.9f),
                    new Color(0.35f, 0.28f, 0.18f, 0.8f),
                    1);
                spBtn = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("drop_search_btn", 64, 64, 4,
                    new Color(0.06f, 0.05f, 0.04f, 0.95f),
                    new Color(0.43f, 0.36f, 0.26f, 1f),
                    1);
                spSlot = KOUIManager.Instance.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 45);
            }
            else
            {
                spBg = CreateProceduralSprite(128, 128, colorBg, colorBorder, 2);
                spInput = CreateProceduralSprite(64, 64, colorInputBg, colorBorder, 1);
                spBtn = CreateProceduralSprite(64, 64, colorBtnBg, colorBorder, 1);
                spSlot = spBtn;
            }

            // 1. Root Panel (Left-aligned sidebar matching el_various_all_us)
            RectTransform rtRoot = gameObject.AddComponent<RectTransform>();
            rtRoot.anchorMin = new Vector2(0f, 0.5f);
            rtRoot.anchorMax = new Vector2(0f, 0.5f);
            rtRoot.pivot = new Vector2(0f, 0.5f);
            rtRoot.sizeDelta = new Vector2(322, 519); // Exact size matching el_inventory_us (362x519 height)
            rtRoot.anchoredPosition = new Vector2(50f, 0f);
            
            Image imgBg = gameObject.AddComponent<Image>();
            imgBg.sprite = spBg;
            imgBg.type = Image.Type.Sliced;

            // Add slide-in animation and scale independent behaviors
            gameObject.AddComponent<KOUIScaleIndependent>();
            var slideIn = gameObject.AddComponent<KOUIPanelSlideIn>();
            slideIn.IsLeft = true;
            slideIn.TargetX = 50f;
            slideIn.StartX = -350f;
            slideIn.Duration = 0.2f;

            // 2. Title Header (Styled exactly like SKILL PAGE)
            GameObject goTitle = new GameObject("Title");
            goTitle.transform.SetParent(transform, false);
            var rtTitle = goTitle.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0f, 1f);
            rtTitle.anchorMax = new Vector2(1f, 1f);
            rtTitle.pivot = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0, -8);
            rtTitle.sizeDelta = new Vector2(-60, 25);

            Text txtTitle = goTitle.AddComponent<Text>();
            txtTitle.text = "DROP LIST SEARCH";
            txtTitle.font = GetSafeFont();
            txtTitle.fontSize = 14;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Bright Gold/Yellow matching SKILL PAGE
            txtTitle.fontStyle = FontStyle.Bold;

            var shadowTitle = goTitle.AddComponent<Shadow>();
            shadowTitle.effectColor = new Color(0, 0, 0, 0.85f);
            shadowTitle.effectDistance = new Vector2(1, -1);

            // Title Divider (Skilltree title divider style: fading gold 280x2 line)
            GameObject goTitleDiv = new GameObject("TitleDivider");
            goTitleDiv.transform.SetParent(transform, false);
            var rtDiv = goTitleDiv.AddComponent<RectTransform>();
            rtDiv.anchorMin = new Vector2(0.5f, 1f);
            rtDiv.anchorMax = new Vector2(0.5f, 1f);
            rtDiv.pivot = new Vector2(0.5f, 1f);
            rtDiv.anchoredPosition = new Vector2(0, -33); // Placed exactly under title
            rtDiv.sizeDelta = new Vector2(280, 2);
            
            var imgDiv = goTitleDiv.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                imgDiv.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite("skilltree_title_divider", 280, 2,
                    new Color(0.9f, 0.75f, 0.25f, 0.8f));
            }
            else
            {
                imgDiv.color = new Color(0.9f, 0.75f, 0.25f, 0.8f);
            }

            // 3. Close Button (Styled exactly like Skilltree close button)
            GameObject goClose = new GameObject("BtnClose");
            goClose.transform.SetParent(transform, false);
            var rtClose = goClose.AddComponent<RectTransform>();
            rtClose.anchorMin = new Vector2(1f, 1f);
            rtClose.anchorMax = new Vector2(1f, 1f);
            rtClose.pivot = new Vector2(1f, 1f);
            rtClose.anchoredPosition = new Vector2(-8, -8);
            rtClose.sizeDelta = new Vector2(24, 24); // 24x24 matching skillclose

            btnClose = goClose.AddComponent<Button>();
            Image imgClose = goClose.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                imgClose.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite("skill_close_btn", 24, 24, 0,
                    new Color(0.18f, 0.18f, 0.18f, 1),
                    new Color(0.45f, 0.35f, 0.15f, 1),
                    1);
            }
            else
            {
                imgClose.sprite = spBtn;
            }
            
            GameObject goCloseText = new GameObject("Text");
            goCloseText.transform.SetParent(goClose.transform, false);
            var rtCloseText = goCloseText.AddComponent<RectTransform>();
            rtCloseText.anchorMin = Vector2.zero;
            rtCloseText.anchorMax = Vector2.one;
            rtCloseText.offsetMin = Vector2.zero;
            rtCloseText.offsetMax = Vector2.zero;
            Text txtClose = goCloseText.AddComponent<Text>();
            txtClose.text = "X";
            txtClose.font = GetSafeFont();
            txtClose.fontSize = 11;
            txtClose.alignment = TextAnchor.MiddleCenter;
            txtClose.color = new Color(0.9f, 0.75f, 0.25f, 1); // Gold text color matching skillclose
            txtClose.fontStyle = FontStyle.Bold;

            var shadowClose = goCloseText.AddComponent<Shadow>();
            shadowClose.effectColor = new Color(0, 0, 0, 0.85f);
            shadowClose.effectDistance = new Vector2(1, -1);

            // 4. Vertical Toggle Column (Stacked vertically like the visual mockup)
            GameObject goToggles = new GameObject("ToggleColumn");
            goToggles.transform.SetParent(transform, false);
            var rtToggles = goToggles.AddComponent<RectTransform>();
            rtToggles.anchorMin = new Vector2(0f, 1f);
            rtToggles.anchorMax = new Vector2(1f, 1f);
            rtToggles.pivot = new Vector2(0.5f, 1f);
            rtToggles.anchoredPosition = new Vector2(8, -45); // Centered visually with 8px right shift
            rtToggles.sizeDelta = new Vector2(-20, 22); // Height reduced to 22px for horizontal alignment

            var toggleGroup = goToggles.AddComponent<ToggleGroup>();

            var hlg = goToggles.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20; // Clean 20px spacing between toggles
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            // Monster Toggle (Left side)
            GameObject goToggleMonster = new GameObject("ToggleMonster");
            goToggleMonster.transform.SetParent(goToggles.transform, false);
            var rtTM = goToggleMonster.AddComponent<RectTransform>();
            rtTM.sizeDelta = new Vector2(75, 22); // Tightened to 75px to remove empty right padding
            toggleMonster = goToggleMonster.AddComponent<Toggle>();
            toggleMonster.group = toggleGroup;

            GameObject goTMBackground = new GameObject("Background");
            goTMBackground.transform.SetParent(goToggleMonster.transform, false);
            var rtTMBg = goTMBackground.AddComponent<RectTransform>();
            rtTMBg.anchorMin = new Vector2(0f, 0.5f);
            rtTMBg.anchorMax = new Vector2(0f, 0.5f);
            rtTMBg.pivot = new Vector2(0f, 0.5f);
            rtTMBg.anchoredPosition = new Vector2(0, 0);
            rtTMBg.sizeDelta = new Vector2(14, 14);
            var imgTMBg = goTMBackground.AddComponent<Image>();
            imgTMBg.sprite = spInput;

            GameObject goTMCheckmark = new GameObject("Checkmark");
            goTMCheckmark.transform.SetParent(goTMBackground.transform, false);
            var rtTMCk = goTMCheckmark.AddComponent<RectTransform>();
            rtTMCk.anchorMin = new Vector2(0.5f, 0.5f);
            rtTMCk.anchorMax = new Vector2(0.5f, 0.5f);
            rtTMCk.sizeDelta = new Vector2(9, 9);
            var imgTMCk = goTMCheckmark.AddComponent<Image>();
            imgTMCk.color = colorTextGold;
            toggleMonster.graphic = imgTMCk;
            toggleMonster.isOn = true;

            GameObject goTMLabel = new GameObject("Label");
            goTMLabel.transform.SetParent(goToggleMonster.transform, false);
            var rtTMLb = goTMLabel.AddComponent<RectTransform>();
            rtTMLb.anchorMin = new Vector2(0f, 0f);
            rtTMLb.anchorMax = new Vector2(1f, 1f);
            rtTMLb.offsetMin = new Vector2(20, 0);
            rtTMLb.offsetMax = Vector2.zero;
            var txtTMLb = goTMLabel.AddComponent<Text>();
            txtTMLb.text = "Monster";
            txtTMLb.font = GetSafeFont();
            txtTMLb.fontSize = 12;
            txtTMLb.alignment = TextAnchor.MiddleLeft;
            txtTMLb.color = colorTextWhite;

            // Item Toggle (Right side)
            GameObject goToggleItem = new GameObject("ToggleItem");
            goToggleItem.transform.SetParent(goToggles.transform, false);
            var rtTI = goToggleItem.AddComponent<RectTransform>();
            rtTI.sizeDelta = new Vector2(90, 22); // Tightened to 90px to remove empty right padding
            toggleItem = goToggleItem.AddComponent<Toggle>();
            toggleItem.group = toggleGroup;

            GameObject goTIBackground = new GameObject("Background");
            goTIBackground.transform.SetParent(goToggleItem.transform, false);
            var rtTIBg = goTIBackground.AddComponent<RectTransform>();
            rtTIBg.anchorMin = new Vector2(0f, 0.5f);
            rtTIBg.anchorMax = new Vector2(0f, 0.5f);
            rtTIBg.pivot = new Vector2(0f, 0.5f);
            rtTIBg.anchoredPosition = new Vector2(0, 0);
            rtTIBg.sizeDelta = new Vector2(14, 14);
            var imgTIBg = goTIBackground.AddComponent<Image>();
            imgTIBg.sprite = spInput;

            GameObject goTICheckmark = new GameObject("Checkmark");
            goTICheckmark.transform.SetParent(goTIBackground.transform, false);
            var rtTICk = goTICheckmark.AddComponent<RectTransform>();
            rtTICk.anchorMin = new Vector2(0.5f, 0.5f);
            rtTICk.anchorMax = new Vector2(0.5f, 0.5f);
            rtTICk.sizeDelta = new Vector2(9, 9);
            var imgTICk = goTICheckmark.AddComponent<Image>();
            imgTICk.color = colorTextGold;
            toggleItem.graphic = imgTICk;

            GameObject goTILabel = new GameObject("Label");
            goTILabel.transform.SetParent(goToggleItem.transform, false);
            var rtTILb = goTILabel.AddComponent<RectTransform>();
            rtTILb.anchorMin = new Vector2(0f, 0f);
            rtTILb.anchorMax = new Vector2(1f, 1f);
            rtTILb.offsetMin = new Vector2(20, 0);
            rtTILb.offsetMax = Vector2.zero;
            var txtTILb = goTILabel.AddComponent<Text>();
            txtTILb.text = "Item Name";
            txtTILb.font = GetSafeFont();
            txtTILb.fontSize = 12;
            txtTILb.alignment = TextAnchor.MiddleLeft;
            txtTILb.color = colorTextWhite;

            // 5. Search Input Field
            GameObject goInput = new GameObject("SearchInput");
            goInput.transform.SetParent(transform, false);
            var rtInput = goInput.AddComponent<RectTransform>();
            rtInput.anchorMin = new Vector2(0f, 1f);
            rtInput.anchorMax = new Vector2(1f, 1f);
            rtInput.pivot = new Vector2(0.5f, 1f);
            rtInput.anchoredPosition = new Vector2(0, -77); // Shifted up to fit 28px rows
            rtInput.sizeDelta = new Vector2(-20, 29); // Increased height by 5px (24px to 29px)

            var imgInput = goInput.AddComponent<Image>();
            imgInput.sprite = spInput;
            imgInput.type = Image.Type.Sliced;

            searchInput = goInput.AddComponent<InputField>();

            GameObject goPlaceholder = new GameObject("Placeholder");
            goPlaceholder.transform.SetParent(goInput.transform, false);
            var rtPlaceholder = goPlaceholder.AddComponent<RectTransform>();
            rtPlaceholder.anchorMin = Vector2.zero;
            rtPlaceholder.anchorMax = Vector2.one;
            rtPlaceholder.offsetMin = new Vector2(6, 0);
            rtPlaceholder.offsetMax = new Vector2(-6, 0);
            var txtPlaceholder = goPlaceholder.AddComponent<Text>();
            txtPlaceholder.text = "Enter name here....";
            txtPlaceholder.font = GetSafeFont();
            txtPlaceholder.fontStyle = FontStyle.Italic;
            txtPlaceholder.fontSize = 11;
            txtPlaceholder.alignment = TextAnchor.MiddleCenter; // Centered matching Nowa Online
            txtPlaceholder.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            searchInput.placeholder = txtPlaceholder;

            GameObject goInputText = new GameObject("Text");
            goInputText.transform.SetParent(goInput.transform, false);
            var rtInputText = goInputText.AddComponent<RectTransform>();
            rtInputText.anchorMin = Vector2.zero;
            rtInputText.anchorMax = Vector2.one;
            rtInputText.offsetMin = new Vector2(6, 0);
            rtInputText.offsetMax = new Vector2(-6, 0);
            var txtInput = goInputText.AddComponent<Text>();
            txtInput.font = GetSafeFont();
            txtInput.fontSize = 11;
            txtInput.alignment = TextAnchor.MiddleCenter; // Centered matching Nowa Online
            txtInput.color = Color.white;
            searchInput.textComponent = txtInput;

            // 6. Results List Panel
            GameObject goResultsPanel = new GameObject("ResultsPanel");
            goResultsPanel.transform.SetParent(transform, false);
            var rtRP = goResultsPanel.AddComponent<RectTransform>();
            rtRP.anchorMin = new Vector2(0f, 1f);
            rtRP.anchorMax = new Vector2(1f, 1f);
            rtRP.pivot = new Vector2(0.5f, 1f);
            rtRP.anchoredPosition = new Vector2(0, -116);
            rtRP.sizeDelta = new Vector2(-40, 142); // Fits 5 rows of 28px with 10px extra margin on left/right (40px total margin)

            resultListContainer = goResultsPanel.transform;
            var vlgRP = goResultsPanel.AddComponent<VerticalLayoutGroup>();
            vlgRP.childControlHeight = true; // Enable so layout group enforces LayoutElement heights!
            vlgRP.childControlWidth = true;
            vlgRP.childForceExpandHeight = false;
            vlgRP.childForceExpandWidth = true;
            vlgRP.spacing = 0; // Spacing 0 to make overlapping border lines perfect

            // Result Item Prefab Template
            resultItemPrefab = new GameObject("ResultItemPrefab");
            resultItemPrefab.transform.SetParent(transform, false);
            var rtRIP = resultItemPrefab.AddComponent<RectTransform>();
            rtRIP.anchorMin = new Vector2(0f, 0.5f); // Stretch anchors to fill container width
            rtRIP.anchorMax = new Vector2(1f, 0.5f); // Stretch anchors to fill container width
            rtRIP.pivot = new Vector2(0.5f, 0.5f);
            rtRIP.sizeDelta = new Vector2(0, 28); // Increased to 28px
            
            var le = resultItemPrefab.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.minHeight = 28f;

            var imgRIP = resultItemPrefab.AddComponent<Image>();
            imgRIP.color = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            var btnRIP = resultItemPrefab.AddComponent<Button>();

            // Excel Borders inside the prefab template:
            var borderLineColor = new Color(0.35f, 0.25f, 0.15f, 0.4f); // subtle gold-charcoal matching seeking party

            // Horizontal Bottom Line
            GameObject goHLine = new GameObject("Grid_HLine");
            goHLine.transform.SetParent(resultItemPrefab.transform, false);
            var rtHLine = goHLine.AddComponent<RectTransform>();
            rtHLine.anchorMin = new Vector2(0f, 0f);
            rtHLine.anchorMax = new Vector2(1f, 0f);
            rtHLine.pivot = new Vector2(0.5f, 0f);
            rtHLine.anchoredPosition = Vector2.zero;
            rtHLine.sizeDelta = new Vector2(0f, 1f);
            var imgHLine = goHLine.AddComponent<Image>();
            imgHLine.color = borderLineColor;

            // Horizontal Top Line (to close the grid at the top)
            GameObject goTLine = new GameObject("Grid_TLine");
            goTLine.transform.SetParent(resultItemPrefab.transform, false);
            var rtTLine = goTLine.AddComponent<RectTransform>();
            rtTLine.anchorMin = new Vector2(0f, 1f);
            rtTLine.anchorMax = new Vector2(1f, 1f);
            rtTLine.pivot = new Vector2(0.5f, 1f);
            rtTLine.anchoredPosition = Vector2.zero;
            rtTLine.sizeDelta = new Vector2(0f, 1f);
            var imgTLine = goTLine.AddComponent<Image>();
            imgTLine.color = borderLineColor;

            // Left boundary line
            GameObject goLLine = new GameObject("Grid_LeftLine");
            goLLine.transform.SetParent(resultItemPrefab.transform, false);
            var rtLLine = goLLine.AddComponent<RectTransform>();
            rtLLine.anchorMin = new Vector2(0f, 0f);
            rtLLine.anchorMax = new Vector2(0f, 1f);
            rtLLine.pivot = new Vector2(0f, 0.5f);
            rtLLine.anchoredPosition = Vector2.zero;
            rtLLine.sizeDelta = new Vector2(1f, 0f);
            var imgLLine = goLLine.AddComponent<Image>();
            imgLLine.color = borderLineColor;

            // Right boundary line
            GameObject goRLine = new GameObject("Grid_RightLine");
            goRLine.transform.SetParent(resultItemPrefab.transform, false);
            var rtRLine = goRLine.AddComponent<RectTransform>();
            rtRLine.anchorMin = new Vector2(1f, 0f);
            rtRLine.anchorMax = new Vector2(1f, 1f);
            rtRLine.pivot = new Vector2(1f, 0.5f);
            rtRLine.anchoredPosition = Vector2.zero;
            rtRLine.sizeDelta = new Vector2(1f, 0f);
            var imgRLine = goRLine.AddComponent<Image>();
            imgRLine.color = borderLineColor;
            
            GameObject goRIPText = new GameObject("Text");
            goRIPText.transform.SetParent(resultItemPrefab.transform, false);
            var rtRIPT = goRIPText.AddComponent<RectTransform>();
            rtRIPT.anchorMin = Vector2.zero;
            rtRIPT.anchorMax = Vector2.one;
            rtRIPT.offsetMin = new Vector2(6, 0);
            rtRIPT.offsetMax = new Vector2(-6, 0);
            var txtRIP = goRIPText.AddComponent<Text>();
            txtRIP.font = GetSafeFont();
            txtRIP.fontSize = 13; // Increased to 13px to match enlarged 26px rows
            txtRIP.alignment = TextAnchor.MiddleCenter; // Centered matching Nowa Online
            txtRIP.color = colorTextWhite;
            resultItemPrefab.SetActive(false);

            // 7. Results Page Controls
            GameObject goResultsPage = new GameObject("ResultsPage");
            goResultsPage.transform.SetParent(transform, false);
            var rtRPG = goResultsPage.AddComponent<RectTransform>();
            rtRPG.anchorMin = new Vector2(0f, 1f);
            rtRPG.anchorMax = new Vector2(1f, 1f);
            rtRPG.pivot = new Vector2(0.5f, 1f);
            rtRPG.anchoredPosition = new Vector2(0, -266); // Positioned exactly 10px below the last row (Y = -256)
            rtRPG.sizeDelta = new Vector2(-20, 22); // Slightly taller for larger buttons

            // Prev Result Button (Enlarged)
            GameObject goPrevR = new GameObject("BtnPrev");
            goPrevR.transform.SetParent(goResultsPage.transform, false);
            var rtPrevR = goPrevR.AddComponent<RectTransform>();
            rtPrevR.anchorMin = new Vector2(0.5f, 0.5f);
            rtPrevR.anchorMax = new Vector2(0.5f, 0.5f);
            rtPrevR.pivot = new Vector2(0.5f, 0.5f);
            rtPrevR.anchoredPosition = new Vector2(-55, 0);
            rtPrevR.sizeDelta = new Vector2(40, 20); // Enlarged button size
            btnPrevResultsPage = goPrevR.AddComponent<Button>();
            var imgPrevR = goPrevR.AddComponent<Image>();
            imgPrevR.sprite = spBtn;
            
            GameObject goPrevRText = new GameObject("Text");
            goPrevRText.transform.SetParent(goPrevR.transform, false);
            var rtPrevRT = goPrevRText.AddComponent<RectTransform>();
            rtPrevRT.anchorMin = Vector2.zero;
            rtPrevRT.anchorMax = Vector2.one;
            rtPrevRT.offsetMin = Vector2.zero; // Clear offsets to stay inside button
            rtPrevRT.offsetMax = Vector2.zero; // Clear offsets to stay inside button
            var txtPrevR = goPrevRText.AddComponent<Text>();
            txtPrevR.text = "◀";
            txtPrevR.font = GetSafeFont();
            txtPrevR.fontSize = 10;
            txtPrevR.alignment = TextAnchor.MiddleCenter;
            txtPrevR.color = colorTextGold;
            txtPrevR.raycastTarget = false; // Disable to prevent click intercepting

            // Page Text
            GameObject goPageR = new GameObject("TxtPage");
            goPageR.transform.SetParent(goResultsPage.transform, false);
            var rtPageR = goPageR.AddComponent<RectTransform>();
            rtPageR.anchorMin = new Vector2(0.5f, 0.5f);
            rtPageR.anchorMax = new Vector2(0.5f, 0.5f);
            rtPageR.pivot = new Vector2(0.5f, 0.5f);
            rtPageR.anchoredPosition = new Vector2(0, 0);
            rtPageR.sizeDelta = new Vector2(70, 20);
            txtResultsPage = goPageR.AddComponent<Text>();
            txtResultsPage.text = "1/51";
            txtResultsPage.font = GetSafeFont();
            txtResultsPage.fontSize = 10;
            txtResultsPage.alignment = TextAnchor.MiddleCenter;
            txtResultsPage.color = colorTextWhite;
            txtResultsPage.raycastTarget = false;

            // Next Result Button (Enlarged)
            GameObject goNextR = new GameObject("BtnNext");
            goNextR.transform.SetParent(goResultsPage.transform, false);
            var rtNextR = goNextR.AddComponent<RectTransform>();
            rtNextR.anchorMin = new Vector2(0.5f, 0.5f);
            rtNextR.anchorMax = new Vector2(0.5f, 0.5f);
            rtNextR.pivot = new Vector2(0.5f, 0.5f);
            rtNextR.anchoredPosition = new Vector2(55, 0);
            rtNextR.sizeDelta = new Vector2(40, 20); // Enlarged button size
            btnNextResultsPage = goNextR.AddComponent<Button>();
            var imgNextR = goNextR.AddComponent<Image>();
            imgNextR.sprite = spBtn;
            
            GameObject goNextRText = new GameObject("Text");
            goNextRText.transform.SetParent(goNextR.transform, false);
            var rtNextRT = goNextRText.AddComponent<RectTransform>();
            rtNextRT.anchorMin = Vector2.zero;
            rtNextRT.anchorMax = Vector2.one;
            rtNextRT.offsetMin = Vector2.zero; // Clear offsets to stay inside button
            rtNextRT.offsetMax = Vector2.zero; // Clear offsets to stay inside button
            var txtNextR = goNextRText.AddComponent<Text>();
            txtNextR.text = "▶";
            txtNextR.font = GetSafeFont();
            txtNextR.fontSize = 10;
            txtNextR.alignment = TextAnchor.MiddleCenter;
            txtNextR.color = colorTextGold;
            txtNextR.raycastTarget = false; // Disable to prevent click intercepting

            // 8. Selected Info Header
            GameObject goSelectedInfo = new GameObject("SelectedInfoHeader");
            goSelectedInfo.transform.SetParent(transform, false);
            var rtSI = goSelectedInfo.AddComponent<RectTransform>();
            rtSI.anchorMin = new Vector2(0f, 1f);
            rtSI.anchorMax = new Vector2(1f, 1f);
            rtSI.pivot = new Vector2(0.5f, 1f);
            rtSI.anchoredPosition = new Vector2(0, -300);
            rtSI.sizeDelta = new Vector2(-57, 22); // Extended by 10px on each side (245px to 265px width)

            var imgSI = goSelectedInfo.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                imgSI.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "drop_selected_info_bg", 265, 22, 10,
                    new Color(0.15f, 0.15f, 0.15f, 0.8f),
                    Color.clear,
                    0
                );
            }
            else
            {
                imgSI.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            }

            // Geri (Back) Button in SelectedInfoHeader (left-aligned, hidden by default)
            GameObject goBack = new GameObject("BtnBack");
            goBack.transform.SetParent(goSelectedInfo.transform, false);
            var rtBack = goBack.AddComponent<RectTransform>();
            rtBack.anchorMin = new Vector2(0f, 0.5f);
            rtBack.anchorMax = new Vector2(0f, 0.5f);
            rtBack.pivot = new Vector2(0f, 0.5f);
            rtBack.anchoredPosition = new Vector2(10, 0); // Offset by 10px to keep absolute position constant (X = 38.5)
            rtBack.sizeDelta = new Vector2(50, 18);
            
            var imgBack = goBack.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                imgBack.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "drop_back_btn_bg_v2", 50, 18, 5,
                    new Color(0.06f, 0.05f, 0.04f, 0.95f),
                    new Color(0.43f, 0.36f, 0.26f, 1f),
                    1
                );
            }
            else
            {
                imgBack.sprite = spBtn;
            }
            imgBack.raycastTarget = true;
            
            btnBack = goBack.AddComponent<Button>();
            btnBack.targetGraphic = imgBack;
            
            GameObject goBackText = new GameObject("Text");
            goBackText.transform.SetParent(goBack.transform, false);
            var rtBackT = goBackText.AddComponent<RectTransform>();
            rtBackT.anchorMin = Vector2.zero;
            rtBackT.anchorMax = Vector2.one;
            rtBackT.offsetMin = Vector2.zero;
            rtBackT.offsetMax = Vector2.zero;
            var txtBack = goBackText.AddComponent<Text>();
            txtBack.text = "Back";
            txtBack.font = GetSafeFont();
            txtBack.fontSize = 10;
            txtBack.fontStyle = FontStyle.Bold;
            txtBack.alignment = TextAnchor.MiddleCenter;
            txtBack.color = colorTextGold;
            txtBack.raycastTarget = false;
            
            goBack.SetActive(false); // Hidden by default

            GameObject goSIName = new GameObject("TxtName");
            goSIName.transform.SetParent(goSelectedInfo.transform, false);
            var rtSIN = goSIName.AddComponent<RectTransform>();
            rtSIN.anchorMin = new Vector2(0.5f, 0.5f);
            rtSIN.anchorMax = new Vector2(1f, 0.5f);
            rtSIN.pivot = new Vector2(1f, 0.5f);
            rtSIN.anchoredPosition = new Vector2(-10, 0); // Offset by -10px to keep absolute position constant (X = 283.5)
            rtSIN.sizeDelta = new Vector2(180, 20); // Widened to allow longer names
            txtSelectedName = goSIName.AddComponent<Text>();
            txtSelectedName.text = "Select Monster or Item";
            txtSelectedName.font = GetSafeFont();
            txtSelectedName.fontSize = 13;
            txtSelectedName.alignment = TextAnchor.MiddleRight; // Right-aligned
            txtSelectedName.color = colorTextGold;
            txtSelectedName.fontStyle = FontStyle.Bold;
            txtSelectedName.raycastTarget = false; // Prevent blocking clicks

            GameObject goSIInfo = new GameObject("TxtInfo");
            goSIInfo.transform.SetParent(goSelectedInfo.transform, false);
            var rtSII = goSIInfo.AddComponent<RectTransform>();
            rtSII.anchorMin = new Vector2(0.5f, 0.5f);
            rtSII.anchorMax = new Vector2(1f, 0.5f);
            rtSII.pivot = new Vector2(1f, 0.5f);
            rtSII.anchoredPosition = new Vector2(-8, 0);
            rtSII.sizeDelta = new Vector2(120, 20);
            txtSelectedInfo = goSIInfo.AddComponent<Text>();
            txtSelectedInfo.text = "";
            txtSelectedInfo.font = GetSafeFont();
            txtSelectedInfo.fontSize = 11;
            txtSelectedInfo.alignment = TextAnchor.MiddleRight;
            txtSelectedInfo.color = colorTextWhite;
            txtSelectedInfo.raycastTarget = false; // Prevent blocking clicks

            // 9. Grid Panel (15 Slots - 5 columns x 3 rows)
            GameObject goGrid = new GameObject("GridPanel");
            goGrid.transform.SetParent(transform, false);
            var rtGrid = goGrid.AddComponent<RectTransform>();
            rtGrid.anchorMin = new Vector2(0f, 1f);
            rtGrid.anchorMax = new Vector2(1f, 1f);
            rtGrid.pivot = new Vector2(0.5f, 1f);
            rtGrid.anchoredPosition = new Vector2(0, -332);
            rtGrid.sizeDelta = new Vector2(-77, 145); // 245px width (5*45 + 4*5) centers perfectly in 322px panel (77px total margins)

            var glg = goGrid.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(45, 45); // Reduced to 45x45px
            glg.spacing = new Vector2(5, 5);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 5;

            // 10. Instantiate 15 Slots
            for (int i = 0; i < 15; i++)
            {
                GameObject slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(goGrid.transform, false);
                var rtS = slot.AddComponent<RectTransform>();
                
                var imgS = slot.AddComponent<Image>();
                imgS.sprite = spSlot;
                imgS.type = Image.Type.Sliced;

                var btnS = slot.AddComponent<Button>();

                // Item Icon (stretches to fill slot completely, matching inventory)
                GameObject goIcon = new GameObject("Icon");
                goIcon.transform.SetParent(slot.transform, false);
                var rtIcon = goIcon.AddComponent<RectTransform>();
                rtIcon.anchorMin = Vector2.zero;
                rtIcon.anchorMax = Vector2.one;
                rtIcon.pivot = new Vector2(0.5f, 0.5f);
                rtIcon.anchoredPosition = Vector2.zero;
                rtIcon.sizeDelta = Vector2.zero;
                var imgIcon = goIcon.AddComponent<Image>();
                imgIcon.preserveAspect = true;
                imgIcon.raycastTarget = false; // Prevent blocking parent slot button clicks
                imgIcon.gameObject.SetActive(false); // Default hidden to avoid white squares

                // Item Name (middle/bottom - used only for monster names, invisible/empty for items)
                GameObject goName = new GameObject("Name");
                goName.transform.SetParent(slot.transform, false);
                var rtName = goName.AddComponent<RectTransform>();
                rtName.anchorMin = new Vector2(0f, 0f);
                rtName.anchorMax = new Vector2(1f, 0f);
                rtName.pivot = new Vector2(0.5f, 0f);
                rtName.anchoredPosition = new Vector2(0, 10);
                rtName.sizeDelta = new Vector2(-2, 10);
                var txtName = goName.AddComponent<Text>();
                txtName.font = GetSafeFont();
                txtName.fontSize = 7;
                txtName.alignment = TextAnchor.MiddleCenter;
                txtName.color = colorTextWhite;
                txtName.raycastTarget = false; // Prevent blocking parent slot button clicks

                // Rate (bottom right count text, matching stack size styling)
                GameObject goRate = new GameObject("Rate");
                goRate.transform.SetParent(slot.transform, false);
                var rtRate = goRate.AddComponent<RectTransform>();
                rtRate.anchorMin = new Vector2(0f, 0f);
                rtRate.anchorMax = new Vector2(1f, 0f);
                rtRate.pivot = new Vector2(1f, 0f);
                rtRate.anchoredPosition = new Vector2(-4, 2);
                rtRate.sizeDelta = new Vector2(0, 14);
                
                var txtRate = goRate.AddComponent<Text>();
                txtRate.font = GetSafeFont();
                txtRate.fontSize = 10;
                txtRate.fontStyle = FontStyle.Bold;
                txtRate.alignment = TextAnchor.LowerRight;
                txtRate.color = Color.green;
                txtRate.raycastTarget = false; // Prevent blocking parent slot button clicks

                var outline = goRate.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);

                goRate.SetActive(false); // Hidden by default

                // Question Mark Text (for group drops, default hidden)
                GameObject goQMark = new GameObject("QMark");
                goQMark.transform.SetParent(slot.transform, false);
                var rtQM = goQMark.AddComponent<RectTransform>();
                rtQM.anchorMin = Vector2.zero;
                rtQM.anchorMax = Vector2.one;
                rtQM.pivot = new Vector2(0.5f, 0.5f);
                rtQM.anchoredPosition = Vector2.zero;
                rtQM.sizeDelta = Vector2.zero;
                var txtQM = goQMark.AddComponent<Text>();
                txtQM.text = "?";
                txtQM.font = GetSafeFont();
                txtQM.fontSize = 24;
                txtQM.fontStyle = FontStyle.Bold;
                txtQM.alignment = TextAnchor.MiddleCenter;
                txtQM.color = colorTextGold;
                txtQM.raycastTarget = false;
                goQMark.SetActive(false);

                dropSlots[i] = slot;
            }

            // 11. Drops Page Controls (Bottom)
            GameObject goDropsPage = new GameObject("DropsPage");
            goDropsPage.transform.SetParent(transform, false);
            var rtDP = goDropsPage.AddComponent<RectTransform>();
            rtDP.anchorMin = new Vector2(0f, 1f);
            rtDP.anchorMax = new Vector2(1f, 1f);
            rtDP.pivot = new Vector2(0.5f, 1f);
            rtDP.anchoredPosition = new Vector2(0, -487);
            rtDP.sizeDelta = new Vector2(-20, 22); // Slightly taller for larger buttons

            // Prev Drop Button (Enlarged)
            GameObject goPrevD = new GameObject("BtnPrev");
            goPrevD.transform.SetParent(goDropsPage.transform, false);
            var rtPrevD = goPrevD.AddComponent<RectTransform>();
            rtPrevD.anchorMin = new Vector2(0.5f, 0.5f);
            rtPrevD.anchorMax = new Vector2(0.5f, 0.5f);
            rtPrevD.pivot = new Vector2(0.5f, 0.5f);
            rtPrevD.anchoredPosition = new Vector2(-55, 0);
            rtPrevD.sizeDelta = new Vector2(40, 20); // Enlarged button size
            btnPrevDropsPage = goPrevD.AddComponent<Button>();
            var imgPrevD = goPrevD.AddComponent<Image>();
            imgPrevD.sprite = spBtn;
            
            GameObject goPrevDText = new GameObject("Text");
            goPrevDText.transform.SetParent(goPrevD.transform, false);
            var rtPrevDT = goPrevDText.AddComponent<RectTransform>();
            rtPrevDT.anchorMin = Vector2.zero;
            rtPrevDT.anchorMax = Vector2.one;
            rtPrevDT.offsetMin = Vector2.zero; // Clear offsets to stay inside button
            rtPrevDT.offsetMax = Vector2.zero; // Clear offsets to stay inside button
            var txtPrevD = goPrevDText.AddComponent<Text>();
            txtPrevD.text = "◀";
            txtPrevD.font = GetSafeFont();
            txtPrevD.fontSize = 10;
            txtPrevD.alignment = TextAnchor.MiddleCenter;
            txtPrevD.color = colorTextGold;
            txtPrevD.raycastTarget = false; // Disable to prevent click intercepting

            // Page Text
            GameObject goPageD = new GameObject("TxtPage");
            goPageD.transform.SetParent(goDropsPage.transform, false);
            var rtPageD = goPageD.AddComponent<RectTransform>();
            rtPageD.anchorMin = new Vector2(0.5f, 0.5f);
            rtPageD.anchorMax = new Vector2(0.5f, 0.5f);
            rtPageD.pivot = new Vector2(0.5f, 0.5f);
            rtPageD.anchoredPosition = new Vector2(0, 0);
            rtPageD.sizeDelta = new Vector2(70, 20);
            txtDropsPage = goPageD.AddComponent<Text>();
            txtDropsPage.text = "0/0";
            txtDropsPage.font = GetSafeFont();
            txtDropsPage.fontSize = 10;
            txtDropsPage.alignment = TextAnchor.MiddleCenter;
            txtDropsPage.color = colorTextWhite;
            txtDropsPage.raycastTarget = false;

            // Next Drop Button (Enlarged)
            GameObject goNextD = new GameObject("BtnNext");
            goNextD.transform.SetParent(goDropsPage.transform, false);
            var rtNextD = goNextD.AddComponent<RectTransform>();
            rtNextD.anchorMin = new Vector2(0.5f, 0.5f);
            rtNextD.anchorMax = new Vector2(0.5f, 0.5f);
            rtNextD.pivot = new Vector2(0.5f, 0.5f);
            rtNextD.anchoredPosition = new Vector2(55, 0);
            rtNextD.sizeDelta = new Vector2(40, 20); // Enlarged button size
            btnNextDropsPage = goNextD.AddComponent<Button>();
            var imgNextD = goNextD.AddComponent<Image>();
            imgNextD.sprite = spBtn;
            
            GameObject goNextDText = new GameObject("Text");
            goNextDText.transform.SetParent(goNextD.transform, false);
            var rtNextDT = goNextDText.AddComponent<RectTransform>();
            rtNextDT.anchorMin = Vector2.zero;
            rtNextDT.anchorMax = Vector2.one;
            rtNextDT.offsetMin = Vector2.zero; // Clear offsets to stay inside button
            rtNextDT.offsetMax = Vector2.zero; // Clear offsets to stay inside button
            var txtNextD = goNextDText.AddComponent<Text>();
            txtNextD.text = "▶";
            txtNextD.font = GetSafeFont();
            txtNextD.fontSize = 10;
            txtNextD.alignment = TextAnchor.MiddleCenter;
            txtNextD.color = colorTextGold;
            txtNextD.raycastTarget = false; // Disable to prevent click intercepting
        }

        private Sprite CreateProceduralSprite(int width, int height, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = (x < borderWidth || x >= width - borderWidth || y < borderWidth || y >= height - borderWidth);
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private Font GetSafeFont()
        {
            if (KOUIManager.Instance != null)
            {
                Font f = KOUIManager.Instance.GetSafeFont(12);
                if (f != null) return f;
            }
            Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtin != null) return builtin;
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void RefreshResultHighlights()
        {
            int startIndex = (_currentResultsPage - 1) * ResultsPerPage;
            for (int i = 0; i < resultListContainer.childCount; i++)
            {
                var child = resultListContainer.GetChild(i);
                var img = child.GetComponent<Image>();
                if (img == null) continue;

                if (startIndex + i < _matchingMonsters.Count)
                {
                    var monster = _matchingMonsters[startIndex + i];
                    bool isSelected = (_selectedMonster != null && _selectedMonster.id == monster.id);
                    img.color = isSelected ? new Color(0.4f, 0.25f, 0.1f, 0.9f) : new Color(0.12f, 0.12f, 0.12f, 0.8f);
                }
                else
                {
                    // Fainter empty/padding row background
                    img.color = new Color(0.12f, 0.12f, 0.12f, 0.4f);
                }
            }
        }
    }
}
