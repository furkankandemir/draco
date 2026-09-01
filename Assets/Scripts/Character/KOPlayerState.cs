// ===================================================================================
// Open-KO birebir: GameDef.h e_StateAction, e_StateMove, e_Ani + PlayerBase.cpp sTableAction
// ===================================================================================
//
// C++ referans:
//   GameDef.h satır 127-303   — e_Ani enum
//   GameDef.h satır 320-327   — e_StateMove enum
//   GameDef.h satır 329-340   — e_StateAction enum
//   PlayerBase.cpp satır 940-1112 — Action() + State Table
//
// Bu dosya C++ yapısını 1:1 C#'a taşır. Hiçbir ekleme/değişiklik yapılmamıştır.
// ===================================================================================

namespace EntropyOnline.Character
{
    /// <summary>
    /// Open-KO birebir: GameDef.h e_StateAction (satır 329-340)
    /// Oyuncu karakter aksiyonu state machine.
    /// </summary>
    public enum PlayerStateAction : byte
    {
        PSA_BASIC       = 0,  // Idle
        PSA_ATTACK      = 1,  // Attacking
        PSA_GUARD       = 2,  // Successfully defended - attack blocked
        PSA_STRUCK      = 3,  // Taking heavy damage
        PSA_DYING       = 4,  // In the process of dying (collapsing)
        PSA_DEATH       = 5,  // Dead and lying down/knocked out
        PSA_SPELLMAGIC  = 6,  // Casting a spell
        PSA_SITDOWN     = 7,  // Sitting down
        PSA_COUNT       = 8
    }

    /// <summary>
    /// Open-KO birebir: GameDef.h e_StateMove (satır 320-327)
    /// </summary>
    public enum PlayerStateMove : byte
    {
        PSM_STOP          = 0,
        PSM_WALK          = 1,
        PSM_RUN           = 2,
        PSM_WALK_BACKWARD = 3,
        PSM_COUNT         = 4
    }

    /// <summary>
    /// Open-KO birebir: GameDef.h e_Ani (satır 127-303)
    /// Player character animation index'leri.
    /// NPC animasyonları aynı offset'leri paylaşır (0'dan başlar).
    /// </summary>
    public enum KOAni : short
    {
        ANI_BREATH = 0,
        ANI_WALK,
        ANI_RUN,
        ANI_WALK_BACKWARD,
        ANI_STRUCK0,
        ANI_STRUCK1,
        ANI_STRUCK2,
        ANI_GUARD,
        ANI_DEAD_NEATLY      = 8,
        ANI_DEAD_KNOCKDOWN,
        ANI_DEAD_ROLL,
        ANI_SITDOWN,
        ANI_SITDOWN_BREATH,
        ANI_STANDUP,
        ANI_ATTACK_WITH_WEAPON_WHEN_MOVE = 14,
        ANI_ATTACK_WITH_NAKED_WHEN_MOVE,

        ANI_SPELLMAGIC0_A = 16,
        ANI_SPELLMAGIC0_B,
        ANI_SPELLMAGIC1_A = 18,
        ANI_SPELLMAGIC1_B,
        ANI_SPELLMAGIC2_A = 20,
        ANI_SPELLMAGIC2_B,
        ANI_SPELLMAGIC3_A = 22,
        ANI_SPELLMAGIC3_B,
        ANI_SPELLMAGIC4_A = 24,
        ANI_SPELLMAGIC4_B,

        ANI_SHOOT_ARROW_A   = 26,
        ANI_SHOOT_ARROW_B,
        ANI_SHOOT_QUARREL_A = 28,
        ANI_SHOOT_QUARREL_B,
        ANI_SHOOT_JAVELIN_A = 30,
        ANI_SHOOT_JAVELIN_B,

        ANI_SWORD_BREATH_A  = 32,
        ANI_SWORD_ATTACK_A0,
        ANI_SWORD_ATTACK_A1,
        ANI_SWORD_BREATH_B,
        ANI_SWORD_ATTACK_B0,
        ANI_SWORD_ATTACK_B1,

