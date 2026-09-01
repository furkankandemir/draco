namespace EntropyOnline.Core
{
    /// <summary>
    /// Open-KO birebir: GameDefine.h sabitlerinin client-side karşılığı.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>Open-KO: ITEM_GOLD = 900000000 (GameDefine.h:106)</summary>
        public const int ITEM_GOLD = 900000000;

        /// <summary>Open-KO: MAX_ITEM_BUNDLE_DROP_PIECE = 6 (GameDefine.h:280)</summary>
        public const int MAX_ITEM_BUNDLE_DROP_PIECE = 6;

        /// <summary>Open-KO: MAX_PARTY_SIZE = 8 (define.h)</summary>
        public const int MAX_PARTY_SIZE = 8;
        
        // ==========================================
        // SINIFLAR (Open-KO: globals.h e_Class enum satır 69-111)
        // ==========================================
        
        // Karus base sınıfları
        public const byte CLASS_KA_WARRIOR    = 101;
        public const byte CLASS_KA_ROGUE      = 102;
        public const byte CLASS_KA_WIZARD     = 103;
        public const byte CLASS_KA_PRIEST     = 104;
        // Karus 1. iş sınıfları
        public const byte CLASS_KA_BERSERKER  = 105;
        public const byte CLASS_KA_HUNTER     = 107;
        public const byte CLASS_KA_SORCERER   = 109;
        public const byte CLASS_KA_SHAMAN     = 111;
        // Karus 2. iş sınıfları (master)
        public const byte CLASS_KA_GUARDIAN    = 106;
        public const byte CLASS_KA_PENETRATOR  = 108;
        public const byte CLASS_KA_NECROMANCER = 110;
        public const byte CLASS_KA_DARKPRIEST  = 112;
        
        // El Morad base sınıfları
        public const byte CLASS_EL_WARRIOR    = 201;
        public const byte CLASS_EL_ROGUE      = 202;
        public const byte CLASS_EL_WIZARD     = 203;
        public const byte CLASS_EL_PRIEST     = 204;
        // El Morad 1. iş sınıfları
        public const byte CLASS_EL_BLADE      = 205;
        public const byte CLASS_EL_RANGER     = 207;
        public const byte CLASS_EL_MAGE       = 209;
        public const byte CLASS_EL_CLERIC     = 211;
        // El Morad 2. iş sınıfları (master)
        public const byte CLASS_EL_PROTECTOR   = 206;
        public const byte CLASS_EL_ASSASSIN    = 208;
        public const byte CLASS_EL_ENCHANTER   = 210;
        public const byte CLASS_EL_DRUID       = 212;
    }
}
