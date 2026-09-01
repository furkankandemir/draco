using UnityEngine;
using System.Collections.Generic;

namespace EntropyOnline.Import
{
    // ================================================================
    // ScriptableObject wrapper'lar — .tbl binary tabloları Unity-native
    // asset formatında saklamak için. Editor'da KOTableConverter ile
    // doldurulur, runtime'da Resources.Load ile yüklenir.
    // ================================================================

    /// <summary>Item_Org_us.tbl → __TABLE_ITEM_BASIC</summary>
    public class KOItemOrgAsset : ScriptableObject
    {
        public KOTableReader.TableItemBasic[] items;

        [System.NonSerialized] private Dictionary<uint, KOTableReader.TableItemBasic> _dict;

        public Dictionary<uint, KOTableReader.TableItemBasic> ToDictionary()
        {
            if (_dict != null) return _dict;
            _dict = new Dictionary<uint, KOTableReader.TableItemBasic>();
            if (items != null)
                foreach (var item in items)
                    if (item != null && !_dict.ContainsKey(item.dwID))
                        _dict[item.dwID] = item;
            return _dict;
        }
    }

    /// <summary>Item_Ext_N_us.tbl → __TABLE_ITEM_EXT (tek bir ext tablosu)</summary>
    public class KOItemExtAsset : ScriptableObject
    {
        public int extIndex;
        public KOTableReader.TableItemExt[] items;

        [System.NonSerialized] private Dictionary<uint, KOTableReader.TableItemExt> _dict;

        public Dictionary<uint, KOTableReader.TableItemExt> ToDictionary()
        {
            if (_dict != null) return _dict;
            _dict = new Dictionary<uint, KOTableReader.TableItemExt>();
            if (items != null)
                foreach (var item in items)
                    if (item != null && !_dict.ContainsKey(item.dwID))
                        _dict[item.dwID] = item;
            return _dict;
        }
    }

    /// <summary>NPC_Looks.tbl → NpcLooksEntry (serializable mirror)</summary>
    public class KONpcLooksAsset : ScriptableObject
    {
        public NpcLooksTblEntry[] entries;
    }

    /// <summary>NPC_Looks.tbl serializable entry</summary>
    [System.Serializable]
    public class NpcLooksTblEntry
    {
        public uint dwID;
        public string szName;
        public string szJointFN;
        public string szAniFN;
        public string[] szPartFNs;
        public string szChrFN;
    }

    /// <summary>UPC_DefaultLooks.tbl → __TABLE_PLAYER_LOOKS</summary>
    public class KOPlayerLooksAsset : ScriptableObject
    {
        public KOTableReader.TablePlayerLooks[] entries;

        [System.NonSerialized] private Dictionary<uint, KOTableReader.TablePlayerLooks> _dict;

