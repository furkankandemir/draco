using System.Collections.Generic;
using UnityEngine;
using System.IO;
using EntropyOnline.UI;

namespace EntropyOnline.Import
{
    [System.Serializable]
    public class JsonMonsterDrop
    {
        public int id;
        public string name;
        public int level;
        public List<JsonDropItem> drops;
    }

    [System.Serializable]
    public class JsonDropItem
    {
        public int itemId;
        public int rate;
    }

    [System.Serializable]
    public class MonsterDropWrapper
    {
        public List<JsonMonsterDrop> monsters;
    }

    public static class KODropDataManager
    {
        private static bool s_IsLoaded = false;
        private static Dictionary<int, JsonMonsterDrop> s_MonsterDropsMap = new Dictionary<int, JsonMonsterDrop>();
        private static Dictionary<int, List<int>> s_ItemDropsMap = new Dictionary<int, List<int>>(); // Key: ItemID, Value: List of MonsterIDs
        private static Dictionary<int, List<int>> s_GroupsMap = new Dictionary<int, List<int>>();

        public static void EnsureLoaded()
        {
            if (s_IsLoaded) return;
            LoadDrops();
        }

        private static void ParseGroups(string jsonText)
        {
            s_GroupsMap.Clear();
            int groupsStartIndex = jsonText.IndexOf("\"groups\"");
            if (groupsStartIndex == -1) return;
            
            int groupsEndIndex = jsonText.IndexOf("\"monsters\"");
            if (groupsEndIndex == -1) groupsEndIndex = jsonText.Length;
            
            string groupsSection = jsonText.Substring(groupsStartIndex, groupsEndIndex - groupsStartIndex);
            
            // Regex to find "key": [ values ]
            var matches = System.Text.RegularExpressions.Regex.Matches(groupsSection, @"""(\d+)""\s*:\s*\[([^\]]+)\]");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count == 3)
                {
                    if (int.TryParse(match.Groups[1].Value, out int groupId))
                    {
                        var list = new List<int>();
                        string[] vals = match.Groups[2].Value.Split(',');
                        foreach (var v in vals)
                        {
                            if (int.TryParse(v.Trim(), out int itemId))
                            {
                                list.Add(itemId);
                            }
                        }
                        if (list.Count > 0)
                        {
                            s_GroupsMap[groupId] = list;
                        }
                    }
                }
            }
        }

        public static void LoadDrops()
        {
            s_MonsterDropsMap.Clear();
            s_ItemDropsMap.Clear();
            s_GroupsMap.Clear();

            TextAsset textAsset = Resources.Load<TextAsset>("KOData/MonsterDrops");
            if (textAsset == null)
            {
                Debug.LogWarning("[KODropDataManager] MonsterDrops.json file not found in Resources/KOData/");
                s_IsLoaded = true;
                return;
            }

            try
            {
                // First parse the groups dictionary manually
                ParseGroups(textAsset.text);

                MonsterDropWrapper wrapper = JsonUtility.FromJson<MonsterDropWrapper>(textAsset.text);
                if (wrapper != null && wrapper.monsters != null)
                {
                    foreach (var m in wrapper.monsters)
                    {
                        s_MonsterDropsMap[m.id] = m;

                        foreach (var d in m.drops)
                        {
                            // If this itemId is a Group ID, map all items inside it to this monster
                            if (s_GroupsMap.TryGetValue(d.itemId, out var groupItems))
                            {
                                foreach (var gItem in groupItems)
                                {
                                    if (!s_ItemDropsMap.ContainsKey(gItem))
                                    {
                                        s_ItemDropsMap[gItem] = new List<int>();
                                    }
                                    if (!s_ItemDropsMap[gItem].Contains(m.id))
                                    {
                                        s_ItemDropsMap[gItem].Add(m.id);
                                    }
                                }
                            }
                            else
                            {
                                // Normal item mapping
                                if (!s_ItemDropsMap.ContainsKey(d.itemId))
                                {
                                    s_ItemDropsMap[d.itemId] = new List<int>();
                                }
                                if (!s_ItemDropsMap[d.itemId].Contains(m.id))
                                {
                                    s_ItemDropsMap[d.itemId].Add(m.id);
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[KODropDataManager] Failed to parse MonsterDrops: {ex.Message}");
            }

            s_IsLoaded = true;
        }

        public static bool IsGroupId(int itemId)
        {
            EnsureLoaded();
            return s_GroupsMap.ContainsKey(itemId);
        }

        public static List<int> GetGroupItems(int groupId)
        {
            EnsureLoaded();
            if (s_GroupsMap.TryGetValue(groupId, out var list))
                return list;
            return new List<int>();
        }

        public static JsonMonsterDrop GetMonsterDrops(int monsterId)
        {
            EnsureLoaded();
            if (s_MonsterDropsMap.TryGetValue(monsterId, out var entry))
                return entry;
            return null;
        }

        public static List<int> GetMonstersByItem(int itemId)
        {
            EnsureLoaded();
            int baseId = itemId / 1000 * 1000;
            var monsterIds = new HashSet<int>();
            foreach (var kvp in s_ItemDropsMap)
            {
                if (kvp.Key / 1000 * 1000 == baseId)
                {
                    foreach (int mId in kvp.Value)
                    {
                        monsterIds.Add(mId);
                    }
                }
            }
            return new List<int>(monsterIds);
        }

        public static List<JsonMonsterDrop> SearchMonsters(string query)
        {
            EnsureLoaded();
            var results = new List<JsonMonsterDrop>();
            if (string.IsNullOrEmpty(query))
            {
                // Return first 50 monsters by default to prevent UI flooding
                foreach (var m in s_MonsterDropsMap.Values)
                {
                    results.Add(m);
                    if (results.Count >= 50) break;
                }
            }
            else
            {
                string cleanQuery = query.Replace('_', ' ').Replace("\u00A0", " ").Trim().ToLowerInvariant();
                foreach (var m in s_MonsterDropsMap.Values)
                {
                    if (m.name != null)
                    {
                        string cleanName = m.name.Replace('_', ' ').Replace("\u00A0", " ").ToLowerInvariant();
                        if (cleanName.Contains(cleanQuery))
                        {
                            results.Add(m);
                            if (results.Count >= 50) break;
                        }
                    }
                }
            }
            return results;
        }

        public static List<KOTableReader.TableItemBasic> SearchItems(string query)
        {
            EnsureLoaded();
            var results = new List<KOTableReader.TableItemBasic>();
            
            // KOItemSlotHandler / KOTableReader has s_pTbl_Items_Basic which is the master dictionary.
            // Let's search inside the loaded basic table items!
            var basicTable = KOInventory.s_pTbl_Items_Basic;
            if (basicTable == null)
            {
                // Fallback to ItemDataManager if table not ready
                return results;
            }

            var addedBaseIds = new HashSet<uint>();

            if (string.IsNullOrEmpty(query))
            {
                foreach (var itemId in s_ItemDropsMap.Keys)
                {
                    uint baseId = (uint)(itemId / 1000 * 1000);
                    if (addedBaseIds.Contains(baseId)) continue;

                    if (basicTable.TryGetValue(baseId, out var basic))
                    {
                        results.Add(basic);
                        addedBaseIds.Add(baseId);
                        if (results.Count >= 50) break;
                    }
                }
            }
            else
            {
                string cleanQuery = query.Replace('_', ' ').Replace("\u00A0", " ").Trim().ToLowerInvariant();
                foreach (var itemId in s_ItemDropsMap.Keys)
                {
                    uint baseId = (uint)(itemId / 1000 * 1000);
                    if (addedBaseIds.Contains(baseId)) continue;

                    if (basicTable.TryGetValue(baseId, out var basic))
                    {
                        bool isMatch = false;
                        if (basic.szName != null)
                        {
                            string cleanName = basic.szName.Replace('_', ' ').Replace("\u00A0", " ").ToLowerInvariant();
                            if (cleanName.Contains(cleanQuery))
                            {
                                isMatch = true;
                            }
                        }

                        // Check unique item header in extension tables if basic name doesn't match
                        if (!isMatch)
                        {
                            var ext = KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, basic.byExtIndex, itemId);
                            if (ext != null && ext.szHeader != null)
                            {
                                string cleanHeader = ext.szHeader.Replace('_', ' ').Replace("\u00A0", " ").ToLowerInvariant();
                                if (cleanHeader.Contains(cleanQuery))
                                {
                                    isMatch = true;
                                }
                            }
                        }

                        if (isMatch)
                        {
                            results.Add(basic);
                            addedBaseIds.Add(baseId);
                            if (results.Count >= 50) break;
                        }
                    }
                }
            }
            return results;
        }

        public static string GetDropRateCategory(int rate)
        {
            // KO veritabanı drop oranları 10000 (100.00%) üzerindedir:
            // >= 1000 (10%) -> High
            // 100-999 (1%-10%) -> Medium
            // < 100 (<1%) -> Low
            if (rate >= 1000)
                return "<color=green>High</color>";
            else if (rate >= 100)
                return "<color=yellow>Medium</color>";
            else
                return "<color=red>Low</color>";
        }
    }
}
