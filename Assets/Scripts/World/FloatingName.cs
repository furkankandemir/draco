using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Character;
using EntropyOnline.UI;
using EntropyOnline.Core;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO birebir: CPlayerBase::Render() — PlayerBase.cpp satır 842-902
    ///
    /// Karakter başının üstünde metin katmanları render eder.
    /// Katman sırası (aşağıdan yukarıya, C++ birebir):
    ///   1. m_pIDFont      → Oyuncu/NPC/Monster ismi (en altta, başın üstünde)
    ///   2. m_pClanFont    → Clan/Knights ismi (ismin üstünde, KNIGHTS_FONT_COLOR = 0xffff0000)
    ///   3. m_pInfoFont    → Bilgi metni ("Parti arıyor" vb.) — henüz implemente edilmedi
    ///   4. m_pBalloonFont → Chat balonu (en üstte, D3DFONT_BOLD, fade-out)
    ///
    /// C++ rendering mantığı:
    ///   pt = başın 2D ekran pozisyonu
    ///   pt.y -= size.cy + 5  → her katman yukarıya kaydırılır
    ///   DrawText 3 kez: siyah shadow (-1,-1), siyah shadow (+1,+1), renkli (0,0)
    ///
    /// Unity'de World Space Canvas + dinamik anchoredPosition ile aynı sonuç.
    /// </summary>
    public class FloatingName : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _canvasRT;

        // Open-KO birebir font pointer karşılıkları (PlayerBase.h:96-99)
        private Text _nameText;        // m_pIDFont
        private Text _clanText;        // m_pClanFont
        private Text _infoText;        // m_pInfoFont
        private Text _chatText;        // m_pBalloonFont

        // Party leader icon
        private Image _leaderIcon;
        private RectTransform _leaderIconRT;
        private bool _wasLeaderState = false;

        // Clan leader underline
        private Image _clanLeaderLine;
        private RectTransform _clanLeaderLineRT;
        private byte _knightsDuty = 0;
        private const byte KNIGHTS_DUTY_CHIEF = 1;

        // RectTransform'lar — pozisyon güncellemesi için cache
        private RectTransform _nameRT;
        private RectTransform _clanRT;
        private RectTransform _infoRT;
        private RectTransform _chatRT;

        // Chat balloon timer — C++ m_fTimeBalloon
        private float _chatHideTime;

        // C++ birebir: her katman arası boşluk = 5 piksel (cpp:857,868,878,898)
        private const float LAYER_SPACING = 5f;
        // C++ birebir: font size 12 → text yüksekliği ~14px (size.cy)
        // Unity'de fontSize 24 → yaklaşık 28px yükseklik
        private const float NAME_HEIGHT = 28f;
        // Clan ve balloon daha küçük font (fontSize 20 → ~24px)
        private const float SMALL_HEIGHT = 24f;

        /// <summary>
        /// Floating isim component'ını başlat.
        /// C++ birebir: CPlayerBase::SetSoundAndInitFont() — PlayerBase.cpp:150-191
        /// </summary>
        public void Initialize(string entityName, bool isNpc, float heightOffset = 2.5f, float localOffsetX = 0f, float localOffsetZ = 0f)
        {
            // World Space Canvas — 3D dünyada metin render etmek için
            var canvasObj = new GameObject("NameCanvas");
            canvasObj.transform.SetParent(transform, false);

            float parentScale = transform.lossyScale.y;
            if (parentScale < 0.01f) parentScale = 1f;

            float offsetX = localOffsetX;
            float offsetZ = localOffsetZ;
            // If custom offsets are not provided, fall back to CapsuleCollider center
            if (Mathf.Abs(offsetX) < 0.001f && Mathf.Abs(offsetZ) < 0.001f)
            {
                var col = GetComponent<CapsuleCollider>();
                if (col != null)
                {
                    offsetX = col.center.x;
                    offsetZ = col.center.z;
                }
            }
            canvasObj.transform.localPosition = new Vector3(offsetX, heightOffset / parentScale, offsetZ);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            _canvasRT = canvasObj.GetComponent<RectTransform>();
            // Canvas genişliği sabit, yüksekliği katman sayısına göre ayarlanır
            _canvasRT.sizeDelta = new Vector2(600f, 200f);
            _canvasRT.localScale = Vector3.one * (0.01f / parentScale);
            // Pivot: alt orta — canvas başın üstünden yukarıya doğru büyür
            _canvasRT.pivot = new Vector2(0.5f, 0f);

            // Layer 1: İsim (m_pIDFont) — en altta, başın hemen üstünde
            _nameText = CreateTextElement("NameText", 24, FontStyle.Bold);
            _nameText.text = entityName;
            _nameRT = _nameText.GetComponent<RectTransform>();

            // Renk: NPC=sarı-yeşil, Monster=kırmızı
            // C++ birebir: m_InfoBase.crID — IDSet() ile ayarlanır (PlayerBase.cpp:287-296)
            if (isNpc)
                _nameText.color = new Color(0.9f, 0.85f, 0.3f, 1f);
            else
                _nameText.color = new Color(1f, 0.3f, 0.25f, 1f);

            if (!isNpc)
            {
                CreateLeaderIconElement();
            }

            // İlk pozisyon hesapla
            RecalculatePositions();
        }

        public void SetNameColor(Color color)
        {
            if (_nameText != null)
                _nameText.color = color;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerOther::KnightsInfoSet() — PlayerOther.cpp:204-233
        /// + CPlayerOther::SetSoundAndInitFont() — PlayerOther.cpp:235-258
        /// + CPlayerMySelf::KnightsInfoSet() — PlayerMySelf.cpp:993-1018
        ///
        /// Clan adını ismin ÜSTÜNDE gösterir.
        /// C++ birebir:
        ///   m_pClanFont = new CDFont(szFontID, 12, D3DFONT_BOLD)
        ///   m_pClanFont->SetText(m_InfoExt.szKnights)
        ///   m_pClanFont->SetFontColor(KNIGHTS_FONT_COLOR) // 0xffff0000 = kırmızı
        /// </summary>
        public void SetKnightsDuty(byte duty)
        {
            _knightsDuty = duty;
            UpdateClanLeaderLineVisibility();
        }

        private void UpdateClanLeaderLineVisibility()
        {
            bool showLine = (_knightsDuty == KNIGHTS_DUTY_CHIEF) && (_clanText != null && _clanText.gameObject.activeSelf);
            if (showLine)
            {
                if (_clanLeaderLine == null)
                {
                    CreateClanLeaderLineElement();
                }
                _clanLeaderLine.gameObject.SetActive(true);
            }
            else
            {
                if (_clanLeaderLine != null)
                {
                    _clanLeaderLine.gameObject.SetActive(false);
                }
            }
            RecalculatePositions();
        }

        private void CreateClanLeaderLineElement()
        {
            var obj = new GameObject("ClanLeaderLine");
            obj.transform.SetParent(_canvas.transform, false);

            _clanLeaderLine = obj.AddComponent<Image>();
            _clanLeaderLine.color = new Color(0f, 1f, 0f, 1f);
            _clanLeaderLine.raycastTarget = false;

            _clanLeaderLineRT = obj.GetComponent<RectTransform>();
            _clanLeaderLineRT.anchorMin = new Vector2(0.5f, 0f);
            _clanLeaderLineRT.anchorMax = new Vector2(0.5f, 0f);
            _clanLeaderLineRT.pivot = new Vector2(0.5f, 0f);
            _clanLeaderLineRT.sizeDelta = new Vector2(100f, 1.5f);
        }

        public void SetClanName(string clanName)
        {
            // C++ birebir: if szKnights.empty() → delete m_pClanFont; m_pClanFont = nullptr;
            if (string.IsNullOrEmpty(clanName))
            {
                if (_clanText != null)
                {
                    _clanText.gameObject.SetActive(false);
                    UpdateClanLeaderLineVisibility();
                }
                return;
            }

            if (_canvas == null) return;

            if (_clanText == null)
            {
                // C++ birebir: m_pClanFont = new CDFont(szFontID, 12, D3DFONT_BOLD)
                _clanText = CreateTextElement("ClanText", 24, FontStyle.Bold);
                _clanRT = _clanText.GetComponent<RectTransform>();

                // Open-KO birebir: KNIGHTS_FONT_COLOR = 0xffff0000 (GameDef.h:1415)
                _clanText.color = new Color(1f, 0f, 0f, 1f);
            }

            // C++ birebir: m_pClanFont->SetText(m_InfoExt.szKnights)
            _clanText.text = clanName;
            _clanText.gameObject.SetActive(true);
            UpdateClanLeaderLineVisibility();
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::BalloonStringSet() — PlayerBase.cpp:262-285
        ///
        /// C++ birebir:
        ///   void BalloonStringSet(const std::string& szBalloon, D3DCOLOR crFont)
        ///   m_pBalloonFont->SetText(szBalloon)
        ///   m_pBalloonFont->SetFontColor(crFont)
        ///   m_fTimeBalloon = szBalloon.size() * 0.2f   (cpp:281)
        ///   Render: son 2 saniyede fade-out  (cpp:887-902)
        /// </summary>
        public void ShowChatBubble(string message, Color balloonColor, bool persistent = false)
        {
            if (_canvas == null) return;

            if (string.IsNullOrEmpty(message))
            {
                // C++ birebir: BalloonStringSet("", 0) — balon temizlenir (cpp:264-269)
                if (_chatText != null)
                {
                    _chatText.gameObject.SetActive(false);
                    RecalculatePositions();
                }
                return;
            }

            if (_chatText == null)
            {
                // C++ birebir: m_pBalloonFont = new CDFont(szFontID, 12)
                _chatText = CreateTextElement("ChatBubble", 20, FontStyle.Bold);
                _chatRT = _chatText.GetComponent<RectTransform>();
            }

            // C++ birebir: m_fTimeBalloon = szBalloon.size() * 0.2f (cpp:281)
            float duration = message.Length * 0.2f;
            if (duration < 3f) duration = 3f;

            _chatText.text = message;
            // C++ birebir: m_pBalloonFont->SetFontColor(crFont) (cpp:284)
            _chatText.color = balloonColor;
            _chatText.gameObject.SetActive(true);
            _chatHideTime = persistent ? float.MaxValue : (Time.time + duration);
            RecalculatePositions();
        }

        /// <summary>
        /// Open-KO: InfoStringSet(const std::string& szInfo, D3DCOLOR crFont) — PlayerBase.cpp:241-260
        /// </summary>
        public void SetInfoText(string infoText, Color color)
        {
            if (string.IsNullOrEmpty(infoText))
            {
                if (_infoText != null)
                {
                    _infoText.gameObject.SetActive(false);
                    RecalculatePositions();
                }
                return;
            }

            if (_canvas == null) return;

            if (_infoText == null)
            {
                _infoText = CreateTextElement("InfoText", 20, FontStyle.Bold);
                _infoRT = _infoText.GetComponent<RectTransform>();
            }

            _infoText.text = infoText;
            _infoText.color = color;
            _infoText.gameObject.SetActive(true);
            RecalculatePositions();
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::Render() — PlayerBase.cpp:842-902
        ///
        /// C++ rendering mantığını Unity'de yeniden üretir:
        ///   pt = başın 2D pozisyonu
        ///   pt.y -= size.cy + 5  → Layer 1: İsim
        ///   pt.y -= size.cy + 5  → Layer 2: Clan (varsa)
        ///   pt.y -= size.cy + 5  → Layer 3: Info (varsa — henüz yok)
        ///   pt.y -= size.cy + 5  → Layer 4: Balon (varsa)
        ///
        /// Unity'de: anchoredPosition.y ile aşağıdan yukarıya istifleme.
        /// Canvas pivot (0.5, 0) olduğu için y=0 başın tam üstü.
        /// </summary>
        private void RecalculatePositions()
        {
            float y = 0f;

            // Balon varsa: en altta (başın hemen üstünde), diğerleri yukarı kayar
            if (_chatText != null && _chatText.gameObject.activeSelf && _chatRT != null)
            {
                _chatRT.anchoredPosition = new Vector2(0, y);
                y += SMALL_HEIGHT + LAYER_SPACING;
            }

            // İsim — balonun üstünde (veya balon yoksa en altta)
            if (_nameRT != null)
            {
                _nameRT.anchoredPosition = new Vector2(0, y);

                // Position the leader icon to the left of the name text if active
                if (_leaderIconRT != null && _leaderIconRT.gameObject.activeSelf)
                {
                    float textWidth = _nameText.preferredWidth;
                    float iconSize = _leaderIconRT.sizeDelta.x;
                    float spacing = 6f; // spacing between icon and name text
                    _leaderIconRT.anchoredPosition = new Vector2(-textWidth / 2f - iconSize / 2f - spacing, y);
                }

                // Clan leader green underline under the player's name
                if (_clanLeaderLineRT != null && _clanLeaderLineRT.gameObject.activeSelf)
                {
                    float nameWidth = _nameText.preferredWidth;
                    _clanLeaderLineRT.sizeDelta = new Vector2(nameWidth, 2f);
                    _clanLeaderLineRT.anchoredPosition = new Vector2(0f, y - 4f);
                }

                y += NAME_HEIGHT + LAYER_SPACING;
            }

            // Clan — ismin üstünde
            if (_clanText != null && _clanText.gameObject.activeSelf && _clanRT != null)
            {
                _clanRT.anchoredPosition = new Vector2(0, y);
                y += NAME_HEIGHT + LAYER_SPACING;
            }

            // Info — Clan'ın üstünde
            if (_infoText != null && _infoText.gameObject.activeSelf && _infoRT != null)
            {
                _infoRT.anchoredPosition = new Vector2(0, y);
            }
        }

        private void LateUpdate()
        {
            if (_canvas == null) return;

            // Billboard: her zaman kameraya bak
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;
            _canvas.transform.rotation = cam.transform.rotation;

            // Adjust scale dynamically based on distance to the camera to maintain constant screen size (Method A)
            float distance = Vector3.Distance(_canvas.transform.position, cam.transform.position);
            float scale = 0.01f * (distance / 9.0f);
            
            // Parent scale'e bölerek dünya ölçeğini sabit tut (özellikle ölçeklenmiş canavarlar için isim boyutunu sabitler)
            float parentScale = transform.lossyScale.y;
            if (parentScale > 0.01f)
            {
                _canvas.transform.localScale = Vector3.one * (scale / parentScale);
            }
            else
            {
                _canvas.transform.localScale = Vector3.one * scale;
            }

            // Open-KO birebir: PlayerBase.cpp Tick() satır 736-744 + Render() satır 887-902
            // Chat balonu timeout ve fade-out
            if (_chatText != null && _chatText.gameObject.activeSelf)
            {
                if (_chatHideTime < float.MaxValue - 1000f) // Not persistent
                {
                    float timeRemaining = _chatHideTime - Time.time;
                    if (timeRemaining <= 0)
                    {
                        // C++ birebir: m_fTimeBalloon < 0 → BalloonStringSet("", 0) (cpp:741-742)
                        _chatText.gameObject.SetActive(false);
                        RecalculatePositions();
                    }
                    else if (timeRemaining < 2.0f)
                    {
                        // C++ birebir: m_fTimeBalloon < 2.0f → alpha fade-out (cpp:890-894)
                        float alpha = timeRemaining / 2.0f;
                        Color c = _chatText.color;
                        c.a = alpha;
                        _chatText.color = c;
                    }
                }
                else
                {
                    // Persistent: reset alpha in case it was faded
                    Color c = _chatText.color;
                    if (c.a < 1.0f)
                    {
                        c.a = 1.0f;
                        _chatText.color = c;
                    }
                }
            }

            UpdateNameColorForParty();
        }

        private void UpdateNameColorForParty()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            bool isLeader = false;

            // Check if this is the local player themselves (attached to PlayerController)
            var localPlayer = GetComponentInParent<PlayerController>();
            if (localPlayer != null)
            {
                bool inParty = KOPartyManager.Instance != null && KOPartyManager.Instance.MemberCount > 0;
                if (inParty)
                {
                    SetNameColor(Color.yellow);
                    isLeader = KOPartyManager.Instance.LeaderId == gm.CharacterId;
                }
                else
                {
                    // Default local player name color: light blue/cyan
                    SetNameColor(new Color(100f / 255f, 210f / 255f, 1f, 1f));
                }
            }
            else
            {
                // Check if this is a remote player (attached to RemotePlayerEntity)
                var remotePlayer = GetComponentInParent<RemotePlayerEntity>();
                if (remotePlayer != null)
                {
                    bool inParty = false;
                    if (KOPartyManager.Instance != null)
                    {
                        for (int i = 0; i < KOPartyManager.Instance.Members.Count; i++)
                        {
                            if (KOPartyManager.Instance.Members[i].CharacterId == remotePlayer.CharId)
                            {
                                inParty = true;
                                break;
                            }
                        }
                    }

                    if (inParty)
                    {
                        SetNameColor(Color.yellow);
                        isLeader = KOPartyManager.Instance.LeaderId == remotePlayer.CharId;
                    }
                    else
                    {
                        // Revert to default nation color
                        bool isEnemy = remotePlayer.Nation != gm.Nation;
                        Color defaultColor = isEnemy 
                            ? new Color(255f / 255f, 96f / 255f, 96f / 255f, 1f)
                            : new Color(128f / 255f, 128f / 255f, 1f, 1f);
                        SetNameColor(defaultColor);
                    }
                }
            }

            // Update leader icon visibility and positions if status changed
            if (_leaderIconRT != null)
            {
                if (isLeader != _wasLeaderState)
                {
                    _leaderIconRT.gameObject.SetActive(isLeader);
                    _wasLeaderState = isLeader;
                    RecalculatePositions();
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: PlayerOtherMgr::Tick() — SOUND_RANGE kontrolü.
        /// Mesafe dışındaki entity'lerin fontlarını gizler.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(visible);
        }

        private void CreateLeaderIconElement()
        {
            var obj = new GameObject("LeaderIcon");
            obj.transform.SetParent(_canvas.transform, false);

            // Add a solid black background
            var bgImage = obj.AddComponent<Image>();
            bgImage.color = Color.black;
            bgImage.raycastTarget = false;

            // Add a child GameObject for the swirl icon itself
            var iconObj = new GameObject("SwirlIcon");
            iconObj.transform.SetParent(obj.transform, false);

            _leaderIcon = iconObj.AddComponent<Image>();
            
            // Load the leader icon from Resources
            Sprite leaderSprite = Resources.Load<Sprite>("UI/party_leader_icon");
            if (leaderSprite != null)
            {
                _leaderIcon.sprite = leaderSprite;
            }
            else
            {
                Debug.LogWarning("[FloatingName] party_leader_icon could not be loaded from Resources/UI/!");
            }

            // Set dynamic color: Golden yellow-orange
            _leaderIcon.color = new Color(1f, 0.75f, 0f, 1f); 
            _leaderIcon.raycastTarget = false;

            _leaderIconRT = obj.GetComponent<RectTransform>();
            _leaderIconRT.anchorMin = new Vector2(0.5f, 0f);
            _leaderIconRT.anchorMax = new Vector2(0.5f, 0f);
            _leaderIconRT.pivot = new Vector2(0.5f, 0f);
            // Size of the icon frame
            _leaderIconRT.sizeDelta = new Vector2(26f, 26f);

            // Position and scale the child swirl icon slightly smaller for a clean black border margin
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(2f, 2f);
            iconRt.offsetMax = new Vector2(-2f, -2f);

            obj.SetActive(false); // Hidden by default
        }

        private static Material s_alwaysOnTopMaterial;
        private static Material GetAlwaysOnTopMaterial()
        {
            if (s_alwaysOnTopMaterial == null)
            {
                Shader shader = Shader.Find("UI/Default");
                if (shader != null)
                {
                    s_alwaysOnTopMaterial = new Material(shader);
                    s_alwaysOnTopMaterial.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                }
            }
            return s_alwaysOnTopMaterial;
        }

        /// <summary>
        /// Yardımcı: Text elementi oluşturur.
        /// C++ birebir: CDFont oluşturma + DrawText 3 kez (siyah shadow + renkli)
        /// Unity'de: Text component + Outline component ile aynı sonuç.
        /// </summary>
        private Text CreateTextElement(string name, int fontSize, FontStyle fontStyle)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(_canvas.transform, false);

            var text = obj.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;

            // Open-KO birebir: Always On Top (3D modellerin arkasında kalmaz, her zaman en önde çizilir)
            Material mat = GetAlwaysOnTopMaterial();
            if (mat != null)
            {
                text.material = mat;
            }

            // Font yükleme
            text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Türkçe karakter desteği: ğüşöçıİĞÜŞÖÇ için font texture'a karakter request et
            if (text.font != null)
                text.font.RequestCharactersInTexture("ğüşöçıİĞÜŞÖÇabcdefghijklmnopqrstuvwxyz0123456789 :!@#$%", fontSize, fontStyle);

            // Open-KO birebir: DrawText 3 kez — siyah shadow (-1,-1) ve (+1,+1)
            // Unity'de Outline component ile aynı sonuç
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // RectTransform — merkez pivot, pozisyon RecalculatePositions() ile ayarlanır
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(600f, fontSize + 4f);

            return text;
        }
    }
}