        public Dictionary<uint, KOTableReader.TablePlayerLooks> ToDictionary()
        {
            if (_dict != null) return _dict;
            _dict = new Dictionary<uint, KOTableReader.TablePlayerLooks>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !_dict.ContainsKey(e.dwID))
                        _dict[e.dwID] = e;
            return _dict;
        }
    }

    /// <summary>fx.tbl → __TABLE_FX</summary>
    public class KOFxTableAsset : ScriptableObject
    {
        public KOTableReader.TableFX[] entries;

        [System.NonSerialized] private Dictionary<uint, KOTableReader.TableFX> _dict;

        public Dictionary<uint, KOTableReader.TableFX> ToDictionary()
        {
            if (_dict != null) return _dict;
            _dict = new Dictionary<uint, KOTableReader.TableFX>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !_dict.ContainsKey(e.dwID))
                        _dict[e.dwID] = e;
            return _dict;
        }
    }

    /// <summary>NewChrValue.tbl → __TABLE_NEW_CHR</summary>
    public class KONewChrAsset : ScriptableObject
    {
        public KOTableReader.TableNewChr[] entries;

        [System.NonSerialized] private Dictionary<uint, KOTableReader.TableNewChr> _dict;

        public Dictionary<uint, KOTableReader.TableNewChr> ToDictionary()
        {
            if (_dict != null) return _dict;
            _dict = new Dictionary<uint, KOTableReader.TableNewChr>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !_dict.ContainsKey(e.dwID))
                        _dict[e.dwID] = e;
            return _dict;
        }
    }

    /// <summary>Quest_Menu_us.tbl + Quest_Talk_us.tbl + Quest_Content_us.tbl</summary>
    public class KOQuestDataAsset : ScriptableObject
    {
        public QuestMenuSerEntry[] menuEntries;
        public QuestTalkSerEntry[] talkEntries;
        public QuestContentSerEntry[] contentEntries;
    }

    [System.Serializable]
    public class QuestMenuSerEntry
    {
        public int dwID;
        public string szMenu;
    }

    [System.Serializable]
    public class QuestTalkSerEntry
    {
        public int dwID;
        public string szTalk;
    }

    [System.Serializable]
    public class QuestContentSerEntry
    {
        public int dwID;
        public int iReqLevel;
        public int iReqClass;
        public string szName;
        public string szDesc;
        public string szReward;
    }

    /// <summary>Skill_Magic_Main_us.tbl → __TABLE_UPC_SKILL</summary>
    public class KOSkillMainAsset : ScriptableObject
    {
        public SkillMainSerEntry[] entries;
    }

    /// <summary>Serializable mirror of SkillEntry</summary>
    [System.Serializable]
    public class SkillMainSerEntry
    {
        public int Id;
        public string EngName;
        public string Name;
        public string Desc;
        public int SelfAnimID1;
        public int SelfAnimID2;
        public int TargetAnimID;
        public int SelfFX1;
        public int SelfPart1;
        public int SelfFX2;
        public int SelfPart2;
        public int FlyingFX;
        public int TargetFX;
        public int TargetPart;
        public int Target;
        public int NeedLevel;
        public int NeedSkill;
        public int ExhaustMSP;
        public int ExhaustHP;
        public uint NeedItem;
        public uint ExhaustItem;
        public int CastTime;
        public int ReCastTime;
        public float IDK0;
        public float IDK1;
        public int PercentSuccess;
        public uint FirstTableType;
        public uint SecondTableType;
        public int ValidDist;
        public int IDK2;
    }

    /// <summary>skill_magic_1.tbl → __TABLE_UPC_SKILL_TYPE_1</summary>
    public class KOSkillType1Asset : ScriptableObject
    {
        public SkillType1SerEntry[] entries;
    }

    [System.Serializable]
    public class SkillType1SerEntry
    {
        public int Id;
        public int SuccessType;
        public int SuccessRatio;
        public int Power;
        public int Delay;
        public int ComboType;
        public int NumCombo;
        public int ComboDamage;
        public int ValidAngle;
        public int[] Act; // size 3
    }

    /// <summary>skill_magic_2.tbl → __TABLE_UPC_SKILL_TYPE_2</summary>
    public class KOSkillType2Asset : ScriptableObject
    {
        public SkillType2SerEntry[] entries;
    }

    [System.Serializable]
    public class SkillType2SerEntry
    {
        public int Id;
        public int SuccessType;
        public int Power;
        public int AddDamage;
        public int AddDist;
        public int NumArrow;
    }

    /// <summary>skill_magic_3.tbl → __TABLE_UPC_SKILL_TYPE_3</summary>
    public class KOSkillType3Asset : ScriptableObject
    {
        public SkillType3SerEntry[] entries;
    }

    [System.Serializable]
    public class SkillType3SerEntry
    {
        public int Id;
        public int Radius;
        public int DDType;
        public int StartDamage;
        public int DuraDamage;
        public int DurationTime;
        public int Attribute;
    }

    /// <summary>skill_magic_4.tbl → __TABLE_UPC_SKILL_TYPE_4</summary>
    public class KOSkillType4Asset : ScriptableObject
    {
        public SkillType4SerEntry[] entries;
    }

    [System.Serializable]
    public class SkillType4SerEntry
    {
        public int Id;
        public int BuffType;
        public int Radius;
        public int Duration;
        public int AttackSpeed;
        public int MoveSpeed;
        public int AC;
        public int ACPct;
        public int Attack;
        public int MagicAttack;
        public int MaxHP;
        public int MaxHPPct;
        public int MaxMP;
        public int MaxMPPct;
        public int Str;
        public int Sta;
        public int Dex;
        public int Int_;  // "Int" C# reserved word
        public int MAP;
        public int FireResist;
        public int ColdResist;
        public int LightningResist;
        public int MagicResist;
        public int DiseaseResist;
        public int PoisonResist;
        public int ExpPct;
    }

    /// <summary>FxTableParser.FxTableEntry serializable mirror</summary>
    [System.Serializable]
    public class FxSerEntry
    {
        public int Id;
        public string Name;
        public string FileName;
        public int SoundId;
        public byte AOE;
    }

    /// <summary>fx.tbl → FxTableEntry (FxTableParser format)</summary>
    public class KOFxParserAsset : ScriptableObject
    {
        public FxSerEntry[] entries;
    }

    /// <summary>ItemDataManager data — Item_Org_us.tbl + Item_Ext_0..23_us.tbl combined</summary>
    public class KOItemDataAsset : ScriptableObject
    {
        public ItemBasicSerEntry[] basicItems;
        public ItemExtGroup[] extGroups; // 24 element
    }

    [System.Serializable]
    public class ItemBasicSerEntry
    {
        public int ItemNum;   // dwID / 1000 * 1000 → base item num
        public byte ExtIndex;
        public string Name;
        public string Remark;
        public uint IDK0;
        public byte IDK1;
        public uint IDResrc;
        public uint IDIcon;
        public uint SoundID0;
        public uint SoundID1;
        public byte Class;
        public byte IsRobeType;
        public byte AttachPoint;
        public byte NeedRace;
        public byte NeedClass;
        public short Damage;
        public short AttackInterval;
        public short AttackRange;
        public short Weight;
        public short MaxDurability;
        public int Price;
        public int SaleType;
        public short Defense;
        public byte Contable;
        public uint EffectID1;
        public uint EffectID2;
        public sbyte NeedLevel;
        public sbyte IDK2;
        public byte NeedRank;
        public byte NeedTitle;
        public byte NeedStrength;
        public byte NeedStamina;
        public byte NeedDexterity;
        public byte NeedInteli;
        public byte NeedMagicAttack;
        public byte SellGroup;
        public byte Grade;
    }

    [System.Serializable]
    public class ItemExtGroup
    {
        public int extIndex;
        public ItemExtSerEntry[] items;
    }

    [System.Serializable]
    public class ItemExtSerEntry
    {
        public uint DwID;
        public string SzHeader;
        public int DwBaseID;
        public string SzRemark;
        public int DwIDK0;
        public int DwIDResrc;
        public int DwIDIcon;
        public byte ByMagicOrRare;
        public short Damage;
        public short AttackIntervalPercentage;
        public short HitRate;
        public short EvasionRate;
        public short SiMaxDurability;
        public short SiPriceMultiply;
        public short SiDefense;
        public short SiDefenseRateDagger;
        public short SiDefenseRateSword;
        public short SiDefenseRateBlow;
        public short SiDefenseRateAxe;
        public short SiDefenseRateSpear;
        public short SiDefenseRateArrow;
        public byte ByDamageFire;
        public byte ByDamageIce;
        public byte ByDamageThuner;
        public byte ByDamagePoison;
        public byte ByStillHP;
        public byte ByDamageMP;
        public byte ByStillMP;
        public byte ByReturnPhysicalDamage;
        public byte BySoulBind;
        public short SiBonusStr;
        public short SiBonusSta;
        public short SiBonusDex;
        public short SiBonusInt;
        public short SiBonusMagicAttak;
        public short SiBonusHP;
        public short SiBonusMSP;
        public short SiRegistFire;
        public short SiRegistIce;
        public short SiRegistElec;
        public short SiRegistMagic;
        public short SiRegistPoison;
        public short SiRegistCurse;
        public int DwEffectID1;
        public int DwEffectID2;
        public short SiNeedLevel;
        public short SiNeedRank;
        public short SiNeedTitle;
        public short SiNeedStrength;
        public short SiNeedStamina;
        public short SiNeedDexterity;
        public short SiNeedInteli;
        public short SiNeedMagicAttack;
    }
}
