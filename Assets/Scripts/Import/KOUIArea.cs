using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO birebir: CN3UIArea (N3UIArea.h:31-57)
    /// UIF dosyasından yüklenen area elementlerine eklenir.
    /// m_eAreaType bilgisini saklar — GetChildAreaByiOrder ile bulunur.
    ///
    /// eUI_AREA_TYPE değerleri (N3UIArea.h:13-29):
    ///   0 = NONE
    ///   1 = SLOT
    ///   2 = INV
    ///   3 = TRADE_NPC
    ///   4 = PER_TRADE_MY
    ///   5 = PER_TRADE_OTHER
    ///   6 = DROP_ITEM
    ///   7 = SKILL_TREE
    ///   8 = SKILL_HOTKEY
    ///   9 = REPAIR_INV
    ///  10 = REPAIR_NPC
    ///  11 = TRADE_MY
    ///  12 = PER_TRADE_INV
    /// </summary>
    public class KOUIArea : MonoBehaviour
    {
        /// <summary>eUI_AREA_TYPE — Open-KO birebir</summary>
        public int AreaType = -1;
    }
}
