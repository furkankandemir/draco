using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Import;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    public class KOPartyBBS : MonoBehaviour
    {
        public static KOPartyBBS Instance { get; private set; }

        private const int PARTY_BBS_MAXLINE = 23;    // Packet entry count (Server standards)
        private const int UI_MAXLINE = 22;           // UI Row count

        private Button _btnClose;
        private List<GameObject> _rowObjects = new List<GameObject>();
        private List<PartyBBSEntry> _datas = new List<PartyBBSEntry>();
        private int _curPage = 0;
        private int _maxPage = 0;
        private int _curIndex = -1;
        private bool _processing = false;
        private float _lastRequestTime = -10f;
        private float _lastPageChangeTime = 0f;

        public struct PartyBBSEntry
        {
            public string Name;
            public byte Level;
            public short Class;
        }

        private void Awake()
        {
            Instance = this;
            BindElements();
        }

        private void OnEnable()
        {
            _curIndex = -1;
            _curPage = 0;
            _datas.Clear();
            foreach (var go in _rowObjects)
            {
                if (go != null) Destroy(go);
            }
            _rowObjects.Clear();

            ResetScrollPosition(true);
            SetPageState(true); // Default to personal page on open
        }

        private void SetPageState(bool showPersonal)
        {
            var t = transform;
            var personalTrans = t.Find("personal")?.gameObject;
            var partyTrans = t.Find("party")?.gameObject;

            if (personalTrans != null) personalTrans.SetActive(showPersonal);
            if (partyTrans != null) partyTrans.SetActive(!showPersonal);

        }

        private void SwitchPage(bool showPersonal)
        {
            _curPage = 0;
            _datas.Clear();
            foreach (var go in _rowObjects)
            {
                if (go != null) Destroy(go);
            }
            _rowObjects.Clear();

            ResetScrollPosition(true);
            SetPageState(showPersonal);
            MsgSend_RefreshData(_curPage);
        }

        private void BindElements()
        {
            var t = transform;

            _btnClose = KOUIRenderer.FindChildButton(t, "btn_exit");

            var personalTrans = KOUIRenderer.FindChildByID(t, "personal");
            var partyTrans = KOUIRenderer.FindChildByID(t, "party");


            // Bind side tab buttons
            var btnPersonalTab = t.Find("btn_personal")?.GetComponent<Button>();
            var btnPartyTab = t.Find("btn_party")?.GetComponent<Button>();

            if (btnPersonalTab != null) btnPersonalTab.onClick.AddListener(() => SwitchPage(true));
            if (btnPartyTab != null) btnPartyTab.onClick.AddListener(() => SwitchPage(false));
            SetButtonTransitions(btnPersonalTab);
            SetButtonTransitions(btnPartyTab);

            // Bind buttons in personal group
            if (personalTrans != null)
            {
                var btnRefresh = KOUIRenderer.FindChildButton(personalTrans, "btn_refresh");
                var btnRegister = KOUIRenderer.FindChildButton(personalTrans, "btn_add");
                var btnRegisterCancel = KOUIRenderer.FindChildButton(personalTrans, "btn_delete");
                var btnWhisper = KOUIRenderer.FindChildButton(personalTrans, "btn_whisper");
                var btnParty = KOUIRenderer.FindChildButton(personalTrans, "btn_party");

                SetButtonTransitions(btnRefresh);
                SetButtonTransitions(btnRegister);
                SetButtonTransitions(btnRegisterCancel);
                SetButtonTransitions(btnWhisper);
                SetButtonTransitions(btnParty);

                if (btnRefresh != null) btnRefresh.onClick.AddListener(() => {
                    _curPage = 0;
                    _datas.Clear();
                    MsgSend_RefreshData(0);
                });
                if (btnRegister != null) btnRegister.onClick.AddListener(OnRegister);
                if (btnRegisterCancel != null) btnRegisterCancel.onClick.AddListener(OnRegisterCancel);
                if (btnWhisper != null) btnWhisper.onClick.AddListener(RequestWhisper);
                if (btnParty != null) btnParty.onClick.AddListener(RequestParty);
            }

            // Bind buttons in party group
            if (partyTrans != null)
            {
                var btnRefresh = KOUIRenderer.FindChildButton(partyTrans, "btn_refresh");
                var btnRegister = KOUIRenderer.FindChildButton(partyTrans, "btn_add");
                var btnRegisterCancel = KOUIRenderer.FindChildButton(partyTrans, "btn_delete");
                var btnAdjust = KOUIRenderer.FindChildButton(partyTrans, "btn_adjust");
                var btnParty = KOUIRenderer.FindChildButton(partyTrans, "btn_party");

                SetButtonTransitions(btnRefresh);
                SetButtonTransitions(btnRegister);
                SetButtonTransitions(btnRegisterCancel);
                SetButtonTransitions(btnAdjust);
                SetButtonTransitions(btnParty);

                if (btnRefresh != null) btnRefresh.onClick.AddListener(() => {
                    _curPage = 0;
                    _datas.Clear();
                    MsgSend_RefreshData(0);
                });
                if (btnRegister != null) btnRegister.onClick.AddListener(OnRegister);
                if (btnRegisterCancel != null) btnRegisterCancel.onClick.AddListener(OnRegisterCancel);
                if (btnAdjust != null) btnAdjust.onClick.AddListener(OnRegister);
                if (btnParty != null) btnParty.onClick.AddListener(RequestParty);
            }

            if (_btnClose != null) _btnClose.onClick.AddListener(OnClose);
            SetButtonTransitions(_btnClose);

            // Bind ScrollRect events for mobile drag-to-paginate
            var scrollObj = t.Find("ListScrollRect");
            if (scrollObj != null)
            {
                var scrollRect = scrollObj.GetComponent<ScrollRect>();
                if (scrollRect != null)
                {
                    scrollRect.onValueChanged.RemoveAllListeners();
                    scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
                }
            }
        }

        private void OnSelectRow(int row)
        {
            if (row < 0 || row >= _datas.Count) return;
            _curIndex = row;
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            for (int i = 0; i < _rowObjects.Count; i++)
            {
                var go = _rowObjects[i];
                if (go != null)
                {
                    var bg = go.transform.Find("SelectionBg")?.gameObject;
                    if (bg != null)
                    {
                        bg.SetActive(i == _curIndex);
                    }
                }
            }
        }

        private void OnPageUp()
        {
            if (_curPage > 0)
            {
                _curPage--;
                MsgSend_RefreshData(_curPage);
            }
        }

        private void OnPageDown()
        {
            if (_curPage < _maxPage - 1)
            {
                _curPage++;
                MsgSend_RefreshData(_curPage);
            }
        }

        private void OnScrollValueChanged(Vector2 pos)
        {
            if (Time.time - _lastPageChangeTime < 1.5f) return;

            if (pos.y <= -0.08f) // Dragged up past the bottom to load more (lazy loading)
            {
                if (_curPage < _maxPage - 1)
                {
                    _lastPageChangeTime = Time.time;
                    OnPageDown();
                }
            }
        }

        private void ResetScrollPosition(bool toTop)
        {
            var scrollObj = transform.Find("ListScrollRect");
            if (scrollObj != null)
            {
                var scrollRect = scrollObj.GetComponent<ScrollRect>();
                if (scrollRect != null)
                {
                    scrollRect.normalizedPosition = new Vector2(0f, toTop ? 1f : 0f);
                }
            }
        }

        private void OnClose()
        {
            _curPage = 0;
            gameObject.SetActive(false);
        }

        public void OnRegister()
        {
            if (_processing) return;

            var netMgr = KONetworkManager.Instance;
            if (netMgr != null)
            {
                using var packet = new KOPacketWriter(WizOpcode.WIZ_PARTY_BBS);
                packet.WriteByte(1); // sub-opcode: PARTY_BBS_REGISTER
                packet.WriteByte(0); // SEEKING_PARTY
                netMgr.SendPacket(packet);
                _processing = true;
            }
        }

        private void OnRegisterCancel()
        {
            MsgSend_RegisterCancel();
        }

        public void MsgSend_RefreshData(int page)
        {
            if (_processing) return;

            float time = Time.time;
            if (time - _lastRequestTime < 2.0f) return; // Cooldown to prevent packet spam
            _lastRequestTime = time;

            var netMgr = KONetworkManager.Instance;
            if (netMgr != null)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY_BBS);
                pkt.WriteByte(3); // sub-opcode: PARTY_BBS_NEEDED (request data)
                pkt.WriteInt16((short)page);
                netMgr.SendPacket(pkt);
                _processing = true;
            }
        }

        public void MsgSend_RegisterCancel()
        {
            if (_processing) return;

            var netMgr = KONetworkManager.Instance;
            if (netMgr != null)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY_BBS);
                pkt.WriteByte(2); // sub-opcode: PARTY_BBS_DELETE
                netMgr.SendPacket(pkt);
                _processing = true;
            }
        }

        public void MsgRecv_RefreshData(KOPacketReader pkt)
        {
            _processing = false;

            byte subType = pkt.ReadByte();
            byte result = pkt.ReadByte();

            if (result != 0x01)
            {
                Debug.LogWarning($"[KOPartyBBS] Packet result failed: {result}");
                return;
            }

            switch (subType)
            {
                case 1: // PARTY_BBS_REGISTER
                    PartyStringSet(subType);
                    break;
                case 2: // PARTY_BBS_DELETE
                    PartyStringSet(subType);
                    break;
                case 3: // PARTY_BBS_NEEDED
                    break;
            }

            // Parse entries
            var tempEntries = new List<PartyBBSEntry>();
            for (int i = 0; i < PARTY_BBS_MAXLINE; i++)
            {
                string name = pkt.ReadKOString();
                byte level = pkt.ReadByte();
                short charClass = pkt.ReadInt16();

                if (!string.IsNullOrEmpty(name))
                {
                    tempEntries.Add(new PartyBBSEntry
                    {
                        Name = name,
                        Level = level,
                        Class = charClass
                    });
                }
            }

            short sPage = pkt.ReadInt16();
            short sTotal = pkt.ReadInt16();

            _curPage = sPage;
            _maxPage = sTotal / PARTY_BBS_MAXLINE;
            if ((sTotal % PARTY_BBS_MAXLINE) > 0)
            {
                _maxPage++;
            }

            // Lazy load pagination: clear list only if page 0 (fresh refresh)
            if (_curPage == 0)
            {
                _datas.Clear();
                ResetScrollPosition(true);
            }

            // Append new entries uniquely
            foreach (var entry in tempEntries)
            {
                if (!_datas.Exists(e => e.Name == entry.Name))
                {
                    _datas.Add(entry);
                }
            }

            RefreshPage();
        }

        private void PartyStringSet(byte subType)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (subType == 1) // Registered
            {
                gm.RecruitParty = true;
                KOUIManager.Instance?.AddMsgOutput("Registered seeking party status.", KOUIManager.D3DColorToUnity(0xff00ff00));

                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
                if (player != null)
                {
                    var fn = player.GetComponent<EntropyOnline.World.FloatingName>();
                    if (fn != null)
                    {
                        int level = gm.Level;
                        int iLMin = Mathf.Min(level - 8, (int)(level / 1.5f));
                        if (iLMin < 1) iLMin = 1;
                        int iLMax = Mathf.Max(level + 8, (int)(level * 1.5f));
                        if (iLMax > 80) iLMax = 80;
                        string szMsg = $"Seeking Party : Level {iLMin} ~ {iLMax}";
                        fn.SetInfoText(szMsg, new Color(0f, 1f, 0f, 1f));
                    }
                }
            }
            else if (subType == 2) // Cancelled
            {
                gm.RecruitParty = false;
                KOUIManager.Instance?.AddMsgOutput("Seeking party status cancelled.", KOUIManager.D3DColorToUnity(0xffffff00));

                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
                if (player != null)
                {
                    var fn = player.GetComponent<EntropyOnline.World.FloatingName>();
                    if (fn != null)
                    {
                        fn.SetInfoText("", Color.white);
                    }
                }
            }
        }

        private void RefreshPage()
        {
            // Destroy existing rows
            foreach (var go in _rowObjects)
            {
                if (go != null) Destroy(go);
            }
            _rowObjects.Clear();

            // Instantiate dynamic rows for all entries in the continuous list
            for (int i = 0; i < _datas.Count; i++)
            {
                var entry = _datas[i];
                var rowGo = CreateRowObject(i, entry.Name, entry.Level, entry.Class, false);
                if (rowGo != null)
                {
                    _rowObjects.Add(rowGo);
                }
            }

            // Fill remaining space with empty grid placeholder rows to match the reference look
            int minVisibleRows = 12;
            if (_datas.Count < minVisibleRows)
            {
                for (int i = _datas.Count; i < minVisibleRows; i++)
                {
                    var rowGo = CreateRowObject(i, "", 0, 0, true);
                    if (rowGo != null)
                    {
                        _rowObjects.Add(rowGo);
                    }
                }
            }

            // Adjust content transform height to cover all rows
            var contentTrans = GetScrollContent();
            if (contentTrans != null)
            {
                float height = Mathf.Max(336f, _rowObjects.Count * 28f);
                contentTrans.sizeDelta = new Vector2(318f, height);
            }

            UpdateSelectionHighlight();

            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.RefreshPartyBBSButtons(gameObject);
            }
        }

        private RectTransform GetScrollContent()
        {
            var contentTrans = transform.Find("ListScrollRect/Viewport/Content");
            return contentTrans != null ? contentTrans.GetComponent<RectTransform>() : null;
        }

        private GameObject CreateRowObject(int index, string name, int level, short charClass, bool isPlaceholder)
        {
            var content = GetScrollContent();
            if (content == null) return null;

            var rowObj = new GameObject($"Row_{index}", typeof(RectTransform));
            rowObj.transform.SetParent(content, false);
            var rtRow = rowObj.GetComponent<RectTransform>();
            rtRow.anchorMin = new Vector2(0.5f, 1f);
            rtRow.anchorMax = new Vector2(0.5f, 1f);
            rtRow.pivot = new Vector2(0.5f, 0.5f);
            rtRow.anchoredPosition = new Vector2(0f, -14f - (index * 28f));
            rtRow.sizeDelta = new Vector2(318f, 24f);

            if (!isPlaceholder)
            {
                var btn = rowObj.AddComponent<Button>();
                int rowIdx = index;
                btn.onClick.AddListener(() => OnSelectRow(rowIdx));

                // Selection Highlight Image (behind texts)
                var bgObj = new GameObject("SelectionBg", typeof(RectTransform));
                bgObj.transform.SetParent(rowObj.transform, false);
                bgObj.transform.SetAsFirstSibling();
                var rtBg = bgObj.GetComponent<RectTransform>();
                rtBg.anchorMin = Vector2.zero;
                rtBg.anchorMax = Vector2.one;
                rtBg.offsetMin = Vector2.zero;
                rtBg.offsetMax = Vector2.zero;
                var bgImg = bgObj.AddComponent<Image>();
                bgImg.color = new Color(0.6f, 0.48f, 0.22f, 0.25f); 
                bgObj.SetActive(false);
            }

            // Excel-style lines (subtle dark borders)
            var lineColor = new Color(0.3f, 0.25f, 0.2f, 0.25f); // subtle gold-charcoal

            // Horizontal bottom line
            var hLine = new GameObject("Grid_HLine", typeof(RectTransform));
            hLine.transform.SetParent(rowObj.transform, false);
            var rtHLine = hLine.GetComponent<RectTransform>();
            rtHLine.anchorMin = new Vector2(0f, 0f);
            rtHLine.anchorMax = new Vector2(1f, 0f);
            rtHLine.pivot = new Vector2(0.5f, 0f);
            rtHLine.anchoredPosition = Vector2.zero;
            rtHLine.sizeDelta = new Vector2(0f, 1f);
            var hLineImg = hLine.AddComponent<Image>();
            hLineImg.color = lineColor;

            // Vertical line 1 (between Name and Class)
            var vLine1 = new GameObject("Grid_VLine1", typeof(RectTransform));
            vLine1.transform.SetParent(rowObj.transform, false);
            var rtVLine1 = vLine1.GetComponent<RectTransform>();
            rtVLine1.anchorMin = new Vector2(0.5f, 0.5f);
            rtVLine1.anchorMax = new Vector2(0.5f, 0.5f);
            rtVLine1.pivot = new Vector2(0.5f, 0.5f);
            rtVLine1.anchoredPosition = new Vector2(-4f, 0f);
            rtVLine1.sizeDelta = new Vector2(1f, 28f);
            var vLine1Img = vLine1.AddComponent<Image>();
            vLine1Img.color = lineColor;

            // Vertical line 2 (between Class and Level)
            var vLine2 = new GameObject("Grid_VLine2", typeof(RectTransform));
            vLine2.transform.SetParent(rowObj.transform, false);
            var rtVLine2 = vLine2.GetComponent<RectTransform>();
            rtVLine2.anchorMin = new Vector2(0.5f, 0.5f);
            rtVLine2.anchorMax = new Vector2(0.5f, 0.5f);
            rtVLine2.pivot = new Vector2(0.5f, 0.5f);
            rtVLine2.anchoredPosition = new Vector2(76f, 0f);
            rtVLine2.sizeDelta = new Vector2(1f, 28f);
            var vLine2Img = vLine2.AddComponent<Image>();
            vLine2Img.color = lineColor;

            // Left boundary line
            var leftLine = new GameObject("Grid_LeftLine", typeof(RectTransform));
            leftLine.transform.SetParent(rowObj.transform, false);
            var rtLeftLine = leftLine.GetComponent<RectTransform>();
            rtLeftLine.anchorMin = new Vector2(0.5f, 0.5f);
            rtLeftLine.anchorMax = new Vector2(0.5f, 0.5f);
            rtLeftLine.pivot = new Vector2(0.5f, 0.5f);
            rtLeftLine.anchoredPosition = new Vector2(-159f, 0f);
            rtLeftLine.sizeDelta = new Vector2(1f, 28f);
            var leftLineImg = leftLine.AddComponent<Image>();
            leftLineImg.color = lineColor;

            // Right boundary line
            var rightLine = new GameObject("Grid_RightLine", typeof(RectTransform));
            rightLine.transform.SetParent(rowObj.transform, false);
            var rtRightLine = rightLine.GetComponent<RectTransform>();
            rtRightLine.anchorMin = new Vector2(0.5f, 0.5f);
            rtRightLine.anchorMax = new Vector2(0.5f, 0.5f);
            rtRightLine.pivot = new Vector2(0.5f, 0.5f);
            rtRightLine.anchoredPosition = new Vector2(159f, 0f);
            rtRightLine.sizeDelta = new Vector2(1f, 28f);
            var rightLineImg = rightLine.AddComponent<Image>();
            rightLineImg.color = lineColor;

            if (!isPlaceholder)
            {
                // Name
                var nameObj = new GameObject("Name", typeof(RectTransform));
                nameObj.transform.SetParent(rowObj.transform, false);
                var rtName = nameObj.GetComponent<RectTransform>();
                rtName.anchorMin = new Vector2(0.5f, 0.5f);
                rtName.anchorMax = new Vector2(0.5f, 0.5f);
                rtName.pivot = new Vector2(0.5f, 0.5f);
                rtName.anchoredPosition = new Vector2(-80f, 0f);
                rtName.sizeDelta = new Vector2(140f, 20f);
                var txtName = nameObj.AddComponent<Text>();
                txtName.text = name;
                txtName.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txtName.alignment = TextAnchor.MiddleCenter;
                txtName.color = Color.white;
                txtName.fontStyle = FontStyle.Bold;
                txtName.fontSize = 11;
                var shName = nameObj.AddComponent<Shadow>();
                shName.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shName.effectDistance = new Vector2(1f, -1f);

                // Class
                var classObj = new GameObject("Class", typeof(RectTransform));
                classObj.transform.SetParent(rowObj.transform, false);
                var rtClass = classObj.GetComponent<RectTransform>();
                rtClass.anchorMin = new Vector2(0.5f, 0.5f);
                rtClass.anchorMax = new Vector2(0.5f, 0.5f);
                rtClass.pivot = new Vector2(0.5f, 0.5f);
                rtClass.anchoredPosition = new Vector2(35f, 0f);
                rtClass.sizeDelta = new Vector2(70f, 20f);
                var txtClass = classObj.AddComponent<Text>();

                // Get base class name (Warrior, Rogue, Mage, Priest)
                string baseClassName = "Unknown";
                int baseIndex = charClass;
                if (baseIndex > 200) baseIndex -= 200;
                else if (baseIndex > 100) baseIndex -= 100;

                if (baseIndex == 1 || baseIndex == 5 || baseIndex == 6) baseClassName = "Warrior";
                else if (baseIndex == 2 || baseIndex == 7 || baseIndex == 8) baseClassName = "Rogue";
                else if (baseIndex == 3 || baseIndex == 9 || baseIndex == 10) baseClassName = "Mage";
                else if (baseIndex == 4 || baseIndex == 11 || baseIndex == 12) baseClassName = "Priest";

                txtClass.text = baseClassName;
                txtClass.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txtClass.alignment = TextAnchor.MiddleCenter;
                txtClass.color = new Color(0.9f, 0.75f, 0.55f, 1f);
                txtClass.fontStyle = FontStyle.Bold;
                txtClass.fontSize = 11;
                var shClass = classObj.AddComponent<Shadow>();
                shClass.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shClass.effectDistance = new Vector2(1f, -1f);

                // Level
                var lvlObj = new GameObject("Level", typeof(RectTransform));
                lvlObj.transform.SetParent(rowObj.transform, false);
                var rtLvl = lvlObj.GetComponent<RectTransform>();
                rtLvl.anchorMin = new Vector2(0.5f, 0.5f);
                rtLvl.anchorMax = new Vector2(0.5f, 0.5f);
                rtLvl.pivot = new Vector2(0.5f, 0.5f);
                rtLvl.anchoredPosition = new Vector2(115f, 0f);
                rtLvl.sizeDelta = new Vector2(70f, 20f);
                var txtLvl = lvlObj.AddComponent<Text>();
                txtLvl.text = level.ToString();
                txtLvl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txtLvl.alignment = TextAnchor.MiddleCenter;
                txtLvl.color = new Color(0.6f, 0.85f, 1f, 1f);
                txtLvl.fontStyle = FontStyle.Bold;
                txtLvl.fontSize = 11;
                var shLvl = lvlObj.AddComponent<Shadow>();
                shLvl.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shLvl.effectDistance = new Vector2(1f, -1f);
            }

            return rowObj;
        }

        private void RequestWhisper()
        {
            if (_curIndex < 0 || _curIndex >= _datas.Count) return;

            var targetName = _datas[_curIndex].Name;
            var myName = GameManager.Instance?.CharacterName;

            if (!string.Equals(targetName, myName, System.StringComparison.OrdinalIgnoreCase))
            {
                KONetworkManager.Instance?.SendChatSelectTarget(targetName);
            }
        }

        private void RequestParty()
        {
            if (_curIndex < 0 || _curIndex >= _datas.Count) return;

            var targetName = _datas[_curIndex].Name;
            var myName = GameManager.Instance?.CharacterName;

            if (!string.Equals(targetName, myName, System.StringComparison.OrdinalIgnoreCase))
            {
                var netMgr = KONetworkManager.Instance;
                if (netMgr != null)
                {
                    using var pkt = new KOPacketWriter(WizOpcode.WIZ_PARTY);
                    bool inParty = KOPartyManager.Instance != null && KOPartyManager.Instance.MemberCount >= 2;

                    if (!inParty && KOPartyManager.Instance != null && GameManager.Instance != null)
                    {
                        KOPartyManager.Instance.LeaderId = GameManager.Instance.CharacterId;
                    }

                    pkt.WriteByte(inParty ? (byte)0x03 : (byte)0x01);
                    pkt.WriteKOString(targetName);
                    netMgr.SendPacket(pkt);

                    KOUIManager.Instance?.AddMsgOutput($"Inviting {targetName} into the party...", KOUIManager.D3DColorToUnity(0xffffff00));
                }
            }
        }

        private void SetButtonTransitions(Button btn)
        {
            if (btn == null) return;
            if (btn.gameObject.GetComponent<UIButtonScaleFeedback>() == null)
            {
                btn.gameObject.AddComponent<UIButtonScaleFeedback>();
            }
        }
    }

    public class UIButtonScaleFeedback : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        private Vector3 _originalScale = Vector3.one;

        private void Start()
        {
            _originalScale = transform.localScale;
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            transform.localScale = _originalScale * 0.92f;
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }

        private void OnDisable()
        {
            transform.localScale = _originalScale;
        }
    }
}
