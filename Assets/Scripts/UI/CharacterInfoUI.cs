using UnityEngine;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Karakter bilgi packet handler'ı.
    /// UI artık KOUIManager tarafından el_page_state_us.uif'den yükleniyor.
    /// Bu sınıf sadece KOPacketHandler.OnPointChange event'ini dinler.
    /// </summary>
    public class CharacterInfoUI : MonoBehaviour
    {
        public static CharacterInfoUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;
            KOPacketHandler.OnPointChange += HandlePointChange_KO;
        }

        private void OnDestroy()
        {
            KOPacketHandler.OnPointChange -= HandlePointChange_KO;
            if (Instance == this) Instance = null;
        }

        private void HandlePointChange_KO(byte[] rawData)
        {
            // Open-KO birebir: MsgRecv_MyInfo_PointChange (GameProcMain.cpp:3829-3882)
            // Wire: [opcode][type:byte][value:int16][hpMax:int16][mspMax:int16][attack:int16][weightMax:uint16]
            // IMPORTANT: value = ABSOLUTE (절대수치), NOT delta!
            var r = new KOPacketReader(rawData);
            byte type = r.ReadByte();          // cpp:3831
            short value = r.ReadInt16();        // cpp:3832 — ABSOLUTE
            short hpMax = r.ReadInt16();        // cpp:3834 — iHPMax
            short mspMax = r.ReadInt16();       // cpp:3835 — iMSPMax
            short attack = r.ReadInt16();       // cpp:3836 — iAttack
            ushort weightMax = r.ReadUInt16();  // cpp:3837 — iWeightMax

            var gm = GameManager.Instance;
            if (gm != null)
            {
                // cpp:3834-3837 — HP/MSP/Attack/Weight max güncelle
                gm.MaxHP = hpMax;
                gm.MaxMP = mspMax;
                gm.TotalHit = attack;
                gm.MaxWeight = (short)weightMax;

                // cpp:3851-3875 — value MUTLAK değer olarak ATAR (SET)
                switch (type)
                {
                    case 1: gm.StatStr = value; break; // cpp:3853
                    case 2: gm.StatSta = value; break; // cpp:3858
                    case 3: gm.StatDex = value; break; // cpp:3863
                    case 4: gm.StatInt = value; break; // cpp:3868
                    case 5: gm.StatCha = value; break; // cpp:3873
                }
                // cpp:3879
                if (type >= 1 && type <= 5) gm.StatPoints--;

                HandleCombatStats(gm.TotalHit, gm.TotalAc, hpMax, mspMax,
                    gm.TotalHitRate, gm.TotalEvasionRate,
                    gm.TotalStr, gm.TotalSta, gm.TotalDex, gm.TotalInt);
            }
        }

        private void HandleCombatStats(int totalHit, int totalAc, int maxHp, int maxMp,
            float hitRate, float evasionRate, short str, short sta, short dex, short intel)
        {
            // GameManager'a stat'ları kaydet — bu veriler UIF panelinin text binding'inde kullanılacak
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.TotalHit = totalHit;
            gm.TotalAc = totalAc;
            gm.MaxHp = maxHp;
            gm.MaxMp = maxMp;
            gm.TotalHitRate = hitRate;
            gm.TotalEvasionRate = evasionRate;
            gm.TotalStr = str;
            gm.TotalSta = sta;
            gm.TotalDex = dex;
            gm.TotalInt = intel;

        }

        /// <summary>
        /// C++ GameBase.cpp GetTextByClass() birebir — KOTextHelper'a yönlendirir.
        /// KRİTİK: charClass % 100 YAPILMAZ — tam eClass değeri kullanılır.
        /// </summary>
        public static string GetClassName(byte charClass) => KOTextHelper.GetTextByClass(charClass);
    }
}
