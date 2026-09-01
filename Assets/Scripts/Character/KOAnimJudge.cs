// ===================================================================================
// Open-KO birebir: PlayerBase.cpp satır 1438-1616
//   JudgeAnimationBreath, JudgeAnimationWalk, JudgeAnimationRun,
//   JudgeAnimationWalkBackward, JudgeAnimationStruck, JudgeAnimationGuard,
//   JudgeAnimationDying, JudgetAnimationSpellMagic
//
// JudgeAnimationAttack — PlayerBase.cpp:1411-1436
// Silah tipine göre animasyon index'i seçimi.
// Static utility class — PlayerController ve RemotePlayerEntity kullanır.
// ===================================================================================

using static EntropyOnline.Character.KOItemClass;
using static EntropyOnline.Character.KOWeaponWeight;

namespace EntropyOnline.Character
{
    /// <summary>
    /// Open-KO birebir: CPlayerBase::JudgeAnimation* serileri (PlayerBase.cpp:1438-1616)
    ///
    /// Silah tipine göre animasyon index (e_Ani) seçer.
    /// isNPC: NPC ise farklı animasyon index kullanılır.
    /// hasTarget: Düşman hedef var mı (Breath animasyonu için).
    /// eICR/eICL: Sağ/sol elde tutulan silahın sınıfı.
    /// fWeightR: Sağ eldeki silahın ağırlığı (siWeight/10f).
    /// </summary>
    public static class KOAnimJudge
    {
        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationAttack (PlayerBase.cpp:1411-1436)
        ///
        /// Saldırı animasyonu — silah tipine göre seçilir.
        ///   NPC: ANI_NPC_ATTACK0 + rand()%2
        ///   Player + Staff: ANI_DAGGER_ATTACK_A0  (cpp:1424-1426: "지팡이 일경우 창 공격으로 한다")
        ///   Player + diğer: JudgeAnimationBreath() + 1 + rand()%2
        ///     → Breath anim'inden sonraki 2 attack varyantı seçilir.
        ///
        /// hasTarget: çağıran taraftan hedef kontrolü yapılmalı (m_iIDTarget != -1).
        /// JudgeAnimationBreath'e iletilecek parametreler de çağıran taraftan gelir.
        /// </summary>
        public static KOAni JudgeAnimationAttack(bool isNPC, bool hasTarget,
            KOItemClass eICR, KOItemClass eICL, float fWeightR)
        {
            KOAni eAni = KOAni.ANI_BREATH;  // cpp:1413

            if (isNPC)  // cpp:1415-1418
            {
                eAni = (KOAni)((int)KOAni.ANI_NPC_ATTACK0 + UnityEngine.Random.Range(0, 2));
            }
            else  // cpp:1419-1433
            {
                if (hasTarget)  // cpp:1421
                {
                    if (eICR == ITEM_CLASS_STAFF)  // cpp:1424-1426: staff → dagger attack
                    {
                        eAni = KOAni.ANI_DAGGER_ATTACK_A0;
                    }
                    else  // cpp:1428-1431
                    {
                        // Breath animasyonundan sonraki 2 attack varyantı:
                        // eAni = (e_Ani)(JudgeAnimationBreath() + 1 + rand() % 2)
                        KOAni breathAni = JudgeAnimationBreath(false, true, eICR, eICL, fWeightR);
                        eAni = (KOAni)((int)breathAni + 1 + UnityEngine.Random.Range(0, 2));
                    }
                }
            }

            return eAni;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationBreath (PlayerBase.cpp:1438-1539)
        ///
        /// Nefes/idle animasyonu — silah tipine + hedef durumuna göre seçilir.
        ///   hasTarget && isHostile → silah tipine göre weapon_breath
        ///   hasTarget yok → ANI_BREATH (genel idle)
        /// </summary>
        public static KOAni JudgeAnimationBreath(bool isNPC, bool hasHostileTarget,
            KOItemClass eICR, KOItemClass eICL, float fWeightR)
        {
            KOAni eAni = KOAni.ANI_BREATH;

            if (isNPC)  // cpp:1442-1448
            {
                // cpp:1444-1447: %90 breath, %10 random talk
                if (UnityEngine.Random.Range(0, 10) != 0)
                    eAni = KOAni.ANI_NPC_BREATH;
                else
                    eAni = (KOAni)((int)KOAni.ANI_NPC_TALK0 + UnityEngine.Random.Range(0, 4));
            }
            else  // Player — cpp:1449-1536
            {
                if (hasHostileTarget)  // cpp:1454
                {
                    // cpp:1464-1465: Dual wield kontrolü (sword+sword, axe+axe, sword+axe, axe+sword)
                    bool isDual = (eICR == ITEM_CLASS_SWORD && eICL == ITEM_CLASS_SWORD) ||
                                  (eICR == ITEM_CLASS_AXE   && eICL == ITEM_CLASS_AXE) ||
                                  (eICR == ITEM_CLASS_SWORD && eICL == ITEM_CLASS_AXE) ||
                                  (eICR == ITEM_CLASS_AXE   && eICL == ITEM_CLASS_SWORD);

                    if (isDual)  // cpp:1464-1480
                    {
                        if (eICR == ITEM_CLASS_SWORD)
                        {
                            eAni = fWeightR < WEAPON_WEIGHT_STAND_SWORD
                                ? KOAni.ANI_DUAL_BREATH_A
                                : KOAni.ANI_DUAL_BREATH_B;
                        }
                        else // AXE
                        {
                            eAni = fWeightR < WEAPON_WEIGHT_STAND_AXE
                                ? KOAni.ANI_DUAL_BREATH_A
                                : KOAni.ANI_DUAL_BREATH_B;
                        }
                    }
                    else if (eICR == ITEM_CLASS_DAGGER || (eICR == ITEM_CLASS_UNKNOWN && eICL == ITEM_CLASS_DAGGER))    // cpp:1482
                        eAni = KOAni.ANI_DAGGER_BREATH_A;
                    else if (eICR == ITEM_CLASS_SWORD || (eICR == ITEM_CLASS_UNKNOWN && eICL == ITEM_CLASS_SWORD))     // cpp:1484-1490
                    {
                        eAni = fWeightR < WEAPON_WEIGHT_STAND_SWORD
                            ? KOAni.ANI_SWORD_BREATH_A
                            : KOAni.ANI_SWORD_BREATH_B;
                    }
                    else if (eICR == ITEM_CLASS_SWORD_2H)  // cpp:1491
                        eAni = KOAni.ANI_SWORD2H_BREATH_A;
                    else if (eICR == ITEM_CLASS_AXE || (eICR == ITEM_CLASS_UNKNOWN && eICL == ITEM_CLASS_AXE))       // cpp:1493-1498
                    {
                        eAni = fWeightR < WEAPON_WEIGHT_STAND_AXE
                            ? KOAni.ANI_AXE_BREATH_A
                            : KOAni.ANI_AXE_BREATH_B;
                    }
                    else if (eICR == ITEM_CLASS_AXE_2H || eICR == ITEM_CLASS_MACE_2H)  // cpp:1500
                        eAni = KOAni.ANI_BLUNT2H_BREATH_A;
                    else if (eICR == ITEM_CLASS_MACE || (eICR == ITEM_CLASS_UNKNOWN && eICL == ITEM_CLASS_MACE))  // cpp:1502-1507
                    {
                        eAni = fWeightR < WEAPON_WEIGHT_STAND_BLUNT
                            ? KOAni.ANI_BLUNT_BREATH_A
                            : KOAni.ANI_BLUNT_BREATH_B;
                    }
                    else if (eICR == ITEM_CLASS_SPEAR)     // cpp:1509
                        eAni = KOAni.ANI_SPEAR_BREATH_A;
                    else if (eICR == ITEM_CLASS_POLEARM)   // cpp:1511
                        eAni = KOAni.ANI_POLEARM_BREATH_A;
                    else if (eICR == ITEM_CLASS_UNKNOWN && eICL == ITEM_CLASS_BOW)  // cpp:1513
                        eAni = KOAni.ANI_BOW_BREATH;
                    else if (eICR == ITEM_CLASS_BOW_CROSS && eICL == ITEM_CLASS_UNKNOWN)  // cpp:1515
                        eAni = KOAni.ANI_CROSS_BOW_BREATH;
                    else if (eICR == ITEM_CLASS_LAUNCHER && (short)eICL >= (short)ITEM_CLASS_UNKNOWN)  // cpp:1517
                        eAni = KOAni.ANI_LAUNCHER_BREATH;
                    else if (eICR == ITEM_CLASS_UNKNOWN && (short)eICL >= (short)ITEM_CLASS_SHIELD)  // cpp:1519
                        eAni = KOAni.ANI_SHIELD_BREATH_A;
                    else if (eICR == ITEM_CLASS_STAFF)  // cpp:1523
                        eAni = KOAni.ANI_BREATH;       // Staff → genel breath
                    else  // cpp:1527
                        eAni = KOAni.ANI_NAKED_BREATH_A;
                }
                else  // cpp:1532-1534: hedef yoksa genel breath
                {
                    eAni = KOAni.ANI_BREATH;
                }
            }

            return eAni;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationWalk (PlayerBase.cpp:1541-1555)
        /// </summary>
        public static KOAni JudgeAnimationWalk(bool isNPC)
        {
            return isNPC ? KOAni.ANI_NPC_WALK : KOAni.ANI_WALK;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationRun (PlayerBase.cpp:1557-1571)
        /// </summary>
        public static KOAni JudgeAnimationRun(bool isNPC)
        {
            return isNPC ? KOAni.ANI_NPC_RUN : KOAni.ANI_RUN;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationWalkBackward (PlayerBase.cpp:1573-1587)
        /// </summary>
        public static KOAni JudgeAnimationWalkBackward(bool isNPC)
        {
            return isNPC ? KOAni.ANI_NPC_WALK_BACKWARD : KOAni.ANI_WALK_BACKWARD;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationStruck (PlayerBase.cpp:1589-1595)
        /// Player: ANI_STRUCK0 + rand()%3, NPC: ANI_NPC_STRUCK0 + rand()%3
        /// </summary>
        public static KOAni JudgeAnimationStruck(bool isNPC)
        {
            if (isNPC)
                return (KOAni)((int)KOAni.ANI_NPC_STRUCK0 + UnityEngine.Random.Range(0, 3));
            else
                return (KOAni)((int)KOAni.ANI_STRUCK0 + UnityEngine.Random.Range(0, 3));
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationGuard (PlayerBase.cpp:1597-1603)
        /// </summary>
        public static KOAni JudgeAnimationGuard(bool isNPC)
        {
            return isNPC ? KOAni.ANI_NPC_GUARD : KOAni.ANI_GUARD;
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgeAnimationDying (PlayerBase.cpp:1605-1611)
        /// Player: ANI_DEAD_NEATLY + rand()%3, NPC: ANI_NPC_DEAD0
        /// </summary>
        public static KOAni JudgeAnimationDying(bool isNPC)
        {
            if (isNPC)
                return KOAni.ANI_NPC_DEAD0;
            else
                return (KOAni)((int)KOAni.ANI_DEAD_NEATLY + UnityEngine.Random.Range(0, 3));
        }

        /// <summary>
        /// Open-KO birebir: CPlayerBase::JudgetAnimationSpellMagic (PlayerBase.cpp:1613-1616)
        /// Doğrudan m_iMagicAni cast — index tabanlı.
        /// </summary>
        public static KOAni JudgetAnimationSpellMagic(int iMagicAni)
        {
            return (KOAni)iMagicAni;
        }
    }
}
