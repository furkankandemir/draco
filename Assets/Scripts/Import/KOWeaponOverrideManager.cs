using System.Collections.Generic;
using UnityEngine;

namespace EntropyOnline.Import
{
    public static class KOWeaponOverrideManager
    {
        // Orijinal .n3cplug dosya adını yeni prefab yoluna eşler
        private static readonly Dictionary<string, string> _prefabMapping = new Dictionary<string, string>();

        // Yeni modele özel ince ayar offsetleri
        private static readonly Dictionary<string, WeaponOffset> _offsetMapping = new Dictionary<string, WeaponOffset>()
        {
            {
                "1_9025_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Match user's Inspector rotation
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_9021_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Match priest weapon rotation (Lobo style)
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_9041_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Match priest weapon rotation (Lobo style)
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_9061_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    HasCustomLeftHand = true,
                    CustomLeftPosition = new Vector3(-0.02f, 0f, 0f),
                    CustomLeftRotation = new Vector3(-90f, 180f, 0f),

                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_9930_10_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f), // 2H Hammer style rotation to face forward
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_8111_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.04f, 0.35f, -0.03f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f), // Enlarge/shrink as appropriate, set to 0.6f
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_8051_00_1", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.04f, 0.35f, -0.03f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(0.7f, 0.7f, 0.7f), // Match Mythril Staff scale (0.7f)
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4111_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4021_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4511_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3021_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3551_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3531_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3041_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2011_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2021_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2091_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2081_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2521_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2531_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2601_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2591_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2571_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2551_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2041_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4025_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4061_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4531_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2641_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3042_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3511_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5031_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.04f, 0.35f, -0.03f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5531_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.035f, 0.4f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f), // No scale reduction
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3561_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3621_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_3111_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2151_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2131_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2121_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1.1f, 1.1f, 1.1f), // Enlarge to 1.1f
                    IsAbsolute = true,
                    
                    HasCustomLeftHand = true,
                    CustomLeftPosition = new Vector3(-0.02f, 0f, 0f),
                    CustomLeftRotation = new Vector3(-90f, 180f, 0f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2101_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f), // Default scale 1.0f
                    IsAbsolute = true,
                    
                    HasCustomLeftHand = true,
                    CustomLeftPosition = new Vector3(-0.02f, 0f, 0f),
                    CustomLeftRotation = new Vector3(-90f, 180f, 0f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2111_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f), // Default scale 1.0f
                    IsAbsolute = true,
                    
                    HasCustomLeftHand = true,
                    CustomLeftPosition = new Vector3(-0.02f, 0f, 0f),
                    CustomLeftRotation = new Vector3(-90f, 180f, 0f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2631_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_2621_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1.125f, 1.125f, 1.125f), // Shrink by 25% from 1.5f
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1031_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f), // Keskin tarafı aşağı
                    RelativeScale = new Vector3(0.55f, 0.55f, 0.55f), // Orijinali kılıç olduğu için boyutu biraz küçülterek hançer formuna getirdik
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1021_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.45f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1041_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.45f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1051_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Hançeri elin içinden öne doğru uzatmak için X ekseninde -90 derece
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f),
                    
                    // İkon oluşturma için özel kamera/hizalama değerleri
                    IconRotation = new Vector3(-45f, -90f, -45f), // Sol-üste çapraz bakması için rotasyon
                    IconPosition = new Vector3(0.105f, -0.11f, 0.5f), // Kameraya göre hizası (sağa kaydırılarak ortalandı)
                    IconScale = 2.0f                              // Boyut 2.0'ye çıkarılarak boşluklar dengelendi
                }
            },
            {
                "1_1061_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0.105f, -0.11f, 0.5f),
                    IconScale = 2.0f
                }
            },
            {
                "1_1081_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Elde düzgün durması için rotasyon
                    RelativeScale = new Vector3(0.5f, 0.5f, 0.5f), // Ölçek %50'ye düşürüldü
                    
                    // İkon oluşturma için özel kamera/hizalama değerleri (Sol boşluğu kapatmak ve ucu kurtarmak için sola-aşağı kaydırıldı)
                    IconRotation = new Vector3(-45f, -90f, -45f), // Stiletto ile birebir aynı rotasyon
                    IconPosition = new Vector3(0.11f, -0.17f, 0.5f),   // Sola (0.18 -> 0.11) ve aşağı (Y: -0.17) kaydırılarak mükemmel denge sağlandı
                    IconScale = 1.4f                              // Ölçek 1.4f
                }
            },
            {
                "1_8011_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Asanın boyunu oyunun beklediği Z eksenine hizalamak için X ekseninde -90 derece döndürüldü
                    RelativeScale = new Vector3(1f, 1f, 0.8f),    // Enini ve kalınlığını bozmadan boyunu %20 kısalttık
                    
                    // İkon oluşturma için özel kamera/hizalama değerleri (Generator bunu otomatik ortalar ve sığdırır)
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_8031_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Asanın boyunu oyunun beklediği Z eksenine hizalamak için X ekseninde -90 derece döndürüldü
                    RelativeScale = new Vector3(0.7f, 0.7f, 0.7f), // Genel boyutunu orantısal olarak %30 küçülttük
                    
                    // İkon oluşturma için özel kamera/hizalama değerleri (Generator bunu otomatik ortalar ve sığdırır)
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_8051_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(0.7f, 0.7f, 0.7f), // N-hance asaları için %30 küçültme orantılı duracaktır
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1071_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Hançer gibi elde düzgün durması için
                    RelativeScale = new Vector3(0.55f, 0.55f, 0.55f), // Orijinali kılıç olduğu için boyutu biraz küçülterek hançer formuna getirdik
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1111_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1121_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f), // Elde düzgün durması için
                    RelativeScale = new Vector3(0.4f, 0.4f, 0.4f), // Hançer ebatlarında kılıç (%40 ölçek)
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1910_11_1", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f), // Keskin kenarı aşağı bakacak şekilde 180 derece döndürüldü
                    RelativeScale = new Vector3(0.5f, 0.5f, 0.5f), // Ölçek %50 yapıldı
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1101_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f), // Elde düzgün durması için
                    RelativeScale = new Vector3(0.4f, 0.4f, 0.4f), // Cleaver için kılıç modelini %40 ölçeklendiriyoruz
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1930_10_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f), // Keskin kenarı aşağı bakacak şekilde 180 derece döndürüldü
                    RelativeScale = new Vector3(0.5f, 0.5f, 0.5f), // Dark Vane ile aynı boyutta (%50 ölçek)
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1931_10_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f),
                    RelativeScale = new Vector3(0.5f, 0.5f, 0.5f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1931_20_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f),
                    RelativeScale = new Vector3(0.5f, 0.5f, 0.5f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1011_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.45f), // Tüm boyut %25 küçültüldü (1.0->0.75, Z ise 0.6->0.45 oldu)
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6011_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Kullanıcının Inspector'da bulduğu tam konum
                    RelativeRotation = new Vector3(90f, -25f, 0f),       // Kullanıcının Inspector'da bulduğu tam yön açısı
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),       // Kullanıcının Inspector'da bulduğu tam ölçek
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6211_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Diğer yay ile aynı paket olduğu için birebir aynı oturtma koordinatları
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6111_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Aynı yay paketi olduğu için yine birebir aynı koordinatlar
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6831_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Yay yerleşimi standart offseti
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6121_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Yay yerleşimi standart offseti
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6910_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Yay yerleşimi standart offseti
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6930_20_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Yay yerleşimi standart offseti
                    RelativeRotation = new Vector3(-90f, -180f, -25f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6930_10_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Yay yerleşimi standart offseti
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_6841_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(-0.03f, 0.08f, 0.05f), // Diğer yaylar ile aynı oturtma koordinatları
                    RelativeRotation = new Vector3(90f, -25f, 0f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7153_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7011_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.07f, -0.1f, 0f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.7f, 0.7f, 0.7f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7015_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.07f, -0.1f, 0f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.7f, 0.7f, 0.7f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7021_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7025_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7031_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7041_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7051_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.8f, 0.8f, 0.8f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7111_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.9f, 0.9f, 0.9f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7061_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.05f, -0.05f, -0.05f),
                    RelativeRotation = new Vector3(0f, -10f, 10f),
                    RelativeScale = new Vector3(0.9f, 0.9f, 0.9f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7157_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7025_10_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7021_10_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7155_00_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7910_02_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7910_20_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7911_20_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_7930_10_0", new WeaponOffset
                {
                    IsAbsolute = true,
                    RelativePosition = new Vector3(0.09f, -0.11f, -0.1f),
                    RelativeRotation = new Vector3(10f, -23f, -5f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1910_20_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.04f, 0f, -0.02f),
                    RelativeRotation = new Vector3(-90f, -10f, -180f),
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f),
                    
                    HasCustomLeftHand = true,
                    CustomLeftPosition = new Vector3(-0.05f, 0.05f, 0.02f),
                    CustomLeftRotation = new Vector3(110f, 3f, -18f),

                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_1091_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, 180f), // Keskin tarafı aşağı
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f),
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5511_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(1f, 1f, 1f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4551_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0.25f, 0.015f),
                    RelativeRotation = new Vector3(-80f, 0f, 180f),
                    RelativeScale = new Vector3(0.6f, 0.6f, 0.6f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_4621_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5621_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0.4f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5551_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0f, 0.4f, 0f),
                    RelativeRotation = new Vector3(-90f, 180f, 0f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5041_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.035f, 0.4f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5081_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.035f, 0.4f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(0.75f, 0.75f, 0.75f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5930_10_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.02f, 0.2f, 0f),
                    RelativeRotation = new Vector3(-90f, 0f, -180f),
                    RelativeScale = new Vector3(0.7f, 0.7f, 0.7f),
                    IsAbsolute = true,
                    
                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            },
            {
                "1_5121_00_0", new WeaponOffset
                {
                    RelativePosition = new Vector3(0.03f, 0.25f, -0.01f),
                    RelativeRotation = new Vector3(-90f, -7.197f, -180f),
                    RelativeScale = new Vector3(1.0f, 1.0f, 1.0f),
                    IsAbsolute = true,
                    
                    HasCustomLeftHand = true,
                    CustomLeftPosition = new Vector3(-0.03f, 0.25f, 0.03f),
                    CustomLeftRotation = new Vector3(-90f, 0f, 180f),

                    IconRotation = new Vector3(-45f, -90f, -45f),
                    IconPosition = new Vector3(0f, 0f, 0.5f),
                    IconScale = 1.0f
                }
            }
        };

        public struct WeaponOffset
        {
            public Vector3 RelativePosition;
            public Vector3 RelativeRotation;
            public Vector3 RelativeScale;
            public bool IsAbsolute; // Eğer true ise, DirectX değerlerini yok sayarak doğrudan bu transform değerlerini uygular

            // Sol el için manuel rotasyon/pozisyon ezme
            public bool HasCustomLeftHand;
            public Vector3 CustomLeftPosition;
            public Vector3 CustomLeftRotation;

            // İkon çekim parametreleri
            public Vector3 IconRotation;
            public Vector3 IconPosition;
            public float IconScale;
        }

        public static IReadOnlyDictionary<string, string> PrefabMapping => _prefabMapping;
        public static IReadOnlyDictionary<string, WeaponOffset> OffsetMapping => _offsetMapping;

        private static string NormalizePlugFileName(string plugFileName)
        {
            if (string.IsNullOrEmpty(plugFileName)) return plugFileName;
            
            string key = plugFileName.ToLower().Trim();
            
            // Sherion (normal & rebirth, all upgrade levels) -> 1_1930_10_0
            if (key.StartsWith("1_1930_") || key.StartsWith("1_1931_"))
            {
                return "1_1930_10_0";
            }
            // Dark Vane (normal & rebirth, all upgrade levels) -> 1_1910_11_1
            if (key.StartsWith("1_1910_1") || key.StartsWith("1_1911_1"))
            {
                return "1_1910_11_1";
            }
            // Cold-Hearted Dagger (normal & rebirth, all upgrade levels) -> 1_1910_20_0
            if (key.StartsWith("1_1910_2") || key.StartsWith("1_1910_4") || 
                key.StartsWith("1_1911_2") || key.StartsWith("1_1911_4"))
            {
                return "1_1910_20_0";
            }
            // Shard (normal & rebirth, all upgrade levels) -> 1_1121_00_0
            if (key.StartsWith("1_1121_") || key.StartsWith("1_1122_"))
            {
                return "1_1121_00_0";
            }
            // Cleaver (normal & rebirth, all upgrade levels) -> 1_1101_00_0
            if (key.StartsWith("1_1101_") || key.StartsWith("1_1102_"))
            {
                return "1_1101_00_0";
            }
            // Kukry (normal & rebirth, all upgrade levels) -> 1_1091_00_0
            if (key.StartsWith("1_1091_") || key.StartsWith("1_1092_"))
            {
                return "1_1091_00_0";
            }
            // Knife (normal & rebirth, all upgrade levels) -> 1_1031_00_0
            if (key.StartsWith("1_1031_") || key.StartsWith("1_1032_"))
            {
                return "1_1031_00_0";
            }
            // Fine Yard (normal & rebirth, all upgrade levels) -> 1_1021_00_0
             if (key.StartsWith("1_1021_") || key.StartsWith("1_1022_"))
            {
                return "1_1021_00_0";
            }
            // Dirk (normal & rebirth, all upgrade levels) -> 1_1041_00_0
            if (key.StartsWith("1_1041_") || key.StartsWith("1_1042_"))
            {
                return "1_1041_00_0";
            }
            // Mail Breaker (normal & rebirth, all upgrade levels) -> 1_1061_00_0
            if (key.StartsWith("1_1061_") || key.StartsWith("1_1062_"))
            {
                return "1_1061_00_0";
            }
            // Syphioric (normal & rebirth, all upgrade levels) -> 1_7930_10_0
            if (key.StartsWith("1_7930_") || key.StartsWith("1_7931_"))
            {
                return "1_7930_10_0";
            }
            // Wooden Shield (normal & rebirth, all upgrade levels) -> 1_7015_00_0
            if (key.StartsWith("1_7015_") || key.StartsWith("1_7016_"))
            {
                return "1_7015_00_0";
            }
            // Chitin Shield (normal & rebirth, all upgrade levels) -> 1_7025_10_0
            if (key.StartsWith("1_7025_10") || key.StartsWith("1_7026_10"))
            {
                return "1_7025_10_0";
            }
            
            // Round Shield (normal & rebirth, all upgrade levels) -> 1_7025_00_0
            if (key.StartsWith("1_7025_") || key.StartsWith("1_7026_"))
            {
                return "1_7025_00_0";
            }
            // Octagon Shield (normal & rebirth, all upgrade levels) -> 1_7041_00_0
            if (key.StartsWith("1_7041_") || key.StartsWith("1_7042_"))
            {
                return "1_7041_00_0";
            }
            // Round Kite Shield (normal & rebirth, all upgrade levels) -> 1_7061_00_0
            if (key.StartsWith("1_7061_") || key.StartsWith("1_7062_"))
            {
                return "1_7061_00_0";
            }
            // Chitin Bow / Hunters Bow (normal & rebirth, all upgrade levels) -> 1_6121_00_0
            if (key.StartsWith("1_6121_") || key.StartsWith("1_6122_"))
            {
                return "1_6121_00_0";
            }

            // Enion Bow (normal & rebirth, all upgrade levels) -> 1_6910_00_0
            if (key.StartsWith("1_6910_") || key.StartsWith("1_6911_"))
            {
                return "1_6910_00_0";
            }

            // Eagle's Eye (normal & rebirth, all upgrade levels) -> 1_6930_20_0
            if (key.StartsWith("1_6930_2") || key.StartsWith("1_6931_2"))
            {
                return "1_6930_20_0";
            }

            // Helenid Crossbow (normal & rebirth, all upgrade levels) -> 1_6930_10_0
            if (key.StartsWith("1_6930_1") || key.StartsWith("1_6931_1"))
            {
                return "1_6930_10_0";
            }

            // Crossbow (normal & rebirth, all upgrade levels) -> 1_6930_10_0
            if (key.StartsWith("1_6811_") || key.StartsWith("1_6812_"))
            {
                return "1_6930_10_0";
            }

            // Horn Crossbow (normal & rebirth, all upgrade levels) -> 1_6930_10_0
            if (key.StartsWith("1_6821_") || key.StartsWith("1_6822_"))
            {
                return "1_6930_10_0";
            }

            // Iron Crossbow (normal & rebirth, all upgrade levels) -> 1_6831_00_0
            if (key.StartsWith("1_6831_") || key.StartsWith("1_6832_"))
            {
                return "1_6831_00_0";
            }

            // Iron Bow (normal & rebirth, all upgrade levels) -> 1_6841_00_0
            if (key.StartsWith("1_6841_") || key.StartsWith("1_6842_"))
            {
                return "1_6841_00_0";
            }
            // Dread Shield (normal & rebirth, all upgrade levels) -> 1_7910_02_0
            if (key.StartsWith("1_7910_") || key.StartsWith("1_7911_"))
            {
                return "1_7910_02_0";
            }
            
            // Lobo/Lupus/Lycaon Hammer (normal & rebirth, all upgrade levels) -> 1_9025_00_0
            if (key.StartsWith("1_9025_"))
            {
                return "1_9025_00_0";
            }
            
            // Priest Maul (all upgrade levels) -> 1_9021_00_0
            if (key.StartsWith("1_9021_"))
            {
                return "1_9021_00_0";
            }
            
            // Priest Mace (all upgrade levels) -> 1_9041_00_0
            if (key.StartsWith("1_9041_"))
            {
                return "1_9041_00_0";
            }
            
            // Priest War Hammer (all upgrade levels) -> 1_9061_00_0
            if (key.StartsWith("1_9061_"))
            {
                return "1_9061_00_0";
            }
            
            // Priest Impact (normal & rebirth, all upgrade levels) -> 1_4111_00_0
            if (key.StartsWith("1_4111_"))
            {
                return "1_4111_00_0";
            }
            
            // Holy Animor (normal & rebirth, all upgrade levels) -> 1_9930_10_0
            if (key.StartsWith("1_9930_") || key.StartsWith("1_9931_"))
            {
                return "1_9930_10_0";
            }
            
            // Elixir Staff (normal & rebirth, all upgrade levels) -> 1_8111_00_0
            if (key.StartsWith("1_8111_") || key.StartsWith("1_8112_"))
            {
                return "1_8111_00_0";
            }
            
            // Ron's Staff (normal & rebirth, all upgrade levels) -> 1_8051_00_1
            if (key.StartsWith("1_8051_00_1") || key.StartsWith("1_8052_00_1"))
            {
                return "1_8051_00_1";
            }

            // Smite Hammer (normal & rebirth, all upgrade levels) -> 1_4021_00_0
            if (key.StartsWith("1_4021_00_") || key.StartsWith("1_4022_00_"))
            {
                return "1_4021_00_0";
            }

            // Twin Axe (normal & rebirth, all upgrade levels) -> 1_3021_00_0
            if (key.StartsWith("1_3021_00_") || key.StartsWith("1_3022_00_"))
            {
                return "1_3021_00_0";
            }

            // Short Blade (normal & rebirth, all upgrade levels) -> 1_2011_00_0
            if (key.StartsWith("1_2011_00_") || key.StartsWith("1_2011_"))
            {
                return "1_2011_00_0";
            }

            // Gradius (normal & rebirth, all upgrade levels) -> 1_2091_00_0
            if (key.StartsWith("1_2091_00_") || key.StartsWith("1_2091_"))
            {
                return "1_2091_00_0";
            }

            // Rapier (normal & rebirth, all upgrade levels) -> 1_2081_00_0
            if (key.StartsWith("1_2081_00_") || key.StartsWith("1_2081_"))
            {
                return "1_2081_00_0";
            }

            // Combined Blade (normal & rebirth, all upgrade levels) -> 1_2521_00_0
            if (key.StartsWith("1_2521_00_") || key.StartsWith("1_2521_"))
            {
                return "1_2521_00_0";
            }

            // Two-Handed Sword (normal & rebirth, all upgrade levels) -> 1_2531_00_0
            if (key.StartsWith("1_2531_00_") || key.StartsWith("1_2531_"))
            {
                return "1_2531_00_0";
            }

            // Flamberge (normal & rebirth, all upgrade levels) -> 1_2601_00_0
            if (key.StartsWith("1_2601_00_") || key.StartsWith("1_2601_"))
            {
                return "1_2601_00_0";
            }

            // Claymore (normal & rebirth, all upgrade levels) -> 1_2591_00_0
            if (key.StartsWith("1_2591_00_") || key.StartsWith("1_2591_"))
            {
                return "1_2591_00_0";
            }

            // Mirage (normal & rebirth, all upgrade levels) -> 1_2641_00_0
            if (key.StartsWith("1_2641_00_") || key.StartsWith("1_2641_"))
            {
                return "1_2641_00_0";
            }

            // Great Sword (normal & rebirth, all upgrade levels) -> 1_2571_00_0
            if (key.StartsWith("1_2571_00_") || key.StartsWith("1_2571_"))
            {
                return "1_2571_00_0";
            }

            // Battle Sword (normal & rebirth, all upgrade levels) -> 1_2551_00_0
            if (key.StartsWith("1_2551_00_") || key.StartsWith("1_2551_"))
            {
                return "1_2551_00_0";
            }

            // Scimitar (normal & rebirth, all upgrade levels) -> 1_2041_00_0
            if (key.StartsWith("1_2041_00_") || key.StartsWith("1_2041_"))
            {
                return "1_2041_00_0";
            }

            // War Hammer (normal & rebirth, all upgrade levels) -> 1_4025_00_0
            if (key.StartsWith("1_4025_00_") || key.StartsWith("1_4025_"))
            {
                return "1_4025_00_0";
            }

            // Maul / Breaker / Cracker (normal & rebirth, all upgrade levels) -> 1_4061_00_0
            if (key.StartsWith("1_4061_") || key.StartsWith("1_4062_"))
            {
                return "1_4061_00_0";
            }
            
            // Battle Mace / Large Breaker / Totamic Club (normal & rebirth, all upgrade levels) -> 1_4531_00_0
            if (key.StartsWith("1_4531_") || key.StartsWith("1_4532_"))
            {
                return "1_4531_00_0";
            }

            // Long Sword (normal & rebirth, all upgrade levels) -> 1_2021_00_0
            if (key.StartsWith("1_2021_00_") || key.StartsWith("1_2021_"))
            {
                return "1_2021_00_0";
            }

            // Cleaver Axe (normal & rebirth, all upgrade levels) -> 1_3041_00_0
            if (key.StartsWith("1_3041_00_") || key.StartsWith("1_3041_"))
            {
                return "1_3041_00_0";
            }

            // Bipennis (normal & rebirth, all upgrade levels) -> 1_3042_00_0
            if (key.StartsWith("1_3042_00_") || key.StartsWith("1_3042_"))
            {
                return "1_3042_00_0";
            }

            // Battle Axe 2H (normal & rebirth, all upgrade levels) -> 1_3511_00_0
            if (key.StartsWith("1_3511_00_") || key.StartsWith("1_3512_00_"))
            {
                return "1_3511_00_0";
            }

            // Timber Axe / Broad Axe (normal & rebirth, all upgrade levels) -> 1_3531_00_0
            if (key.StartsWith("1_3531_00_") || key.StartsWith("1_3532_00_"))
            {
                return "1_3531_00_0";
            }

            // Long Spear (normal & rebirth, all upgrade levels) -> 1_5031_00_0
            if (key.StartsWith("1_5031_00_") || key.StartsWith("1_5032_00_"))
            {
                return "1_5031_00_0";
            }
            
            // Halberd / Battle Scythe (normal & rebirth, all upgrade levels) -> 1_5531_00_0
            if (key.StartsWith("1_5531_") || key.StartsWith("1_5532_"))
            {
                return "1_5531_00_0";
            }

            // Twin Axe 2H (normal & rebirth, all upgrade levels) -> 1_3551_00_0
            if (key.StartsWith("1_3551_00_") || key.StartsWith("1_3552_00_"))
            {
                return "1_3551_00_0";
            }

            // Giantic Axe 2H (normal & rebirth, all upgrade levels) -> 1_3561_00_0
            if (key.StartsWith("1_3561_00_") || key.StartsWith("1_3562_00_"))
            {
                return "1_3561_00_0";
            }

            // Blade Axe / Gigantic Axe 2H (normal & rebirth, all upgrade levels) -> 1_3621_00_0
            if (key.StartsWith("1_3621_00_") || key.StartsWith("1_3622_00_"))
            {
                return "1_3621_00_0";
            }

            // Gigantic Axe 1H (normal & rebirth, all upgrade levels) -> 1_3111_00_0
            if (key.StartsWith("1_3111_00_") || key.StartsWith("1_3112_00_"))
            {
                return "1_3111_00_0";
            }

            // Hanguk Sword (normal & rebirth, all upgrade levels) -> 1_2151_00_0
            if (key.StartsWith("1_2151_00_") || key.StartsWith("1_2152_00_"))
            {
                return "1_2151_00_0";
            }
            
            // Graham (normal & rebirth, all upgrade levels) -> 1_2131_00_0
            if (key.StartsWith("1_2131_") || key.StartsWith("1_2132_"))
            {
                return "1_2131_00_0";
            }
            
            // Slayer (normal & rebirth, all upgrade levels) -> 1_2121_00_0
            if (key.StartsWith("1_2121_") || key.StartsWith("1_2122_"))
            {
                return "1_2121_00_0";
            }
            
            // Crescent (normal & rebirth, all upgrade levels) -> 1_2101_00_0
            if (key.StartsWith("1_2101_") || key.StartsWith("1_2102_"))
            {
                return "1_2101_00_0";
            }
            
            // Blade (normal & rebirth, all upgrade levels) -> 1_2111_00_0
            if (key.StartsWith("1_2111_") || key.StartsWith("1_2112_"))
            {
                return "1_2111_00_0";
            }

            // Durandal (normal & rebirth, all upgrade levels) -> 1_2631_00_0
            if (key.StartsWith("1_2631_00_") || key.StartsWith("1_2632_00_"))
            {
                return "1_2631_00_0";
            }
            
            // Destroyer (normal & rebirth, all upgrade levels) -> 1_2621_00_0
            if (key.StartsWith("1_2621_") || key.StartsWith("1_2622_"))
            {
                return "1_2621_00_0";
            }

            // Pole Axe / Bill / Glave (normal & rebirth, all upgrade levels) -> 1_5511_00_0
            if (key.StartsWith("1_5511_"))
            {
                return "1_5511_00_0";
            }

            // Hell Breaker (normal & rebirth, all upgrade levels) -> 1_4551_00_0
            if (key.StartsWith("1_4551_"))
            {
                return "1_4551_00_0";
            }

            // Iron Impact (normal & rebirth, all upgrade levels) -> 1_4621_00_0
            if (key.StartsWith("1_4621_"))
            {
                return "1_4621_00_0";
            }

            // Raptor (normal & rebirth, all upgrade levels) -> 1_5621_00_0
            if (key.StartsWith("1_5621_"))
            {
                return "1_5621_00_0";
            }
            
            // Grim Scythe (normal & rebirth, all upgrade levels) -> 1_5551_00_0
            if (key.StartsWith("1_5551_") || key.StartsWith("1_5552_"))
            {
                return "1_5551_00_0";
            }
            
            // Cross Spear / Pike / Harpoon (normal & rebirth, all upgrade levels) -> 1_5041_00_0
            if (key.StartsWith("1_5041_") || key.StartsWith("1_5042_"))
            {
                return "1_5041_00_0";
            }
            
            // Spetum / Trident (normal & rebirth, all upgrade levels) -> 1_5081_00_0
            if (key.StartsWith("1_5081_") || key.StartsWith("1_5082_"))
            {
                return "1_5081_00_0";
            }

            // Lugias (normal & rebirth, all upgrade levels) -> 1_5930_10_0
            if (key.StartsWith("1_5930_") || key.StartsWith("1_5931_"))
            {
                return "1_5930_10_0";
            }

            // Totamic Spear (normal & rebirth, all upgrade levels) -> 1_5121_00_0
            if (key.StartsWith("1_5121_") || key.StartsWith("1_5122_"))
            {
                return "1_5121_00_0";
            }

            // Weight Hammer / Great Maul / Large Hacker (normal & rebirth, all upgrade levels) -> 1_4511_00_0
            if (key.StartsWith("1_4511_") || key.StartsWith("1_4512_"))
            {
                return "1_4511_00_0";
            }
            
            return key;
        }

        private static readonly Dictionary<string, string> _resolvedPathCache = new Dictionary<string, string>();

        public static string GetOverridePrefabPath(string plugFileName)
        {
            string key = NormalizePlugFileName(plugFileName);
            if (string.IsNullOrEmpty(key)) return null;

            if (_resolvedPathCache.TryGetValue(key, out string cachedPath))
            {
                return cachedPath;
            }

            // Check for convention-based drop-in prefab
            string conventionPath = $"Weapon/Overrides/{key}";
            GameObject tempPrefab = Resources.Load<GameObject>(conventionPath);
            if (tempPrefab != null)
            {
                _resolvedPathCache[key] = conventionPath;
                return conventionPath;
            }

            // Fallback to empty mapping (or not overridden)
            _resolvedPathCache[key] = null;
            return null;
        }

        public static void ApplyRelativeOffsets(string plugFileName, Transform weaponTransform, string plugTag = "PLUG")
        {
            string key = NormalizePlugFileName(plugFileName);
            
            try
            {
                string debugMsg = $"[WeaponDebug] Time={System.DateTime.Now:HH:mm:ss} File={plugFileName} Tag={plugTag} Key={key}\n";
                System.IO.File.AppendAllText("c:\\_dev\\knightonline-mobil\\weapon_debug.log", debugMsg);
            }
            catch {}

            if (_offsetMapping.TryGetValue(key, out var offset))
            {
                Vector3 relativePosition = offset.RelativePosition;
                Vector3 relativeRotation = offset.RelativeRotation;
                
                bool isRightHand = (plugTag == "PLUG_RH" || plugTag == "PLUG_0" || plugTag == "PLUG");
                bool isLeftHand = (plugTag == "PLUG_LH" || plugTag == "PLUG_1");

                // Generic automatic mirroring for left hand (except shields starting with 1_7 and bows starting with 1_6)
                if (isLeftHand && !key.StartsWith("1_7") && !key.StartsWith("1_6"))
                {
                    if (offset.HasCustomLeftHand)
                    {
                        relativePosition = offset.CustomLeftPosition;
                        relativeRotation = offset.CustomLeftRotation;
                    }
                    else
                    {
                        relativePosition.x = -relativePosition.x;
                        relativePosition.z = -relativePosition.z;

                        if (relativeRotation.y == 180f || relativeRotation.y == -180f)
                        {
                            relativeRotation.y = 0f;
                        }
                    }
                }

                if (offset.IsAbsolute)
                {
                    weaponTransform.localPosition = relativePosition;
                    weaponTransform.localRotation = Quaternion.Euler(relativeRotation);
                    weaponTransform.localScale = offset.RelativeScale;
                }
                else
                {
                    weaponTransform.localPosition += relativePosition;
                    weaponTransform.localRotation *= Quaternion.Euler(relativeRotation);
                    weaponTransform.localScale = Vector3.Scale(weaponTransform.localScale, offset.RelativeScale);
                }
                
                try
                {
                    string endLog = $"  [Result] finalLocalPos={weaponTransform.localPosition} finalLocalRot={weaponTransform.localRotation.eulerAngles}\n";
                    System.IO.File.AppendAllText("c:\\_dev\\knightonline-mobil\\weapon_debug.log", endLog);
                }
                catch {}
            }
        }
    }
}
