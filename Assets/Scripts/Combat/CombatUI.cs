using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.Core;
using EntropyOnline.World;


namespace EntropyOnline.Combat
{
    /// <summary>
    /// Entropy Online — Savaş Arayüzü (Combat HUD)
    /// 
    /// Sağ altta saldırı butonları:
    /// - "R" Temel Saldırı butonu (büyük, ortada)
    /// - Skill butonları (etrafında)
    /// 
    /// Sol üstte hedef bilgileri:
    /// - Hedef adı + HP barı
    /// </summary>
    public class CombatUI : MonoBehaviour
    {
        [Header("Saldırı Butonları")]
        [SerializeField] private Button attackButton;

        [Header("Hedef Paneli")]
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private TextMeshProUGUI targetNameText;
        [SerializeField] private Image targetHpFill;

        [Header("Hasar Gösterimi")]
        [SerializeField] private Transform damageNumberParent;

        private TargetSystem _targetSystem;

        private void Start()
        {
            _targetSystem = TargetSystem.Instance;

            // Buton olayları
            if (attackButton != null)
                attackButton.onClick.AddListener(OnAttackClicked);

            // Hedef panelini gizle
            if (targetPanel != null)
                targetPanel.SetActive(false);

            // Event abonelikleri
            if (_targetSystem != null)
            {
                _targetSystem.OnTargetChanged += HandleTargetChanged;
                _targetSystem.OnTargetLost += HandleTargetLost;
            }

            KOPacketHandler.OnAttackResult += HandleAttackResult_KO;
        }

        private void Update()
        {
            // Keyboard kısayolları (Editor)
            var kb = Keyboard.current;
            if (kb == null) return;

            // Eğer bir input field odaklıysa kısayolları yoksay
            if (EntropyOnline.UI.KOUIManager.IsAnyInputFieldFocused()) return;

            // R tuşu = Temel saldırı
            if (kb.rKey.wasPressedThisFrame)
            {
                OnAttackClicked();
            }

            // Z tuşu = En yakın düşmanı hedef al
            if (kb.zKey.wasPressedThisFrame)
            {
                AutoTargetNearest();
            }
        }

        private void AutoTargetNearest()
        {
            var monsters = FindObjectsByType<MonsterEntity>(FindObjectsInactive.Exclude);
            if (monsters.Length == 0) return;

            MonsterEntity nearest = null;
            float minDist = float.MaxValue;
            var myPos = _targetSystem != null ? _targetSystem.transform.position : Vector3.zero;

            foreach (var m in monsters)
            {
                if (!m.IsAlive || !m.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(myPos, m.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = m;
                }
            }

            if (nearest != null && _targetSystem != null)
            {
                _targetSystem.SetTarget(nearest.transform, nearest.InstanceId, false);
            }
        }

        private void OnDestroy()
        {
            if (_targetSystem != null)
            {
                _targetSystem.OnTargetChanged -= HandleTargetChanged;
                _targetSystem.OnTargetLost -= HandleTargetLost;
            }

            KOPacketHandler.OnAttackResult -= HandleAttackResult_KO;
        }

        /// <summary>
        /// "R" tuşu / saldırı butonu basıldı.
        /// Sunucuya C2S_BASIC_ATTACK gönderir.
        /// </summary>
        private void OnAttackClicked()
        {
            if (_targetSystem == null || !_targetSystem.HasTarget) return;
            if (KONetworkManager.Instance == null) return;

            // Open-KO birebir: WIZ_ATTACK (GameProcMain.cpp:1474-1482)
            // Wire: [WIZ_ATTACK][attackType:byte][bySuccess:byte][targetId:int16][interval:int16][distance:int16]
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_ATTACK);
            pkt.WriteByte(1);    // cpp:1475 — attackType: 0x01 = melee
            pkt.WriteByte(1);    // cpp:1476 — bySuccess: true (항상 성공으로 보냄)
            pkt.WriteInt16((short)_targetSystem.CurrentTargetId);
            pkt.WriteInt16(110); // default interval centiseconds
            pkt.WriteInt16(20);  // default distance decimetres
            KONetworkManager.Instance.SendPacket(pkt);

        }

        // ============================
        // Hedef Bilgileri
        // ============================

        private void HandleTargetChanged(Transform target, long targetId, bool isPlayer)
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(true);

