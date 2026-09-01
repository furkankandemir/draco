using System;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Zone başına tüm convert edilmiş veriyi tutan ScriptableObject.
    /// Editor'da KOZoneConverter tarafından oluşturulur.
    /// Runtime'da WorldBuilder tarafından Resources.Load ile yüklenir.
    /// </summary>
    [CreateAssetMenu(fileName = "NewZoneAsset", menuName = "KO/Zone Asset")]
    public class KOZoneAsset : ScriptableObject
    {
        [Header("Terrain")]
        public TerrainData terrainData;
        // public Texture2D compositeTexture; // Unused legacy reference to large .asset textures (replaced by compressed PNGs in TerrainAssets)
        public float terrainBaseY;
        public float terrainSizeY;
        public float worldSize;

        [Header("Shapes")]
        public KOShapeEntry[] shapes = Array.Empty<KOShapeEntry>();

        [Header("Collision")]
        public float mapWidth;
        public float mapLength;

        [Header("Water")]
        public KOWaterEntry[] rivers = Array.Empty<KOWaterEntry>();
        public KOWaterEntry[] ponds = Array.Empty<KOWaterEntry>();

        [Header("Lights")]
        public KOLightEntry[] lights = Array.Empty<KOLightEntry>();

        [Header("Sky")]
        public Material skyboxMaterial;
    }

    [Serializable]
    public class KOShapeEntry
    {
        public string name;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public uint shapeType;
        public int eventID;
        public int eventType;
        public int npcID;
        public int npcStatus;
        public int belong;
        public bool isCustom;
        public KOPartEntry[] parts = Array.Empty<KOPartEntry>();
    }

    [Serializable]
    public class KOPartEntry
    {
        public Mesh mesh;
        public Material material;
        public Material[] materials;
        public Mesh colliderMesh; // Gövde-only mesh (yaprak hariç) — export sırasında oluşturulur
        public string textureName;
        public string[] textureNames;
        public Vector3 pivot;
        public float texFPS;
        public Texture2D[] animTextures;
        public uint renderFlags;
        public uint srcBlend;
        public uint destBlend;
    }

    [Serializable]
    public class KOWaterEntry
    {
        public Mesh mesh;
        public Material material;
        public Vector2[] baseUVs;
        public string waveTextureName;
    }

    [Serializable]
    public class KOLightEntry
    {
        public string name;
        public int type; // 1=Point, 2=Spot, 3=Directional
        public Vector3 position;
        public Quaternion rotation;
        public Color diffuse;
        public Color specular;
        public Color ambient;
        public Vector3 direction;
        public float range;
        public float falloff;
        public float attenuation0;
        public float attenuation1;
        public float attenuation2;
        public float theta;
        public float phi;
        public bool isOn;
    }
}
