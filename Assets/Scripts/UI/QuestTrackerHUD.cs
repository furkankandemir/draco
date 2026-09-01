using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using KOImport;
using System.Collections.Generic;

namespace EntropyOnline.UI
{
    /// <summary>
    /// HUD Quest Tracker - HP/MP state barının altında yer alır ve aktif görevleri listeler.
    /// Tıklandığında yan detay panelini (son 2 görevi) açar/kapatır.
    /// </summary>
    public class QuestTrackerHUD : MonoBehaviour
    {
        public static QuestTrackerHUD Instance { get; private set; }

        private GameObject _container;
        private Text _headerText;
        private Image _iconImg;
        private GameObject _bgPanel;
        private RectTransform _rectTransform;

        // Side Details Panel
        private GameObject _sidePanel;
        private readonly List<GameObject> _sidePanelRows = new();

        private struct QuestItemRequirement
        {
            public int ItemDefId;
            public int Count;
        }

        // Copy of item collect quest requirements from QuestJournalUI
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
            { 30, new List<QuestItemRequirement> { new() { ItemDefId = 379048000, Count = 10 } } }, // Silk bundle (Worm)
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

        // Copy of slot positions from QuestJournalUI
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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _rectTransform = GetComponent<RectTransform>();
            BuildUI();
        }

        private void Start()
        {
            QuestDialogUI.OnQuestStatesChanged += RefreshTracker;
            RefreshTracker();
        }

        private void OnDestroy()
        {
            QuestDialogUI.OnQuestStatesChanged -= RefreshTracker;
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            if (_rectTransform == null)
                _rectTransform = gameObject.AddComponent<RectTransform>();

            // Default positioning below top-left vitals
            _rectTransform.anchorMin = new Vector2(0, 1);
            _rectTransform.anchorMax = new Vector2(0, 1);
            _rectTransform.pivot = new Vector2(0, 1);
            _rectTransform.sizeDelta = new Vector2(30f, 80f); // Enlarged vertically to 80f

            // Background Panel (Main Button)
            _bgPanel = new GameObject("BgPanel");
            _bgPanel.transform.SetParent(transform, false);
            var bgRt = _bgPanel.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var bgImg = _bgPanel.AddComponent<Image>();
            
            // Set the background using KOUIManager's GetSkillThemeRoundedRectSprite
            if (KOUIManager.Instance != null)
            {
                bgImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    "quest_tracker_hud_bg_v3", 30, 80, 3,
                    new Color(0.12f, 0.10f, 0.08f, 0.9f), 
                    new Color(0.6f, 0.48f, 0.22f, 0.9f), 1
                );
            }
            else
            {
                bgImg.color = new Color(0.12f, 0.10f, 0.08f, 0.9f);
                var outline = _bgPanel.AddComponent<Outline>();
                outline.effectColor = new Color(0.6f, 0.48f, 0.22f, 0.9f);
                outline.effectDistance = new Vector2(1, -1);
            }