                var koEntity = target.GetComponent<KOEntity>();
                var monster = target.GetComponent<MonsterEntity>();
                var remotePlayer = target.GetComponent<RemotePlayerEntity>();

                if (koEntity != null && koEntity.IsNpc)
                {
                    // Dost NPC ise: Sadece ismini mavi renkle göster, seviyesini ve HP barını gizle
                    if (targetNameText != null)
                    {
                        targetNameText.text = koEntity.EntityName;
                        targetNameText.color = koEntity.GetTargetColor(); // Mavi (0xff1064ff)
                    }
                    if (targetHpFill != null && targetHpFill.transform.parent != null)
                    {
                        targetHpFill.transform.parent.gameObject.SetActive(false);
                    }
                }
                else if (monster != null)
                {
                    // Düşman Canavar ise: İsim + Seviye (Kırmızı renkle) göster, HP barını güncelle
                    if (targetNameText != null)
                    {
                        targetNameText.text = $"{monster.MonsterName} (Lv.{monster.Level})";
                        if (koEntity != null)
                            targetNameText.color = koEntity.GetTargetColor(); // Kırmızı (0xffff6060)
                        else
                            targetNameText.color = new Color(1f, 0.376f, 0.376f, 1f);
                    }
                    if (targetHpFill != null && targetHpFill.transform.parent != null)
                    {
                        targetHpFill.transform.parent.gameObject.SetActive(true);
                    }
                    UpdateTargetHpBar(monster.HpPercent);
                }
                else if (remotePlayer != null)
                {
                    // Uzak Oyuncu ise: İsim göster, seviye ve HP barını gizle
                    if (targetNameText != null)
                    {
                        targetNameText.text = remotePlayer.PlayerName;
                        targetNameText.color = (remotePlayer.Nation == 1) ? Color.red : Color.blue;
                    }
                    if (targetHpFill != null && targetHpFill.transform.parent != null)
                    {
                        targetHpFill.transform.parent.gameObject.SetActive(false);
                    }
                }
                else
                {
                    // Diğer
                    if (targetNameText != null)
                    {
                        targetNameText.text = target.name;
                        targetNameText.color = Color.white;
                    }
                    if (targetHpFill != null && targetHpFill.transform.parent != null)
                    {
                        targetHpFill.transform.parent.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void HandleTargetLost()
        {
            if (targetPanel != null)
                targetPanel.SetActive(false);
        }

        private void UpdateTargetHpBar(float percent)
        {
            if (targetHpFill != null)
                targetHpFill.fillAmount = percent;
        }

        // ============================
        // Savaş Sonuçları
        // ============================

        private void HandleAttackResult_KO(byte[] rawData)
        {
            // Open-KO birebir: MsgRecv_Attack (GameProcMain.cpp:3213-3218)
            // Wire: [opcode][type:byte][result:byte][attackerId:int16][targetId:int16]
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();       // 0x01=physical, 0x02=magic
            byte result = r.ReadByte();     // 0x00=miss, 0x01=hit, 0x02=kill
            short attackerId = r.ReadInt16();
            short targetId = r.ReadInt16();

            // C++ satır 3298: 0x0 → miss (damage=0), 0x2 → kill
            int damage = (result == 0) ? 0 : 1; // gerçek damage HP_CHANGE'den gelir
            bool died = (result == 0x02);
            HandleAttackResult(attackerId, targetId, targetId < 10000, damage, 0, died);
        }

        private void HandleAttackResult(long attackerId, long targetId, bool targetIsPlayer, int damage, int targetHp, bool targetDied)
        {
            // Hedef HP güncelle
            if (_targetSystem != null && _targetSystem.CurrentTargetId == targetId)
            {
                var monster = _targetSystem.CurrentTarget?.GetComponent<MonsterEntity>();
                if (monster != null)
                {
                    monster.ApplyDamage(damage, targetHp);
                    UpdateTargetHpBar(monster.HpPercent);

                    if (targetDied)
                    {
                        _targetSystem.ClearTarget();
                    }
                }
            }

            // Hasar sayısı göster
            ShowDamageNumber(damage, targetDied);
        }

        private void ShowDamageNumber(int damage, bool killed)
        {
            // Basit hasar gösterimi (ileride floating text olacak)
            string color = killed ? "red" : "yellow";
        }
    }
}
