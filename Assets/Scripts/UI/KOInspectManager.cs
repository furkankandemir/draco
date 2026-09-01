using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using EntropyOnline.Network.KO;
using EntropyOnline.Network;
using EntropyOnline.Core;
using EntropyOnline.Import;
using KOImport;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Oyuncu İnceleme (Inspect Player Info) yöneticisi.
    /// Başka bir oyuncunun envanter ve stat bilgilerini gösteren salt okunur panelleri yönetir.
    /// </summary>
    public class KOInspectManager : MonoBehaviour
    {
        public static KOInspectManager Instance { get; private set; }

        [Header("Prefabs & UI")]
        private GameObject _inspectVariousPanel;
        private GameObject _inspectInventoryPanel;
        private GameObject _inspectContainer;

        public bool IsInspectActive => _inspectInventoryPanel != null && _inspectInventoryPanel.activeSelf;
        public GameObject InspectInventoryPanel => _inspectInventoryPanel;
        public GameObject InspectVariousPanel => _inspectVariousPanel;

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
            KOPacketHandler.OnInspectData += HandleInspectData;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnInspectData -= HandleInspectData;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Sunucuya oyuncu inceleme isteği gönderir.
        /// </summary>
        public void RequestInspect(long targetSocketId)
        {
            if (KONetworkManager.Instance == null || !KONetworkManager.Instance.IsConnected) return;

            // Custom packet: WIZ_INSPECT (0xE1), sub-opcode: 1 (Request)
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_INSPECT);
            pkt.WriteInt16((short)targetSocketId);
            KONetworkManager.Instance.SendPacket(pkt);

        }

        /// <summary>
        /// Sunucudan gelen inceleme paketi verisini işler.
        /// </summary>
        private void HandleInspectData(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            // Opcode ve targetSocketId okundu (Opcode constructor içinde okunur)
            short targetSocketId = r.ReadInt16();
            byte result = r.ReadByte();

            if (result == 0)
            {
                Debug.LogWarning($"[INSPECT] Inspect failed on server for socketId: {targetSocketId}");
                KOUIManager.Instance?.ShowToast("Failed to inspect player.");
                return;
            }

            // Inspect data model parsing
            string charName = r.ReadKOString1();
            byte nation = r.ReadByte();
            byte race = r.ReadByte();
            ushort charClass = r.ReadUInt16();
            byte level = r.ReadByte();

            short maxHp = r.ReadInt16();
            short curHp = r.ReadInt16();
            short maxMp = r.ReadInt16();
            short curMp = r.ReadInt16();

            byte baseStr = r.ReadByte();
            byte itemStr = r.ReadByte();
            byte baseSta = r.ReadByte();
            byte itemSta = r.ReadByte();
            byte baseDex = r.ReadByte();
            byte itemDex = r.ReadByte();
            byte baseInt = r.ReadByte();
            byte itemInt = r.ReadByte();
            byte baseCha = r.ReadByte();
            byte itemCha = r.ReadByte();

            short totalHit = r.ReadInt16();
            short totalAc = r.ReadInt16();

            byte fireR = r.ReadByte();
            byte coldR = r.ReadByte();
            byte lightningR = r.ReadByte();
            byte magicR = r.ReadByte();
            byte diseaseR = r.ReadByte();
            byte poisonR = r.ReadByte();

            // Skill points (4 bytes)
            byte skill1 = r.ReadByte();
            byte skill2 = r.ReadByte();
            byte skill3 = r.ReadByte();
            byte skill4 = r.ReadByte();

            // War points (4 bytes)
            int warPoints = r.ReadInt32();

            string clanName = r.ReadKOString1();

            // Equipped items
            var equippedItems = new List<InventoryItemData>();
            for (int i = 0; i < 14; i++)
            {
                int itemId = (int)r.ReadUInt32();
                short count = r.ReadInt16();
                short durability = r.ReadInt16();

                if (itemId > 0)
                {
                    equippedItems.Add(CreateItemData(itemId, count, durability, 1, (byte)i));
                }
            }

            // Bag items
            var bagItems = new List<InventoryItemData>();
            for (int i = 0; i < 28; i++)
            {
                int itemId = (int)r.ReadUInt32();
                short count = r.ReadInt16();
                short durability = r.ReadInt16();

                if (itemId > 0)
                {
                    bagItems.Add(CreateItemData(itemId, count, durability, 0, (byte)i));
                }
            }


            // Open/Build UI panels
            OpenInspectPanels(nation, charName, race, charClass, level, maxHp, curHp, maxMp, curMp,
                baseStr, itemStr, baseSta, itemSta, baseDex, itemDex, baseInt, itemInt, baseCha, itemCha,
                totalHit, totalAc, fireR, coldR, lightningR, magicR, diseaseR, poisonR,
                skill1, skill2, skill3, skill4, warPoints, clanName,
                equippedItems, bagItems);
        }

        private InventoryItemData CreateItemData(int itemId, short count, short durability, byte slotType, byte slotIndex)
        {
            var item = new InventoryItemData
            {
                InstanceId = itemId, // Custom inspect data uses itemId as mock instanceId
                ItemDefId = itemId,
                SlotType = slotType,
                SlotIndex = slotIndex,
                StackCount = count > 0 ? count : (short)1,
                Durability = durability
            };

            // Basic item detail lookup
            var pItem = ItemDataManager.GetItemBasic(itemId);
            if (pItem != null)
            {
                item.Name = pItem.SzName;
                item.AttachPoint = pItem.ByAttachPoint;
                item.Type = pItem.ByClass;
                item.SubType = 0;
                item.IconId = pItem.DwIDIcon.ToString();
                item.Countable = pItem.ByContable;
            }
            return item;
        }

        private void OpenInspectPanels(byte nation, string charName, byte race, ushort charClass, byte level,
            short maxHp, short curHp, short maxMp, short curMp,
            byte baseStr, byte itemStr, byte baseSta, byte itemSta, byte baseDex, byte itemDex, byte baseInt, byte itemInt, byte baseCha, byte itemCha,
            short totalHit, short totalAc, byte fireR, byte coldR, byte lightningR, byte magicR, byte diseaseR, byte poisonR,
            byte skill1, byte skill2, byte skill3, byte skill4, int warPoints, string clanName,
            List<InventoryItemData> equippedItems, List<InventoryItemData> bagItems)
        {
             // Close other active panels first
             if (KOUIManager.Instance != null)
             {
                 KOUIManager.Instance.CloseAllActivePanelsForInspect();
             }

             // Close any existing inspect windows first
             CloseInspectWindows();

             if (KOUIManager.Instance == null || KOUIManager.Instance.Canvas == null) return;
             Transform canvasTransform = KOUIManager.Instance.Canvas.transform;

             // Load appropriate prefabs based on nation (1: Karus, 2: El Morad)
             string prefix = (nation == 1) ? "ka_" : "el_";
             string inventoryPrefabName = prefix + "inventory_us";
             string variousPrefabName = prefix + "various_all_us";

             // Create a single container GameObject to group and scale both panels together natively
             _inspectContainer = new GameObject("Inspect_Container", typeof(RectTransform));
             _inspectContainer.transform.SetParent(canvasTransform, false);
             _inspectContainer.SetActive(false); // Keep inactive during population to prevent frame jumps
             _inspectContainer.AddComponent<KOUIScaleIndependent>();

             var slideContainer = _inspectContainer.AddComponent<EntropyOnline.UI.KOUIPanelSlideIn>();
             slideContainer.TargetX = -50f;
             slideContainer.StartX = 600f; // Start offscreen on the right
             slideContainer.Duration = 0.2f;

             var rtContainer = _inspectContainer.GetComponent<RectTransform>();
             rtContainer.anchorMin = new Vector2(1f, 0.5f);
             rtContainer.anchorMax = new Vector2(1f, 0.5f);
             rtContainer.pivot = new Vector2(1f, 0.5f);
             rtContainer.anchoredPosition = new Vector2(-50f, 0f);

             // Load right panel prefab
             var invPrefab = Resources.Load<GameObject>($"ModernUI/{inventoryPrefabName}");
             if (invPrefab == null)
             {
                 Destroy(_inspectContainer);
                 _inspectContainer = null;
                 return;
             }

             // Load left panel prefab
             var varPrefab = Resources.Load<GameObject>($"ModernUI/{variousPrefabName}");
             if (varPrefab == null)
             {
                 Destroy(_inspectContainer);
                 _inspectContainer = null;
                 return;
             }

             // Instantiate both as children of _inspectContainer
            _inspectInventoryPanel = Instantiate(invPrefab, _inspectContainer.transform);
            _inspectInventoryPanel.name = "Inspect_Inventory";

            _inspectVariousPanel = Instantiate(varPrefab, _inspectContainer.transform);
            _inspectVariousPanel.name = "Inspect_Various";

            // Destroy transition slide components to prevent slide jumps
            var slideInv = _inspectInventoryPanel.GetComponent<KOUIPanelSlideIn>();
            if (slideInv != null) DestroyImmediate(slideInv);

            var slideVar = _inspectVariousPanel.GetComponent<KOUIPanelSlideIn>();
            if (slideVar != null) DestroyImmediate(slideVar);

            // Destroy KOUIScaleIndependent on both child prefabs so they don't scale themselves and separate
            var scaleInv = _inspectInventoryPanel.GetComponent<KOUIScaleIndependent>();
            if (scaleInv != null) DestroyImmediate(scaleInv);

            var scaleVar = _inspectVariousPanel.GetComponent<KOUIScaleIndependent>();
            if (scaleVar != null) DestroyImmediate(scaleVar);

            // 1. Populate Left Panel (Character Detail / Various page_state) - This dynamically sets sizes (e.g. Various to 322px width)
            ConfigureVariousPanel(charName, race, charClass, level, maxHp, curHp, maxMp, curMp,
                baseStr, itemStr, baseSta, itemSta, baseDex, itemDex, baseInt, itemInt, baseCha, itemCha,
                totalHit, totalAc, fireR, coldR, lightningR, magicR, diseaseR, poisonR,
                skill1, skill2, skill3, skill4, warPoints, clanName);

            // 2. Populate Right Panel (Inventory slots)
            ConfigureInventoryPanel(equippedItems, bagItems, charName);

            // 3. Position Inventory and Various panels side-by-side using the resolved dimensions
            var rtRight = _inspectInventoryPanel.GetComponent<RectTransform>();
            float rightWidth = rtRight.rect.width;
            if (rightWidth <= 0f) rightWidth = rtRight.sizeDelta.x;
            if (rightWidth <= 0f) rightWidth = 280f;

            var rtLeft = _inspectVariousPanel.GetComponent<RectTransform>();
            float leftWidth = 272f; // Shorten Various panel width horizontally from 322f to 272f

            // Shorten left panel to 444f and offset Y center by -33f to align perfectly at the bottom with inventory
            rtLeft.sizeDelta = new Vector2(leftWidth, 444f);

            float overlap = 8f; // 8px overlap closes the visual gap of the border shadow so they touch perfectly

            // Various (left panel) right-aligned to leftWidth inside container
            rtLeft.anchorMin = new Vector2(0f, 0.5f);
            rtLeft.anchorMax = new Vector2(0f, 0.5f);
            rtLeft.pivot = new Vector2(1f, 0.5f);
            rtLeft.anchoredPosition = new Vector2(leftWidth, -33f);

            // Inventory (right panel) right-aligned to total width (with overlap)
            // Vertically center it exactly relative to the left panel (which is centered at Y = -33f on screen)
            rtRight.anchorMin = new Vector2(0f, 0.5f);
            rtRight.anchorMax = new Vector2(0f, 0.5f);
            rtRight.pivot = new Vector2(1f, 0.5f);
            rtRight.anchoredPosition = new Vector2(leftWidth + rightWidth - overlap, -33f);

            // Set container size to natively fit both panels side-by-side with overlap
            float totalWidth = leftWidth + rightWidth - overlap;
            float maxPanelHeight = Mathf.Max(rtLeft.rect.height, rtRight.rect.height);
            if (maxPanelHeight <= 0f) maxPanelHeight = Mathf.Max(rtLeft.sizeDelta.y, rtRight.sizeDelta.y);
            if (maxPanelHeight <= 0f) maxPanelHeight = 510f; // Fallback to 510f (resolved height of various panel)
            rtContainer.sizeDelta = new Vector2(totalWidth, maxPanelHeight);

            // Arrange sibling order inside container so Various panel is drawn first (below Inventory border)
            rtLeft.SetAsFirstSibling();
            rtRight.SetAsLastSibling();

            // Bring parent container to the front of the Canvas
            _inspectContainer.transform.SetAsLastSibling();

            // 3. Connect Close Buttons
            BindCloseButtons();

            // Now activate the container to trigger the slide-in animation!
            _inspectContainer.SetActive(true);

             // Reposition HUD skillbar & adjust top-right canvas layering priority
             if (KOUIManager.Instance != null)
             {
                 KOUIManager.Instance.RepositionSkillBarForPanel(false);
             }
        }



        private void ConfigureVariousPanel(string charName, byte race, ushort charClass, byte level,
            short maxHp, short curHp, short maxMp, short curMp,
            byte baseStr, byte itemStr, byte baseSta, byte itemSta, byte baseDex, byte itemDex, byte baseInt, byte itemInt, byte baseCha, byte itemCha,
            short totalHit, short totalAc, byte fireR, byte coldR, byte lightningR, byte magicR, byte diseaseR, byte poisonR,
            byte skill1, byte skill2, byte skill3, byte skill4, int warPoints, string clanName)
        {
            if (_inspectVariousPanel == null) return;
            Transform root = _inspectVariousPanel.transform;

            // Apply modern UI themes & styles to remove legacy white background
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.ApplyVariousUITheme(root);
                KOUIManager.Instance.ModernizeVariousTexts(root);
            }

            // Hide tab headers to make it look like a dedicated Character Detail panel
            var btnState = KOUIRenderer.FindChildButton(root, "btn_state");
            if (btnState != null) btnState.gameObject.SetActive(false);
            var btnClan = KOUIRenderer.FindChildButton(root, "btn_clan");
            if (btnClan != null) btnClan.gameObject.SetActive(false);
            var btnFriends = KOUIRenderer.FindChildButton(root, "btn_friends");
            if (btnFriends != null) btnFriends.gameObject.SetActive(false);

            // Deactivate pages except page_state
            var pageIds = new string[] { "page_state", "page_quest", "page_knights", "page_friend", "page_clan" };
            foreach (var pid in pageIds)
            {
                var pageTr = KOUIRenderer.FindChildByID(root, pid);
                if (pageTr != null)
                {
                    pageTr.gameObject.SetActive(pid == "page_state");
                }
            }

            var pageStateTr = KOUIRenderer.FindChildByID(root, "page_state");
            if (pageStateTr == null) return;

            Transform psRoot = pageStateTr;

            // Clean labels just like standard KOUIManager.RefreshPageState does
            var labelMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Level", "Level" }, { "Level:", "Level" },
                { "Class", "Job" }, { "Class:", "Job" },
                { "Hit Point", "HP Point" }, { "Hit Point:", "HP Point" },
                { "Mana Point", "MP Point" }, { "Mana Point:", "MP Point" },
                { "Experience Points", "Exp Points" }, { "Experience Points:", "Exp Points" },
                { "National Point", "War Points" }, { "National Point:", "War Points" },
                { "Attack", "Attack" }, { "Attack:", "Attack" },
                { "Defense", "Defence" }, { "Defense:", "Defence" },
                { "Flame Attack", "Flame Resist" }, { "Flame Attack:", "Flame Resist" },
                { "Glacier Attack", "Glacier Resist" }, { "Glacier Attack:", "Glacier Resist" },
                { "Lightning Attack", "Lightning Resist" }, { "Lightning Attack:", "Lightning Resist" },
                { "Magic Attack", "Magic Resist" }, { "Magic Attack:", "Magic Resist" },
                { "Curse Attack", "Curse Resist" }, { "Curse Attack:", "Curse Resist" },
                { "Poison Attacks", "Poison Resist" }, { "Poison Attacks:", "Poison Resist" }
            };

            var allTexts = psRoot.GetComponentsInChildren<Text>(true);
            foreach (var txt in allTexts)
            {
                string tClean = txt.text.Trim();
                if (labelMap.TryGetValue(tClean, out string newLabel))
                {
                    txt.text = newLabel;
                }
                else if (tClean.EndsWith(":") && labelMap.TryGetValue(tClean.Substring(0, tClean.Length - 1).Trim(), out string newLabelColon))
                {
                    txt.text = newLabelColon;
                }
            }

            // Hide Manner & Weight elements
            var txtWeightVal = KOUIRenderer.FindChildByID(psRoot, "Text_Weight");
            if (txtWeightVal != null) txtWeightVal.gameObject.SetActive(false);
            var txtMannerVal = KOUIRenderer.FindChildByID(psRoot, "Text_Manner");
            if (txtMannerVal != null) txtMannerVal.gameObject.SetActive(false);
            foreach (var txt in allTexts)
            {
                if (txt.text == "Weight" || txt.text == "Manner")
                {
                    txt.gameObject.SetActive(false);
                }
            }

            // Disable stat-up buttons
            var btnNames = new string[] { "btn_strength", "btn_stamina", "btn_dexterity", "btn_intelligence", "btn_MagicAttack" };
            foreach (var bname in btnNames)
            {
                var bTr = KOUIRenderer.FindChildByID(psRoot, bname);
                if (bTr != null) bTr.gameObject.SetActive(false);
            }

            var pointsPanel = KOUIRenderer.FindChildByID(psRoot, "Panel_BonusPoint");
            if (pointsPanel != null) pointsPanel.gameObject.SetActive(false);

            // Populate text values
            var trCulture = new System.Globalization.CultureInfo("tr-TR");

            var textIdTr = FindChildByIDIgnoreCase(root, "text_Id") ?? FindChildByIDIgnoreCase(psRoot, "text_Id");
            if (textIdTr != null)
            {
                var txt = textIdTr.GetComponent<Text>();
                if (txt != null) txt.text = charName;
            }

            SetText(psRoot, "Text_Level", level.ToString());
            SetText(psRoot, "Text_Class", GetBaseClassText((byte)charClass));
            SetText(psRoot, "Text_Race", KOTextHelper.GetTextByRace(race));
            SetText(psRoot, "Text_HP", $"{curHp.ToString("N0", trCulture)} / {maxHp.ToString("N0", trCulture)}");
            SetText(psRoot, "Text_MP", $"{curMp.ToString("N0", trCulture)} / {maxMp.ToString("N0", trCulture)}");
            SetText(psRoot, "Text_Exp", "---"); // Exp is hidden for other players
            SetText(psRoot, "Text_AP", totalHit.ToString("N0", trCulture));
            SetText(psRoot, "Text_GP", totalAc.ToString("N0", trCulture));

            SetText(psRoot, "Text_Strength", FormatWithDelta(baseStr, itemStr));
            SetText(psRoot, "Text_Stamina", FormatWithDelta(baseSta, itemSta));
            SetText(psRoot, "Text_Dexterity", FormatWithDelta(baseDex, itemDex));
            SetText(psRoot, "Text_Intelligence", FormatWithDelta(baseInt, itemInt));
            SetText(psRoot, "Text_MagicAttack", FormatWithDelta(baseCha, itemCha));

            SetText(psRoot, "Text_BonusPoint", "0");
            SetText(psRoot, "Text_RealmPoint", warPoints.ToString("N0", trCulture));

            // Resistances
            SetText(psRoot, "Text_RegistFire", fireR.ToString());
            SetText(psRoot, "Text_RegistIce", coldR.ToString());
            SetText(psRoot, "Text_RegistLight", lightningR.ToString());
            SetText(psRoot, "Text_RegistMagic", magicR.ToString());
            SetText(psRoot, "Text_RegistCurse", diseaseR.ToString());
            SetText(psRoot, "Text_RegistPoison", poisonR.ToString());

            // Call RearrangePageStateLayout to dynamically style/recolor the text field background boxes from white to dark translucent
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.RearrangePageStateLayout(psRoot);
            }

            // 1. Style top player name (text_Id) as [Character]'s Character Details header, and hide race/nation (Text_Race, Text_Nation)
            if (textIdTr != null)
            {
                textIdTr.SetParent(root, false); // Parent to main root so its position is absolute to the panel
                textIdTr.gameObject.SetActive(true);
                var txt = textIdTr.GetComponent<Text>();
                if (txt != null)
                {
                    txt.text = $"{charName}'s Character Details";
                    if (KOUIManager.Instance != null)
                    {
                        txt.font = KOUIManager.Instance.GetSafeFont(14);
                    }
                    txt.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Gold/Yellow matching SKILL PAGE
                    txt.fontStyle = FontStyle.Bold;
                    txt.fontSize = 14;
                    txt.alignment = TextAnchor.MiddleCenter;

                    // Add shadow matching SKILL PAGE header
                    if (txt.gameObject.GetComponent<Shadow>() == null)
                    {
                        var shadow = txt.gameObject.AddComponent<Shadow>();
                        shadow.effectColor = new Color(0, 0, 0, 0.85f);
                        shadow.effectDistance = new Vector2(1, -1);
                    }
                }

                var rt = textIdTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(250f, 30f);
                    rt.anchoredPosition = new Vector2(0f, -4f); // Exactly matching SKILL PAGE title position
                }
            }

            // Create title divider matching SKILL PAGE divider exactly
            var dividerObj = root.Find("SkillTitleDivider")?.gameObject;
            if (dividerObj == null)
            {
                dividerObj = new GameObject("SkillTitleDivider", typeof(RectTransform));
                dividerObj.transform.SetParent(root, false);
                var divRt = dividerObj.GetComponent<RectTransform>();
                if (divRt != null)
                {
                    divRt.anchorMin = new Vector2(0.5f, 1f);
                    divRt.anchorMax = new Vector2(0.5f, 1f);
                    divRt.pivot = new Vector2(0.5f, 1f);
                    divRt.sizeDelta = new Vector2(240f, 2f);
                    divRt.anchoredPosition = new Vector2(0f, -34f); // Under the title perfectly
                }
            }
            var dividerImg = dividerObj.GetComponent<Image>() ?? dividerObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                dividerImg.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite("inspect_title_divider", 240, 2, new Color(0.9f, 0.75f, 0.25f, 0.8f));
            }
            dividerImg.color = Color.white;

            var txtNation = KOUIRenderer.FindChildByID(psRoot, "Text_Nation");
            if (txtNation != null) txtNation.gameObject.SetActive(false);
            var txtRace = KOUIRenderer.FindChildByID(psRoot, "Text_Race");
            if (txtRace != null) txtRace.gameObject.SetActive(false);

            // 2. Hide HP, MP, and Exp fields + their backgrounds + their labels
            var hideFieldIds = new string[] { "Text_HP", "Text_MP", "Text_Exp" };
            foreach (var fid in hideFieldIds)
            {
                var fTr = KOUIRenderer.FindChildByID(psRoot, fid);
                if (fTr != null) fTr.gameObject.SetActive(false);

                var fBgTr = KOUIRenderer.FindChildByID(psRoot, fid + "_Bg");
                if (fBgTr != null) fBgTr.gameObject.SetActive(false);
            }

            // Hide labels by text search
            foreach (var txt in allTexts)
            {
                string t = txt.text.Trim();
                if (t == "HP Point" || t == "Hit Point" || t == "Hit Point:" ||
                    t == "MP Point" || t == "Mana Point" || t == "Mana Point:" ||
                    t == "Exp Points" || t == "Experience Points" || t == "Experience Points:" ||
                    t == "Resistance" || t == "Resistance:")
                {
                    txt.gameObject.SetActive(false);
                }
            }

            // Legacy Job positioning moved to Step 5 (Absolute layout manager)

            // 4. Hide Stat Preset and Stat Reset buttons + Stat Point capsule
            var btnList = psRoot.GetComponentsInChildren<Button>(true);
            foreach (var btn in btnList)
            {
                if (btn.name.Contains("preset") || btn.name.Contains("reset") || btn.name.Contains("Preset") || btn.name.Contains("Reset"))
                {
                    btn.gameObject.SetActive(false);
                }
            }
            foreach (var txt in allTexts)
            {
                string t = txt.text.Trim();
                if (t.Contains("Stat Point") || t.Contains("Stat Preset") || t.Contains("Stat Reset") || t.Contains("Preset") || t.Contains("Reset"))
                {
                    txt.gameObject.SetActive(false);
                }
            }

            var txtBonus = KOUIRenderer.FindChildByID(psRoot, "Text_BonusPoint");
            if (txtBonus != null) txtBonus.gameObject.SetActive(false);

            var bgBonus = KOUIRenderer.FindChildByID(psRoot, "Text_BonusPoint_Bg");
            if (bgBonus != null) bgBonus.gameObject.SetActive(false);

            var panelBonus = KOUIRenderer.FindChildByID(psRoot, "Panel_BonusPoint");
            if (panelBonus != null) panelBonus.gameObject.SetActive(false);

            // Also search for any child containing "Bonus", "preset", "reset" or "Capsule" in its name and deactivate it
            for (int i = 0; i < psRoot.childCount; i++)
            {
                var child = psRoot.GetChild(i);
                if (child.name.Contains("Bonus") || child.name.Contains("preset") || child.name.Contains("reset") || child.name.Contains("Capsule"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 5. Reposition Level-Job row (to Y = -55f) and align columns to shortened layout (panel width 272f)
            float levelJobY = -55f;
            float leftLabelX = 15f;
            float leftValueX = 65f;
            float rightLabelX = 145f;
            float rightValueX = 190f;
            float boxWidth = 65f;
            
            var levelIds = new string[] { "Text_Level", "Text_Level_Bg" };
            foreach (var id in levelIds)
            {
                var tr = KOUIRenderer.FindChildByID(psRoot, id);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(leftValueX, levelJobY);
                        rt.sizeDelta = new Vector2(boxWidth, 24f);
                    }
                }
            }

            foreach (var txt in allTexts)
            {
                string t = txt.text.Trim();
                var rt = txt.GetComponent<RectTransform>();
                if (rt != null)
                {
                    if (t == "Level" && rt.anchoredPosition.y < -50f && rt.anchoredPosition.y > -120f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(leftLabelX, levelJobY);
                        rt.sizeDelta = new Vector2(50f, 24f);
                        txt.alignment = TextAnchor.MiddleLeft;
                    }
                }
            }

            // Move Job (Text_Class) to the right of Level at Y = -55f, and bring Job label closer (X = 145f)
            var txtClass = KOUIRenderer.FindChildByID(psRoot, "Text_Class");
            if (txtClass != null)
            {
                var rt = txtClass.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(rightValueX, levelJobY);
                    rt.sizeDelta = new Vector2(boxWidth, 24f);
                }

                // Also move the background of Text_Class
                var bgClass = KOUIRenderer.FindChildByID(psRoot, "Text_Class_Bg");
                if (bgClass != null)
                {
                    var rtBg = bgClass.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = new Vector2(0f, 1f);
                        rtBg.anchorMax = new Vector2(0f, 1f);
                        rtBg.pivot = new Vector2(0f, 1f);
                        rtBg.anchoredPosition = new Vector2(rightValueX, levelJobY);
                        rtBg.sizeDelta = new Vector2(boxWidth, 24f);
                    }
                }

                // Move the Job label (Class label) to x = 145f, y = -55f (aligned with Defence label)
                foreach (var txt in allTexts)
                {
                    if (txt.text == "Job" || txt.text == "Class" || txt.text == "Class:")
                    {
                        var rtLbl = txt.GetComponent<RectTransform>();
                        if (rtLbl != null)
                        {
                            rtLbl.anchorMin = new Vector2(0f, 1f);
                            rtLbl.anchorMax = new Vector2(0f, 1f);
                            rtLbl.pivot = new Vector2(0f, 1f);
                            rtLbl.anchoredPosition = new Vector2(rightLabelX, levelJobY);
                            rtLbl.sizeDelta = new Vector2(50f, 24f);
                            txt.alignment = TextAnchor.MiddleLeft;
                        }
                    }
                }
            }

            // 6. Swap Attack-Defence and War Points rows (Attack-Defence at Y = -84f, War Points at Y = -118f) and align columns
            float attackDefenceY = -84f;
            float warPointsY = -118f;

            // Attack (Text_AP) value box and background -> Left Column (X = 65f, Width = 65f)
            var apIds = new string[] { "Text_AP", "Text_AP_Bg" };
            foreach (var id in apIds)
            {
                var tr = KOUIRenderer.FindChildByID(psRoot, id);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(leftValueX, attackDefenceY);
                        rt.sizeDelta = new Vector2(boxWidth, 24f);
                    }
                }
            }

            // Defence (Text_GP) value box and background -> Right Column (X = 190f, Width = 65f)
            var gpIds = new string[] { "Text_GP", "Text_GP_Bg" };
            foreach (var id in gpIds)
            {
                var tr = KOUIRenderer.FindChildByID(psRoot, id);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(rightValueX, attackDefenceY);
                        rt.sizeDelta = new Vector2(boxWidth, 24f);
                    }
                }
            }

            // War Points values and backgrounds -> Right Column (X = 85f, Width = 170f)
            var wpIds = new string[] { "Text_RealmPoint", "Text_RealmPoint_Bg" };
            foreach (var id in wpIds)
            {
                var tr = FindChildByIDIgnoreCase(psRoot, id);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(85f, warPointsY);
                        rt.sizeDelta = new Vector2(170f, 24f);
                    }
                }
            }

            // Swap labels and align them
            foreach (var txt in allTexts)
            {
                string t = txt.text.Trim();
                var rt = txt.GetComponent<RectTransform>();
                if (rt != null)
                {
                    if (t == "War Points" && rt.anchoredPosition.y < -100f && rt.anchoredPosition.y > -220f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(leftLabelX, warPointsY);
                        rt.sizeDelta = new Vector2(100f, 24f);
                        txt.alignment = TextAnchor.MiddleLeft;
                    }
                    else if (t == "Attack" && rt.anchoredPosition.y < -100f && rt.anchoredPosition.y > -250f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(leftLabelX, attackDefenceY);
                        rt.sizeDelta = new Vector2(50f, 24f);
                        txt.alignment = TextAnchor.MiddleLeft;
                    }
                    else if (t == "Defence" && rt.anchoredPosition.y < -100f && rt.anchoredPosition.y > -250f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(rightLabelX, attackDefenceY);
                        rt.sizeDelta = new Vector2(50f, 24f);
                        txt.alignment = TextAnchor.MiddleLeft;
                    }
                }
            }

            // Position Separators under Level/Job, Attack-Defence, and War Points
            for (int i = 0; i < psRoot.childCount; i++)
            {
                var child = psRoot.GetChild(i);
                if (child.name == "HorizontalSeparator")
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.sizeDelta = new Vector2(245f, rt.sizeDelta.y); // Shorten separator width horizontally to 245f
                        
                        float y = rt.anchoredPosition.y;
                        if (y < -100f && y > -165f) // Job separator (originally -153) -> deactivate
                        {
                            child.gameObject.SetActive(false);
                        }
                        else if (y < -165f && y > -225f) // War Points separator (originally -215) -> set to -113f
                        {
                            rt.anchoredPosition = new Vector2(15f, -113f);
                        }
                        else if (y < -225f && y > -270f) // Attack/Defence separator (originally -251) -> deactivate
                        {
                            child.gameObject.SetActive(false);
                        }
                        else if (y < -320f && y > -410f) // Separator under INT row -> set to -337f
                        {
                            rt.anchoredPosition = new Vector2(15f, -337f);
                        }
                    }
                }
            }

            // 6. Reposition and scale stat points (STR, DEX, INT, HP, MP) and their backgrounds/separators
            float statsY = -250f; // Row 1 (STR/HP) Y position
            float statsRowHeight = 28f; // Row spacing

            // Position STR, DEX, INT value fields
            var leftStatIds = new string[] { "Text_Strength", "Text_Dexterity", "Text_Intelligence" };
            for (int i = 0; i < leftStatIds.Length; i++)
            {
                var tr = KOUIRenderer.FindChildByID(psRoot, leftStatIds[i]);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(55f, statsY - (i * statsRowHeight));
                        rt.sizeDelta = new Vector2(75f, 24f);
                    }
                }
                var bg = KOUIRenderer.FindChildByID(psRoot, leftStatIds[i] + "_Bg");
                if (bg != null)
                {
                    var rtBg = bg.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = new Vector2(0f, 1f);
                        rtBg.anchorMax = new Vector2(0f, 1f);
                        rtBg.pivot = new Vector2(0f, 1f);
                        rtBg.anchoredPosition = new Vector2(55f, statsY - (i * statsRowHeight));
                        rtBg.sizeDelta = new Vector2(75f, 24f);
                    }
                }
            }

            // Position HP, MP value fields
            var rightStatIds = new string[] { "Text_Stamina", "Text_MagicAttack" };
            for (int i = 0; i < rightStatIds.Length; i++)
            {
                var tr = KOUIRenderer.FindChildByID(psRoot, rightStatIds[i]);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(180f, statsY - (i * statsRowHeight));
                        rt.sizeDelta = new Vector2(75f, 24f);
                    }
                }
                var bg = KOUIRenderer.FindChildByID(psRoot, rightStatIds[i] + "_Bg");
                if (bg != null)
                {
                    var rtBg = bg.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = new Vector2(0f, 1f);
                        rtBg.anchorMax = new Vector2(0f, 1f);
                        rtBg.pivot = new Vector2(0f, 1f);
                        rtBg.anchoredPosition = new Vector2(180f, statsY - (i * statsRowHeight));
                        rtBg.sizeDelta = new Vector2(75f, 24f);
                    }
                }
            }

            // Reposition BoxBackgrounds for STR, DEX, INT (Left Column) and HP, MP (Right Column)
            int leftBgCount = 0;
            int rightBgCount = 0;
            for (int i = 0; i < psRoot.childCount; i++)
            {
                var child = psRoot.GetChild(i);
                if (child.name == "BoxBackground")
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < -200f && rt.anchoredPosition.y > -360f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        if (rt.anchoredPosition.x < 100f) // Left column background
                        {
                            rt.anchoredPosition = new Vector2(15f, statsY - (leftBgCount * statsRowHeight));
                            rt.sizeDelta = new Vector2(115f, 24f);
                            leftBgCount++;
                        }
                        else // Right column background
                        {
                            rt.anchoredPosition = new Vector2(140f, statsY - (rightBgCount * statsRowHeight));
                            rt.sizeDelta = new Vector2(115f, 24f);
                            rightBgCount++;
                        }
                    }
                }
            }

            // Reposition VerticalSeparators for STR, DEX, INT and HP, MP
            int leftSepCount = 0;
            int rightSepCount = 0;
            for (int i = 0; i < psRoot.childCount; i++)
            {
                var child = psRoot.GetChild(i);
                if (child.name == "VerticalSeparator")
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < -200f && rt.anchoredPosition.y > -360f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        if (rt.anchoredPosition.x < 100f) // Left column separator
                        {
                            rt.anchoredPosition = new Vector2(55f, statsY - (leftSepCount * statsRowHeight));
                            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 24f);
                            leftSepCount++;
                        }
                        else // Right column separator
                        {
                            rt.anchoredPosition = new Vector2(180f, statsY - (rightSepCount * statsRowHeight));
                            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 24f);
                            rightSepCount++;
                        }
                    }
                }
            }

            // Reposition VerticalSeparators for Resistances (originally at -410, -438, -466)
            int leftResistSepCount = 0;
            int rightResistSepCount = 0;
            for (int i = 0; i < psRoot.childCount; i++)
            {
                var child = psRoot.GetChild(i);
                if (child.name == "VerticalSeparator")
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < -360f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        if (rt.anchoredPosition.x < 150f) // Left column separator
                        {
                            rt.anchoredPosition = new Vector2(85f, -344f - (leftResistSepCount * 28f));
                            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 24f);
                            leftResistSepCount++;
                        }
                        else // Right column separator
                        {
                            rt.anchoredPosition = new Vector2(210f, -344f - (rightResistSepCount * 28f));
                            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 24f);
                            rightResistSepCount++;
                        }
                    }
                }
            }

            // Reposition labels (STR, DEX, INT, HP, MP text elements)
            int leftLblCount = 0;
            int rightLblCount = 0;
            foreach (var txt in allTexts)
            {
                string t = txt.text.Trim();
                if (t == "STR" || t == "DEX" || t == "INT" || t == "HP" || t == "MP")
                {
                    var rt = txt.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < -200f && rt.anchoredPosition.y > -360f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        if (t == "STR" || t == "DEX" || t == "INT")
                        {
                            rt.anchoredPosition = new Vector2(15f, statsY - (leftLblCount * statsRowHeight));
                            rt.sizeDelta = new Vector2(40f, 24f);
                            txt.alignment = TextAnchor.MiddleLeft;
                            leftLblCount++;
                        }
                        else
                        {
                            rt.anchoredPosition = new Vector2(140f, statsY - (rightLblCount * statsRowHeight));
                            rt.sizeDelta = new Vector2(40f, 24f);
                            txt.alignment = TextAnchor.MiddleLeft;
                            rightLblCount++;
                        }
                    }
                }
            }

            // Shift Resistances block elements upwards by 66f (to Y = -344f, -372f, -400f) to fit shortened panel

            
            // Shift Left Resistances values (Fire, Ice, Light)
            var leftResistIds = new string[] { "Text_RegistFire", "Text_RegistIce", "Text_RegistLightR" };
            for (int i = 0; i < leftResistIds.Length; i++)
            {
                var tr = FindChildByIDIgnoreCase(psRoot, leftResistIds[i]);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(85f, -344f - (i * 28f));
                        rt.sizeDelta = new Vector2(45f, 24f);
                    }
                }
                var bg = FindChildByIDIgnoreCase(psRoot, leftResistIds[i] + "_Bg");
                if (bg != null)
                {
                    var rtBg = bg.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = new Vector2(0f, 1f);
                        rtBg.anchorMax = new Vector2(0f, 1f);
                        rtBg.pivot = new Vector2(0f, 1f);
                        rtBg.anchoredPosition = new Vector2(85f, -344f - (i * 28f));
                        rtBg.sizeDelta = new Vector2(45f, 24f);
                    }
                }
            }

            // Position Right Resistances values (Magic, Curse, Poison)
            var rightResistIds = new string[] { "Text_RegistMagic", "Text_RegistCurse", "Text_RegistPoison" };
            for (int i = 0; i < rightResistIds.Length; i++)
            {
                var tr = FindChildByIDIgnoreCase(psRoot, rightResistIds[i]);
                if (tr != null)
                {
                    var rt = tr.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(210f, -344f - (i * 28f));
                        rt.sizeDelta = new Vector2(45f, 24f);
                    }
                }
                var bg = FindChildByIDIgnoreCase(psRoot, rightResistIds[i] + "_Bg");
                if (bg != null)
                {
                    var rtBg = bg.GetComponent<RectTransform>();
                    if (rtBg != null)
                    {
                        rtBg.anchorMin = new Vector2(0f, 1f);
                        rtBg.anchorMax = new Vector2(0f, 1f);
                        rtBg.pivot = new Vector2(0f, 1f);
                        rtBg.anchoredPosition = new Vector2(210f, -344f - (i * 28f));
                        rtBg.sizeDelta = new Vector2(45f, 24f);
                    }
                }
            }

            // Shift dynamic BoxBackground and CapsuleBackground elements for Resistances
            int leftResistBgCount = 0;
            int rightResistBgCount = 0;
            for (int i = 0; i < psRoot.childCount; i++)
            {
                var child = psRoot.GetChild(i);
                if (child.name == "BoxBackground")
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < -360f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        if (rt.anchoredPosition.x < 100f) // Left column
                        {
                            rt.anchoredPosition = new Vector2(15f, -344f - (leftResistBgCount * 28f));
                            rt.sizeDelta = new Vector2(115f, 24f);
                            leftResistBgCount++;
                        }
                        else // Right column
                        {
                            rt.anchoredPosition = new Vector2(140f, -344f - (rightResistBgCount * 28f));
                            rt.sizeDelta = new Vector2(115f, 24f);
                            rightResistBgCount++;
                        }
                    }
                }
                else if (child.name == "CapsuleBackground")
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null && rt.anchoredPosition.y < -360f)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.anchoredPosition = new Vector2(15f, -314f);
                        rt.sizeDelta = new Vector2(95f, 20f);
                    }
                }
            }

            // Shift labels by text search and shorten/map their text content
            var resistLabelMap = new Dictionary<string, string>() {
                { "Flame Resist", "Flame R." },
                { "Glacier Resist", "Glacier R." },
                { "Lightning Resist", "Light. R." },
                { "Magic Resist", "Magic R." },
                { "Curse Resist", "Curse R." },
                { "Poison Resist", "Poison R." }
            };
            foreach (var txt in allTexts)
            {
                string t = txt.text.Trim();
                if (resistLabelMap.ContainsKey(t))
                {
                    txt.text = resistLabelMap[t];
                    var rt = txt.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0f, 1f);
                        rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        if (t == "Flame Resist" || t == "Glacier Resist" || t == "Lightning Resist")
                        {
                            float targetY = (t == "Flame Resist") ? -344f : (t == "Glacier Resist") ? -372f : -400f;
                            rt.anchoredPosition = new Vector2(15f, targetY);
                            rt.sizeDelta = new Vector2(70f, 24f);
                        }
                        else
                        {
                            float targetY = (t == "Magic Resist") ? -344f : (t == "Curse Resist") ? -372f : -400f;
                            rt.anchoredPosition = new Vector2(140f, targetY);
                            rt.sizeDelta = new Vector2(70f, 24f);
                        }
                        txt.alignment = TextAnchor.MiddleLeft;
                    }
                }
            }

            // 7. Draw dynamic skill points block (Archery, Assassin, Explore, Master etc.)
            var skillNames = GetSkillNames(charClass);
            CreateSkillRow(psRoot, skillNames[0], skill1, 15f, 85f, -174f);
            CreateSkillRow(psRoot, skillNames[1], skill2, 145f, 210f, -174f);
            CreateSkillRow(psRoot, skillNames[2], skill3, 15f, 85f, -200f);
            CreateSkillRow(psRoot, skillNames[3], skill4, 145f, 210f, -200f);

            // 8. Add Skill Points and Stat Points Headers
            CreateSectionHeader(psRoot, "Skill Points", -152f);
            CreateSectionHeader(psRoot, "Stat Points", -228f);
        }

        private void ConfigureInventoryPanel(List<InventoryItemData> equippedItems, List<InventoryItemData> bagItems, string charName)
        {
            if (_inspectInventoryPanel == null) return;
            Transform root = _inspectInventoryPanel.transform;

            // Apply modern UI themes & styles to remove legacy white background
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.ModernizeInventoryPanel(root);
            }

            // Hide gold/weight texts and backgrounds as they are irrelevant for inspect
            var textGold = KOUIRenderer.FindChildText(root, "text_gold");
            if (textGold != null) textGold.gameObject.SetActive(false);
            var goldCapsule = KOUIRenderer.FindChildByID(root, "GoldCapsule");
            if (goldCapsule != null) goldCapsule.gameObject.SetActive(false);

            var textWeight = KOUIRenderer.FindChildText(root, "text_weight");
            if (textWeight != null) textWeight.gameObject.SetActive(false);
            var weightCapsule = KOUIRenderer.FindChildByID(root, "WeightCapsule");
            if (weightCapsule != null) weightCapsule.gameObject.SetActive(false);

            // Hide trash can (area_samma) and auto-sort/reload button (btn_SortInventory) for inspect view
            var areaSamma = root.Find("area_samma") ?? KOUIRenderer.FindChildByID(root, "area_samma");
            if (areaSamma != null) areaSamma.gameObject.SetActive(false);
            var sortBtn = root.Find("btn_SortInventory") ?? KOUIRenderer.FindChildByID(root, "btn_SortInventory");
            if (sortBtn != null) sortBtn.gameObject.SetActive(false);



            var allKOAreas = root.GetComponentsInChildren<KOUIArea>(true);
            var equipAreaRTs = new List<RectTransform>();
            var bagAreaRTs = new List<RectTransform>();

            // Pass 1: Collect areas and apply the vertical offset to equipped slots
            foreach (var area in allKOAreas)
            {
                var rt = area.GetComponent<RectTransform>();
                if (rt == null) continue;

                if (area.AreaType == 1)
                {
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y - 20f); // Shift down by 20px
                    equipAreaRTs.Add(rt);
                }
                else if (area.AreaType == 2)
                {
                    bagAreaRTs.Add(rt);
                }
            }

            // Sort bag areas sol-üstten sağ-alta
            bagAreaRTs.Sort((a, b) =>
            {
                int yCompare = b.anchorMin.y.CompareTo(a.anchorMin.y);
                return yCompare != 0 ? yCompare : a.anchorMin.x.CompareTo(b.anchorMin.x);
            });

            // Pass 2: Calculate columns and rows from bag areas
            var uniqueX = new List<float>();
            foreach (var rt in bagAreaRTs)
            {
                float x = rt.anchoredPosition.x;
                bool found = false;
                foreach (var ux in uniqueX)
                {
                    if (Mathf.Abs(ux - x) < 5f) { found = true; break; }
                }
                if (!found) uniqueX.Add(x);
            }
            uniqueX.Sort(); // Ascending: left-to-right

            var uniqueY = new List<float>();
            foreach (var rt in bagAreaRTs)
            {
                float y = rt.anchoredPosition.y;
                bool found = false;
                foreach (var uy in uniqueY)
                {
                    if (Mathf.Abs(uy - y) < 5f) { found = true; break; }
                }
                if (!found) uniqueY.Add(y);
            }
            uniqueY.Sort((a, b) => b.CompareTo(a)); // Descending: top-to-bottom

            // Also calculate unique Y coordinates for equipped slots to determine their rows
            var uniqueEquipY = new List<float>();
            foreach (var rt in equipAreaRTs)
            {
                float y = rt.anchoredPosition.y;
                bool found = false;
                foreach (var uy in uniqueEquipY)
                {
                    if (Mathf.Abs(uy - y) < 5f) { found = true; break; }
                }
                if (!found) uniqueEquipY.Add(y);
            }
            uniqueEquipY.Sort((a, b) => b.CompareTo(a)); // Descending: top-to-bottom

            int C = uniqueX.Count - 1; // Rightmost column index

            // Apply size 40x40 and right-alignment / dikey clustering shift to all slots and their glass backgrounds
            foreach (var area in allKOAreas)
            {
                if (area.AreaType != 1 && area.AreaType != 2) continue;

                var rt = area.GetComponent<RectTransform>();
                if (rt == null) continue;

                // 1. Calculate column index
                int col = 0;
                float minDiffX = float.MaxValue;
                for (int i = 0; i < uniqueX.Count; i++)
                {
                    float diff = Mathf.Abs(uniqueX[i] - rt.anchoredPosition.x);
                    if (diff < minDiffX) { minDiffX = diff; col = i; }
                }

                // 2. Set new size and position (shift right by (C - col) * 5px)
                rt.sizeDelta = new Vector2(40f, 40f);
                float shiftX = (C - col) * 5f;
                float newX = rt.anchoredPosition.x + shiftX;
                float newY = rt.anchoredPosition.y;

                if (area.AreaType == 1) // Equipped slots: also shift vertically (downwards) towards the fixed bottom row
                {
                    int row = 0;
                    float minDiffY = float.MaxValue;
                    for (int i = 0; i < uniqueEquipY.Count; i++)
                    {
                        float diff = Mathf.Abs(uniqueEquipY[i] - rt.anchoredPosition.y);
                        if (diff < minDiffY) { minDiffY = diff; row = i; }
                    }
                    newY = rt.anchoredPosition.y - (uniqueEquipY.Count - 1 - row) * 5f;
                }
                else if (area.AreaType == 2) // Bag slots: also shift vertically (downwards) towards the fixed bottom row
                {
                    int row = 0;
                    float minDiffY = float.MaxValue;
                    for (int i = 0; i < uniqueY.Count; i++)
                    {
                        float diff = Mathf.Abs(uniqueY[i] - rt.anchoredPosition.y);
                        if (diff < minDiffY) { minDiffY = diff; row = i; }
                    }
                    newY = rt.anchoredPosition.y - (uniqueY.Count - 1 - row) * 5f;
                }

                rt.anchoredPosition = new Vector2(newX, newY);

                // 3. Update glass socket background image with targetSize = 40
                var areaImg = area.GetComponent<Image>() ?? area.gameObject.AddComponent<Image>();
                areaImg.sprite = KOUIManager.Instance?.GetSkillThemeGlassSlotSprite("slot_socket_glass_v5", 40);
                areaImg.color = Color.white;
                areaImg.raycastTarget = false;
            }

            // Calculate dynamic vertical gaps and shift all equipped slots to be exactly 10px above the separator
            float newTitleY = -15f;
            float newDividerY = -34f;

            float minEquipY = 0f;
            bool hasEquip = false;
            foreach (var rt in equipAreaRTs)
            {
                if (rt != null)
                {
                    float y = rt.anchoredPosition.y;
                    if (!hasEquip || y < minEquipY) { minEquipY = y; hasEquip = true; }
                }
            }

            float maxBagY = -300f;
            bool hasBag = false;
            foreach (var rt in bagAreaRTs)
            {
                if (rt != null)
                {
                    float y = rt.anchoredPosition.y;
                    if (!hasBag || y > maxBagY) { maxBagY = y; hasBag = true; }
                }
            }

            float separatorY = -243f; // Pre-calculated Y
            if (hasEquip && hasBag)
            {
                // Bag slots top edge is maxBagY
                // Separator Y is 10px above maxBagY: maxBagY + 10px
                separatorY = maxBagY + 10f;

                // Equipped slots bottom edge (lowestEquipBottom = minEquipY - 40f) must be 10px above separatorY: separatorY + 10f
                float targetBottom = separatorY + 10f;
                float currentBottom = minEquipY - 40f;
                float dy = targetBottom - currentBottom;

                // Shift all equipped slots vertically by dy
                foreach (var rt in equipAreaRTs)
                {
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + dy);
                    }
                }
            }

            // 3. Position the title and title divider exactly matching the left panel proportions,
            // and shift all slots upwards relative to the panel to align topmost slot at Y = -44f.
            float maxEquipY = -50f;
            bool hasMaxEquip = false;
            foreach (var rt in equipAreaRTs)
            {
                if (rt != null)
                {
                    float y = rt.anchoredPosition.y;
                    if (!hasMaxEquip || y > maxEquipY) { maxEquipY = y; hasMaxEquip = true; }
                }
            }

            // We want topmost slot top edge (maxEquipY) to be at Y = -44f
            float shiftY = -44f - maxEquipY;
            if (shiftY > 0f)
            {
                // Shift all slot RectTransforms in place relative to the panel's top edge
                foreach (var area in allKOAreas)
                {
                    if (area.AreaType == 1 || area.AreaType == 2)
                    {
                        var rtSlot = area.GetComponent<RectTransform>();
                        if (rtSlot != null)
                        {
                            rtSlot.anchoredPosition = new Vector2(rtSlot.anchoredPosition.x, rtSlot.anchoredPosition.y + shiftY);
                        }
                    }
                }

                // Shift the section separator position as well
                separatorY += shiftY;
            }

            // Final coordinates of the title and title divider relative to the top edge
            newTitleY = -4f;
            newDividerY = -34f;

            // Calculate target panel height based on the bottommost slot to guarantee a perfect 15px bottom margin
            float minBagY = 0f;
            bool hasMinBag = false;
            foreach (var rt in bagAreaRTs)
            {
                if (rt != null)
                {
                    float y = rt.anchoredPosition.y;
                    if (!hasMinBag || y < minBagY) { minBagY = y; hasMinBag = true; }
                }
            }
            float lowestBagBottom = minBagY - 40f;
            float targetHeight = -lowestBagBottom + 15f; // Leaves exactly 15px bottom margin

            // Calculate target panel width based on the slot grid bounds and original right margin
            float leftmostX = float.MaxValue;
            float rightmostX = float.MinValue;
            foreach (var rt in bagAreaRTs)
            {
                if (rt != null)
                {
                    float x = rt.anchoredPosition.x;
                    if (x < leftmostX) leftmostX = x;
                    if (x + rt.sizeDelta.x > rightmostX) rightmostX = x + rt.sizeDelta.x;
                }
            }

            var rootRT = root.GetComponent<RectTransform>();
            if (rootRT != null)
            {
                float originalWidth = rootRT.sizeDelta.x;
                float rightMargin = originalWidth - rightmostX;
                float slotGridWidth = rightmostX - leftmostX;
                float targetWidth = slotGridWidth + 2f * rightMargin;
                float shiftAmount = originalWidth - targetWidth;

                // Adjust panel sizeDelta width and height
                rootRT.sizeDelta = new Vector2(targetWidth, targetHeight);

                // Shift all slot positions leftwards by shiftAmount relative to the panel's left edge
                // to keep their screen positions completely fixed
                foreach (var area in allKOAreas)
                {
                    if (area.AreaType == 1 || area.AreaType == 2)
                    {
                        var rtSlot = area.GetComponent<RectTransform>();
                        if (rtSlot != null)
                        {
                            rtSlot.anchoredPosition = new Vector2(rtSlot.anchoredPosition.x - shiftAmount, rtSlot.anchoredPosition.y);
                        }
                    }
                }
            }

            // Sort bag areas sol-üstten sağ-alta
            bagAreaRTs.Sort((a, b) =>
            {
                int yCompare = b.anchorMin.y.CompareTo(a.anchorMin.y);
                return yCompare != 0 ? yCompare : a.anchorMin.x.CompareTo(b.anchorMin.x);
            });

            // Map equipped item slots
            var equipMap = new Dictionary<int, InventoryItemData>();
            foreach (var item in equippedItems)
            {
                equipMap[item.SlotIndex] = item;
            }

            // Populate Equipped Slots (14 slots)
            for (int i = 0; i < KOInventory.ITEM_SLOT_COUNT; i++)
            {
                var slotObj = new GameObject($"Inspect_EqSlot_{i}");
                slotObj.transform.SetParent(root, false);
                var slotRT = slotObj.AddComponent<RectTransform>();

                if (i < equipAreaRTs.Count)
                {
                    var areaRT = equipAreaRTs[i];
                    slotRT.anchorMin = areaRT.anchorMin;
                    slotRT.anchorMax = areaRT.anchorMax;
                    slotRT.offsetMin = areaRT.offsetMin;
                    slotRT.offsetMax = areaRT.offsetMax;
                }
                else
                {
                    slotRT.anchorMin = new Vector2(0, 1);
                    slotRT.anchorMax = new Vector2(0, 1);
                    slotRT.pivot = new Vector2(0, 1);
                    slotRT.sizeDelta = new Vector2(32, 32);
                    slotRT.anchoredPosition = new Vector2(-100, -100);
                }

                if (equipMap.TryGetValue(i, out var itemData))
                {
                    int iconId = KOUIManager.ResolveIconId(itemData.ItemDefId);
                    var icon = KOItemIconLoader.LoadItemIcon(iconId);
                    if (icon != null)
                    {
                        var img = slotObj.AddComponent<Image>();
                        img.sprite = icon;
                        img.raycastTarget = true;
                    }
                    else
                    {
                        var img = slotObj.AddComponent<Image>();
                        img.color = new Color(0.2f, 0.3f, 0.5f, 0.5f);
                        img.raycastTarget = true;
                    }

                    var slotHandler = slotObj.AddComponent<KOItemSlotHandler>();
                    slotHandler.slotType = KOItemSlotHandler.SlotType.InspectEquipSlot;
                    slotHandler.slotIndex = i;
                    slotHandler.itemData = itemData;
                }
                else
                {
                    var img = slotObj.AddComponent<Image>();
                    img.color = Color.clear;
                    img.raycastTarget = true;
                }

                // Sürüklemeyi tamamen engellemek için KOItemDragHandler eklenmiyor!
            }

            // Map bag slots (28 slots)
            var bagMap = new Dictionary<int, InventoryItemData>();
            foreach (var item in bagItems)
            {
                bagMap[item.SlotIndex] = item;
            }

            for (int i = 0; i < KOInventory.MAX_ITEM_INVENTORY; i++)
            {
                var slotObj = new GameObject($"Inspect_BagSlot_{i}");
                slotObj.transform.SetParent(root, false);
                var slotRT = slotObj.AddComponent<RectTransform>();

                if (i < bagAreaRTs.Count)
                {
                    var areaRT = bagAreaRTs[i];
                    slotRT.anchorMin = areaRT.anchorMin;
                    slotRT.anchorMax = areaRT.anchorMax;
                    slotRT.offsetMin = areaRT.offsetMin;
                    slotRT.offsetMax = areaRT.offsetMax;
                }
                else
                {
                    slotRT.anchorMin = new Vector2(0, 1);
                    slotRT.anchorMax = new Vector2(0, 1);
                    slotRT.pivot = new Vector2(0, 1);
                    slotRT.sizeDelta = new Vector2(32, 32);
                    slotRT.anchoredPosition = new Vector2(-100, -100);
                }

                if (bagMap.TryGetValue(i, out var itemData))
                {
                    int iconId = KOUIManager.ResolveIconId(itemData.ItemDefId);
                    var icon = KOItemIconLoader.LoadItemIcon(iconId);
                    if (icon != null)
                    {
                        var img = slotObj.AddComponent<Image>();
                        img.sprite = icon;
                        img.raycastTarget = true;
                    }
                    else
                    {
                        var img = slotObj.AddComponent<Image>();
                        img.color = new Color(0.2f, 0.3f, 0.5f, 0.5f);
                        img.raycastTarget = true;
                    }

                    // Display count if > 1
                    if (itemData.StackCount > 1)
                    {
                        var countObj = new GameObject("CountText");
                        countObj.transform.SetParent(slotObj.transform, false);
                        var countRT = countObj.AddComponent<RectTransform>();
                        countRT.anchorMin = new Vector2(1, 0);
                        countRT.anchorMax = new Vector2(1, 0);
                        countRT.pivot = new Vector2(1, 0);
                        countRT.anchoredPosition = new Vector2(-2, 2);
                        countRT.sizeDelta = new Vector2(30, 12);
                        var countText = countObj.AddComponent<Text>();
                        countText.text = itemData.StackCount.ToString();
                        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        countText.fontSize = 9;
                        countText.color = Color.white;
                        countText.alignment = TextAnchor.LowerRight;
                        countText.raycastTarget = false;
                    }

                    var slotHandler = slotObj.AddComponent<KOItemSlotHandler>();
                    slotHandler.slotType = KOItemSlotHandler.SlotType.InspectBagSlot;
                    slotHandler.slotIndex = i;
                    slotHandler.itemData = itemData;
                }
                else
                {
                    var img = slotObj.AddComponent<Image>();
                    img.color = Color.clear;
                    img.raycastTarget = true;
                }

                // Sürüklemeyi tamamen engellemek için KOItemDragHandler eklenmiyor!
            }



            // Create separator line between equipped slots and bag slots
            var sepObj = new GameObject("Inven_Section_Separator", typeof(RectTransform));
            sepObj.transform.SetParent(root, false);
            var imgSep = sepObj.AddComponent<Image>();
            imgSep.color = new Color(0.45f, 0.35f, 0.15f, 0.3f); // Toned down bronze separator
            
            // Try to copy sprite from any existing HorizontalSeparator
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == "HorizontalSeparator" || child.name.StartsWith("Separator_At"))
                {
                    var existingImg = child.GetComponent<Image>();
                    if (existingImg != null)
                    {
                        imgSep.sprite = existingImg.sprite;
                        imgSep.type = existingImg.type;
                        imgSep.color = existingImg.color;
                        break;
                    }
                }
            }
            leftmostX = float.MaxValue;
            rightmostX = float.MinValue;
            foreach (var rt in bagAreaRTs)
            {
                if (rt != null)
                {
                    float x = rt.anchoredPosition.x;
                    if (x < leftmostX) leftmostX = x;
                    if (x + rt.sizeDelta.x > rightmostX) rightmostX = x + rt.sizeDelta.x;
                }
            }

            var rtSep = sepObj.GetComponent<RectTransform>();
            rtSep.anchorMin = new Vector2(0f, 1f);
            rtSep.anchorMax = new Vector2(0f, 1f);
            rtSep.pivot = new Vector2(0f, 0.5f); // Left aligned pivot
            rtSep.anchoredPosition = new Vector2(leftmostX, separatorY); // Starts exactly at the left edge of leftmost slot
            rtSep.sizeDelta = new Vector2(rightmostX - leftmostX, 1f); // Width matches the grid width exactly



            var titleText = KOUIRenderer.FindChildText(root, "text_title") ?? KOUIRenderer.FindChildText(root, "text_Id");
            if (titleText == null)
            {
                var titleObj = new GameObject("text_title", typeof(RectTransform));
                titleObj.transform.SetParent(root, false);
                titleText = titleObj.AddComponent<Text>();
            }

            if (titleText != null)
            {
                titleText.text = $"{charName}'s Inventory";
                titleText.alignment = TextAnchor.MiddleCenter;
                
                // Copy style from left panel title if available
                if (_inspectVariousPanel != null)
                {
                    var varTitleText = KOUIRenderer.FindChildText(_inspectVariousPanel.transform, "text_title") ?? KOUIRenderer.FindChildText(_inspectVariousPanel.transform, "text_Id");
                    if (varTitleText != null)
                    {
                        titleText.font = varTitleText.font;
                        titleText.fontSize = varTitleText.fontSize;
                        titleText.fontStyle = varTitleText.fontStyle;
                        titleText.color = varTitleText.color;
                    }
                    else
                    {
                        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        titleText.fontSize = 14;
                        titleText.fontStyle = FontStyle.Bold;
                        titleText.color = new Color(0.95f, 0.85f, 0.35f, 1f);
                    }
                }
                else
                {
                    titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    titleText.fontSize = 14;
                    titleText.fontStyle = FontStyle.Bold;
                    titleText.color = new Color(0.95f, 0.85f, 0.35f, 1f);
                }

                // Add black shadow matching left panel title exactly
                if (titleText.gameObject.GetComponent<Shadow>() == null)
                {
                    var shadow = titleText.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0, 0, 0, 0.85f);
                    shadow.effectDistance = new Vector2(1, -1);
                }

                var rt = titleText.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, newTitleY); // Dynamic position Y
                    rt.sizeDelta = new Vector2(250f, 30f);
                }
            }

            // Create title divider matching left panel title divider exactly
            var titleDivObj = root.Find("InvenTitleDivider")?.gameObject;
            if (titleDivObj == null)
            {
                titleDivObj = new GameObject("InvenTitleDivider", typeof(RectTransform));
                titleDivObj.transform.SetParent(root, false);
                var divRt = titleDivObj.GetComponent<RectTransform>();
                if (divRt != null)
                {
                    divRt.anchorMin = new Vector2(0.5f, 1f);
                    divRt.anchorMax = new Vector2(0.5f, 1f);
                    divRt.pivot = new Vector2(0.5f, 1f);
                    divRt.sizeDelta = new Vector2(240f, 2f);
                    divRt.anchoredPosition = new Vector2(0f, newDividerY); // Dynamic position Y
                }
            }
            var titleDivImg = titleDivObj.GetComponent<Image>() ?? titleDivObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                titleDivImg.sprite = KOUIManager.Instance.GetSkillThemeFadingDividerSprite("inspect_title_divider", 240, 2, new Color(0.9f, 0.75f, 0.25f, 0.8f));
            }
            titleDivImg.color = Color.white;
        }

        private void BindCloseButtons()
        {
            if (_inspectInventoryPanel != null)
            {
                // Bind close button on Inventory panel
                var btnCloseInv = KOUIRenderer.FindChildButton(_inspectInventoryPanel.transform, "btn_close");
                if (btnCloseInv != null)
                {
                    btnCloseInv.onClick.RemoveAllListeners();
                    btnCloseInv.onClick.AddListener(CloseInspectWindows);

                    // Align it to the new top-right corner of the resized panel!
                    var rtClose = btnCloseInv.GetComponent<RectTransform>();
                    if (rtClose != null)
                    {
                        rtClose.anchorMin = new Vector2(1f, 1f);
                        rtClose.anchorMax = new Vector2(1f, 1f);
                        rtClose.pivot = new Vector2(1f, 1f);
                        rtClose.anchoredPosition = new Vector2(-6f, -6f); // Clean top-right padding
                    }
                }
            }

            if (_inspectVariousPanel != null)
            {
                // Find and hide the close button on the left panel (Various / Character Details)
                var btnCloseVar = KOUIRenderer.FindChildButton(_inspectVariousPanel.transform, "btn_close");
                if (btnCloseVar != null)
                {
                    btnCloseVar.gameObject.SetActive(false); // Hide the close button completely
                }
            }
        }

        /// <summary>
        /// Açık olan inceleme pencerelerini yok eder.
        /// </summary>
        public void CloseInspectWindows()
        {
            KOItemSlotHandler.HideTooltip();

            if (_inspectContainer != null)
            {
                var slide = _inspectContainer.GetComponent<EntropyOnline.UI.KOUIPanelSlideIn>();
                if (slide != null && !slide.IsSlidingOut && _inspectContainer.activeInHierarchy)
                {
                    var containerToDestroy = _inspectContainer;
                    _inspectContainer = null; // Set to null immediately to prevent re-entry
                    _inspectVariousPanel = null;
                    _inspectInventoryPanel = null;

                    // Reset HUD skillbar immediately to sync with the slide out animation
                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.RepositionSkillBarForPanel(false);
                    }

                    slide.SlideOut(() =>
                    {
                        if (containerToDestroy != null) Destroy(containerToDestroy);
                    });
                }
                else
                {
                    Destroy(_inspectContainer);
                    _inspectContainer = null;
                    _inspectVariousPanel = null;
                    _inspectInventoryPanel = null;

                    if (KOUIManager.Instance != null)
                    {
                        KOUIManager.Instance.RepositionSkillBarForPanel(false);
                    }
                }
            }
            else
            {
                _inspectVariousPanel = null;
                _inspectInventoryPanel = null;

                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.RepositionSkillBarForPanel(false);
                }
            }
        }

        // Helpers
        private void SetText(Transform root, string textName, string value)
        {
            var tr = KOUIRenderer.FindChildByID(root, textName);
            if (tr != null)
            {
                var txt = tr.GetComponent<Text>();
                if (txt != null) txt.text = value;
            }
        }

        private string FormatWithDelta(int baseVal, int itemVal)
        {
            if (itemVal > 0)
                return $"{baseVal} (+{itemVal})";
            return baseVal.ToString();
        }

        private string GetBaseClassText(byte charClass)
        {
            int baseClass = charClass % 100;
            if (baseClass == 1 || baseClass == 5 || baseClass == 6) return "Warrior";
            if (baseClass == 2 || baseClass == 7 || baseClass == 8) return "Rogue";
            if (baseClass == 3 || baseClass == 9 || baseClass == 10) return "Magician";
            if (baseClass == 4 || baseClass == 11 || baseClass == 12) return "Priest";
            return "Unknown Class";
        }

        private string[] GetSkillNames(ushort charClass)
        {
            int baseClass = charClass % 100;
            if (baseClass == 1 || baseClass == 5 || baseClass == 6) // Warrior
                return new string[] { "Attack", "Defense", "Passion", "Master" };
            if (baseClass == 2 || baseClass == 7 || baseClass == 8) // Rogue
                return new string[] { "Archery", "Assassin", "Explore", "Master" };
            if (baseClass == 3 || baseClass == 9 || baseClass == 10) // Magician
                return new string[] { "Flame", "Glacier", "Lightning", "Master" };
            if (baseClass == 4 || baseClass == 11 || baseClass == 12) // Priest
                return new string[] { "Healing", "Aura", "Holy", "Master" };
            return new string[] { "Skill 1", "Skill 2", "Skill 3", "Master" };
        }

        private void CreateSkillRow(Transform parent, string labelText, int value, float xLabel, float xValue, float y)
        {
            // 1. Create label text
            var labelObj = new GameObject($"Label_Skill_{labelText}", typeof(RectTransform));
            labelObj.transform.SetParent(parent, false);
            var txtLabel = labelObj.AddComponent<Text>();
            txtLabel.fontSize = 11;
            txtLabel.color = Color.white;
            txtLabel.text = labelText;
            txtLabel.alignment = TextAnchor.MiddleLeft;

            // Dynamically copy font from existing UI text
            var existingText = parent.GetComponentInChildren<Text>();
            if (existingText != null) txtLabel.font = existingText.font;
            else txtLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            
            var rtLabel = labelObj.GetComponent<RectTransform>();
            rtLabel.anchorMin = new Vector2(0f, 1f);
            rtLabel.anchorMax = new Vector2(0f, 1f);
            rtLabel.pivot = new Vector2(0f, 1f);
            rtLabel.anchoredPosition = new Vector2(xLabel, y);
            rtLabel.sizeDelta = new Vector2(65f, 22f);

            // 2. Create value background box
            var bgObj = new GameObject($"Bg_Skill_{labelText}", typeof(RectTransform));
            bgObj.transform.SetParent(parent, false);
            var imgBg = bgObj.AddComponent<Image>();
            imgBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark translucent
            imgBg.type = Image.Type.Simple;

            var rtBg = bgObj.GetComponent<RectTransform>();
            rtBg.anchorMin = new Vector2(0f, 1f);
            rtBg.anchorMax = new Vector2(0f, 1f);
            rtBg.pivot = new Vector2(0f, 1f);
            rtBg.anchoredPosition = new Vector2(xValue, y);
            rtBg.sizeDelta = new Vector2(40f, 22f);

            // 3. Create value text inside background box
            var valueObj = new GameObject($"Text_Skill_{labelText}", typeof(RectTransform));
            valueObj.transform.SetParent(bgObj.transform, false);
            var txtValue = valueObj.AddComponent<Text>();
            txtValue.fontSize = 11;
            txtValue.color = Color.white;
            txtValue.text = value.ToString();
            txtValue.alignment = TextAnchor.MiddleCenter;
            txtValue.font = txtLabel.font;

            var rtValue = valueObj.GetComponent<RectTransform>();
            rtValue.anchorMin = Vector2.zero;
            rtValue.anchorMax = Vector2.one;
            rtValue.pivot = new Vector2(0.5f, 0.5f);
            rtValue.sizeDelta = Vector2.zero; // Stretches to fill parent bg
        }

        private void CreateSectionHeader(Transform parent, string headerText, float y)
        {
            var headerObj = new GameObject($"Header_{headerText}", typeof(RectTransform));
            headerObj.transform.SetParent(parent, false);

            // 1. Add background image with fade gradient (pure white-to-clear gradient, tinted with gold/bronze color)
            var bgObj = new GameObject("Header_BG", typeof(RectTransform));
            bgObj.transform.SetParent(headerObj.transform, false);
            var imgBg = bgObj.AddComponent<Image>();
            if (KOUIManager.Instance != null)
            {
                imgBg.sprite = KOUIManager.Instance.GetFadeGradientSprite($"inspect_header_fade_bg_{headerText}", 240, 18, Color.white, Color.clear, 0);
            }
            imgBg.color = new Color(0.6f, 0.48f, 0.22f, 0.35f); // Same color/tint as Warp selection highlight in Gate list!
            var rtBg = bgObj.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;

            // 2. Add text component on top of background
            var txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(headerObj.transform, false);
            var txtHeader = txtObj.AddComponent<Text>();
            txtHeader.fontSize = 13; // Increased size to 13
            txtHeader.fontStyle = FontStyle.Bold;
            txtHeader.color = new Color(0.95f, 0.85f, 0.35f, 1f); // Bright gold
            txtHeader.text = headerText;
            txtHeader.alignment = TextAnchor.MiddleCenter;

            // Dynamically copy font from existing UI text
            var existingText = parent.GetComponentInChildren<Text>();
            if (existingText != null) txtHeader.font = existingText.font;
            else txtHeader.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var rtTxt = txtObj.GetComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.offsetMin = Vector2.zero;
            rtTxt.offsetMax = Vector2.zero;

            var rt = headerObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(135f, y);
            rt.sizeDelta = new Vector2(240f, 18f); // 18px height
        }

        private void CreateSeparatorLine(Transform parent, float y)
        {
            var separatorObj = new GameObject($"Separator_At_{Mathf.Abs(y)}", typeof(RectTransform));
            separatorObj.transform.SetParent(parent, false);
            var imgSep = separatorObj.AddComponent<Image>();

            // Try to find an existing horizontal separator to copy its style
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == "HorizontalSeparator" && child.gameObject != separatorObj)
                {
                    var existingImg = child.GetComponent<Image>();
                    if (existingImg != null)
                    {
                        imgSep.sprite = existingImg.sprite;
                        imgSep.type = existingImg.type;
                        imgSep.color = existingImg.color;
                        break;
                    }
                }
            }
            if (imgSep.sprite == null)
            {
                imgSep.color = new Color(0.45f, 0.35f, 0.15f, 0.3f);
            }

            var rtSep = separatorObj.GetComponent<RectTransform>();
            rtSep.anchorMin = new Vector2(0f, 1f);
            rtSep.anchorMax = new Vector2(0f, 1f);
            rtSep.pivot = new Vector2(0f, 1f);
            rtSep.anchoredPosition = new Vector2(15f, y);
            rtSep.sizeDelta = new Vector2(245f, 1f);
        }

        private Transform FindChildByIDIgnoreCase(Transform root, string id)
        {
            if (root == null) return null;
            var t = root.Find(id);
            if (t != null) return t;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (string.Equals(child.name, id, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
            return null;
        }
    }
}
