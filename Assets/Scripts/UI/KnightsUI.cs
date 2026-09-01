using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Network;
using EntropyOnline.Import;
using EntropyOnline.World;
using System.Collections.Generic;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUIKnightsOperation UI portu (UIKnightsOperation.cpp)
    ///                 + m_pPageKnights (GameProcMain.cpp:6893-6897)
    ///
    /// Knights panelinin UI tarafı — KOKnightsManager event'lerine abone olarak
    /// klan bilgisini, üye listesini ve klan listesini dinamik olarak render eder.
    ///
    /// Open-KO buton mantığı (UIKnightsOperation.cpp:179-199):
    ///   Chief → Destroy:enable, Withdraw:disable, Join:disable
    ///   Diğer → Destroy:disable, Withdraw:enable, Join:enable
    /// </summary>
    public class KnightsUI : MonoBehaviour
    {
        public static KnightsUI Instance { get; private set; }

        // UI root referansları — KOUIManager.InitUI() tarafından atanır
        private Transform _panelRoot;

        // Open-KO birebir: UIKnightsOperation.h:33-39
        private Button _btnCreate;
        private Button _btnDestroy;
        private Button _btnWithdraw;
        private Button _btnJoin;
        private Button _btnClose;
        private Button _btnUp;      // Sayfa yukarı
        private Button _btnDown;    // Sayfa aşağı

        // Dinamik içerik
        private Text _textClanName;
        private Text _textClanInfo;  // Grade, points, member count
        private readonly List<GameObject> _memberListItems = new();
        private readonly List<GameObject> _knightsListItems = new();
        private Transform _memberListContent;
        private Transform _knightsListContent;

        // Davet popup state
        private int _pendingRequierId;
        private int _pendingClanId;
        private string _pendingInviterName;
        private string _pendingClanName;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            // KOKnightsManager event'lerine abone ol
            if (KOKnightsManager.Instance != null)
            {
                KOKnightsManager.Instance.OnKnightsInfoChanged += RefreshClanInfo;
                KOKnightsManager.Instance.OnMemberListUpdated  += RefreshMemberList;
                KOKnightsManager.Instance.OnKnightsListReceived += RefreshKnightsList;
                KOKnightsManager.Instance.OnInviteReceived     += ShowInvitePopup;
                KOKnightsManager.Instance.OnClanResult         += ShowResultMessage;
            }
        }

        private void OnDisable()
        {
            if (KOKnightsManager.Instance != null)
            {
                KOKnightsManager.Instance.OnKnightsInfoChanged -= RefreshClanInfo;
                KOKnightsManager.Instance.OnMemberListUpdated  -= RefreshMemberList;
                KOKnightsManager.Instance.OnKnightsListReceived -= RefreshKnightsList;
                KOKnightsManager.Instance.OnInviteReceived     -= ShowInvitePopup;
                KOKnightsManager.Instance.OnClanResult         -= ShowResultMessage;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================
        // InitPanel — KOUIManager tarafından çağrılır
        // ================================================================

        /// <summary>
        /// Knights UI panelini bağlar.
        /// Open-KO birebir: CUIKnightsOperation::Load() (UIKnightsOperation.cpp:56-73)
        /// </summary>
        public void InitPanel(Transform panelRoot)
        {
            _panelRoot = panelRoot;

            // UIF'deki gerçek buton adlarıyla eşleştir (el_page_knights_us.uif)
            // NOT: C++'da Create/Destroy/Withdraw ayrı bir UIF'den yükleniyor (szKnightsOperation, index 38)
            // Bu UIF projede mevcut değil — butonları runtime'da oluşturuyoruz
            _btnUp       = KOUIRenderer.FindChildButton(panelRoot, "btn_clan_up");
            _btnDown     = KOUIRenderer.FindChildButton(panelRoot, "btn_clan_down");
            _btnClose    = KOUIRenderer.FindChildButton(panelRoot, "btn_close");
            _btnJoin     = KOUIRenderer.FindChildButton(panelRoot, "btn_clan_admit");

            // Create/Destroy/Withdraw butonları bu UIF'de yok — runtime'da oluştur
            _btnCreate   = CreateRuntimeButton(panelRoot, "Btn_Create", "Create");
            _btnDestroy  = CreateRuntimeButton(panelRoot, "Btn_Destroy", "Destroy");
            _btnWithdraw = CreateRuntimeButton(panelRoot, "Btn_Withdraw", "Withdraw");

            // Buton event bağlama — Open-KO birebir: UIKnightsOperation.cpp:75-119
            if (_btnUp != null)
                _btnUp.onClick.AddListener(OnBtnUp);
            if (_btnDown != null)
                _btnDown.onClick.AddListener(OnBtnDown);
            if (_btnClose != null)
                _btnClose.onClick.AddListener(OnBtnClose);
            if (_btnJoin != null)
                _btnJoin.onClick.AddListener(OnBtnJoin);
            if (_btnCreate != null)
                _btnCreate.onClick.AddListener(OnBtnCreate);
            if (_btnDestroy != null)
                _btnDestroy.onClick.AddListener(OnBtnDestroy);
            if (_btnWithdraw != null)
                _btnWithdraw.onClick.AddListener(OnBtnWithdraw);

            // Ek UIF butonları — page_knights_us.uif'e özel
            var btnRefresh = KOUIRenderer.FindChildButton(panelRoot, "btn_clan_refresh");
            if (btnRefresh != null)
                btnRefresh.onClick.AddListener(() => KOKnightsManager.Instance?.MsgSend_MemberInfoAll());

            var btnInvite = KOUIRenderer.FindChildButton(panelRoot, "btn_Invite");
            if (btnInvite != null)
                btnInvite.onClick.AddListener(OnBtnInvite);

            // Dinamik içerik alanları
            _textClanName = KOUIRenderer.FindChildText(panelRoot, "text_knights_name");
            _textClanInfo = KOUIRenderer.FindChildText(panelRoot, "text_knights_info");

            // Liste alanları
            var listKnights = panelRoot.Find("List_Knights");
            if (listKnights != null)
                _knightsListContent = listKnights;

            var listMembers = panelRoot.Find("List_Members");
            if (listMembers != null)
                _memberListContent = listMembers;

        }

        /// <summary>
        /// UIKnightsOperation UIF'i projede mevcut olmadığı için
        /// eksik butonları (Create/Destroy/Withdraw) runtime'da oluşturur.
        /// C++ birebir: UIKnightsOperation.cpp:61-70 — GetChildByID karşılığı
        /// </summary>
        private Button CreateRuntimeButton(Transform parent, string id, string label)
        {
            // Mevcut bir referans buton bul — stili kopyalayacağız
            var refBtn = KOUIRenderer.FindChildButton(parent, "btn_clan_admit");
            
            var btnObj = new GameObject(id);
            btnObj.transform.SetParent(parent, false);

            var rt = btnObj.AddComponent<RectTransform>();
            if (refBtn != null)
            {
                // Referans butonun boyutunu kopyala
                var refRT = refBtn.GetComponent<RectTransform>();
                rt.sizeDelta = refRT.sizeDelta;
            }
            else
            {
                rt.sizeDelta = new Vector2(70, 22);
            }

            // Pozisyonla — panel alt kısmında yan yana
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            float xOffset = label switch
            {
                "Create"   => 5,
                "Destroy"  => 80,
                "Withdraw" => 155,
                _          => 0
            };
            rt.anchoredPosition = new Vector2(xOffset, 5);

            // Arka plan
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.25f, 0.22f, 0.18f, 0.9f);

            // Button component
            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.22f, 0.18f, 0.9f);
            colors.highlightedColor = new Color(0.4f, 0.35f, 0.28f, 1f);
            colors.pressedColor = new Color(0.5f, 0.45f, 0.35f, 1f);
            btn.colors = colors;

            // Label text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 11;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.85f, 0.7f);

            return btn;
        }

        // ================================================================
        // Open — Open-KO birebir: UIKnightsOperation.cpp:201-209
        // ================================================================

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::Open()
        /// UIKnightsOperation.cpp:201-209
        /// </summary>
        public void Open()
        {
            if (_panelRoot == null) return;

            _panelRoot.gameObject.SetActive(true);

            // Open-KO birebir: cpp:203 — m_iPageCur = 0
            // cpp:204 — KnightsListClear()
            // cpp:208 — ChangeUIByDuty(eDuty)
            ChangeUIByDuty();

            // Klan bilgisi yenile
            if (KOKnightsManager.Instance != null && KOKnightsManager.Instance.IsInClan)
            {
                KOKnightsManager.Instance.MsgSend_MemberInfoAll();
            }
            else
            {
                // Klansızsa klan listesini göster
                KOKnightsManager.Instance?.MsgSend_KnightsList(0);
            }

        }

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::Close()
        /// UIKnightsOperation.cpp:211-217
        /// </summary>
        public void Close()
        {
            if (_panelRoot != null)
                _panelRoot.gameObject.SetActive(false);
        }

        // ================================================================
        // ChangeUIByDuty — Open-KO birebir: UIKnightsOperation.cpp:179-199
        // ================================================================

        /// <summary>
        /// Open-KO birebir: CUIKnightsOperation::ChangeUIByDuty()
        /// UIKnightsOperation.cpp:179-199
        ///
        /// Chief ise Destroy açık, Withdraw kapalı, Join kapalı.
        /// Diğerleri ise tam tersi.
        /// </summary>
        private void ChangeUIByDuty()
        {
            if (KOKnightsManager.Instance == null) return;

            // Open-KO birebir: cpp:179-199 — sadece Destroy, Withdraw, Join dokunulur.
            // Create butonu ChangeUIByDuty'de HİÇ dokunulmuyor — sunucu validation yapar.
            var (canDestroy, canWithdraw, canJoin) = KOKnightsManager.Instance.GetUIPermissions();

            if (_btnDestroy != null)
                _btnDestroy.interactable = canDestroy;
            if (_btnWithdraw != null)
                _btnWithdraw.interactable = canWithdraw;
            if (_btnJoin != null)
                _btnJoin.interactable = canJoin;
        }

        // ================================================================
        // Button Handlers — Open-KO birebir: UIKnightsOperation.cpp:75-119
        // ================================================================

        /// <summary>Open-KO birebir: cpp:82-93 — m_pBtn_Up</summary>
        private void OnBtnUp()
        {
            if (KOKnightsManager.Instance == null) return;
            int page = KOKnightsManager.Instance.PageCurrent - 1;
            if (page < 0) return;
            KOKnightsManager.Instance.MsgSend_KnightsList(page);
        }

        /// <summary>Sayfa aşağı — cpp'de m_pBtn_Down karşılığı</summary>
        private void OnBtnDown()
        {
            if (KOKnightsManager.Instance == null) return;
            int page = KOKnightsManager.Instance.PageCurrent + 1;
            KOKnightsManager.Instance.MsgSend_KnightsList(page);
        }

        /// <summary>Open-KO birebir: cpp:94-97 — m_pBtn_Close</summary>
        private void OnBtnClose()
        {
            Close();
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:98-101 → m_pUICreateClanName->Open()
        /// C++ UICreateClanName.cpp:80-91 → MakeClan() → MessageBox onay → MsgSend_KnightsCreate()
        /// </summary>
        private void OnBtnCreate()
        {
            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowInput(
                    "Enter the name of the Knights to create:",  // IDS_CLAN_INPUT_NAME
                    "",
                    MsgBoxBehavior.BEHAVIOR_KNIGHTS_CREATE,
                    (clanName) =>
                    {
                        if (string.IsNullOrWhiteSpace(clanName))
                        {
                            if (KOUIManager.Instance != null)
                                KOUIManager.Instance.AddMsgOutput("You need to have a name in order to create a Knights.",
                                    KOUIManager.D3DColorToUnity(0xffffff00));
                            return;
                        }

                        // C++ birebir: UICreateClanName.cpp:80-91 → MakeClan()
                        // 500,000 coin maliyet uyarısı
                        KOMessageBox.Instance.ShowYesNo(
                            $"Creating a Knights costs 500,000 coins. Do you want to create '{clanName}'?",
                            "",
                            MsgBoxBehavior.BEHAVIOR_KNIGHTS_CREATE,
                            onYes: () =>
                            {
                                KOKnightsManager.Instance?.MsgSend_KnightsCreate(clanName);
                            },
                            onNo: () =>
                            {
                            }
                        );
                    }
                );
            }
            else
            {
                // KOMessageBox yok — doğrudan gönder
                Debug.LogWarning("[KNIGHTS-UI] KOMessageBox yüklenmemiş — doğrudan gönderiliyor");
                KOKnightsManager.Instance?.MsgSend_KnightsCreate("TestClan");
            }
        }

        /// <summary>
        /// Open-KO birebir: UIVarious.cpp:757 & GameProcMain.cpp:5950 — MsgSend_KnightsJoin(s_pPlayer->m_iIDTarget)
        /// Hedefteki oyuncuyu klana davet eder (Admit / Join).
        /// </summary>
        private void OnBtnJoin()
        {
            SendClanInviteToCurrentTarget();
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp — btn_Invite handler
        /// </summary>
        private void OnBtnInvite()
        {
            SendClanInviteToCurrentTarget();
        }

        private void SendClanInviteToCurrentTarget()
        {
            long targetId = -1;
            if (TargetInfoUI.Instance != null && TargetInfoUI.Instance.CurrentTargetId >= 0)
            {
                targetId = TargetInfoUI.Instance.CurrentTargetId;
            }
            else if (KOTargetSelector.Instance != null && KOTargetSelector.Instance.CurrentTarget != null)
            {
                targetId = KOTargetSelector.Instance.CurrentTarget.ServerInstanceId;
            }

            if (targetId >= 0)
            {
                KOKnightsManager.Instance?.MsgSend_KnightsJoin((int)targetId);
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Invitation sent to player.", KOUIManager.D3DColorToUnity(0xffffff00));
                }
            }
            else
            {
                Debug.LogWarning("[KNIGHTS-UI] ⚠️ Hedef oyuncu seçili değil!");
                if (KOUIManager.Instance != null)
                {
                    KOUIManager.Instance.AddMsgOutput("Please select a target player first.", KOUIManager.D3DColorToUnity(0xffffff00));
                }
            }
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:106-110 — m_pBtn_Destroy
        ///
        /// C++ akışı:
        ///   1. MessageBoxPost(IDS_KNIGHTS_DESTROY_CONFIRM, "", MB_YESNO, BEHAVIOR_KNIGHTS_DESTROY)
        ///   2. UIMessageBox.cpp:156-158 — BEHAVIOR_KNIGHTS_DESTROY + Yes:
        ///        pProcMain->MsgSend_KnightsDestroy();
        ///   3. UIMessageBox.cpp:197 — No: hiçbir şey yapılmaz
        /// </summary>
        private void OnBtnDestroy()
        {
            if (KOKnightsManager.Instance == null) return;

            // Open-KO birebir: cpp:108 — MessageBoxPost(IDS_KNIGHTS_DESTROY_CONFIRM, "", MB_YESNO, BEHAVIOR_KNIGHTS_DESTROY)
            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    "Are you sure you want to disband the Clan?",  // IDS_KNIGHTS_DESTROY_CONFIRM
                    "",
                    MsgBoxBehavior.BEHAVIOR_KNIGHTS_DESTROY,
                    onYes: () =>
                    {
                        // Open-KO birebir: UIMessageBox.cpp:156-158
                        // case BEHAVIOR_KNIGHTS_DESTROY:
                        //   pProcMain->MsgSend_KnightsDestroy();
                        KOKnightsManager.Instance?.MsgSend_KnightsDestroy();
                    },
                    onNo: () =>
                    {
                        // Open-KO birebir: UIMessageBox.cpp:197 — No → hiçbir şey yapılmaz
                    }
                );
            }
            else
            {
                // Fallback: KOMessageBox yoksa doğrudan gönder (C++ akışında bu olmaz)
                Debug.LogWarning("[KNIGHTS-UI] KOMessageBox yüklenmemiş — doğrudan gönderiliyor");
                KOKnightsManager.Instance.MsgSend_KnightsDestroy();
            }
        }

        /// <summary>
        /// Open-KO birebir: UIKnightsOperation.cpp:111-115 — m_pBtn_Withdraw
        ///
        /// C++ akışı:
        ///   1. MessageBoxPost(IDS_KNIGHTS_WITHDRAW_CONFIRM, "", MB_YESNO, BEHAVIOR_KNIGHTS_WITHDRAW)
        ///   2. UIMessageBox.cpp:160-162 — BEHAVIOR_KNIGHTS_WITHDRAW + Yes:
        ///        pProcMain->MsgSend_KnightsWithdraw();
        ///   3. UIMessageBox.cpp:197 — No: hiçbir şey yapılmaz
        /// </summary>
        private void OnBtnWithdraw()
        {
            if (KOKnightsManager.Instance == null) return;

            // Open-KO birebir: cpp:113 — MessageBoxPost(IDS_KNIGHTS_WITHDRAW_CONFIRM, "", MB_YESNO, BEHAVIOR_KNIGHTS_WITHDRAW)
            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(
                    "Are you sure you want to withdraw from the Knights?",  // IDS_KNIGHTS_WITHDRAW_CONFIRM
                    "",
                    MsgBoxBehavior.BEHAVIOR_KNIGHTS_WITHDRAW,
                    onYes: () =>
                    {
                        // Open-KO birebir: UIMessageBox.cpp:160-162
                        // case BEHAVIOR_KNIGHTS_WITHDRAW:
                        //   pProcMain->MsgSend_KnightsWithdraw();
                        KOKnightsManager.Instance?.MsgSend_KnightsWithdraw();
                    },
                    onNo: () =>
                    {
                        // Open-KO birebir: UIMessageBox.cpp:197 — No → hiçbir şey yapılmaz
                    }
                );
            }
            else
            {
                // Fallback: KOMessageBox yoksa doğrudan gönder
                Debug.LogWarning("[KNIGHTS-UI] KOMessageBox yüklenmemiş — doğrudan gönderiliyor");
                KOKnightsManager.Instance.MsgSend_KnightsWithdraw();
            }
        }

        // ================================================================
        // RefreshClanInfo — S2C_CLAN_INFO'dan sonra çağrılır
        // ================================================================

        private void RefreshClanInfo()
        {
            if (KOKnightsManager.Instance == null) return;

            var mgr = KOKnightsManager.Instance;

            if (_textClanName != null)
            {
                _textClanName.text = mgr.IsInClan ? mgr.ClanName : "Klansız";
            }

            if (_textClanInfo != null)
            {
                if (mgr.IsInClan)
                {
                    string gradeName = mgr.Grade switch
                    {
                        5 => "★★★★★",
                        4 => "★★★★",
                        3 => "★★★",
                        2 => "★★",
                        1 => "★",
                        _ => "?"
                    };
                    _textClanInfo.text = $"Grade: {gradeName}  Üye: {mgr.MemberCount}/{mgr.MaxMembers}  Puan: {mgr.Points}";
                }
                else
                {
                    _textClanInfo.text = string.Empty;
                }
            }

            // Buton durumlarını güncelle
            ChangeUIByDuty();
        }

        // ================================================================
        // RefreshMemberList — S2C_CLAN_INFO'daki üye listesini render et
        // ================================================================

        private void RefreshMemberList(ClanMemberEntry[] members)
        {
            // Mevcut üye list item'larını temizle
            foreach (var item in _memberListItems)
            {
                if (item != null) Destroy(item);
            }
            _memberListItems.Clear();

            if (_memberListContent == null || members == null) return;

            foreach (var m in members)
            {
                var go = new GameObject($"Member_{m.Name}");
                go.transform.SetParent(_memberListContent, false);

                var text = go.AddComponent<Text>();
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                text.fontSize = 12;
                text.color = m.IsOnline ? Color.green : Color.gray;

                string dutyName = KOKnightsManager.GetDutyName((byte)m.Rank);
                string className = m.GetClassName();
                text.text = $"{m.GetRankName()} {m.Name} Lv.{m.Level} [{className}] {(m.IsOnline ? "●" : "○")}";

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(300, 18);

                _memberListItems.Add(go);
            }
        }

        // ================================================================
        // RefreshKnightsList — S2C_CLAN_LIST'den gelen klan listesini render et
        // Open-KO birebir: UIKnightsOperation.cpp:157-170 — KnightsListUpdate()
        // ================================================================

        private void RefreshKnightsList(int page, ClanListEntry[] clans)
        {
            foreach (var item in _knightsListItems)
            {
                if (item != null) Destroy(item);
            }
            _knightsListItems.Clear();

            if (_knightsListContent == null || clans == null) return;

            // Open-KO birebir: cpp:165-169
            // szBuff = format("{:16} {:12} {:4} {:8}", name, chief, members, points)
            foreach (var c in clans)
            {
                var go = new GameObject($"Clan_{c.Name}");
                go.transform.SetParent(_knightsListContent, false);

                var text = go.AddComponent<Text>();
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
                text.fontSize = 11;
                text.color = Color.white;
                text.text = $"{c.Name,-16} {c.LeaderName,-12} {c.MemberCount,4} {c.Points,8}";

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400, 16);

                _knightsListItems.Add(go);
            }
        }

        // ================================================================
        // Invite Popup — S2C_CLAN_INVITE_INCOMING handler
        // ================================================================

        private void ShowInvitePopup(long inviterId, string inviterName, string clanName)
        {
            _pendingRequierId   = (int)inviterId;
            _pendingClanId      = 0;
            _pendingInviterName = inviterName;
            _pendingClanName    = clanName;
        }

        /// <summary>Daveti kabul et. C++ birebir: GameProcMain.cpp:1778-1790 — MsgSend_KnightsJoinReq(true)</summary>
        public void AcceptInvite()
        {
            KOKnightsManager.Instance?.MsgSend_KnightsJoinReq(true, _pendingRequierId, _pendingClanId);
        }

        /// <summary>Daveti reddet. C++ birebir: MsgSend_KnightsJoinReq(false)</summary>
        public void RejectInvite()
        {
            KOKnightsManager.Instance?.MsgSend_KnightsJoinReq(false, _pendingRequierId, _pendingClanId);
        }

        // ================================================================
        // Result Message — S2C_CLAN_RESULT handler
        // ================================================================

        private void ShowResultMessage(bool success, string message)
        {
            // C++ birebir: GameProcMain.cpp:6952,7028,7092,7128 — MsgOutput(szMsg, 0xffffff00)
            if (KOUIManager.Instance != null && !string.IsNullOrEmpty(message))
            {
                uint color = success ? 0xffffff00u : 0xffff3b3bu; // sarı: başarı, kırmızı: hata
                KOUIManager.Instance.AddMsgOutput(message, KOUIManager.D3DColorToUnity(color));
            }

            // Panel açıksa güncelle
            if (_panelRoot != null && _panelRoot.gameObject.activeSelf)
            {
                RefreshClanInfo();
            }
        }
    }
}
