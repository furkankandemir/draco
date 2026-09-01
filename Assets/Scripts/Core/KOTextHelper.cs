namespace EntropyOnline.Core
{
    /// <summary>
    /// C++ GameBase.cpp GetTextByClass/GetTextByRace/GetTextByNation birebir port.
    /// Tüm string'ler text_resources.h IDS değerlerinden alınmıştır.
    /// 
    /// KRİTİK: charClass % 100 YAPILMAZ — Karus ve El Morad sınıfları farklı isimler alır.
    /// </summary>
    public static class KOTextHelper
    {
        /// <summary>
        /// C++ GameBase.cpp GetTextByClass() satır 101-213 birebir.
        /// String'ler text_resources.h satır 24-56'dan.
        /// Not: C++ tam eClass değeriyle switch yapar — Karus ve El Morad farklı isimler alır.
        /// </summary>
        public static string GetTextByClass(byte eClass)
        {
            return eClass switch
            {
                // KINDOF (pre-specialization) — globals.h:71-82
                1 => "Warrior",             // IDS_CLASS_KINDOF_WARRIOR (1304)
                2 => "Rogue",               // IDS_CLASS_KINDOF_ROGUE (1305)
                3 => "Magician",            // IDS_CLASS_KINDOF_WIZARD (1306)
                4 => "Priest",              // IDS_CLASS_KINDOF_PRIEST (1307)
                5 => "Offensive Warrior",   // IDS_CLASS_KINDOF_ATTACK_WARRIOR (1308)
                6 => "Defensive Warrior",   // IDS_CLASS_KINDOF_DEFEND_WARRIOR (1309)
                7 => "Archer",              // IDS_CLASS_KINDOF_ARCHER (1310)
                8 => "Assassin",            // IDS_CLASS_KINDOF_ASSASSIN (1311)
                9 => "Offensive Magician",  // IDS_CLASS_KINDOF_ATTACK_WIZARD (1312)
                10 => "Pet Magician",       // IDS_CLASS_KINDOF_PET_WIZARD (1313)
                11 => "Healing Priest",     // IDS_CLASS_KINDOF_HEAL_PRIEST (1314)
                12 => "Cursing Priest",     // IDS_CLASS_KINDOF_CURSE_PRIEST (1315)

                // Karus base classes (101-104) — C++ satır 142-156
                101 => "Warrior",           // CLASS_KA_WARRIOR / IDS_CLASS_WARRIOR (1420)
                102 => "Rogue",             // CLASS_KA_ROGUE / IDS_CLASS_ROGUE (1418)
                103 => "Magician",          // CLASS_KA_WIZARD / IDS_CLASS_WIZARD (1421)
                104 => "Priest",            // CLASS_KA_PRIEST / IDS_CLASS_PRIEST (1417)

                // Karus specializations (105-112)
                105 => "Warrior",
                106 => "Warrior",
                107 => "Rogue",
                108 => "Rogue",
                109 => "Mage",
                110 => "Mage",
                111 => "Priest",
                112 => "Priest",

                // El Morad base classes (201-204)
                201 => "Warrior",
                202 => "Rogue",
                203 => "Mage",
                204 => "Priest",

                // El Morad specializations (205-212)
                205 => "Warrior",
                206 => "Warrior",
                207 => "Rogue",
                208 => "Rogue",
                209 => "Mage",
                210 => "Mage",
                211 => "Priest",
                212 => "Priest",

                _ => "Unknown Class"        // C++ default case
            };
        }

        /// <summary>
        /// C++ GameBase.cpp GetTextByRace() satır 447-480 birebir.
        /// String'ler text_resources.h satır 246-254'ten.
        /// </summary>
        public static string GetTextByRace(byte race)
        {
            return race switch
            {
                1 => "Arch Tuarek",           // IDS_RACE_KA_ARKTUAREK (3605)
                2 => "Tuarek",                // IDS_RACE_KA_TUAREK (3607)
                3 => "Wrinkle Tuarek",        // IDS_RACE_KA_WRINKLETUAREK (3608)
                4 => "Puri Tuarek",           // IDS_RACE_KA_PURITUAREK (3606)
                11 => "Barbarian",            // IDS_RACE_EL_BABARIAN (3602)
                12 => "Male El Moradian",     // IDS_RACE_EL_MAN (3603)
                13 => "Female El Moradian",   // IDS_RACE_EL_WOMEN (3604)
                _ => "Unconfirmed race"       // IDS_RACE_UNKNOWN (3609) — C++ default case
            };
        }

        /// <summary>
        /// C++ GameBase.cpp GetTextByNation() satır 429-445 birebir.
        /// String'ler text_resources.h satır 202-204'ten.
        /// </summary>
        public static string GetTextByNation(byte nation)
        {
            return nation switch
            {
                1 => "Karus",                 // IDS_NATION_KARUS (3102)
                2 => "El Morad",              // IDS_NATION_ELMORAD (3101)
                _ => "Unconfirmed nation"     // IDS_NATION_UNKNOWN (3103) — C++ default case
            };
        }
    }
}
