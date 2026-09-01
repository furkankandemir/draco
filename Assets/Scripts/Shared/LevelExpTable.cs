namespace EntropyOnline.Shared
{
    /// <summary>
    /// Open-KO'daki LEVEL_UP tablosunun birebir C# karşılığı.
    /// Her seviye için gereken toplam EXP miktarını tanımlar.
    /// 
    /// Kaynak: openko-db/ManualSetup/6_InsertData_LEVEL_UP.sql
    /// </summary>
    public static class LevelExpTable
    {
        private static readonly long[] ExpTable = new long[]
        {
            0,          // Index 0 — kullanılmaz
            50,         // Level 1
            100,        // Level 2
            190,        // Level 3
            342,        // Level 4
            581,        // Level 5
            929,        // Level 6
            1393,       // Level 7
            1950,       // Level 8
            2535,       // Level 9
            5070,       // Level 10
            6084,       // Level 11
            7300,       // Level 12
            8760,       // Level 13
            10512,      // Level 14
            12614,      // Level 15
            15136,      // Level 16
            18163,      // Level 17
            21795,      // Level 18
            26154,      // Level 19
            52308,      // Level 20
            60154,      // Level 21
            69177,      // Level 22
            79553,      // Level 23
            91485,      // Level 24
            105207,     // Level 25
            120988,     // Level 26
            139136,     // Level 27
            160006,     // Level 28
            184006,     // Level 29
            368012,     // Level 30
            404813,     // Level 31
            445294,     // Level 32
            489823,     // Level 33
            538805,     // Level 34
            808207,     // Level 35
            889027,     // Level 36
            977929,     // Level 37
            1075721,    // Level 38
            1183293,    // Level 39
            2366586,    // Level 40
            2603244,    // Level 41
            2863568,    // Level 42
            3149924,    // Level 43
            3464916,    // Level 44
            5197374,    // Level 45
            5717111,    // Level 46
            6288822,    // Level 47
            6917704,    // Level 48
            7609474,    // Level 49
            15218948,   // Level 50
            16740842,   // Level 51
            18414926,   // Level 52
            20256418,   // Level 53
            22282059,   // Level 54
            33423088,   // Level 55
            36765396,   // Level 56
            40441935,   // Level 57
            44486128,   // Level 58
            48934740,   // Level 59
            73402110,   // Level 60
            132123798,  // Level 61
            135336177,  // Level 62
            139869794,  // Level 63
            135856773,  // Level 64
            133442450,  // Level 65
            132786695,  // Level 66
            134065364,  // Level 67
            137471900,  // Level 68
            133219090,  // Level 69
            211540999,  // Level 70
            242695098,  // Level 71
            276964607,  // Level 72
            214661067,  // Level 73
            256127173,  // Level 74
            201739890,  // Level 75
            251913879,  // Level 76
            207105266,  // Level 77
            267815792,  // Level 78
            234597371,  // Level 79
            508057108,  // Level 80
            988862818,  // Level 81
            977749099,  // Level 82
            1075524008, // Level 83
            1183076408, // Level 84
            1301384048, // Level 85
            1431522452, // Level 86
            1574674697, // Level 87
            1732142166, // Level 88
            1905356382, // Level 89
            2095892020  // Level 90
        };

        public static long GetExpForLevel(int level)
        {
            if (level < 1 || level >= ExpTable.Length)
                return 0;
            return ExpTable[level];
        }

        /// <summary>Open-KO: globals.h satır 34 — MAX_LEVEL = 83 (v1.298)</summary>
        public const int MAX_LEVEL = 83;
    }
}
