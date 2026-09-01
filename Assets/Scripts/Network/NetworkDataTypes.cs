// ============================================================
// Entropy Online — Network Data Classes
// Eski PacketHandler.cs dosyasından taşınan data class tanımları.
// Tüm packet data class/struct/enum'lar burada toplanmıştır.
// ============================================================

using System;

namespace EntropyOnline.Network
{
    // === ENUMS ===

    /// <summary>
    /// Open-KO birebir: packets.h:459-463 — büyü başarısızlık sebepleri.
    /// WIZ_MAGIC_PROCESS FAIL sub-opcode ile Data[3] olarak gelir.
    /// C++ birebir: int16_t (signed short) negatif değerler.
    /// </summary>
    public enum MagicFailReason : short
    {
        Casting     = -100,  // packets.h:459 — SKILLMAGIC_FAIL_CASTING
        KillFlying  = -101,  // packets.h:460 — SKILLMAGIC_FAIL_KILLFLYING
        NoEffect    = -103,  // packets.h:462 — SKILLMAGIC_FAIL_NOEFFECT ("%s failed")
        AttackZero  = -104,  // packets.h:463 — SKILLMAGIC_FAIL_ATTACKZERO ("%s Missed.")
    }

    // === DATA CLASSES ===

    public class BundleLootSlot
    {
        public int ItemId;   // 0 = boş slot
        public short Count;
    }

    public class WarehouseSlot
    {
        public byte Slot;       // 0-191 (WAREHOUSE_MAX)
        public int ItemId;      // Open-KO: nNum
        public short Count;     // Open-KO: sCount
        public short Durability; // Open-KO: sDuration
    }

    public class ShopItemData
    {
        public int ItemDefId;
        public string Name;
        public byte Type;
        public byte SubType;
        public byte RequiredClass;
        public short RequiredLevel;
        public short AttackMin;
        public short AttackMax;
        public short Defense;
        public short MagicAttack;
        public int BuyPrice;
        public int SellPrice;
        public string IconId;
        /// <summary>Open-KO: pItemBasic->byContable (0=ONLYONE, 1=COUNTABLE, 2=COUNTABLE_SMALL)</summary>
        public byte ByContable;

        public string GetTypeName() => Type switch
        {
            1 => "Silah",
            2 => "Zırh",
            4 => "Tüketilebilir",
            _ => "Eşya"
        };
    }

    /// <summary>
    /// Open-KO birebir: __InfoPartyOrForce (GameDef.h:574-606)
    /// Parti üyesi bilgilerini tutar.
    /// </summary>
    public class PartyMemberData
    {
        public long CharacterId;    // __InfoPartyOrForce::iID
        public string Name;         // __InfoPartyOrForce::szID
        public short Level;         // __InfoPartyOrForce::iLevel
        public byte Class;          // __InfoPartyOrForce::eClass
        public int CurrentHp;       // __InfoPartyOrForce::iHP
        public int MaxHp;           // __InfoPartyOrForce::iHPMax
        public int CurrentMp;       // __InfoPartyOrForce::iMP
        public int MaxMp;           // __InfoPartyOrForce::iMPMax
        public bool SufferDownHP;   // __InfoPartyOrForce::bSufferDown_HP
        public bool SufferDownEtc;  // __InfoPartyOrForce::bSufferDown_Etc
        
        // Open-KO birebir: CUIPartyOrForce::Tick() (cpp:456-522) tarafından hesaplanan
        // bar visibility state'leri. KOPartyManager.Update() her frame günceller.
        public bool BlinkHpBarVisible = true;       // m_pProgress_HPs visibility
        public bool BlinkHpReduceVisible = false;   // m_pProgress_HPReduce visibility
        public bool BlinkMpBarVisible = true;        // m_pProgress_MP visibility
    }

    public class PartyHpData
    {
        public long CharacterId;
        public int CurrentHp;
        public int MaxHp;
        public int CurrentMp;
        public int MaxMp;
    }

    /// <summary>
    /// Karakter seçim ekranında gösterilecek bilgiler.
    /// </summary>
    public class CharacterListItem
    {
        public long Id;
        public string Name;
        public byte Race;
        public byte Class;
        public short Level;
        public short ZoneId;

        /// <summary>
        /// C++ GameBase.cpp GetTextByClass() birebir — KOTextHelper'a yönlendirir.
        /// KRİTİK: charClass % 100 YAPILMAZ — tam eClass değeri kullanılır.
        /// </summary>
        public string GetClassName() => EntropyOnline.Core.KOTextHelper.GetTextByClass(Class);

        /// <summary>
        /// C++ GameBase.cpp GetTextByRace() birebir — KOTextHelper'a yönlendirir.
        /// </summary>
        public string GetRaceName() => EntropyOnline.Core.KOTextHelper.GetTextByRace(Race);
        
        // Open-KO birebir: _InfoCharacter struct (GameProcCharacterSelect.cpp:1422-1423)
        public byte Face;
        public byte Hair;
        
