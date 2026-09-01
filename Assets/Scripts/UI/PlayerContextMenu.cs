using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Entropy Online — Mobil Oyuncu Etkileşim Menüsü
    /// 
    /// KO'da sağ-tık context menü yoktur — parti davet UICmd btn_invite ile,
    /// takas/klan chat komutları ile yapılır. Ama mobilde dokunmatik ekran
    /// olduğundan bu menü mobil adaptasyondur.
    /// 
    /// Packet mantığı Open-KO referanstan:
    /// - Parti: C++ UICmd.cpp satır 123-130 → MsgSend_PartyOrForceCreate
    /// - Takas: C++ GameProcMain.cpp satır 5876-5906 → MsgSend_PerTradeReq
    /// - Klan: C++ GameProcMain.cpp satır 5947-5952 → MsgSend_KnightsJoin
    /// 
    /// UI paneli: Invented ama mobil için gerekli adaptasyon.
    /// Canvas'ı KOUIManager'dan alır, kendi canvas oluşturmaz.
    /// </summary>
    public class PlayerContextMenu : MonoBehaviour
    {
        public static PlayerContextMenu Instance { get; private set; }

        private GameObject _panel;
        private Text _playerNameText;

        private long _targetCharId;
        private string _targetName;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Panel açıkken başka yere tıklayınca kapat
            if (_panel != null && _panel.activeSelf &&
                ((UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame ?? false) ||
                 (UnityEngine.InputSystem.Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ?? false)))
            {
                Invoke(nameof(TryAutoClose), 0.15f);
            }
        }

        private void TryAutoClose()
        {
            if (_panel != null && _panel.activeSelf)
            {
                var rt = _panel.GetComponent<RectTransform>();
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, UnityEngine.InputSystem.Pointer.current?.position.ReadValue() ?? Vector2.zero, null, out localPoint);
                if (!rt.rect.Contains(localPoint))
                {
                    _panel.SetActive(false);
                }
            }
        }

        // ============================
        // KOUIManager Canvas'ına panel oluşturma
        // InitUI sonrası çağrılmalı
        // ============================

        /// <summary>
        /// KOUIManager tarafından canvas hazır olduktan sonra çağrılır.
        /// </summary>
        public void CreatePanel(Transform canvasParent)
        {
            if (_panel != null) return;

            // Panel container — ekran ortasında 200x180 piksel
            _panel = new GameObject("ContextMenuPanel");
            _panel.transform.SetParent(canvasParent, false);
            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200, 180);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.12f, 0.92f);

            // Oyuncu ismi başlık
            _playerNameText = CreateText(_panel.transform, "PlayerName", "Oyuncu",
                new Vector2(0, 140), new Vector2(200, 30),
                14, FontStyle.Bold, new Color(0.9f, 0.8f, 0.3f), TextAnchor.MiddleCenter);

            // Butonlar — KO etkileşim sırasıyla
            float y = 105f;
            float btnH = 28f;
            float gap = 4f;

            // C++ UICmd.cpp satır 123-130: btn_invite → MsgSend_PartyOrForceCreate
            CreateMenuButton(_panel.transform, "PartyInvite", "Partiye Davet",
                y, btnH, new Color(0.2f, 0.4f, 0.6f, 0.8f), OnPartyInvite);
            y -= btnH + gap;

            // C++ GameProcMain.cpp satır 5947-5952: CMD_JOINCLAN → MsgSend_KnightsJoin
            CreateMenuButton(_panel.transform, "ClanInvite", "Klana Davet",
                y, btnH, new Color(0.4f, 0.3f, 0.6f, 0.8f), OnClanInvite);
            y -= btnH + gap;

            // C++ GameProcMain.cpp satır 5876-5906: CMD_TRADE → MsgSend_PerTradeReq
            CreateMenuButton(_panel.transform, "TradeRequest", "Takas",
                y, btnH, new Color(0.5f, 0.4f, 0.2f, 0.8f), OnTradeRequest);
            y -= btnH + gap;

            // C++ GameProcMain.cpp satır 5848-5851: CMD_WHISPER → MsgSend_ChatSelectTarget
            CreateMenuButton(_panel.transform, "Whisper", "Fisilda",
                y, btnH, new Color(0.2f, 0.5f, 0.3f, 0.8f), OnWhisper);

            _panel.SetActive(false);
        }

        // ============================
        // MENÜYÜ AÇ
        // ============================

        /// <summary>
        /// Uzak oyuncuya tıklayınca çağrılır.
        /// </summary>
        public void ShowForPlayer(long charId, string playerName, byte nation)
        {
            _targetCharId = charId;
            _targetName = playerName;

            if (_playerNameText != null)
                _playerNameText.text = playerName;

            if (_panel != null)
                _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // ============================
        // BUTON AKSIYONLARI — C++ referans packet mantığı
        // ============================

        /// <summary>
        /// C++ UICmd.cpp satır 123-130: Parti daveti.
        /// MsgSend_PartyOrForceCreate(pUPC->IDString())
        /// </summary>
        private void OnPartyInvite()
        {
            if (PartyUI.Instance != null && !string.IsNullOrEmpty(_targetName))
            {
                PartyUI.Instance.SendPartyInvite(_targetName);
            }
            Hide();
        }

        /// <summary>
        /// C++ GameProcMain.cpp satır 5947-5952: Klan daveti.
        /// MsgSend_KnightsJoin(s_pPlayer->m_iIDTarget)
        /// </summary>
        private void OnClanInvite()
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_KNIGHTS_PROCESS + sub-opcode join/invite
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_KNIGHTS_PROCESS);
            pkt.WriteByte(1); // sub-opcode: invite
            pkt.WriteInt16((short)_targetCharId);
            netMgr.SendPacket(pkt);

            Hide();
        }

        /// <summary>
        /// C++ GameProcMain.cpp satır 5876-5906: Takas isteği.
        /// MsgSend_PerTradeReq(pOPC->IDNumber())
        /// </summary>
        private void OnTradeRequest()
        {
            if (KONetworkManager.Instance == null) return;
            
            // C++ GameProcMain.cpp satır 5887 birebir:
            // if (s_pPlayer->Nation() != pOPC->Nation() && !s_pPlayer->m_InfoExt.bCanTradeWithOtherNation)
            //     return;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                // Hedef oyuncunun ulusunu EntityManager'dan al
                var em = EntropyOnline.World.EntityManager.Instance;
                if (em != null)
                {
                    var targetPlayer = em.GetRemotePlayer(_targetCharId);
                    if (targetPlayer != null && targetPlayer.Nation != gm.Nation && !gm.CanTradeWithOtherNation)
                    {
                        // C++ birebir: karşı ulus trade engeli — sessizce return
                        Hide();
                        return;
                    }
                }
            }

            // Open-KO birebir: WIZ_EXCHANGE + sub-opcode request via KOTradeManager
            if (EntropyOnline.Trade.KOTradeManager.Instance != null)
            {
                EntropyOnline.Trade.KOTradeManager.Instance.SendExchangeReq((short)_targetCharId);
            }

            Hide();
        }

        /// <summary>
        /// C++ GameProcMain.cpp satır 5848-5851: Fısıltı.
        /// MsgSend_ChatSelectTarget(szCmds[1])
        /// </summary>
        private void OnWhisper()
        {
            KONetworkManager.Instance?.SendChatSelectTarget(_targetName);
            Hide();
        }

        // ============================
        // UI YARDIMCILAR
        // ============================

        private void CreateMenuButton(Transform parent, string name, string label,
            float y, float h, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y + h);
            rt.sizeDelta = new Vector2(-20, h); // 10px padding each side

            var img = obj.AddComponent<Image>();
            img.color = bgColor;

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.3f;
            colors.pressedColor = bgColor * 0.7f;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            CreateText(obj.transform, "Label", label,
                Vector2.zero, new Vector2(0, 0),
                12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, true);
        }

        private Text CreateText(Transform parent, string name, string text,
            Vector2 offset, Vector2 size, int fontSize, FontStyle style,
            Color color, TextAnchor alignment, bool stretch = false)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(offset.x, -offset.y);
                rt.sizeDelta = size;
            }
            var t = obj.AddComponent<Text>();
            t.text = text;
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.raycastTarget = false;
            return t;
        }
    }
}
