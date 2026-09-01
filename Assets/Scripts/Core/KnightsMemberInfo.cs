namespace EntropyOnline.Core
{
    /// <summary>
    /// Open-KO birebir: __KnightsMemberInfo (UIVarious.h:102-109)
    /// Clan üye listesinde gösterilen bilgi yapısı.
    /// </summary>
    public class KnightsMemberInfo
    {
        /// <summary>Üye karakter adı</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Clan görev/rütbe
        /// C++ birebir: e_KnightsDuty enum
        /// 0=Unknown, 1=Chief, 2=ViceChief, 3=Officer, 4=Knight, 5=Trainee, 6=Punish
        /// </summary>
        public byte Duty { get; set; }

        /// <summary>Karakter seviyesi</summary>
        public short Level { get; set; }

        /// <summary>Karakter sınıfı (class)</summary>
        public byte Class { get; set; }

        /// <summary>Online durumu: true=online (yeşil), false=offline (gri)</summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// Görev string'ini döndürür.
        /// C++ birebir: UIVarious.cpp UpdateKnightsDuty satır 955-988
        /// </summary>
        public static string DutyToString(byte duty)
        {
            return duty switch
            {
                1 => "Chief",
                2 => "Vice Chief",
                3 => "Officer",
                4 => "Knight",
                5 => "Trainee",
                6 => "Punish",
                _ => "Unknown"
            };
        }
    }
}
