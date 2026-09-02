using UnityEngine;

namespace EntropyOnline.Import
{
    public static class KOMaterialHelper
    {
        private static Material _cachedTemplate;

        public static Material CreateLitMaterial()
        {
            if (_cachedTemplate == null)
            {
                _cachedTemplate = Resources.Load<Material>("KO_DefaultLit");
            }

            if (_cachedTemplate != null)
            {
                return new Material(_cachedTemplate);
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) : null;
        }
    }
}
