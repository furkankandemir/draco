using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using KOImport;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Programmatic UI for the Quest Journal (Görev Günlüğü).
    /// Displays Available, Active, and Completed quests with Remote Accept/Deliver capabilities.
    /// </summary>
    public class QuestJournalUI : MonoBehaviour
    {
        public static QuestJournalUI Instance { get; private set; }

        private ScrollRect _scrollRect;
        private RectTransform _contentRt;
        private Button _btnClose;
        private readonly List<GameObject> _createdElements = new();
        private int _expandedQuestId = -1;

        // Quest Maximum Level constraints mapped from server-side event rules (.evt files)
        private readonly Dictionary<int, int> _questMaxLevels = new()
        {
            { 6, 11 },   // I'll tell you all about it: max level 11
            { 30, 20 },  // Worm Extermination: max level 20 (standard KO Beginner quest upper limit)
            { 50, 20 }   // Beginner Quest: max level 20
        };

        // Auto-generated Quest Slot Coordinates mapped from active SQL Database spawns
        private readonly Dictionary<int, QuestSlotInfo> _questSlots = new()
        {
            { 5, new QuestSlotInfo { ZoneId = 21, SlotName = "Proconsul", Position = new Vector3(310f, 0f, 350f) } }, // Proconsul's Request
            { 6, new QuestSlotInfo { ZoneId = 21, SlotName = "Isaac", Position = new Vector3(316f, 0f, 342f) } }, // I'll tell you all about it.
            { 7, new QuestSlotInfo { ZoneId = 52, SlotName = "Blood Don", Position = new Vector3(110f, 0f, 120f) } }, // Gem of Bravery
            { 8, new QuestSlotInfo { ZoneId = 2, SlotName = "Scout", Position = new Vector3(1486f, 0f, 957f) } }, // Recoinnassaince Report
            { 11, new QuestSlotInfo { ZoneId = 21, SlotName = "Orc Watcher", Position = new Vector3(77f, 0f, 193f) } }, // A Duel with the Ork
            { 12, new QuestSlotInfo { ZoneId = 21, SlotName = "Werewolf", Position = new Vector3(90f, 0f, 288f) } }, // Fang of Wolf Man
            { 14, new QuestSlotInfo { ZoneId = 21, SlotName = "Guard Trainee", Position = new Vector3(310f, 0f, 350f) } }, // Guard Trainee in Moradon
            { 15, new QuestSlotInfo { ZoneId = 21, SlotName = "Bulture", Position = new Vector3(61f, 0f, 422f) } }, // Three Sacrificial Offerings
            { 30, new QuestSlotInfo { ZoneId = 21, SlotName = "Blood worm", Position = new Vector3(201f, 0f, 304f) } }, // Worm Extermination
            { 31, new QuestSlotInfo { ZoneId = 21, SlotName = "Fury gavolt", Position = new Vector3(334f, 0f, 47f) } }, // Gavolt Extermination
            { 32, new QuestSlotInfo { ZoneId = 21, SlotName = "Shilan", Position = new Vector3(111f, 0f, 87f) } }, // Collecting Silan Bones
            { 33, new QuestSlotInfo { ZoneId = 21, SlotName = "Bulture", Position = new Vector3(61f, 0f, 422f) } }, // Collecting Bulture Horns
            { 34, new QuestSlotInfo { ZoneId = 21, SlotName = "Werewolf", Position = new Vector3(90f, 0f, 288f) } }, // Wolf Man Extermination
            { 44, new QuestSlotInfo { ZoneId = 21, SlotName = "Goddess of Moradon", Position = new Vector3(380f, 0f, 400f) } }, // Thanksgiving Event
            { 45, new QuestSlotInfo { ZoneId = 21, SlotName = "Renold", Position = new Vector3(385f, 0f, 395f) } }, // Christmas Cross
            { 50, new QuestSlotInfo { ZoneId = 21, SlotName = "Worm", Position = new Vector3(201f, 0f, 304f) } }, // Beginner Quest
            { 58, new QuestSlotInfo { ZoneId = 2, SlotName = "Master Skaky/Minerva", Position = new Vector3(1657f, 0f, 325f) } }, // Warrior/Priest Guardian Object
            { 59, new QuestSlotInfo { ZoneId = 2, SlotName = "Agent Clarence", Position = new Vector3(1631f, 0f, 1333f) } }, // Rogue Guardian Object
            { 60, new QuestSlotInfo { ZoneId = 2, SlotName = "Mage Drake", Position = new Vector3(372f, 0f, 1225f) } }, // Magician Guardian Object
            { 61, new QuestSlotInfo { ZoneId = 1, SlotName = "Cardinal", Position = new Vector3(600f, 0f, 797f) } }, // Asga Fruit
            { 62, new QuestSlotInfo { ZoneId = 1, SlotName = "Cardinal", Position = new Vector3(600f, 0f, 797f) } }, // Bell of Bellua
            { 65, new QuestSlotInfo { ZoneId = 1, SlotName = "Tyon", Position = new Vector3(352f, 0f, 1245f) } }, // Tyon Meat
            { 89, new QuestSlotInfo { ZoneId = 21, SlotName = "Renold", Position = new Vector3(385f, 0f, 395f) } }, // X-Mas Candy Cane
        };

        public static GameObject CreatePanel(Transform canvasParent)
        {
            // Root panel object
            var root = new GameObject("QuestJournalPanel");
            root.transform.SetParent(canvasParent, false);

            var rt = root.AddComponent<RectTransform>();
            // Centered panel, width 420, height 500
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420, 500);

            // Background
            var bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.95f);

            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.5f, 0.15f, 0.8f); // Golden border
            outline.effectDistance = new Vector2(2, -2);

            // Title Bar
            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(root.transform, false);
            var titleRt = titleBar.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = new Vector2(0, 0);
            titleRt.sizeDelta = new Vector2(0, 35);

            var titleBarImg = titleBar.AddComponent<Image>();
            titleBarImg.color = new Color(0.12f, 0.12f, 0.18f, 1f);

            // Title Text
            var titleTextObj = new GameObject("TitleText");
            titleTextObj.transform.SetParent(titleBar.transform, false);
            var titleTextRt = titleTextObj.AddComponent<RectTransform>();
            titleTextRt.anchorMin = Vector2.zero;
            titleTextRt.anchorMax = Vector2.one;
            titleTextRt.offsetMin = new Vector2(15, 0);
            titleTextRt.offsetMax = new Vector2(-45, 0);

            var titleText = titleTextObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleText.font == null) titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 13;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.8f, 0.1f, 1f);
            titleText.text = "📜 QUEST JOURNAL";
            titleText.alignment = TextAnchor.MiddleLeft;

            // Close Button
            var closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(titleBar.transform, false);
            var closeRt = closeBtnObj.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1, 0.5f);
            closeRt.anchorMax = new Vector2(1, 0.5f);
            closeRt.pivot = new Vector2(1, 0.5f);
            closeRt.anchoredPosition = new Vector2(-8, 0);
            closeRt.sizeDelta = new Vector2(24, 24);

            var closeBtnImg = closeBtnObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.4f, 0.1f, 0.1f, 1f); // Reddish Close button

            var closeOutline = closeBtnObj.AddComponent<Outline>();
            closeOutline.effectColor = Color.black;
            closeOutline.effectDistance = new Vector2(1, -1);

            var closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTextRt = closeTextObj.AddComponent<RectTransform>();
            closeTextRt.anchorMin = Vector2.zero;
            closeTextRt.anchorMax = Vector2.one;
            closeTextRt.offsetMin = Vector2.zero;
            closeTextRt.offsetMax = Vector2.zero;
            var closeText = closeTextObj.AddComponent<Text>();
            closeText.font = titleText.font;
            closeText.fontSize = 11;
            closeText.fontStyle = FontStyle.Bold;
            closeText.color = Color.white;
            closeText.text = "X";
            closeText.alignment = TextAnchor.MiddleCenter;

            // Scroll View area
            var scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(root.transform, false);
            var scrollRt = scrollView.AddComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(10, 10);
            scrollRt.offsetMax = new Vector2(-10, -45);

            var scrollImage = scrollView.AddComponent<Image>();
            scrollImage.color = new Color(0.04f, 0.04f, 0.06f, 0.8f);

            var mask = scrollView.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 15f;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(scrollView.transform, false);
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var vGroup = content.AddComponent<VerticalLayoutGroup>();
            vGroup.spacing = 6f;
            vGroup.padding = new RectOffset(6, 6, 6, 6);
            vGroup.childAlignment = TextAnchor.UpperCenter;
            vGroup.childControlHeight = true;
            vGroup.childControlWidth = true;
            vGroup.childForceExpandHeight = false;
            vGroup.childForceExpandWidth = false;

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.content = contentRt;

            // Attach controller script
            var journalUi = root.AddComponent<QuestJournalUI>();
            journalUi._scrollRect = scrollRect;
            journalUi._contentRt = contentRt;
            
            var closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(() => root.SetActive(false));
            journalUi._btnClose = closeBtn;

            return root;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            QuestDialogUI.OnQuestStatesChanged += RefreshJournal;
            RefreshJournal();
        }

        private void OnDisable()
        {
            QuestDialogUI.OnQuestStatesChanged -= RefreshJournal;
        }

        private void OnDestroy()
        {
            QuestDialogUI.OnQuestStatesChanged -= RefreshJournal;
            if (Instance == this) Instance = null;
        }

        private struct QuestItemRequirement
        {
            public int ItemDefId;
            public int Count;
        }

        private readonly Dictionary<int, List<QuestItemRequirement>> _questItemRequirements = new()
        {
            { 7, new List<QuestItemRequirement> { new() { ItemDefId = 910039000, Count = 10 } } }, // Gem of bravery
            { 11, new List<QuestItemRequirement> { new() { ItemDefId = 910038000, Count = 5 } } }, // Certificate of Duel
            { 12, new List<QuestItemRequirement> { new() { ItemDefId = 910020000, Count = 5 } } }, // Fang of Wolf Man
            { 13, new List<QuestItemRequirement> { new() { ItemDefId = 910018000, Count = 1 } } }, // Scroll of Seal
            { 14, new List<QuestItemRequirement> { new() { ItemDefId = 910017000, Count = 3 } } }, // Bulture Horn
            { 15, new List<QuestItemRequirement> { 
                new() { ItemDefId = 910020000, Count = 1 },
                new() { ItemDefId = 910017000, Count = 1 },
                new() { ItemDefId = 910021000, Count = 1 }
            } }, // Three Sacrificial Offerings
            { 30, new List<QuestItemRequirement> { new() { ItemDefId = 379048000, Count = 10 } } }, // Silk bundle
            { 31, new List<QuestItemRequirement> { new() { ItemDefId = 379043000, Count = 5 } } }, // Gavolt wing
            { 32, new List<QuestItemRequirement> { new() { ItemDefId = 379077000, Count = 10 } } }, // Silan Bone
            { 33, new List<QuestItemRequirement> { new() { ItemDefId = 910017000, Count = 5 } } }, // Bulture Horn
            { 34, new List<QuestItemRequirement> { new() { ItemDefId = 910020000, Count = 10 } } }, // Fang of Wolf Man
            { 61, new List<QuestItemRequirement> { new() { ItemDefId = 910082000, Count = 5 } } }, // Asga Fruit
            { 62, new List<QuestItemRequirement> { new() { ItemDefId = 910082000, Count = 5 } } }, // Asga Fruit (Bell of Bellua)
            { 65, new List<QuestItemRequirement> { new() { ItemDefId = 379204000, Count = 5 } } }, // Tyon Meat
            { 8, new List<QuestItemRequirement> { 
                new() { ItemDefId = 910040000, Count = 5 },
                new() { ItemDefId = 910041000, Count = 1 }
            } }, // Recoinnassaince Report
            { 9, new List<QuestItemRequirement> { new() { ItemDefId = 910057000, Count = 1 } } }  // Guardians of the 7 Keys
        };

        private bool HasRequiredQuestItems(int questId)
        {
            if (!_questItemRequirements.TryGetValue(questId, out var reqs))
                return false;

            var inventory = GameManager.Instance?.Inventory;
            if (inventory == null) return false;

            foreach (var req in reqs)
            {
                int totalCount = 0;
                foreach (var item in inventory)
                {
                    if (item != null && item.ItemDefId == req.ItemDefId)
                    {
                        totalCount += item.StackCount;
                    }
                }
                if (totalCount < req.Count)
                    return false;
            }

            return true;
        }

        public void ShowQuestAndExpand(int questId)
        {
            _expandedQuestId = questId;
            RefreshJournal();
        }

        public void RefreshJournal()
        {
            // Clear existing elements
            foreach (var elem in _createdElements)
            {
                if (elem != null) Destroy(elem);
            }
            _createdElements.Clear();

            var gm = GameManager.Instance;
            if (gm == null) return;

            int playerTblClass = GetTblClass(gm.CharClass);
            var allQuests = QuestTableParser.GetAllContent();
            if (allQuests == null) return;

            var displayList = new List<(QuestContentEntry entry, byte state)>();

            foreach (var kvp in allQuests)
            {
                var entry = kvp.Value;
                if (entry == null) continue;

                // Level Filter: Player must meet min level requirement
                if (gm.Level < entry.ReqLevel)
                    continue;

                // Max Level Filter: Player must not exceed max level limit if configured
                if (_questMaxLevels.TryGetValue(entry.Id, out int maxLvl) && gm.Level > maxLvl)
                    continue;

                // Class Filter: Player must meet class requirement
                if (entry.ReqClass != 5 && entry.ReqClass != playerTblClass)
                    continue;

                // Nation Filter: Player must not see quests of the other nation
                if (IsQuestForOtherNation(entry.Id, gm.Nation))
                    continue;

                byte state = QuestDialogUI.Instance != null ? QuestDialogUI.Instance.GetQuestState((short)entry.Id) : (byte)0;

                // Completely skip/hide completed quests (state 2)
                if (state == 2)
                    continue;

                displayList.Add((entry, state));
            }

            // Sort by Quest ID (ascending) to maintain the original database order
            displayList.Sort((a, b) => a.entry.Id.CompareTo(b.entry.Id));

            // Populate unified quest list
            foreach (var item in displayList)
            {
                CreateQuestRow(item.entry, item.state);
            }

            // Rebuild layout instantly
            Canvas.ForceUpdateCanvases();
            if (_contentRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRt);
            }
        }

        private void CreateQuestRow(QuestContentEntry quest, int state)
        {
            // Row container
            var rowObj = new GameObject($"QuestRow_{quest.Id}");
            rowObj.transform.SetParent(_contentRt, false);
            _createdElements.Add(rowObj);

            var rowRt = rowObj.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0, 30); // Default collapsed height

            var rowLayout = rowObj.AddComponent<LayoutElement>();
            rowLayout.preferredWidth = 380f;

            var vLayout = rowObj.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 2f;
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.childControlHeight = true;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childForceExpandWidth = false;

            // Header part of row
            var rowHeaderObj = new GameObject("RowHeader");
            rowHeaderObj.transform.SetParent(rowObj.transform, false);
            var rowHeaderRt = rowHeaderObj.AddComponent<RectTransform>();
            rowHeaderRt.sizeDelta = new Vector2(0, 26);

            var rowHeaderLayout = rowHeaderObj.AddComponent<LayoutElement>();
            rowHeaderLayout.minHeight = 26f;
            rowHeaderLayout.preferredHeight = 26f;
            rowHeaderLayout.preferredWidth = 380f;

            var rowHeaderImg = rowHeaderObj.AddComponent<Image>();
            rowHeaderImg.color = new Color(0.1f, 0.1f, 0.13f, 0.85f);

            var rowOutline = rowHeaderObj.AddComponent<Outline>();
            rowOutline.effectColor = new Color(0.2f, 0.2f, 0.25f, 0.4f);
            rowOutline.effectDistance = new Vector2(1, -1);

            // Title Text
            var titleTextObj = new GameObject("Text");
            titleTextObj.transform.SetParent(rowHeaderObj.transform, false);
            var titleTextRt = titleTextObj.AddComponent<RectTransform>();
            titleTextRt.anchorMin = Vector2.zero;
            titleTextRt.anchorMax = Vector2.one;
            titleTextRt.offsetMin = new Vector2(10, 0);
            titleTextRt.offsetMax = new Vector2(-40, 0);

            var titleTxt = titleTextObj.AddComponent<Text>();
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (titleTxt.font == null) titleTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleTxt.fontSize = 11;

            bool isDeliverable = state == 1 && HasRequiredQuestItems(quest.Id);

            titleTxt.color = state switch
            {
                1 => isDeliverable ? new Color(0.4f, 0.85f, 0.4f, 1f) : new Color(0.9f, 0.8f, 0.4f, 1f), // Deliverable: Light green, Active: Light amber
                _ => Color.white // Available
            };
            titleTxt.text = $"[Lv.{quest.ReqLevel}] {quest.Name}";
            titleTxt.alignment = TextAnchor.MiddleLeft;

            // Toggle Expand Button
            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(rowHeaderObj.transform, false);
            var arrowRt = arrowObj.AddComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(1, 0.5f);
            arrowRt.anchorMax = new Vector2(1, 0.5f);
            arrowRt.pivot = new Vector2(1, 0.5f);
            arrowRt.anchoredPosition = new Vector2(-10, 0);
            arrowRt.sizeDelta = new Vector2(20, 20);

            bool currentlyExpanded = _expandedQuestId == quest.Id;

            var arrowTxt = arrowObj.AddComponent<Text>();
            arrowTxt.font = titleTxt.font;
            arrowTxt.fontSize = 11;
            arrowTxt.fontStyle = FontStyle.Bold;
            arrowTxt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            arrowTxt.text = currentlyExpanded ? "-" : "+";
            arrowTxt.alignment = TextAnchor.MiddleCenter;

            // Details part (collapsible)
            var detailsObj = new GameObject("RowDetails");
            detailsObj.transform.SetParent(rowObj.transform, false);
            var detailsRt = detailsObj.AddComponent<RectTransform>();
            detailsObj.SetActive(currentlyExpanded);

            var detailsLayout = detailsObj.AddComponent<LayoutElement>();
            detailsLayout.preferredWidth = 380f;

            var detailsImg = detailsObj.AddComponent<Image>();
            detailsImg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);

            var detailsVLayout = detailsObj.AddComponent<VerticalLayoutGroup>();
            detailsVLayout.spacing = 6f;
            detailsVLayout.padding = new RectOffset(10, 10, 8, 8);
            detailsVLayout.childControlHeight = true;
            detailsVLayout.childControlWidth = true;
            detailsVLayout.childForceExpandHeight = false;
            detailsVLayout.childForceExpandWidth = false;

            // Description Label
            var descObj = new GameObject("Desc");
            descObj.transform.SetParent(detailsObj.transform, false);
            var descTxt = descObj.AddComponent<Text>();
            descTxt.font = titleTxt.font;
            descTxt.fontSize = 10;
            descTxt.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            descTxt.text = string.IsNullOrEmpty(quest.Description) ? "Description not available." : quest.Description;
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Rewards Label
            var rewardObj = new GameObject("Rewards");
            rewardObj.transform.SetParent(detailsObj.transform, false);
            var rewardTxt = rewardObj.AddComponent<Text>();
            rewardTxt.font = titleTxt.font;
            rewardTxt.fontSize = 10;
            rewardTxt.color = new Color(0.95f, 0.75f, 0.3f, 1f); // Gold rewards color
            rewardTxt.text = string.IsNullOrEmpty(quest.Reward) ? "Reward: Not specified" : $"🎁 Reward: {quest.Reward}";
            rewardTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Action Button container
            var actionBtnRow = new GameObject("ActionRow");
            actionBtnRow.transform.SetParent(detailsObj.transform, false);
            var actionBtnRowRt = actionBtnRow.AddComponent<RectTransform>();
            actionBtnRowRt.sizeDelta = new Vector2(0, 26);

            var actionRowLayout = actionBtnRow.AddComponent<LayoutElement>();
            actionRowLayout.minHeight = 26f;
            actionRowLayout.preferredHeight = 26f;
            actionRowLayout.preferredWidth = 380f;

            var hLayout = actionBtnRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlHeight = true;
            hLayout.childControlWidth = false;
            hLayout.childForceExpandHeight = false;
            hLayout.childForceExpandWidth = false;

            // State specific action setup
            if (state == 0) // Available
            {
                var actionBtnObj = new GameObject("AcceptButton");
                actionBtnObj.transform.SetParent(actionBtnRow.transform, false);
                var actionBtnRt = actionBtnObj.AddComponent<RectTransform>();
                actionBtnRt.sizeDelta = new Vector2(100, 24);

                var actionBtnImg = actionBtnObj.AddComponent<Image>();
                actionBtnImg.color = new Color(0.2f, 0.45f, 0.2f, 1f); // Green Accept
                var actionBtn = actionBtnObj.AddComponent<Button>();

                var btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(actionBtnObj.transform, false);
                var btnTextRt = btnTextObj.AddComponent<RectTransform>();
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                var btnTxt = btnTextObj.AddComponent<Text>();
                btnTxt.font = titleTxt.font;
                btnTxt.fontSize = 10;
                btnTxt.fontStyle = FontStyle.Bold;
                btnTxt.color = Color.white;
                btnTxt.alignment = TextAnchor.MiddleCenter;
                btnTxt.text = "Accept";

                actionBtn.onClick.AddListener(() =>
                {
                    SendRemoteQuestPacket(0x0A, (ushort)quest.Id);
                    actionBtn.interactable = false;
                    btnTxt.text = "Processing...";
                });
            }
            else if (state == 1) // Active / In Progress
            {
                if (isDeliverable)
                {
                    // Deliver Button only
                    var deliverBtnObj = new GameObject("DeliverButton");
                    deliverBtnObj.transform.SetParent(actionBtnRow.transform, false);
                    var deliverBtnRt = deliverBtnObj.AddComponent<RectTransform>();
                    deliverBtnRt.sizeDelta = new Vector2(100, 24);

                    var deliverBtnImg = deliverBtnObj.AddComponent<Image>();
                    deliverBtnImg.color = new Color(0.2f, 0.35f, 0.55f, 1f); // Blue Deliver
                    var deliverBtn = deliverBtnObj.AddComponent<Button>();

                    var delTextObj = new GameObject("Text");
                    delTextObj.transform.SetParent(deliverBtnObj.transform, false);
                    var delTextRt = delTextObj.AddComponent<RectTransform>();
                    delTextRt.anchorMin = Vector2.zero;
                    delTextRt.anchorMax = Vector2.one;
                    var delTxt = delTextObj.AddComponent<Text>();
                    delTxt.font = titleTxt.font;
                    delTxt.fontSize = 10;
                    delTxt.fontStyle = FontStyle.Bold;
                    delTxt.color = Color.white;
                    delTxt.alignment = TextAnchor.MiddleCenter;
                    delTxt.text = "Deliver";

                    deliverBtn.onClick.AddListener(() =>
                    {
                        SendRemoteQuestPacket(0x0B, (ushort)quest.Id); // QUEST_REMOTE_COMPLETE
                        deliverBtn.interactable = false;
                        delTxt.text = "Processing...";
                    });
                }
                else
                {
                    // Reject Button
                    var rejectBtnObj = new GameObject("RejectButton");
                    rejectBtnObj.transform.SetParent(actionBtnRow.transform, false);
                    var rejectBtnRt = rejectBtnObj.AddComponent<RectTransform>();
                    rejectBtnRt.sizeDelta = new Vector2(100, 24);

                    var rejectBtnImg = rejectBtnObj.AddComponent<Image>();
                    rejectBtnImg.color = new Color(0.45f, 0.2f, 0.2f, 1f); // Red Reject
                    var rejectBtn = rejectBtnObj.AddComponent<Button>();

                    var rejTextObj = new GameObject("Text");
                    rejTextObj.transform.SetParent(rejectBtnObj.transform, false);
                    var rejTextRt = rejTextObj.AddComponent<RectTransform>();
                    rejTextRt.anchorMin = Vector2.zero;
                    rejTextRt.anchorMax = Vector2.one;
                    var rejTxt = rejTextObj.AddComponent<Text>();
                    rejTxt.font = titleTxt.font;
                    rejTxt.fontSize = 10;
                    rejTxt.fontStyle = FontStyle.Bold;
                    rejTxt.color = Color.white;
                    rejTxt.alignment = TextAnchor.MiddleCenter;
                    rejTxt.text = "Reject";

                    // Teleport Button
                    var tpBtnObj = new GameObject("TeleportButton");
                    tpBtnObj.transform.SetParent(actionBtnRow.transform, false);
                    var tpBtnRt = tpBtnObj.AddComponent<RectTransform>();
                    tpBtnRt.sizeDelta = new Vector2(100, 24);

                    var tpBtnImg = tpBtnObj.AddComponent<Image>();
                    tpBtnImg.color = new Color(0.55f, 0.45f, 0.15f, 1f); // Gold Teleport
                    var tpBtn = tpBtnObj.AddComponent<Button>();

                    var tpTextObj = new GameObject("Text");
                    tpTextObj.transform.SetParent(tpBtnObj.transform, false);
                    var tpTextRt = tpTextObj.AddComponent<RectTransform>();
                    tpTextRt.anchorMin = Vector2.zero;
                    tpTextRt.anchorMax = Vector2.one;
                    var tpTxt = tpTextObj.AddComponent<Text>();
                    tpTxt.font = titleTxt.font;
                    tpTxt.fontSize = 10;
                    tpTxt.fontStyle = FontStyle.Bold;
                    tpTxt.color = Color.white;
                    tpTxt.alignment = TextAnchor.MiddleCenter;
                    tpTxt.text = "Teleport";

                    rejectBtn.onClick.AddListener(() =>
                    {
                        SendRemoteQuestPacket(0x0C, (ushort)quest.Id); // QUEST_REMOTE_REJECT
                        rejectBtn.interactable = false;
                        tpBtn.interactable = false;
                        rejTxt.text = "Processing...";
                    });

                    tpBtn.onClick.AddListener(() =>
                    {
                        if (_questSlots.TryGetValue(quest.Id, out var slotInfo) && slotInfo != null)
                        {
                            short currentZone = GameManager.Instance != null ? GameManager.Instance.CurrentZoneId : (short)0;
                            int targetZone = slotInfo.ZoneId;
                            Vector3 targetPos = slotInfo.Position;
                            
                            // Handle mirrored nation map IDs (1 for Luferson / Karus, 2 for El Morad Castle)
                            if ((targetZone == 1 && currentZone == 2) || (targetZone == 2 && currentZone == 1))
                            {
                                targetZone = currentZone;
                                // Dynamically mirror coordinates for common castle NPCs/scouts if necessary
                                if (quest.Id == 8) // Recoinnassaince Report
                                {
                                    targetPos = currentZone == 1 ? new Vector3(815f, 0f, 1802f) : new Vector3(1486f, 0f, 957f);
                                }
                                else if (quest.Id == 58) // Warrior Master Skaky / Priest Minerva
                                {
                                    targetPos = currentZone == 1 ? new Vector3(380f, 0f, 1745f) : new Vector3(1657f, 0f, 325f);
                                }
                                else if (quest.Id == 59) // Secret Agent Clarence
                                {
                                    targetPos = currentZone == 1 ? new Vector3(431f, 0f, 708f) : new Vector3(1631f, 0f, 1333f);
                                }
                                else if (quest.Id == 60) // Arch Mage Drake
                                {
                                    targetPos = currentZone == 1 ? new Vector3(1695f, 0f, 805f) : new Vector3(372f, 0f, 1225f);
                                }
                            }

                            if (targetZone == currentZone)
                            {
                                ShowConfirmDialog($"You will be teleported to the {slotInfo.SlotName} slot. Are you sure?", () =>
                                {
                                    SendRemoteQuestTeleportPacket((ushort)quest.Id, targetPos.x, targetPos.z);
                                });
                            }
                            else
                            {
                                ShowWarningDialog("You are in the wrong zone. You cannot teleport to this slot.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[QUEST-UI] No slot mapping found for Quest ID={quest.Id}. Showing Warning Dialog...");
                            ShowWarningDialog("Teleport coordinates for this quest are not configured.");
                        }
                    });
                }
            }

            // Click behavior to collapse/expand
            var rowHeaderBtn = rowHeaderObj.AddComponent<Button>();
            rowHeaderBtn.transition = Selectable.Transition.None;
            rowHeaderBtn.onClick.AddListener(() =>
            {
                if (_expandedQuestId == quest.Id)
                {
                    _expandedQuestId = -1; // Collapse
                }
                else
                {
                    _expandedQuestId = quest.Id; // Expand
                }

                // Re-render the journal to apply new expanded states
                RefreshJournal();
            });
        }

        private void SendRemoteQuestPacket(byte subOpcode, ushort questId)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_QUEST);
            pkt.WriteByte(subOpcode);
            pkt.WriteUInt16(questId);
            KONetworkManager.Instance?.SendPacket(pkt);

        }

        private int GetTblClass(byte eClass)
        {
            int baseClass = eClass % 100;
            return baseClass switch
            {
                1 or 5 or 6 => 1,   // Warrior in TBL is 1
                2 or 7 or 8 => 2,   // Rogue in TBL is 2
                3 or 9 or 10 => 3,  // Mage in TBL is 3
                4 or 11 or 12 => 4, // Priest in TBL is 4
                _ => 5              // General / All is 5
            };
        }

        private void SendRemoteQuestTeleportPacket(ushort questId, float x, float z)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_QUEST);
            pkt.WriteByte(0x0D); // QUEST_REMOTE_TELEPORT
            pkt.WriteUInt16(questId);
            pkt.WriteFloat(x);
            pkt.WriteFloat(z);
            KONetworkManager.Instance?.SendPacket(pkt);

        }

        private void ShowConfirmDialog(string message, System.Action onConfirm)
        {
            var popup = new GameObject("QuestPopup");
            popup.transform.SetParent(transform, false);

            var rt = popup.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320, 160);

            var bgImg = popup.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            var outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.5f, 0.15f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(popup.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0.4f);
            textRt.anchorMax = new Vector2(1, 1);
            textRt.offsetMin = new Vector2(15, 15);
            textRt.offsetMax = new Vector2(-15, -15);

            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 11;
            text.color = Color.white;
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btnsObj = new GameObject("Buttons");
            btnsObj.transform.SetParent(popup.transform, false);
            var btnsRt = btnsObj.AddComponent<RectTransform>();
            btnsRt.anchorMin = new Vector2(0, 0);
            btnsRt.anchorMax = new Vector2(1, 0.4f);
            btnsRt.offsetMin = new Vector2(15, 10);
            btnsRt.offsetMax = new Vector2(-15, -10);

            var hGroup = btnsObj.AddComponent<HorizontalLayoutGroup>();
            hGroup.spacing = 15f;
            hGroup.childAlignment = TextAnchor.MiddleCenter;
            hGroup.childControlHeight = true;
            hGroup.childControlWidth = true;

            var okBtnObj = new GameObject("OkButton");
            okBtnObj.transform.SetParent(btnsObj.transform, false);

            var okLayout = okBtnObj.AddComponent<LayoutElement>();
            okLayout.preferredWidth = 100f;
            okLayout.preferredHeight = 24f;

            var okImg = okBtnObj.AddComponent<Image>();
            okImg.color = new Color(0.15f, 0.4f, 0.15f, 1f);
            var okBtn = okBtnObj.AddComponent<Button>();
            var okTxtObj = new GameObject("Text");
            okTxtObj.transform.SetParent(okBtnObj.transform, false);
            var okTxtRt = okTxtObj.AddComponent<RectTransform>();
            okTxtRt.anchorMin = Vector2.zero;
            okTxtRt.anchorMax = Vector2.one;
            okTxtRt.offsetMin = Vector2.zero;
            okTxtRt.offsetMax = Vector2.zero;
            var okTxt = okTxtObj.AddComponent<Text>();
            okTxt.font = text.font;
            okTxt.fontSize = 10;
            okTxt.fontStyle = FontStyle.Bold;
            okTxt.color = Color.white;
            okTxt.text = "OK";
            okTxt.alignment = TextAnchor.MiddleCenter;

            okBtn.onClick.AddListener(() =>
            {
                Destroy(popup);
                onConfirm?.Invoke();
            });

            var cancelBtnObj = new GameObject("CancelButton");
            cancelBtnObj.transform.SetParent(btnsObj.transform, false);

            var cancelLayout = cancelBtnObj.AddComponent<LayoutElement>();
            cancelLayout.preferredWidth = 100f;
            cancelLayout.preferredHeight = 24f;

            var cancelImg = cancelBtnObj.AddComponent<Image>();
            cancelImg.color = new Color(0.4f, 0.15f, 0.15f, 1f);
            var cancelBtn = cancelBtnObj.AddComponent<Button>();
            var cancelTxtObj = new GameObject("Text");
            cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
            var cancelTxtRt = cancelTxtObj.AddComponent<RectTransform>();
            cancelTxtRt.anchorMin = Vector2.zero;
            cancelTxtRt.anchorMax = Vector2.one;
            cancelTxtRt.offsetMin = Vector2.zero;
            cancelTxtRt.offsetMax = Vector2.zero;
            var cancelTxt = cancelTxtObj.AddComponent<Text>();
            cancelTxt.font = text.font;
            cancelTxt.fontSize = 10;
            cancelTxt.fontStyle = FontStyle.Bold;
            cancelTxt.color = Color.white;
            cancelTxt.text = "Cancel";
            cancelTxt.alignment = TextAnchor.MiddleCenter;

            cancelBtn.onClick.AddListener(() =>
            {
                Destroy(popup);
            });
        }

        private void ShowWarningDialog(string message)
        {
            var popup = new GameObject("QuestPopup");
            popup.transform.SetParent(transform, false);

            var rt = popup.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320, 160);

            var bgImg = popup.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);

            var outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.5f, 0.15f, 0.9f);
            outline.effectDistance = new Vector2(2, -2);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(popup.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0, 0.4f);
            textRt.anchorMax = new Vector2(1, 1);
            textRt.offsetMin = new Vector2(15, 15);
            textRt.offsetMax = new Vector2(-15, -15);

            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 11;
            text.color = Color.white;
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btnsObj = new GameObject("Buttons");
            btnsObj.transform.SetParent(popup.transform, false);
            var btnsRt = btnsObj.AddComponent<RectTransform>();
            btnsRt.anchorMin = new Vector2(0, 0);
            btnsRt.anchorMax = new Vector2(1, 0.4f);
            btnsRt.offsetMin = new Vector2(15, 10);
            btnsRt.offsetMax = new Vector2(-15, -10);

            var hGroup = btnsObj.AddComponent<HorizontalLayoutGroup>();
            hGroup.childAlignment = TextAnchor.MiddleCenter;
            hGroup.childControlHeight = true;
            hGroup.childControlWidth = true;

            var okBtnObj = new GameObject("OkButton");
            okBtnObj.transform.SetParent(btnsObj.transform, false);
            
            var okLayout = okBtnObj.AddComponent<LayoutElement>();
            okLayout.preferredWidth = 100f;
            okLayout.preferredHeight = 24f;

            var okImg = okBtnObj.AddComponent<Image>();
            okImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            var okBtn = okBtnObj.AddComponent<Button>();
            var okTxtObj = new GameObject("Text");
            okTxtObj.transform.SetParent(okBtnObj.transform, false);
            var okTxtRt = okTxtObj.AddComponent<RectTransform>();
            okTxtRt.anchorMin = Vector2.zero;
            okTxtRt.anchorMax = Vector2.one;
            okTxtRt.offsetMin = Vector2.zero;
            okTxtRt.offsetMax = Vector2.zero;
            var okTxt = okTxtObj.AddComponent<Text>();
            okTxt.font = text.font;
            okTxt.fontSize = 10;
            okTxt.fontStyle = FontStyle.Bold;
            okTxt.color = Color.white;
            okTxt.text = "OK";
            okTxt.alignment = TextAnchor.MiddleCenter;

            okBtn.onClick.AddListener(() =>
            {
                Destroy(popup);
            });
        }

        private static readonly HashSet<int> _karusOnlyQuests = new() { 62 };
        private static readonly HashSet<int> _elMoradOnlyQuests = new() { 61 };

        private bool IsQuestForOtherNation(int questId, byte playerNation)
        {
            if (_karusOnlyQuests.Contains(questId) && playerNation != 1)
                return true;
            if (_elMoradOnlyQuests.Contains(questId) && playerNation != 2)
                return true;

            return false;
        }
    }

    public class QuestSlotInfo
    {
        public int ZoneId;
        public string SlotName = "";
        public Vector3 Position;
    }
}
