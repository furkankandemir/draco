using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EntropyOnline.Import;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: CUITradeSellBBS (UITradeSellBBS.h/cpp)
    /// UIF: el_saleboard_us.uif / ka_saleboard_us.uif
    /// </summary>
    public class KOTradeBBS : MonoBehaviour
    {
        public static KOTradeBBS Instance { get; private set; }

        private const int TRADE_BBS_MAX_LINE = 23;    // Satır sayısı
        private const int TRADE_BBS_MAXSTRING = 69;   // Toplam yazı alanı (23 * 3)

        private Button _btnPageUp;
        private Button _btnPageDown;
        private Button _btnRefresh;
        private Button _btnClose;
        private Button _btnRegister;
        private Button _btnRegisterCancel;
        private Button _btnWhisper;
        private Button _btnTrade;

        private Image _imgSellGold;
        private Image _imgBuyGold;
        private Image _imgSellTitle;
        private Image _imgBuyTitle;

        private Text _textPage;
        private Text[] _texts = new Text[TRADE_BBS_MAXSTRING];

        private List<TradeBBSEntry> _datas = new List<TradeBBSEntry>();
        private TradeBBSEntry _selectedEntry;

        private byte _bbsKind = TradeBBSKind.N3_SP_TRADE_BBS_SELL;
        private int _curPage = 0;
        private int _maxPage = 0;
        private int _curIndex = -1;
        private bool _processing = false;
        private float _lastRequestTime = -10f;

        private float _lastRowClickTime;
        private int _lastRowClicked = -1;

        public byte BBSKind => _bbsKind;

        public struct TradeBBSEntry
        {
            public short sID;           // Character numeric ID
            public string Name;         // szID in C++
            public string Title;        // szTitle
            public string Explanation;  // szExplanation
            public int Price;           // iPrice
            public short Index;         // sIndex
        }

        private void Awake()
        {
            Instance = this;
            BindElements();
        }

        private void OnEnable()
        {
            _curIndex = -1;
            UpdateSelectionHighlight();
        }

        private void Update()
        {
            // ESC ile kapat (C++ OnKeyPress DIK_ESCAPE birebir)
            if (gameObject.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnCloseClick();
            }
        }

        private void BindElements()
        {
            var t = transform;

            _btnPageUp = KOUIRenderer.FindChildButton(t, "btn_page_up");
            _btnPageDown = KOUIRenderer.FindChildButton(t, "btn_page_down");
            _btnRefresh = KOUIRenderer.FindChildButton(t, "btn_refresh");
            _btnClose = KOUIRenderer.FindChildButton(t, "btn_exit");
            _btnRegister = KOUIRenderer.FindChildButton(t, "btn_add");
            _btnWhisper = KOUIRenderer.FindChildButton(t, "btn_whisper");
            _btnTrade = KOUIRenderer.FindChildButton(t, "btn_sale");
            _btnRegisterCancel = KOUIRenderer.FindChildButton(t, "btn_delete");

            var imgSellGoldTr = KOUIRenderer.FindChildByID(t, "img_sell gold");
            if (imgSellGoldTr != null) _imgSellGold = imgSellGoldTr.GetComponent<Image>();

            var imgBuyGoldTr = KOUIRenderer.FindChildByID(t, "img_buy gold");
            if (imgBuyGoldTr != null) _imgBuyGold = imgBuyGoldTr.GetComponent<Image>();

            var imgSellTr = KOUIRenderer.FindChildByID(t, "img_sell");
            if (imgSellTr != null) _imgSellTitle = imgSellTr.GetComponent<Image>();

            var imgBuyTr = KOUIRenderer.FindChildByID(t, "img_buy");
            if (imgBuyTr != null) _imgBuyTitle = imgBuyTr.GetComponent<Image>();

            _textPage = KOUIRenderer.FindChildText(t, "string_page");

            // Text alanlarını bul ve event'leri bağla
            for (int i = 0; i < TRADE_BBS_MAXSTRING; i++)
            {
                string id = $"text_{i:D2}";
                _texts[i] = KOUIRenderer.FindChildText(t, id);
                if (_texts[i] != null)
                {
                    int index = i;
                    int row = index % TRADE_BBS_MAX_LINE;
                    _texts[i].raycastTarget = true;
                    var btn = _texts[i].gameObject.GetComponent<Button>();
                    if (btn == null) btn = _texts[i].gameObject.AddComponent<Button>();
                    btn.onClick.AddListener(() => OnRowClick(row));
                }
            }

            // Click listener'ları
            if (_btnPageUp != null) _btnPageUp.onClick.AddListener(OnPageUpClick);
            if (_btnPageDown != null) _btnPageDown.onClick.AddListener(OnPageDownClick);
            if (_btnRefresh != null) _btnRefresh.onClick.AddListener(OnRefreshClick);
            if (_btnClose != null) _btnClose.onClick.AddListener(OnCloseClick);
            if (_btnRegister != null) _btnRegister.onClick.AddListener(OnRegisterClick);
            if (_btnRegisterCancel != null) _btnRegisterCancel.onClick.AddListener(OnRegisterCancelClick);
            if (_btnWhisper != null) _btnWhisper.onClick.AddListener(OnWhisperClick);
            if (_btnTrade != null) _btnTrade.onClick.AddListener(OnTradeClick);
        }

        private void OnRowClick(int row)
        {
            if (row < 0 || row >= _datas.Count) return;

            float now = Time.unscaledTime;
            if (_lastRowClicked == row && (now - _lastRowClickTime) < 0.4f) // Double Click (Double Tap)
            {
                _lastRowClickTime = 0f;
                _lastRowClicked = -1;
                OnListExplanation();
            }
            else
            {
                _lastRowClickTime = now;
                _lastRowClicked = row;

                _curIndex = row;
                _selectedEntry = _datas[row];
                UpdateSelectionHighlight();
            }
        }

        private void UpdateSelectionHighlight()
        {
            for (int i = 0; i < TRADE_BBS_MAX_LINE; i++)
            {
                Color col = (i == _curIndex) ? Color.yellow : Color.white;
                SetStringColor(i, col);
            }
        }

        private void SetStringColor(int iIndex, Color color)
        {
            if (iIndex < 0 || iIndex >= TRADE_BBS_MAX_LINE) return;

            if (_texts[iIndex] != null)
                _texts[iIndex].color = color;

            if (_texts[iIndex + TRADE_BBS_MAX_LINE] != null)
                _texts[iIndex + TRADE_BBS_MAX_LINE].color = color;

            if (_texts[iIndex + TRADE_BBS_MAX_LINE * 2] != null)
                _texts[iIndex + TRADE_BBS_MAX_LINE * 2].color = color;
        }

        private void OnPageUpClick()
        {
            int prevPage = _curPage - 1;
            if (prevPage >= 0)
            {
                MsgSend_RefreshData(prevPage);
            }
        }

        private void OnPageDownClick()
        {
            int nextPage = _curPage + 1;
            if (nextPage < _maxPage)
            {
                MsgSend_RefreshData(nextPage);
            }
        }

        private void OnRefreshClick()
        {
            float fTime = Time.time;
            if (fTime - _lastRequestTime < 3.0f) return; // C++ UITradeSellBBS.cpp:118 Cooldown
            _lastRequestTime = fTime;

            MsgSend_RefreshData(_curPage);
        }

        private void OnCloseClick()
        {
            _curPage = 0;
            _lastRequestTime = -10f;
            SetVisible(false);
        }

        private void OnRegisterClick()
        {
            if (_processing) return;

            // Fiyat uyarı mesaj kutusu (C++ OnButtonRegister birebir)
            string msg = (_bbsKind == TradeBBSKind.N3_SP_TRADE_BBS_BUY) 
                ? "Registering items you want to buy will cost 500 Coins. Do you want to continue?" 
                : "You need 1,000 Coins to register the selling item. Do you want to continue?";

            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(msg, "", MsgBoxBehavior.BEHAVIOR_NOTHING,
                    onYes: () =>
                    {
                        KOTradeBBSEditDlg.Instance?.Show(_bbsKind);
                    });
            }
            else
            {
                KOTradeBBSEditDlg.Instance?.Show(_bbsKind);
            }
        }

        private void OnRegisterCancelClick()
        {
            if (_processing || _curIndex < 0 || _curIndex >= _datas.Count) return;

            var entry = _datas[_curIndex];
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Orijinal C++: Sadece kendi pazar kaydını silebilirsin (veya GM ise silebilir)
            bool isMe = string.Equals(entry.Name, gm.CharacterName, System.StringComparison.OrdinalIgnoreCase);
            bool isGM = gm.Authority == 0; // GM Authority

            if (isMe || isGM)
            {
                MsgSend_RegisterCancel(entry.Index);
            }
            else
            {
                KOUIManager.Instance?.AddMsgOutput("You can only delete your own register.", KOUIManager.D3DColorToUnity(0xffff0000));
            }
        }

        private void OnWhisperClick()
        {
            if (_curIndex < 0 || _curIndex >= _datas.Count) return;

            var entry = _datas[_curIndex];
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Kendi kendine fısıldayamazsın (C++ UITradeSellBBS.cpp:546)
            if (!string.Equals(entry.Name, gm.CharacterName, System.StringComparison.OrdinalIgnoreCase))
            {
                KONetworkManager.Instance?.SendChatSelectTarget(entry.Name);
            }
        }

        private void OnTradeClick()
        {
            if (_processing || _curIndex < 0 || _curIndex >= _datas.Count) return;

            var entry = _datas[_curIndex];
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Kendi kendine uzaktan trade teklifi atamazsın (C++ UITradeSellBBS.cpp:580)
            if (string.Equals(entry.Name, gm.CharacterName, System.StringComparison.OrdinalIgnoreCase))
                return;

            string msg = "You will have to pay the Inn Keeper 5,000 Coins in order to trade with somebody that is far away. Do you want to continue?";
            
            if (KOMessageBox.Instance != null)
            {
                KOMessageBox.Instance.ShowYesNo(msg, "", MsgBoxBehavior.BEHAVIOR_NOTHING,
                    onYes: () =>
                    {
                        MsgSend_PerTrade();
                    });
            }
            else
            {
                MsgSend_PerTrade();
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (!visible)
            {
                KOTradeBBSExplanation.Instance?.SetVisible(false);
                KOTradeBBSEditDlg.Instance?.SetVisible(false);
            }
        }

        // ================================================
        // NETWORKING - C2S SEND PACKETS
        // ================================================

        /// <summary>
        /// C++ birebir: MsgSend_RefreshData (UITradeSellBBS.cpp:362-377)
        /// Wire: WIZ_MARKET_BBS [N3_SP_TYPE_BBS_DATA=0x03] [byBBSKind:byte] [curPage:int16]
        /// </summary>
        public void MsgSend_RefreshData(int page)
        {
            if (_processing) return;

            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_BBS_DATA);
                pkt.WriteByte(_bbsKind);
                pkt.WriteInt16((short)page);
                netMgr.SendPacket(pkt);

                _processing = true;
            }
        }

        /// <summary>
        /// C++ birebir: MsgSend_RegisterCancel (UITradeSellBBS.cpp:415-430)
        /// Wire: WIZ_MARKET_BBS [N3_SP_TYPE_REGISTER_CANCEL=0x02] [byBBSKind:byte] [index:int16]
        /// </summary>
        private void MsgSend_RegisterCancel(short index)
        {
            if (_processing) return;

            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_REGISTER_CANCEL);
                pkt.WriteByte(_bbsKind);
                pkt.WriteInt16(index);
                netMgr.SendPacket(pkt);

                _processing = true;
            }
        }

        /// <summary>
        /// C++ birebir: MsgSend_PerTrade (UITradeSellBBS.cpp:653-674)
        /// Wire: WIZ_MARKET_BBS [N3_SP_TYPE_BBS_TRADE=0x05] [byBBSKind:byte] [sIndex:int16]
        /// </summary>
        private void MsgSend_PerTrade()
        {
            if (_processing || _curIndex < 0) return;

            var entry = _datas[_curIndex];

            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_MARKET_BBS);
                pkt.WriteByte(TradeBBSSub.N3_SP_TYPE_BBS_TRADE);
                pkt.WriteByte(_bbsKind);
                pkt.WriteInt16(entry.Index);
                netMgr.SendPacket(pkt);

                _processing = true;
            }
        }

        // ================================================
        // NETWORKING - S2C RECEIVE PACKET DISPATCH
        // ================================================

        /// <summary>
        /// C++ birebir: MsgRecv_TradeBBS (UITradeSellBBS.cpp:183-256)
        /// Wire: WIZ_MARKET_BBS [bySubType:byte] [byBBSKind:byte] [byResult:byte] ...
        /// </summary>
        public void MsgRecv_TradeBBS(KOPacketReader pkt)
        {
            _processing = false;

            byte bySubType = pkt.ReadByte();
            byte byBBSKind = pkt.ReadByte();
            byte byResult = pkt.ReadByte();


            if (byResult != 0x01) // FAILED (C++ satır 191)
            {
                byte bySubResult = pkt.ReadByte();
                string szMsg = "Failed.";

                if (bySubType == TradeBBSSub.N3_SP_TYPE_BBS_OPEN)
                {
                    szMsg = "Could not access the trade board."; // IDS_TRADE_BBS_FAIL6
                }
                else if (bySubType == TradeBBSSub.N3_SP_TYPE_REGISTER)
                {
                    switch (bySubResult)
                    {
                        case 1: szMsg = "Failed registering."; break; // IDS_TRADE_BBS_FAIL1
                        case 2: szMsg = "You don't have enough Coins."; break; // IDS_TRADE_BBS_FAIL2
                        case 3: szMsg = "Failed. Please press the refresh button."; break; // IDS_TRADE_BBS_FAIL4
                    }
                }
                else if (bySubType == TradeBBSSub.N3_SP_TYPE_REGISTER_CANCEL)
                {
                    szMsg = "Failed canceling the registration."; // IDS_TRADE_BBS_FAIL3
                }
                else if (bySubType == TradeBBSSub.N3_SP_TYPE_BBS_TRADE)
                {
                    switch (bySubResult)
                    {
                        case 1: szMsg = "Failed requesting for a trade."; break; // IDS_TRADE_BBS_FAIL5
                        case 2: szMsg = "You don't have enough Coins."; break; // IDS_TRADE_BBS_FAIL2
                        case 3: szMsg = "Failed. Please press the refresh button."; break; // IDS_TRADE_BBS_FAIL4
                    }
                }

                KOUIManager.Instance?.AddMsgOutput(szMsg, KOUIManager.D3DColorToUnity(0xffff0000));
                return;
            }

            // SUCCESS PATH
            if (bySubType == TradeBBSSub.N3_SP_TYPE_BBS_OPEN)
            {
                _bbsKind = byBBSKind;

                // Tab görüntülerini BBSKind'e göre ayarla
                bool isBuy = (byBBSKind == TradeBBSKind.N3_SP_TRADE_BBS_BUY);
                if (_imgSellGold != null) _imgSellGold.gameObject.SetActive(!isBuy);
                if (_imgBuyGold != null) _imgBuyGold.gameObject.SetActive(isBuy);
                if (_imgSellTitle != null) _imgSellTitle.gameObject.SetActive(!isBuy);
                if (_imgBuyTitle != null) _imgBuyTitle.gameObject.SetActive(isBuy);

                SetVisible(true);
            }
            else if (bySubType == TradeBBSSub.N3_SP_TYPE_BBS_TRADE)
            {
                // C++ UITradeSellBBS.cpp:298: MsgSend_PerTradeBBSReq(m_ITSB.szID, m_ITSB.sID)
                // Bu paket başarıyla gelirse takas arayüzü tetiklenir
                
                // TradeUI üzerinden takas isteği gönder
                if (EntropyOnline.Trade.KOTradeManager.Instance != null)
                {
                    EntropyOnline.Trade.KOTradeManager.Instance.SendExchangeReq(_selectedEntry.sID);
                }
                SetVisible(false);
                return;
            }

            // Gelen arama verilerini işle (refresh data)
            MsgRecv_RefreshData(pkt);
        }

        /// <summary>
        /// C++ birebir: MsgRecv_RefreshData (UITradeSellBBS.cpp:306-340)
        /// </summary>
        private void MsgRecv_RefreshData(KOPacketReader pkt)
        {
            _datas.Clear();

            // Orijinal C++ paket yapısı: her zaman 23 kez döner
            for (int i = 0; i < TRADE_BBS_MAX_LINE; i++)
            {
                short sID = pkt.ReadInt16();
                string szID = pkt.ReadKOString();
                string szTitle = pkt.ReadKOString();
                string szExplanation = pkt.ReadKOString();
                int iPrice = pkt.ReadInt32();
                short sIndex = pkt.ReadInt16();

                if (sID != -1)
                {
                    _datas.Add(new TradeBBSEntry
                    {
                        sID = sID,
                        Name = szID,
                        Title = szTitle,
                        Explanation = szExplanation,
                        Price = iPrice,
                        Index = sIndex
                    });
                }
            }

            short sPage = pkt.ReadInt16();
            short sTotal = pkt.ReadInt16();

            _curPage = sPage;
            _maxPage = sTotal / TRADE_BBS_MAX_LINE;
            if ((sTotal % TRADE_BBS_MAX_LINE) > 0)
            {
                _maxPage++;
            }

            RefreshPage();
        }

        /// <summary>
        /// C++ birebir: RefreshPage (UITradeSellBBS.cpp:342-360)
        /// </summary>
        private void RefreshPage()
        {
            if (_textPage != null)
                _textPage.text = (_curPage + 1).ToString();

            ResetContent();

            for (int i = 0; i < TRADE_BBS_MAX_LINE; i++)
            {
                if (i >= _datas.Count) break;

                var entry = _datas[i];
                SetContentString(i, entry.Name, entry.Price, entry.Title);
            }

            UpdateSelectionHighlight();
        }

        private void ResetContent()
        {
            _curIndex = (_datas.Count > 0) ? 0 : -1;
            if (_datas.Count > 0) _selectedEntry = _datas[0];

            for (int i = 0; i < TRADE_BBS_MAXSTRING; i++)
            {
                if (_texts[i] != null)
                {
                    _texts[i].text = "";
                    _texts[i].color = Color.white;
                }
            }
        }

        /// <summary>
        /// C++ birebir: SetContentString (UITradeSellBBS.cpp:801-816)
        /// </summary>
        private void SetContentString(int iIndex, string szID, int iPrice, string szTitle)
        {
            if (iIndex < 0 || iIndex >= TRADE_BBS_MAX_LINE) return;

            // Name (Column 0: 0..22)
            if (_texts[iIndex] != null)
                _texts[iIndex].text = szID;

            // Title (Column 1: 23..45)
            if (_texts[iIndex + TRADE_BBS_MAX_LINE] != null)
                _texts[iIndex + TRADE_BBS_MAX_LINE].text = szTitle;

            // Price (Column 2: 46..68)
            if (_texts[iIndex + TRADE_BBS_MAX_LINE * 2] != null)
            {
                _texts[iIndex + TRADE_BBS_MAX_LINE * 2].text = $"{iPrice:N0} Coins";
            }
        }

        // ================================================
        // MEMO DETAILS - EXPLANATION PANEL BRIDGE
        // ================================================

        /// <summary>
        /// C++ birebir: OnListExplanation (UITradeSellBBS.cpp:632-651)
        /// </summary>
        private void OnListExplanation()
        {
            if (_curIndex < 0 || _curIndex >= _datas.Count) return;

            var entry = _datas[_curIndex];
            KOTradeBBSExplanation.Instance?.Show(entry.Explanation);
        }

        /// <summary>
        /// C++ birebir: RefreshExplanation (UITradeSellBBS.cpp:596-630)
        /// </summary>
        public void RefreshExplanation(bool bPageUp)
        {
            if (_curIndex < 0 || _datas.Count == 0) return;

            if (bPageUp)
            {
                if (_curIndex == 0) return;
                _curIndex--;
            }
            else
            {
                if (_curIndex + 1 >= _datas.Count) return;
                _curIndex++;
            }

            _selectedEntry = _datas[_curIndex];
            UpdateSelectionHighlight();

            KOTradeBBSExplanation.Instance?.SetExplanationText(_selectedEntry.Explanation);
        }
    }
}