        // Open-KO birebir: _InfoCharacter struct (GameProcCharacterSelect.cpp:1426-1443)
        // 8 visible equipment slot — her biri (itemId, durability)
        public int ItemHelmet;           public short ItemHelmetDurability;
        public int ItemUpper;            public short ItemUpperDurability;
        public int ItemCloak;            public short ItemCloakDurability;
        public int ItemRightHand;        public short ItemRightHandDurability;
        public int ItemLeftHand;         public short ItemLeftHandDurability;
        public int ItemLower;            public short ItemLowerDurability;
        public int ItemGloves;           public short ItemGlovesDurability;
        public int ItemShoes;            public short ItemShoesDurability;
    }

    /// <summary>
    /// Client tarafında bir envanter eşyasının verisi.
    /// Sunucudan S2C_INVENTORY_DATA paketi ile gelir.
    /// </summary>
    public class InventoryItemData
    {
        public long InstanceId;
        public int ItemDefId;
        public string Name;
        public byte Type;       // 1: Weapon, 2: Armor, 4: Consumable
        public byte SubType;
        /// <summary>Open-KO: byAttachPoint — hangi slot'a takılacağını belirleyen değer (ItemDefinition.Slot)</summary>
        public byte AttachPoint;
        public byte SlotType;   // 0: Inventory, 1: Equipped
        public byte SlotIndex;
        public short StackCount;
        public short UpgradeLevel;
        public short Durability;
        public short AttackMin;
        public short AttackMax;
        public short Defense;
        public short MagicAttack;
        /// <summary>Open-KO: siAttackRange — silah menzili</summary>
        public short Range;
        /// <summary>Open-KO: siAttackInterval — saldırı aralığı (centisaniye)</summary>
        public short Delay;
        /// <summary>
        /// Open-KO: siAttackIntervalPercentage — artık sunucudan GELMEZ.
        /// Client-side .tbl lookup: ItemDataManager.GetAttackIntervalPct(ItemDefId)
        /// Open-KO ref: PlayerMySelf.cpp:221-223, GameProcMain.cpp:2022-2023
        /// </summary>
        [Obsolete("Sunucudan gelmez. ItemDataManager.GetAttackIntervalPct(ItemDefId) kullanın.")]
        public short AttackIntervalPct;
        /// <summary>Open-KO: byContable — stacklenebilir mi (0=hayır, 1=COUNTABLE, 2=COUNTABLE_SMALL)</summary>
        public short Countable;
        public bool IsBound;
        public string IconId;
        public byte byFlag;
        public short sTimeRemaining;

        public bool IsEquipped => SlotType == 1;
        public bool IsWeapon => Type == 1;
        public bool IsArmor => Type == 2;
        public bool IsConsumable => Type == 4;

        public string GetDisplayName()
        {
            if (UpgradeLevel > 0)
                return $"{Name} +{UpgradeLevel}";
            if (StackCount > 1)
                return $"{Name} x{StackCount}";
            return Name;
        }

        public string GetTypeName() => Type switch
        {
            1 => "Silah",
            2 => "Zırh",
            4 => "Tüketilebilir",
            _ => "Eşya"
        };
    }

    public class SkillTrainerEntry
    {
        public int MagicNum;
        public string SkillName;
        public short SkillLevel;
        public short MpCost;
        public short SkillGroup;
        public bool IsLearned;
    }

    /// <summary>
    /// C++ __WarpInfo (UIWarp.h:14-23) birebir port.
    /// Sunucu GetWarpList paketindeki her warp noktasının verileri.
    /// C++ birebir: User.cpp:3985-4021 — pozisyon bilgisi gönderilmez,
    /// sunucu tarafında _WARP_INFO.fX/fY/fZ tutulur ve SelectWarpList'te kullanılır.
    /// </summary>
    public class WarpListEntry
    {
        public short WarpId;         // openko-ref: SetShort(pWarp->sWarpID)
        public string Name;          // openko-ref: SetString2(pWarp->strWarpName)
        public string Agreement;     // openko-ref: SetString2(pWarp->strAnnounce)
        public short TargetZoneId;   // openko-ref: SetShort(pWarp->sZone)
        public short MaxUser;        // openko-ref: SetShort(pTargetMap->m_sMaxUser)
        public int GoldCost;         // openko-ref: SetDWORD(pWarp->dwPay)
        public short PosX;           // openko-ref: SetShort(pWarp->fX * 10)
        public short PosZ;           // openko-ref: SetShort(pWarp->fZ * 10)
        public short PosY;           // openko-ref: SetShort(pWarp->fY * 10)
    }

    public class ClanMemberEntry
    {
        public long CharId;
        public string Name;
        public short Level;
        public byte CharClass;
        public short Rank;
        public bool IsOnline;

        /// <summary>
        /// C++ GameBase.cpp GetTextByClass() birebir — KOTextHelper'a yönlendirir.
        /// KRİTİK: charClass % 100 YAPILMAZ — tam eClass değeri kullanılır.
        /// </summary>
        public string GetClassName() => EntropyOnline.Core.KOTextHelper.GetTextByClass(CharClass);

