using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;

namespace EntropyOnline.UI
{
    /// <summary>
    /// M65: Sınıf Değişikliği (Class Change) UI.
    /// Open-KO: WIZ_CLASS_CHANGE (0x34) birebir port.
    /// 
    /// NPC ile etkileşim sonrası açılır:
    /// - Captain NPC (type=35): Novice Promotion (Lv10)
    /// - Master NPC (type=73-76): Master Promotion (Lv60) + Stat/Skill Reset
    /// </summary>
    public class ClassChangeUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Butonlar")]
        [SerializeField] private Button promoteButton;
        [SerializeField] private Button statResetButton;
        [SerializeField] private Button skillResetButton;
        [SerializeField] private Button closeButton;

        [Header("Reset Panel")]
        [SerializeField] private GameObject resetPanel;
        [SerializeField] private TextMeshProUGUI resetCostText;
        [SerializeField] private Button confirmResetButton;
        [SerializeField] private Button cancelResetButton;

        // Sub-opcode sabitleri (server ile aynı)
        private const byte CLASS_CHANGE_STATUS_REQ = 0x01;
        private const byte CLASS_CHANGE_RESULT     = 0x02;
        private const byte CLASS_RESET_STAT_REQ    = 0x03;
        private const byte CLASS_RESET_SKILL_REQ   = 0x04;
        private const byte CLASS_RESET_COST_REQ    = 0x05;
        private const byte CLASS_PROMOTION_REQ     = 0x06;

        // Sonuç kodları
        private const byte RESULT_FAILURE     = 0x00;
        private const byte RESULT_SUCCESS     = 0x01;
        private const byte RESULT_NOT_YET     = 0x02;
        private const byte RESULT_ALREADY     = 0x03;
        private const byte RESULT_ITEM_IN_SLOT = 0x04;

        private byte _pendingResetType; // 1=stat, 2=skill
        private byte _npcType; // 35=captain, 73-76=master

