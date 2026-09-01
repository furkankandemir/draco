using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Skill Trainer packet handler'ı.
    /// UI artık KOUIManager tarafından El_SkillTree_us.uif'den yükleniyor.
    /// </summary>
    public class SkillTrainerUI : MonoBehaviour
    {
        public static SkillTrainerUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            // NOT: WIZ_SKILLDATA (0x79) resmi KO protokolünde sadece Skillbar (Hotkeys) için kullanılır.
            // NPC Skill Trainer için bu paketin dinlenmesi hatalı bir eşleşmedir ve kaldırılmıştır.
            KOPacketHandler.OnNpcEvent += HandleNpcEvent_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnNpcEvent -= HandleNpcEvent_KO;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [System.Obsolete("WIZ_SKILLDATA (0x79) is for skillbar only. This method is obsolete and no longer subscribed to.", true)]
        private void HandleSkillData_KO(byte[] rawData)
        {
            if (rawData == null) return;

            // Open-KO: WIZ_SKILLDATA — skill trainer listesi
            // Wire: [opcode][npcId:int16][npcName:string][sp:int16][count:byte][skills...]
            var r = new KOPacketReader(rawData);
            short npcId = r.ReadInt16();
            string npcName = r.ReadKOString();
            short skillPoints = r.ReadInt16();
            byte count = r.ReadByte();

            var skills = new SkillTrainerEntry[count];
            for (int i = 0; i < count; i++)
            {
                skills[i] = new SkillTrainerEntry
                {
                    MagicNum = r.ReadInt32(),
                    SkillName = r.ReadKOString(),
                    SkillLevel = r.ReadInt16(),
                    MpCost = r.ReadInt16(),
                    SkillGroup = r.ReadInt16(),
                    IsLearned = r.ReadByte() != 0
                };
            }

            HandleNpcSkillData(npcId, npcName, skillPoints, skills);
        }

        /// <summary>KO wrapper — WIZ_NPC_EVENT (skill trainer response)</summary>
        private void HandleNpcEvent_KO(byte[] rawData)
        {
            // Open-KO birebir: MsgRecv_NpcEvent (GameProcMain.cpp:6305-6313)
            // Wire: [opcode][tradeId:uint32]
            // NPC event tipi (trade/repair) mevcut hedef NPC'den belirlenir
            var r = new KOPacketReader(rawData);
            int tradeId = (int)r.ReadUInt32();

        }

        private void HandleNpcSkillData(int npcId, string npcName, short skillPoints, SkillTrainerEntry[] skills)
        {
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ToggleSkillTree();

        }

        private void HandleSkillLearnResult(bool success, int magicNum, short remainingSP, string message)
        {
        }

        public void SendDistributeSkill(int skillId)
        {
            var netMgr = KONetworkManager.Instance;
            if (netMgr == null) return;

            // Open-KO birebir: WIZ_SKILLPT_CHANGE (skill learn = aynı opcode, farklı payload)
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_SKILLPT_CHANGE);
            pkt.WriteInt32(skillId);
            netMgr.SendPacket(pkt);
        }
    }
}