        ANI_DAGGER_BREATH_A = 38,
        ANI_DAGGER_ATTACK_A0,
        ANI_DAGGER_ATTACK_A1,
        ANI_DAGGER_BREATH_B,
        ANI_DAGGER_ATTACK_B0,
        ANI_DAGGER_ATTACK_B1,

        ANI_DUAL_BREATH_A = 44,
        ANI_DUAL_ATTACK_A0,
        ANI_DUAL_ATTACK_A1,
        ANI_DUAL_BREATH_B,
        ANI_DUAL_ATTACK_B0,
        ANI_DUAL_ATTACK_B1,

        ANI_SWORD2H_BREATH_A = 50,
        ANI_SWORD2H_ATTACK_A0,
        ANI_SWORD2H_ATTACK_A1,
        ANI_SWORD2H_BREATH_B,
        ANI_SWORD2H_ATTACK_B0,
        ANI_SWORD2H_ATTACK_B1,

        ANI_BLUNT_BREATH_A = 56,
        ANI_BLUNT_ATTACK_A0,
        ANI_BLUNT_ATTACK_A1,
        ANI_BLUNT_BREATH_B,
        ANI_BLUNT_ATTACK_B0,
        ANI_BLUNT_ATTACK_B1,

        ANI_BLUNT2H_BREATH_A = 62,
        ANI_BLUNT2H_ATTACK_A0,
        ANI_BLUNT2H_ATTACK_A1,
        ANI_BLUNT2H_BREATH_B,
        ANI_BLUNT2H_ATTACK_B0,
        ANI_BLUNT2H_ATTACK_B1,

        ANI_AXE_BREATH_A = 68,
        ANI_AXE_ATTACK_A0,
        ANI_AXE_ATTACK_A1,
        ANI_AXE_BREATH_B,
        ANI_AXE_ATTACK_B0,
        ANI_AXE_ATTACK_B1,

        ANI_SPEAR_BREATH_A = 74,
        ANI_SPEAR_ATTACK_A0,
        ANI_SPEAR_ATTACK_A1,
        ANI_SPEAR_BREATH_B,
        ANI_SPEAR_ATTACK_B0,
        ANI_SPEAR_ATTACK_B1,

        ANI_POLEARM_BREATH_A = 80,
        ANI_POLEARM_ATTACK_A0,
        ANI_POLEARM_ATTACK_A1,
        ANI_POLEARM_BREATH_B,
        ANI_POLEARM_ATTACK_B0,
        ANI_POLEARM_ATTACK_B1,

        ANI_NAKED_BREATH_A = 86,
        ANI_NAKED_ATTACK_A0,
        ANI_NAKED_ATTACK_A1,
        ANI_NAKED_BREATH_B,
        ANI_NAKED_ATTACK_B0,
        ANI_NAKED_ATTACK_B1,

        ANI_BOW_BREATH       = 92,
        ANI_CROSS_BOW_BREATH,
        ANI_LAUNCHER_BREATH,
        ANI_BOW_BREATH_B,
        ANI_BOW_ATTACK_B0,
        ANI_BOW_ATTACK_B1,

        ANI_SHIELD_BREATH_A = 98,
        ANI_SHIELD_ATTACK_A0,
        ANI_SHIELD_ATTACK_A1,
        ANI_SHIELD_BREATH_B,
        ANI_SHIELD_ATTACK_B0,
        ANI_SHIELD_ATTACK_B1,

        ANI_GREETING0 = 104,
        ANI_GREETING1,
        ANI_GREETING2,
        ANI_WAR_CRY0 = 107,
        ANI_WAR_CRY1,
        ANI_WAR_CRY2,
        ANI_WAR_CRY3,
        ANI_WAR_CRY4,