        private void OnEnable()
        {
            KOPacketHandler.OnClassChange += HandleClassChange_KO;

            if (promoteButton != null) promoteButton.onClick.AddListener(OnPromoteClicked);
            if (statResetButton != null) statResetButton.onClick.AddListener(OnStatResetClicked);
            if (skillResetButton != null) skillResetButton.onClick.AddListener(OnSkillResetClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (confirmResetButton != null) confirmResetButton.onClick.AddListener(OnConfirmReset);
            if (cancelResetButton != null) cancelResetButton.onClick.AddListener(OnCancelReset);
        }

        private void OnDisable()
        {
            KOPacketHandler.OnClassChange -= HandleClassChange_KO;

            if (promoteButton != null) promoteButton.onClick.RemoveListener(OnPromoteClicked);
            if (statResetButton != null) statResetButton.onClick.RemoveListener(OnStatResetClicked);
            if (skillResetButton != null) skillResetButton.onClick.RemoveListener(OnSkillResetClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (confirmResetButton != null) confirmResetButton.onClick.RemoveListener(OnConfirmReset);
            if (cancelResetButton != null) cancelResetButton.onClick.RemoveListener(OnCancelReset);
        }

        /// <summary>
        /// NPC etkileşimi sonrası çağrılır.
        /// npcType: 35=Captain (Novice), 73-76=Master
        /// </summary>
        public void Open(byte npcType)
        {
            _npcType = npcType;
            if (panel != null) panel.SetActive(true);
            if (resetPanel != null) resetPanel.SetActive(false);

            bool isCaptain = npcType == 35;

            if (titleText != null)
                titleText.text = isCaptain ? "Sınıf Yükseltme" : "Master Sınıf Yükseltme";

            if (descriptionText != null)
                descriptionText.text = isCaptain
                    ? "Bu NPC ile sınıfınızı yükseltebilirsiniz.\nSeviye 10 gereklidir."
                    : "Bu NPC ile master sınıfına yükselebilir,\nstat veya skill puanlarınızı sıfırlayabilirsiniz.";

            // Captain: sadece promote göster
            // Master: promote + stat/skill reset göster
            if (promoteButton != null) promoteButton.gameObject.SetActive(true);
            if (statResetButton != null) statResetButton.gameObject.SetActive(!isCaptain);
            if (skillResetButton != null) skillResetButton.gameObject.SetActive(!isCaptain);

            if (statusText != null) statusText.text = "";

            // Durumu sor
            if (isCaptain)
                SendClassChangePacket(CLASS_CHANGE_STATUS_REQ);

        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            if (resetPanel != null) resetPanel.SetActive(false);
        }

        // ============================
        // BUTON HANDLERLARİ
        // ============================

        private void OnPromoteClicked()
        {
            bool isMaster = _npcType >= 73 && _npcType <= 76;
            byte promotionType = isMaster ? (byte)2 : (byte)1;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
            pkt.WriteByte(CLASS_PROMOTION_REQ);
            pkt.WriteByte(promotionType);
            KONetworkManager.Instance?.SendPacket(pkt);

            if (statusText != null) statusText.text = "Yükseltme isteği gönderildi...";
        }

        private void OnStatResetClicked()
        {
            _pendingResetType = 1;
            // Önce maliyeti sor
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
            pkt.WriteByte(CLASS_RESET_COST_REQ);
            pkt.WriteByte(1); // stat
            KONetworkManager.Instance?.SendPacket(pkt);

            if (statusText != null) statusText.text = "Maliyet hesaplanıyor...";
        }

        private void OnSkillResetClicked()
        {
            _pendingResetType = 2;
            // Önce maliyeti sor
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
            pkt.WriteByte(CLASS_RESET_COST_REQ);
            pkt.WriteByte(2); // skill
            KONetworkManager.Instance?.SendPacket(pkt);

            if (statusText != null) statusText.text = "Maliyet hesaplanıyor...";
        }

        private void OnConfirmReset()
        {
            byte subOpcode = _pendingResetType == 1 ? CLASS_RESET_STAT_REQ : CLASS_RESET_SKILL_REQ;

            using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
            pkt.WriteByte(subOpcode);
            KONetworkManager.Instance?.SendPacket(pkt);

            if (resetPanel != null) resetPanel.SetActive(false);
            if (statusText != null) statusText.text = "Sıfırlama isteği gönderildi...";
        }

        private void OnCancelReset()
        {
            if (resetPanel != null) resetPanel.SetActive(false);
            _pendingResetType = 0;
        }

        // ============================
        // PAKET GÖNDERME
        // ============================

        private void SendClassChangePacket(byte subOpcode)
        {
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_CLASS_CHANGE);
            pkt.WriteByte(subOpcode);
            KONetworkManager.Instance?.SendPacket(pkt);
        }

        /// <summary>KO wrapper — WIZ_CLASS_CHANGE</summary>
        private void HandleClassChange_KO(byte[] rawData)
        {
            // Open-KO birebir: MsgRecv_ClassChange (GameProcMain.cpp:5644-5673)
            // Wire: [opcode][subOpcode:byte][...conditional data]
            var r = new KOPacketReader(rawData);
            byte subOpcode = r.ReadByte();

            int extraData = 0;
            byte result = 0;

            if (subOpcode == CLASS_RESET_COST_REQ) // 0x05 — maliyet dönüyor
            {
                extraData = r.ReadInt32();
            }
            else if (subOpcode == CLASS_PROMOTION_REQ) // 0x06  promotion broadcast
            {
                short newClass = r.ReadInt16();
                short socketId = r.ReadInt16();
                extraData = newClass;

                var gm = GameManager.Instance;
                if (gm != null && socketId == gm.CharacterId)
                {
                    gm.CharClass = (byte)newClass;
                    KOUIManager.Instance?.RefreshSkillTreeUI();
                }
            }
            else
            {
                result = r.ReadByte();
            }

            HandleClassChangeResult(subOpcode, result, extraData);
        }

        // ============================
        // SUNUCU CEVABI
        // ============================

        private void HandleClassChangeResult(byte subOpcode, byte result, int extraData)
        {
            switch (subOpcode)
            {
                case CLASS_CHANGE_RESULT: // Status check veya promotion sonucu
                    HandlePromotionResult(result);
                    break;

                case CLASS_RESET_STAT_REQ:
                    HandleResetResult("Stat", result);
                    break;

                case CLASS_RESET_SKILL_REQ:
                    HandleResetResult("Skill", result);
                    break;

                case CLASS_RESET_COST_REQ:
                    ShowResetConfirmation(extraData);
                    break;

                case CLASS_PROMOTION_REQ: // Broadcast — başkası promote oldu
                    // Çevredeki oyuncuya class güncelleme bildirimi
                    break;
            }
        }

        private void HandlePromotionResult(byte result)
        {
            string message = result switch
            {
                RESULT_SUCCESS => "✅ Sınıf yükseltme başarılı!",
                RESULT_NOT_YET => "⚠️ Henüz yeterli seviyede değilsiniz.",
                RESULT_ALREADY => "⚠️ Zaten sınıf yükseltmişsiniz.",
                RESULT_FAILURE => "❌ Sınıf yükseltme başarısız oldu.",
                _ => $"❌ Bilinmeyen sonuç: {result}"
            };

            if (statusText != null) statusText.text = message;

            if (result == RESULT_SUCCESS)
            {
                // Panel'i kapat — bilgiler S2C_MY_INFO ile güncellenecek
                Invoke(nameof(Close), 2f);
            }
        }

        private void HandleResetResult(string type, byte result)
        {
            string message = result switch
            {
                RESULT_SUCCESS => $"✅ {type} puanları başarıyla sıfırlandı!",
                RESULT_FAILURE => $"❌ {type} sıfırlama başarısız. Yeterli gold yok.",
                RESULT_ITEM_IN_SLOT => "❌ Önce tüm ekipmanlarınızı çıkarın!",
                _ => $"❌ Bilinmeyen sonuç: {result}"
            };

            if (statusText != null) statusText.text = message;
        }

        private void ShowResetConfirmation(int cost)
        {
            string resetType = _pendingResetType == 1 ? "Stat" : "Skill";

            if (resetPanel != null) resetPanel.SetActive(true);
            if (resetCostText != null)
                resetCostText.text = $"{resetType} sıfırlama maliyeti:\n<color=#FFD700>{cost:N0} Gold</color>\n\nOnaylıyor musunuz?";
        }
    }
}
