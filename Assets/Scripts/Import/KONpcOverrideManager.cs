using System.Collections.Generic;
using UnityEngine;

namespace EntropyOnline.Import
{
    public static class KONpcOverrideManager
    {
        // Maps appearance ID (looks.Id / dwID in NPC_Looks.tbl) to new prefab path under Resources
        private static readonly Dictionary<uint, string> _prefabMapping = new Dictionary<uint, string>();

        // Cache to store lookups at runtime
        private static readonly Dictionary<uint, string> _resolvedPathCache = new Dictionary<uint, string>();

        // Custom position, rotation, and scale offsets for overridden creatures
        public struct NpcOffset
        {
            public Vector3 RelativePosition;
            public Vector3 RelativeRotation;
            public Vector3 RelativeScale;

            public NpcOffset(Vector3 pos, Vector3 rot, Vector3 scale)
            {
                RelativePosition = pos;
                RelativeRotation = rot;
                RelativeScale = scale;
            }
        }

        /// <summary>
        /// Resolves the override prefab path under Resources/ for a given look ID.
        /// Checks Resources/Monster/Overrides/{lookId} first, then falls back to _prefabMapping.
        /// </summary>
        public static string GetOverridePrefabPath(uint lookId)
        {
            if (_resolvedPathCache.TryGetValue(lookId, out string cachedPath))
            {
                return cachedPath;
            }

            // 1. Convention check: Resources/Monster/Overrides/{lookId}
            string conventionPath = $"Monster/Overrides/{lookId}";
            GameObject tempPrefab = Resources.Load<GameObject>(conventionPath);
            if (tempPrefab != null)
            {
                _resolvedPathCache[lookId] = conventionPath;
                return conventionPath;
            }

            // 2. Mapping check
            if (_prefabMapping.TryGetValue(lookId, out string mappedPath))
            {
                _resolvedPathCache[lookId] = mappedPath;
                return mappedPath;
            }

            // Do not cache null values to prevent stale resolution state
            return null;
        }

        public static void ClearCache()
        {
            _resolvedPathCache.Clear();
        }

        /// <summary>
        /// Applies custom position, rotation, and scale overrides to the instantiated creature object.
        /// </summary>
        public static void ApplyNpcOverrides(uint lookId, GameObject npcObj)
        {
            // Custom offsets removed from runtime code. Scalings are stored directly in serialized prefab assets.
        }
    }
}
