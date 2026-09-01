using System.IO;
using UnityEngine;
using UnityEngine.UI;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;
using EntropyOnline.Import;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO v1.298 birebir: CGameProcNationSelect + CUINationSelectDlg
    ///
    /// C++ kaynak:
    ///   GameProcNationSelect.h/cpp — Nation seçim prosedürü
    ///   UINationSelectDlg.h/cpp   — UI dialog (butonlar + layout)
    ///
    /// Akış (GameProcNationSelect.cpp:54-59):
    ///   1. Login → server list → connect → nation==0 → bu ekran gösterilir
    ///   2. .uif dosyasından UI yüklenir
    ///      C++: s_pTbl_UI.Find(NATION_ELMORAD)->szNationSelectNew (GameDef.h:803, index 130)
    ///      .tbl şifrelenmiş olduğundan dosya adı doğrulanamıyor.
    ///      Dizinde mevcut olan her iki .uif denenir:
    ///        co_nation_Select_us.uif (muhtemel szNationSelectNew — index 130)
    ///        Co_nationselect_us.uif  (muhtemel szNationSelect — index 56)
    ///   3. btn_karus_selection / btn_elmo_selection tıklanınca
    ///      MsgSendNationSelect(nation) → WIZ_SEL_NATION paketi gönderilir
    ///   4. Server yanıt: nation>0 → CharacterSelect'e geç, nation==0 → fail
    ///   5. btn_back → Login ekranına dön
    /// </summary>
    public class NationSelectUI : MonoBehaviour
    {
        
        // .uif'den yüklenen ana panel
        private GameObject _uiNationSelect;
        
        // .uif child butonları — UINationSelectDlg.h:15-17
        private Button _btnKarus;    // m_pBtnKarus   → "btn_karus_selection"
        private Button _btnElmorad;  // m_pBtnElmorad → "btn_elmo_selection"
        private Button _btnBack;     // m_pBtnBack    → "btn_back"
        
        // Canvas
        private Canvas _canvas;
        
        // State
        // C++ birebir: s_pUIMgr->EnableOperationSet(false) — yanıt beklerken input kapatılır
        private bool _bWaitingResponse = false;
        
        // ============================================
        // Init — GameProcNationSelect::Init() birebir (cpp:36-52)
        // ============================================
        private void OnEnable()
        {
            
            // Canvas yoksa oluştur
            if (_canvas == null)
                CreateCanvas();
            
            // .uif yükle
            if (_uiNationSelect == null)
                LoadNationSelectUI();
            else
                _uiNationSelect.SetActive(true);
            
            // Paket handler'ı bağla
            KOPacketHandler.OnSelectNationResult += MsgRecv_SelNation;
            
            // C++ birebir: s_pPlayer->m_InfoBase.eNation = NATION_NOTSELECTED (cpp:51)
            _bWaitingResponse = false;
        }
        
        private void OnDisable()
        {
            KOPacketHandler.OnSelectNationResult -= MsgRecv_SelNation;
        }
        
        private void OnDestroy()
        {
            KOPacketHandler.OnSelectNationResult -= MsgRecv_SelNation;
        }
        
        // ============================================
        // Canvas — LoginUI ile aynı mimari
        // ============================================
        private void CreateCanvas()
        {
            var canvasObj = new GameObject("NationSelectCanvas");
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        
        // ============================================
        // .uif yükle — GameProcNationSelect::Init birebir (cpp:42-48)
        //   pTbl = s_pTbl_UI.Find(NATION_ELMORAD)
        //   szTemp = pTbl->szNationSelectNew
        //   m_pUINationSelectDlg->LoadFromFile(szTemp)
        //
        // NOT: szNationSelectNew (index 130) ve szNationSelect (index 56)
        // .tbl dosyası binary XOR şifrelenmiş — hangi .uif'e eşlendiği doğrulanamıyor.
        // Dizindeki her iki .uif sırayla denenir.
        // ============================================
        private void LoadNationSelectUI()
        {
            string uiDir = "UI_US";
            
            // Muhtemel szNationSelectNew (index 130) ve szNationSelect (index 56) .uif dosyaları
            string[] candidates = {
                Path.Combine(uiDir, "co_nation_Select_us.uif"),  // muhtemel index 130
                Path.Combine(uiDir, "Co_nationselect_us.uif"),   // muhtemel index 56
            };
            
            string uifPath = null;
            foreach (var path in candidates)
            {
                    uifPath = path;
                    break;
            }
            
            if (uifPath == null)
            {
                Debug.LogError("[NATION] Hiçbir nation select .uif bulunamadı: co_nation_Select_us.uif / Co_nationselect_us.uif");
                return;
            }
            
            // C++ birebir: LoadFromFile(szTemp) → SetPosCenter (cpp:37-40)
            var fullScreenRegion = new UIFImporter.Rect { Left = 0, Top = 0, Right = 1024, Bottom = 768 };
            _uiNationSelect = KOUIRenderer.LoadUI(uifPath, _canvas.transform, fullScreenRegion);
            
            if (_uiNationSelect == null)
            {
                Debug.LogError($"[NATION] .uif yüklenemedi: {Path.GetFileName(uifPath)}");
                return;
            }
            
            // C++ birebir: UINationSelectDlg::Load (cpp:37-40)
            // RECT rc = this->GetRegion();
            // int iX = (vpW - (rc.right - rc.left)) / 2;
            // int iY = (vpH - (rc.bottom - rc.top)) / 2;
            // this->SetPos(iX, iY);
            var rt = _uiNationSelect.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            
            
            // Butonları bağla — UINationSelectDlg::Load birebir (cpp:34-36)
            BindUIElements();
        }
        
        /// <summary>
        /// UINationSelectDlg::Load birebir (cpp:34-36)
        ///   N3_VERIFY_UI_COMPONENT(m_pBtnKarus, GetChildByID("btn_karus_selection"))
        ///   N3_VERIFY_UI_COMPONENT(m_pBtnElmorad, GetChildByID("btn_elmo_selection"))
        ///   N3_VERIFY_UI_COMPONENT(m_pBtnBack, GetChildByID("btn_back"))
        /// </summary>
        private void BindUIElements()
        {
            var root = _uiNationSelect.transform;
            
            _btnKarus = KOUIRenderer.FindChildButton(root, "btn_karus_selection");
            if (_btnKarus != null)
            {
                _btnKarus.onClick.AddListener(OnBtnKarusClick);
            }
            else
            {
                Debug.LogError("[NATION] btn_karus_selection bulunamadı!");
            }
            
            _btnElmorad = KOUIRenderer.FindChildButton(root, "btn_elmo_selection");
            if (_btnElmorad != null)
            {
                _btnElmorad.onClick.AddListener(OnBtnElmoradClick);
            }
            else
            {
                Debug.LogError("[NATION] btn_elmo_selection bulunamadı!");
            }
            
            _btnBack = KOUIRenderer.FindChildButton(root, "btn_back");
            if (_btnBack != null)
            {
                _btnBack.onClick.AddListener(OnBtnBackClick);
            }
            else
            {
                Debug.LogError("[NATION] btn_back bulunamadı!");
            }
        }
        
        // ============================================
        // Buton handler'ları — UINationSelectDlg::ReceiveMessage birebir (cpp:45-68)
        // ============================================
        
        /// <summary>cpp:52-56 — if (pSender == m_pBtnKarus) MsgSendNationSelect(NATION_KARUS)</summary>
        private void OnBtnKarusClick()
        {
            if (_bWaitingResponse) return;
            MsgSendNationSelect(1); // NATION_KARUS
        }
        
        /// <summary>cpp:57-61 — if (pSender == m_pBtnElmorad) MsgSendNationSelect(NATION_ELMORAD)</summary>
        private void OnBtnElmoradClick()
        {
            if (_bWaitingResponse) return;
            MsgSendNationSelect(2); // NATION_ELMORAD
        }
        
        /// <summary>
        /// cpp:62-64 — btn_back → Login ekranına dön
        /// CGameProcedure::ProcActiveSet((CGameProcedure*) CGameProcedure::s_pProcLogIn)
        /// </summary>
        private void OnBtnBackClick()
        {
            gameObject.SetActive(false);
            var loginUI = FindAnyObjectByType<LoginUI>(FindObjectsInactive.Include);
            if (loginUI != null)
                loginUI.gameObject.SetActive(true);
        }
        
        // ============================================
        // MsgSendNationSelect — GameProcNationSelect.cpp:74-85 birebir
        //   MP_AddByte(byBuff, iOffset, WIZ_SEL_NATION)
        //   MP_AddByte(byBuff, iOffset, (uint8_t) eNation)
        //   s_pSocket->Send(byBuff, iOffset)
        //   s_pUIMgr->EnableOperationSet(false)
        // ============================================
        private void MsgSendNationSelect(byte nation)
        {
            _bWaitingResponse = true;
            
            // Open-KO: KONetworkManager üzerinden WIZ_SEL_NATION gönder
            KONetworkManager.Instance.SendSelectNation(nation);
            
            // C++ birebir: s_pUIMgr->EnableOperationSet(false) — yanıt gelene kadar disable (cpp:84)
            SetButtonsInteractable(false);
        }
        
        // ============================================
        // MsgRecv_SelNation — GameProcNationSelect.cpp:109-119 birebir
        //   int iNation = pkt.read<uint8_t>()
        //   if (0 == iNation)      → NATION_NOTSELECTED
        //   else if (1 == iNation) → NATION_KARUS
        //   else if (2 == iNation) → NATION_ELMORAD
        // ============================================
        private void MsgRecv_SelNation(byte nation)
        {
            _bWaitingResponse = false;
            
            // cpp:113: if (0 == iNation) → NATION_NOTSELECTED
            if (nation == 0)
            {
                Debug.LogWarning("[NATION] Nation selection failed (server returned 0)");
                SetButtonsInteractable(true);
                return;
            }
            
            // cpp:115-118: nation=1 → NATION_KARUS, nation=2 → NATION_ELMORAD
            
            if (GameManager.Instance != null)
                GameManager.Instance.Nation = nation;
            
            // cpp:58-59: Tick() → eNation == KARUS || ELMORAD → ProcActiveSet(s_pProcCharacterSelect)
            GoToCharacterSelect();
        }
        
        // ============================================
        // Tick — GameProcNationSelect::Tick birebir (cpp:54-60)
        //   if (NATION_KARUS == eNation || NATION_ELMORAD == eNation)
        //       ProcActiveSet(s_pProcCharacterSelect)
        //   Bizde event-based: MsgRecv_SelNation → GoToCharacterSelect
        // ============================================
        
        /// <summary>
        /// C++: ProcActiveSet(s_pProcCharacterSelect)
        /// </summary>
        private void GoToCharacterSelect()
        {
            gameObject.SetActive(false);
            var selectUI = FindAnyObjectByType<CharacterSelectUI>(FindObjectsInactive.Include);
            if (selectUI != null)
            {
                selectUI.gameObject.SetActive(true);
            }
            else
            {
                // CharacterSelectUI henüz sahneye eklenmemiş — dinamik oluştur
                var selectObj = new GameObject("CharacterSelectUI");
                selectUI = selectObj.AddComponent<CharacterSelectUI>();
            }
        }
        
        // ============================================
        // Yardımcılar
        // ============================================
        private void SetButtonsInteractable(bool state)
        {
            if (_btnKarus != null) _btnKarus.interactable = state;
            if (_btnElmorad != null) _btnElmorad.interactable = state;
            if (_btnBack != null) _btnBack.interactable = state;
        }
    }
}