        /// <summary>
        /// Open-KO birebir: e_KnightsDuty (globals.h:186-196)
        ///   0 = KNIGHTS_DUTY_UNKNOWN   — Klansız
        ///   1 = KNIGHTS_DUTY_CHIEF     — Lider
        ///   2 = KNIGHTS_DUTY_VICECHIEF — Yardımcı Lider
        ///   3 = KNIGHTS_DUTY_PUNISH    — Cezalı
        ///   4 = KNIGHTS_DUTY_TRAINEE   — Çaylak
        ///   5 = KNIGHTS_DUTY_KNIGHT    — Üye
        ///   6 = KNIGHTS_DUTY_OFFICER   — Subay
        ///   100 = KNIGHTS_DUTY_CAPTAIN — Komutan
        /// </summary>
        public string GetRankName() => Rank switch
        {
            1 => "Lider",
            2 => "Yrd. Lider",
            3 => "Cezali",
            4 => "Caylak",
            5 => "Uye",
            6 => "Subay",
            100 => "Komutan",
            _ => "?"
        };
    }

    /// <summary>
    /// Open-KO birebir: AllKnightsList() response — klan listesi girişi
    /// Open-KO birebir: UIKnightsOperation.cpp:228-234 — MsgRecv_KnightsList
    /// Wire: {iID, szName, iMemberCount, szChiefName, iPoint}
    /// </summary>
    public class ClanListEntry
    {
        public long ClanId;
        public string Name;
        public short MemberCount;
        public string LeaderName;
        public int Points;
    }

    /// <summary>
    /// Open-KO birebir: MsgRecv_Knights_GradeChangeAll (cpp:7307-7311)
    /// Wire: {knightsId, grade, ranking} — iIDs[], iGrades[], iRanks[]
    /// </summary>
    public class ClanGradeEntry
    {
        public long ClanId;
        public byte Grade;
        public byte Ranking;  // Open-KO birebir: iRanks[i] (cpp:7305,7311)
    }

    public class QuestEntry
    {
        public short QuestId;
        public byte QuestState;
    }

    /// <summary>
    /// Open-KO birebir: MsgRecv_ItemMove response data.
    /// GameProcMain.cpp:3366-3427 — success ise 16x int16 stat değeri.
    /// </summary>
    public struct ItemMoveResultData
    {
        public byte Result;
        public short TotalHit;
        public short TotalAc;
        public short MaxWeight;
        public short MaxHp;
        public short MaxMp;
        public short StrDelta;
        public short StaDelta;
        public short DexDelta;
        public short IntDelta;
        public short ChaDelta;
        public short FireR;
        public short ColdR;
        public short LightningR;
        public short MagicR;
        public short DiseaseR;
        public short PoisonR;
    }
    
    /// <summary>
    /// Open-KO birebir: EXCHANGE_DONE paketindeki item bilgisi.
    /// Wire: [pos: byte] [itemId: int] [count: short] [durability: short]
    /// </summary>
    public class TradeResultItem
    {
        public byte Pos;
        public int ItemId;
        public short Count;
        public short Durability;
    }

    /// <summary>
    /// Open-KO birebir: GetNpcInfo() — Npc.cpp:292-311
    /// NPC/Monster'ın region sistemi üzerinden client'a gönderilen bilgileri.
    /// NOT: C++ GetNpcInfo HP göndermez — HP sadece WIZ_TARGET_HP ile alınır.
    /// </summary>
    public class NpcInfoData
    {
        public long InstanceId;     // Open-KO: m_sNid
        public short Pid;           // Open-KO: m_sPid — model/picture ID
        public byte NpcType;        // Open-KO: m_tNpcType
        public int SellingGroup;    // Open-KO: m_iSellingGroup
        public short Size;          // Open-KO: m_sSize (default 100)
        public int Weapon1;         // Open-KO: m_iWeapon_1
        public int Weapon2;         // Open-KO: m_iWeapon_2
        public string Name;         // Open-KO: m_strName
        public byte Group;          // Open-KO: m_byGroup
        public byte Level;          // Open-KO: m_sLevel
        public float PosX;          // Open-KO: m_fCurX
        public float PosZ;          // Open-KO: m_fCurZ
        public float PosY;          // Open-KO: m_fCurY
        public byte Direction;      // Open-KO: m_byDirection
        public bool GateOpen;       // Open-KO: m_byGateOpen
        public byte ObjectType;     // Open-KO: m_byObjectType
        // NOT: C++ GetNpcInfo HP göndermez (Npc.cpp:292-311).
        // HP sadece oyuncu NPC'yi hedef seçtiğinde WIZ_TARGET_HP ile alınır.
    }

    /// <summary>
    /// Open-KO birebir: __FriendsInfo — UIVarious.h:37-43
    /// iID, bOnLine, bIsParty alanları server'dan status byte ile gelir.
    /// </summary>
    public class FriendInfoData
    {
        public string Name;         // Open-KO: szName
        public short Sid;           // Open-KO: iID (socket ID, -1=offline)
        public bool OnLine;        // Open-KO: bOnLine — status & 0x01
        public bool IsParty;       // Open-KO: bIsParty — status & 0x02
    }
}
