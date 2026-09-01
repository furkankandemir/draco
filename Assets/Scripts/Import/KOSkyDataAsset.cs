using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// .n3sky dosyasından dönüştürülmüş sky verisini tutan ScriptableObject.
/// KOSkyConverter tarafından oluşturulur.
/// </summary>
public class KOSkyDataAsset : ScriptableObject
{
    public string[] SunTextures = new string[3];
    public string[] CloudTextures = new string[6];
    public string MoonTexture;
    public List<DayChangeData> DayChanges = new();

    [Serializable]
    public class DayChangeData
    {
        public string Name;
        public int ChangeType;    // SkyDayChangeType enum
        public uint When;         // game-seconds since midnight
        public uint Param1;       // D3DCOLOR ARGB
        public uint Param2;
        public float HowLong;     // transition duration
    }
}
