// ===================================================================================
// Open-KO birebir: globals.h e_ItemClass (satır 139-183)
//                  GameDef.h WEAPON_WEIGHT_STAND_* sabitleri (satır 123-125)
//
// Silah/zırh sınıflandırma enum'u. JudgeAnimation* fonksiyonlarında
// silah tipine göre animasyon seçiminde kullanılır.
// ===================================================================================

namespace EntropyOnline.Character
{
    /// <summary>
    /// Open-KO birebir: globals.h e_ItemClass (satır 139-183)
    /// </summary>
    public enum KOItemClass : short
    {
        ITEM_CLASS_DAGGER        = 11,   // dagger
        ITEM_CLASS_SWORD         = 21,   // onehandsword
        ITEM_CLASS_SWORD_2H      = 22,   // twohandsword
        ITEM_CLASS_AXE           = 31,   // onehandaxe
        ITEM_CLASS_AXE_2H        = 32,   // twohandaxe
        ITEM_CLASS_MACE          = 41,   // mace
        ITEM_CLASS_MACE_2H       = 42,   // twohandmace
        ITEM_CLASS_SPEAR         = 51,   // spear
        ITEM_CLASS_POLEARM       = 52,   // polearm

        ITEM_CLASS_SHIELD        = 60,   // shield

        ITEM_CLASS_BOW           = 70,   // shortbow
        ITEM_CLASS_BOW_CROSS     = 71,   // crossbow
        ITEM_CLASS_BOW_LONG      = 80,   // longbow

        ITEM_CLASS_EARRING       = 91,   // earring
        ITEM_CLASS_AMULET        = 92,   // necklace
        ITEM_CLASS_RING          = 93,   // ring
        ITEM_CLASS_BELT          = 94,   // belt
        ITEM_CLASS_CHARM         = 95,   // charm
        ITEM_CLASS_JEWEL         = 96,   // jewel
        ITEM_CLASS_POTION        = 97,   // potion
        ITEM_CLASS_SCROLL        = 98,   // scroll

        ITEM_CLASS_LAUNCHER      = 100,  // spear launcher

        ITEM_CLASS_STAFF         = 110,  // staff
        ITEM_CLASS_ARROW         = 120,  // arrow
        ITEM_CLASS_JAVELIN       = 130,  // javelin

        ITEM_CLASS_ARMOR_WARRIOR = 210,  // warrior armor
        ITEM_CLASS_ARMOR_ROGUE   = 220,  // rogue armor
        ITEM_CLASS_ARMOR_MAGE    = 230,  // mage armor
        ITEM_CLASS_ARMOR_PRIEST  = 240,  // priest armor

        ITEM_CLASS_ETC           = 251,  // miscellaneous
        ITEM_CLASS_CONSUMABLE    = 255,  // consumable with charges

        ITEM_CLASS_UNKNOWN       = -1
    }

    /// <summary>
    /// Open-KO birebir: GameDef.h satır 123-125
    /// Silah ağırlık eşikleri — A/B animasyon seçiminde kullanılır.
    /// Ağırlık >= eşik → B (ağır), < eşik → A (hafif)
    /// </summary>
    public static class KOWeaponWeight
    {
        public const float WEAPON_WEIGHT_STAND_SWORD = 5.0f;  // cpp:123
        public const float WEAPON_WEIGHT_STAND_AXE   = 5.0f;  // cpp:124
        public const float WEAPON_WEIGHT_STAND_BLUNT = 8.0f;  // cpp:125
    }
}