            // Click interaction button overlay (Toggles the Side details panel instead of QuestJournal)
            var btn = _bgPanel.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (_sidePanel != null)
                {
                    bool show = !_sidePanel.activeSelf;
                    _sidePanel.SetActive(show);
                    if (show)
                    {
                        RefreshTracker();
                    }
                }
            });

            // Container for inner elements
            _container = new GameObject("Container");
            _container.transform.SetParent(_bgPanel.transform, false);
            var containerRt = _container.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;

            // Icon element (scroll-quill)
            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(_container.transform, false);
            var iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0, -12f); // 12px padding from top
            iconRt.sizeDelta = new Vector2(24f, 24f);      // Icon size

            _iconImg = iconObj.AddComponent<Image>();
            _iconImg.raycastTarget = false;
            
            var tex = Resources.Load<Texture2D>("UI/scroll-quill");
            if (tex != null)
            {
                _iconImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _iconImg.color = new Color(0.82f, 0.68f, 0.38f, 1f); // Gold-Bronze tint
            }
            else
            {
                _iconImg.color = new Color(0.82f, 0.68f, 0.38f, 0.8f);
            }

            // Header text (Active Quest Count in Parentheses)
            var headerObj = new GameObject("Header");
            headerObj.transform.SetParent(_container.transform, false);
            var headerRt = headerObj.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 0f);
            headerRt.anchorMax = new Vector2(0.5f, 0f);
            headerRt.pivot = new Vector2(0.5f, 0f);
            headerRt.anchoredPosition = new Vector2(0, 10f); // 10px padding from bottom
            headerRt.sizeDelta = new Vector2(30f, 18f);

            _headerText = headerObj.AddComponent<Text>();
            _headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_headerText.font == null)
                _headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _headerText.fontSize = 11;
            _headerText.fontStyle = FontStyle.Bold;
            _headerText.color = new Color(0.95f, 0.85f, 0.35f, 1f); // KO gold
            _headerText.text = "(0)";
            _headerText.alignment = TextAnchor.MiddleCenter;
            _headerText.raycastTarget = false;

            // Initialize Side Panel (Collapsible to the right of the button)
            _sidePanel = new GameObject("SidePanel");
            _sidePanel.transform.SetParent(transform, false);
            var sideRt = _sidePanel.AddComponent<RectTransform>();
            sideRt.anchorMin = new Vector2(1f, 1f);
            sideRt.anchorMax = new Vector2(1f, 1f);
            sideRt.pivot = new Vector2(0f, 1f);
            sideRt.anchoredPosition = new Vector2(0f, 0f); // Spacing adjusted to 0px (moved 10px to the left)
            sideRt.sizeDelta = new Vector2(220f, 88f);      // Expanded size

            var sideImg = _sidePanel.AddComponent<Image>();
            sideImg.color = new Color(0.08f, 0.08f, 0.1f, 0.8f); // Yarı saydam koyu zemin

            // Vertical Layout Group for side panel rows
            var sideVlg = _sidePanel.AddComponent<VerticalLayoutGroup>();
            sideVlg.spacing = 6f;
            sideVlg.padding = new RectOffset(6, 6, 6, 6);
            sideVlg.childAlignment = TextAnchor.UpperLeft;
            sideVlg.childControlHeight = false;
            sideVlg.childControlWidth = false;
            sideVlg.childForceExpandHeight = false;
            sideVlg.childForceExpandWidth = false;

            _sidePanel.SetActive(false); // Collapsed at startup
        }

        public void RefreshTracker()
        {
            var states = QuestDialogUI.Instance != null ? QuestDialogUI.Instance.GetQuestStates() : null;
            int activeCount = 0;

            var activeQuestIds = new List<int>();
            if (states != null)
            {
                foreach (var kvp in states)
                {
                    if (kvp.Value == 1) // 1 = Active / In Progress
                    {
                        activeCount++;
                        activeQuestIds.Add(kvp.Key);
                    }
                }
            }

            _headerText.text = $"({activeCount})"; // Count in parentheses: (X)
            _rectTransform.sizeDelta = new Vector2(30f, 80f);

            // Rebuild side panel rows if active
            if (_sidePanel != null && _sidePanel.activeSelf)
            {
                // Clear existing
                foreach (var row in _sidePanelRows)
                {
                    if (row != null) Destroy(row);
                }
                _sidePanelRows.Clear();

                activeQuestIds.Sort(); // Ascending Order

                // Take last 2 active quests (preserving ascending order for layout)
                var lastQuests = new List<int>();
                int totalActive = activeQuestIds.Count;
                if (totalActive >= 2) lastQuests.Add(activeQuestIds[totalActive - 2]);
                if (totalActive >= 1) lastQuests.Add(activeQuestIds[totalActive - 1]);

                var allContent = QuestTableParser.GetAllContent();

                if (lastQuests.Count == 0)
                {
                    // No active quests label
                    var emptyRow = new GameObject("EmptyRow");
                    emptyRow.transform.SetParent(_sidePanel.transform, false);
                    var emptyRt = emptyRow.AddComponent<RectTransform>();
                    emptyRt.sizeDelta = new Vector2(208f, 20f);
                    _sidePanelRows.Add(emptyRow);

                    var txt = emptyRow.AddComponent<Text>();
                    txt.font = _headerText.font;
                    txt.fontSize = 11;
                    txt.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    txt.text = "No active quests.";
                    txt.alignment = TextAnchor.MiddleLeft;

                    UpdateSidePanelHeight(32f);
                }
                else
                {
                    foreach (int questId in lastQuests)
                    {
                        if (allContent != null && allContent.TryGetValue(questId, out var quest))
                        {
                            byte qState = QuestDialogUI.Instance.GetQuestState((short)questId);

                            var rowObj = new GameObject($"QuestRow_{questId}");
                            rowObj.transform.SetParent(_sidePanel.transform, false);
                            var rowRt = rowObj.AddComponent<RectTransform>();
                            rowRt.sizeDelta = new Vector2(208f, 36f); // Size matching row content
                            _sidePanelRows.Add(rowObj);

                            // Left side Name and Description Text
                            var txtObj = new GameObject("Text");
                            txtObj.transform.SetParent(rowObj.transform, false);
                            var txtRt = txtObj.AddComponent<RectTransform>();
                            txtRt.anchorMin = new Vector2(0f, 0.5f);
                            txtRt.anchorMax = new Vector2(0f, 0.5f);
                            txtRt.pivot = new Vector2(0f, 0.5f);
                            txtRt.anchoredPosition = new Vector2(0f, 0f);
                            txtRt.sizeDelta = new Vector2(178f, 36f);

                            var txt = txtObj.AddComponent<Text>();
                            txt.font = _headerText.font;
                            txt.fontSize = 10;
                            txt.supportRichText = true;
                            txt.lineSpacing = 1.1f;
                            txt.alignment = TextAnchor.MiddleLeft;
                            txt.raycastTarget = true; // raycast target enabled for text

                            string formattedDesc = FormatQuestDescription(questId, quest.Description, qState);
                            txt.text = $"<b><color=#FFFFFF>{quest.Name}</color></b>\n{formattedDesc}";

                            // Make the text clickable to open the Quest Journal and expand the quest
                            var txtBtn = txtObj.AddComponent<Button>();
                            txtBtn.transition = Selectable.Transition.None;
                            int targetQuestId = questId;
                            txtBtn.onClick.AddListener(() =>
                            {
                                if (KOUIManager.Instance != null)
                                {
                                    KOUIManager.Instance.OpenQuestJournalWithQuest(targetQuestId);
                                }
                            });

                            // Right side Teleport (Vortex) button
                            var tpBtnObj = new GameObject("TeleportButton");
                            tpBtnObj.transform.SetParent(rowObj.transform, false);
                            var tpRt = tpBtnObj.AddComponent<RectTransform>();
                            tpRt.anchorMin = new Vector2(1f, 0.5f);
                            tpRt.anchorMax = new Vector2(1f, 0.5f);
                            tpRt.pivot = new Vector2(1f, 0.5f);
                            tpRt.anchoredPosition = new Vector2(0f, 0f);
                            tpRt.sizeDelta = new Vector2(24f, 24f);

                            var tpImg = tpBtnObj.AddComponent<Image>();
                            var tpTex = Resources.Load<Texture2D>("UI/quest-teleport");
                            if (tpTex != null)
                            {
                                tpImg.sprite = Sprite.Create(tpTex, new Rect(0, 0, tpTex.width, tpTex.height), new Vector2(0.5f, 0.5f));
                            }
                            tpImg.color = Color.white;

                            var tpBtn = tpBtnObj.AddComponent<Button>();
                            tpBtn.transition = Selectable.Transition.ColorTint;
                            
                            string qName = quest.Name;
                            tpBtn.onClick.AddListener(() =>
                            {
                                TryTeleportToQuest(questId, qName);
                            });
                        }
                    }

                    // Dynamically set height depending on row count
                    float targetHeight = lastQuests.Count == 1 ? 48f : 86f;
                    UpdateSidePanelHeight(targetHeight);
                }
            }
        }

        private void UpdateSidePanelHeight(float height)
        {
            if (_sidePanel == null) return;
            var sideRt = _sidePanel.GetComponent<RectTransform>();
            sideRt.sizeDelta = new Vector2(220f, height);

            var sideImg = _sidePanel.GetComponent<Image>();
            if (sideImg != null && KOUIManager.Instance != null)
            {
                // Dynamic border outline matching the height
                sideImg.sprite = KOUIManager.Instance.GetSkillThemeRoundedRectSprite(
                    $"quest_side_bg_{(int)height}", 220, (int)height, 3,
                    new Color(0.12f, 0.10f, 0.08f, 0.8f),
                    new Color(0.6f, 0.48f, 0.22f, 0.5f), 1
                );
            }
        }

        /// <summary>
        /// Description string contains "X/Y" placeholders which are updated dynamically.
        /// </summary>
        private string FormatQuestDescription(int questId, string description, int state)
        {
            if (string.IsNullOrEmpty(description)) return "";

            // Matches patterns like "0/15" or "3/10"
            var matches = System.Text.RegularExpressions.Regex.Matches(description, @"(\d+)\s*/\s*(\d+)");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string fullMatch = match.Value;
                    int targetCount = int.Parse(match.Groups[2].Value);
                    int currentCount = 0;

                    if (_questItemRequirements.TryGetValue(questId, out var reqs))
                    {
                        // Item collection quest: count target items in player inventory
                        var inventory = GameManager.Instance?.Inventory;
                        if (inventory != null)
                        {
                            foreach (var req in reqs)
                            {
                                if (req.Count == targetCount)
                                {
                                    foreach (var item in inventory)
                                    {
                                        if (item != null && item.ItemDefId == req.ItemDefId)
                                        {
                                            currentCount += item.StackCount;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Hunt (kill count) quest: kills are stored in quest state byte (kills = state - 1)
                        currentCount = state > 0 ? (state - 1) : 0;
                        currentCount = Mathf.Min(currentCount, targetCount);
                    }

                    // Replace original "X/Y" with styled green rich text fraction
                    string replacement = $"<color=#40E040>{currentCount}/{targetCount}</color>";
                    description = description.Replace(fullMatch, replacement);
                }
            }

            return description;
        }

        private void TryTeleportToQuest(int questId, string questName)
        {
            if (_questSlots.TryGetValue(questId, out var slotInfo) && slotInfo != null)
            {
                short currentZone = GameManager.Instance != null ? GameManager.Instance.CurrentZoneId : (short)0;
                int targetZone = slotInfo.ZoneId;
                Vector3 targetPos = slotInfo.Position;

                // Handle mirrored nation map IDs (1 Luferson Castle / Karus, 2 El Morad Castle)
                if ((targetZone == 1 && currentZone == 2) || (targetZone == 2 && currentZone == 1))
                {
                    targetZone = currentZone;
                    if (questId == 8) // Recoinnassaince Report
                    {
                        targetPos = currentZone == 1 ? new Vector3(815f, 0f, 1802f) : new Vector3(1486f, 0f, 957f);
                    }
                    else if (questId == 58) // Warrior Master Skaky / Priest Minerva
                    {
                        targetPos = currentZone == 1 ? new Vector3(380f, 0f, 1745f) : new Vector3(1657f, 0f, 325f);
                    }
                    else if (questId == 59) // Secret Agent Clarence
                    {
                        targetPos = currentZone == 1 ? new Vector3(431f, 0f, 708f) : new Vector3(1631f, 0f, 1333f);
                    }
                    else if (questId == 60) // Arch Mage Drake
                    {
                        targetPos = currentZone == 1 ? new Vector3(1695f, 0f, 805f) : new Vector3(372f, 0f, 1225f);
                    }
                }

                if (targetZone == currentZone)
                {
                    ShowConfirmDialog($"You will be teleported to the {slotInfo.SlotName} slot. Are you sure?", () =>
                    {
                        SendRemoteQuestTeleportPacket((ushort)questId, targetPos.x, targetPos.z);
                    });
                }
                else
                {
                    ShowWarningDialog("You are in the wrong zone. You cannot teleport to this slot.");
                }
            }
            else
            {
                ShowWarningDialog("Teleport coordinates for this quest are not configured.");
            }
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
            popup.transform.SetParent(transform.parent, false);

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
            text.font = _headerText.font;
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
            popup.transform.SetParent(transform.parent, false);

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
            text.font = _headerText.font;
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
    }
}