        ANI_SKILL_AXE0     = 112,
        ANI_SKILL_AXE1,
        ANI_SKILL_AXE2,
        ANI_SKILL_AXE3,
        ANI_SKILL_DAGGER0  = 116,
        ANI_SKILL_DAGGER1,
        ANI_SKILL_DUAL0    = 118,
        ANI_SKILL_DUAL1,
        ANI_SKILL_BLUNT0   = 120,
        ANI_SKILL_BLUNT1,
        ANI_SKILL_BLUNT2,
        ANI_SKILL_BLUNT3,
        ANI_SKILL_POLEARM0 = 124,
        ANI_SKILL_POLEARM1,
        ANI_SKILL_SPEAR0   = 126,
        ANI_SKILL_SPEAR1,
        ANI_SKILL_SWORD0   = 128,
        ANI_SKILL_SWORD1,
        ANI_SKILL_SWORD2,
        ANI_SKILL_SWORD3,
        ANI_SKILL_AXE2H0   = 132,
        ANI_SKILL_AXE2H1,
        ANI_SKILL_SWORD2H0 = 134,
        ANI_SKILL_SWORD2H1,

        // NPC animations (same offset range, separate usage)
        ANI_NPC_BREATH       = 0,
        ANI_NPC_WALK         = 1,
        ANI_NPC_RUN          = 2,
        ANI_NPC_WALK_BACKWARD = 3,
        ANI_NPC_ATTACK0      = 4,
        ANI_NPC_ATTACK1      = 5,
        ANI_NPC_STRUCK0      = 6,
        ANI_NPC_STRUCK1      = 7,
        ANI_NPC_STRUCK2      = 8, // shares value with ANI_DEAD_NEATLY in player context
        ANI_NPC_GUARD        = 9,
        ANI_NPC_DEAD0        = 10,
        ANI_NPC_DEAD1        = 11,
        ANI_NPC_TALK0        = 12,
        ANI_NPC_TALK1        = 13,
        ANI_NPC_TALK2        = 14,
        ANI_NPC_TALK3        = 15,
        ANI_NPC_SPELLMAGIC0  = 16,
        ANI_NPC_SPELLMAGIC1  = 17,

        ANI_UNKNOWN = -1
    }

    /// <summary>
    /// Open-KO birebir: PlayerBase.cpp satır 940-968 — State Table Action
    /// 
    /// sTableAction[currentState][newState]: 1=geçiş izinli, 0=yasak
    /// Satırlar: mevcut state, Sütunlar: istenilen yeni state
    ///
    /// Örnek: PSA_SPELLMAGIC (6) state'indeyken:
    ///   PSA_BASIC(0)=1, PSA_ATTACK(1)=0, PSA_GUARD(2)=0, PSA_STRUCK(3)=0,
    ///   PSA_DYING(4)=1, PSA_DEATH(5)=1, PSA_SPELLMAGIC(6)=1, PSA_SITDOWN(7)=0
    /// </summary>
    public static class KOStateTable
    {
        // C++ PlayerBase.cpp satır 945-965 birebir
        //                           BASIC ATK  GRD  STRK DYN  DTH  SPELL SIT
        private static readonly bool[,] Table = new bool[,]
        {
            /* PSA_BASIC       → */ { true,  true,  true,  true,  true,  false, true,  true  },
            /* PSA_ATTACK      → */ { true,  true,  false, false, true,  false, true,  false },
            /* PSA_GUARD       → */ { true,  true,  true,  false, true,  false, true,  false },
            /* PSA_STRUCK      → */ { true,  true,  true,  true,  true,  false, true,  false },
            /* PSA_DYING       → */ { false, false, false, false, false, true,  false, false },
            /* PSA_DEATH       → */ { false, false, false, false, false, false, false, false },
            /* PSA_SPELLMAGIC  → */ { true,  false, false, false, true,  true,  true,  false },
            /* PSA_SITDOWN     → */ { true,  false, false, false, true,  false, false, false },
        };

        /// <summary>
        /// State geçişi izinli mi?
        /// C++ PlayerBase.cpp satır 967: if (FALSE == sTableAction[m_eState][eState]) return false;
        /// </summary>
        public static bool CanTransition(PlayerStateAction from, PlayerStateAction to)
        {
            int f = (int)from;
            int t = (int)to;
            if (f < 0 || f >= (int)PlayerStateAction.PSA_COUNT) return false;
            if (t < 0 || t >= (int)PlayerStateAction.PSA_COUNT) return false;
            return Table[f, t];
        }
    }
}
