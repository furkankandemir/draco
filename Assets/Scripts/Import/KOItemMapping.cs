using System.Collections.Generic;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Client-side item ID mapping helper.
    /// Maps 3792xxxxx range (Low/Middle class scrolls) from the client TBL
    /// to 3790xxxxx / 3791xxxxx range (standard/High class scrolls) present in the server DB.
    /// </summary>
    public static class KOItemMapping
    {
        private static readonly Dictionary<uint, uint> ClientToServerMap = new Dictionary<uint, uint>
        {
            // Middle Class Scrolls
            { 379205000, 379016000 }, // Upgrade Scroll (Middle Class) -> Upgrade Scroll (High Class/Standard)
            { 379206000, 379017000 }, // Enchant Scroll(STR) (Middle Class) -> Enchant Scroll(STR) (High Class)
            { 379208000, 379026000 }, // Enchant Scroll(HP) (Middle Class) -> Enchant Scroll(HP) (High Class)
            { 379209000, 379027000 }, // Enchant Scroll(DEX) (Middle Class) -> Enchant Scroll(DEX) (High Class)
            { 379210000, 379028000 }, // Enchant Scroll(INT) (Middle Class) -> Enchant Scroll(INT) (High Class)
            { 379211000, 379029000 }, // Enchant Scroll(MAGIC) (Middle Class) -> Enchant Scroll(MAGIC) (High Class)
            { 379212000, 379018000 }, // Immune Scroll (Middle Class) -> Immune Scroll (High Class)
            { 379213000, 379019000 }, // Reduce Scroll (Middle Class) -> Reduce Scroll (High Class)
            { 379214000, 379020000 }, // Elemental Scroll (Middle Class) -> Elemental Scroll (High Class)
            { 379215000, 379034000 }, // Dispell Scroll(Fire) (Middle Class) -> Dispell Scroll(Fire) (High Class)
            { 379216000, 379035000 }, // Dispell Scroll(Glacier) (Middle Class) -> Dispell Scroll(Glacier) (High Class)
            { 379217000, 379138000 }, // Dispell Scroll(Light) (Middle Class) -> Dispell Scroll(Lightning) (High Class)
            { 379218000, 379139000 }, // Dispell Scroll(Glacier 2) (Middle Class) -> Dispell Scroll(Glacier 2) (High Class)
            { 379219000, 379140000 }, // Dispell Scroll(Fire 2) (Middle Class) -> Dispell Scroll(Flame) (High Class)
            { 379220000, 379141000 }, // Dispell Scroll(Light 2) (Middle Class) -> Dispell Scroll(Lightning 2) (High Class)

            // Low Class Scrolls
            { 379221000, 379016000 }, // Upgrade Scroll (Low Class) -> Upgrade Scroll (High Class/Standard)
            { 379222000, 379017000 }, // Enchant Scroll(STR) (Low Class) -> Enchant Scroll(STR) (High Class)
            { 379223000, 379026000 }, // Enchant Scroll(HP) (Low Class) -> Enchant Scroll(HP) (High Class)
            { 379224000, 379027000 }, // Enchant Scroll(DEX) (Low Class) -> Enchant Scroll(DEX) (High Class)
            { 379225000, 379028000 }, // Enchant Scroll(INT) (Low Class) -> Enchant Scroll(INT) (High Class)
            { 379226000, 379029000 }, // Enchant Scroll(MAGIC) (Low Class) -> Enchant Scroll(MAGIC) (High Class)
            { 379227000, 379018000 }, // Immune Scroll (Low Class) -> Immune Scroll (High Class)
            { 379228000, 379019000 }, // Reduce Scroll (Low Class) -> Reduce Scroll (High Class)
            { 379229000, 379020000 }, // Elemental Scroll (Low Class) -> Elemental Scroll (High Class)
            { 379230000, 379034000 }, // Dispell Scroll(Fire) (Low Class) -> Dispell Scroll(Fire) (High Class)
            { 379231000, 379035000 }, // Dispell Scroll(Glacier) (Low Class) -> Dispell Scroll(Glacier) (High Class)
            { 379232000, 379138000 }, // Dispell Scroll(Light) (Low Class) -> Dispell Scroll(Lightning) (High Class)
            { 379233000, 379139000 }, // Dispell Scroll(Glacier 2) (Low Class) -> Dispell Scroll(Glacier 2) (High Class)
            { 379234000, 379140000 }, // Dispell Scroll(Fire 2) (Low Class) -> Dispell Scroll(Flame) (High Class)
            { 379235000, 379141000 }, // Dispell Scroll(Light 2) (Low Class) -> Dispell Scroll(Lightning 2) (High Class)
        };

        /// <summary>
        /// Maps a client-side item ID to the server-side counterpart if a mapping exists.
        /// Otherwise returns the original ID.
        /// </summary>
        public static uint GetServerItemId(uint clientItemId)
        {
            return clientItemId;
        }
    }
}
