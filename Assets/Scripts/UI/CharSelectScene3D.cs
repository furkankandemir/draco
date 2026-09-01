using System.IO;
using UnityEngine;
using EntropyOnline.Import;
using EntropyOnline.Network;
using EntropyOnline.World;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: GameProcCharacterSelect.cpp satır 96-641
    /// Karakter seçim ekranının 3D sahnesini yönetir:
    ///   - Arka plan (el_chairs.n3shape / ka_chairs.n3shape) — cpp:170-247
    ///   - Kamera (m_vEye / m_vAt / m_vUp) — cpp:151-165
    ///   - 3 Spot ışık (m_lgt[3]) — cpp:174-242
    ///   - 3 Karakter modeli (m_pChrs[3]) — cpp:359-641 AddChr()
    /// </summary>
    public class CharSelectScene3D : MonoBehaviour
    {
        
        // Open-KO birebir: m_pCamera — cpp:128
        private UnityEngine.Camera _camera;
        

        
        // Open-KO birebir: m_pLights[8] — cpp:129-130
        // Sahne aydınlatması için 3 spot ışık — cpp:174-242
        private Light[] _spotLights = new Light[3];
        
        // Open-KO birebir: m_pChrs[MAX_AVAILABLE_CHARACTER=3] — cpp:34-35
        private GameObject[] _chrModels = new GameObject[5];
        
        // Nation — Karus=1, El Morad=2
        private byte _nation = 2; // Varsayılan El Morad
        
        // ============================================
        // Kamera orbit animasyonu — cpp:1009-1101
        // RotateLeft/Right: m_vEye'ı m_vAt etrafında Y ekseninde döndürür
        // Hız: 1.2 rad/s — cpp:1086,1098
        // Durdurma: dot product < 0.74~0.77 — cpp:826,834,890,898
        // ============================================
        private Vector3 _vAt;         // cpp: m_vAt — kamera bakış noktası (sabit)
        private Vector3 _vEyeBackup;  // cpp: m_vEyeBackup — başlangıç pozisyonu

        // ============================================
        // Initialize — cpp:96-253 Init()
        // ============================================
        
        public void Initialize(byte nation)
        {
            _nation = nation;
            // N3CharBuilder.FindAssetFile için hint path — parser seviyesinde
            // KOBinaryProvider zaten Resources/KOBinary/'den yükleyecek
            
            SetupCamera();
            SetupLights();

            // Sahnede yer alan özel orc ve human çevrelerini bulup ırka göre aç/kapat (kapalı olsalar dahi bulur)
            GameObject orcEnv = null;
            GameObject humanEnv = null;
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                if (go.name == "OrcEnvironment") orcEnv = go;
                else if (go.name == "HumanEnvironment") humanEnv = go;
            }

            if (orcEnv != null) orcEnv.SetActive(_nation == 1); // Karus/Orc = 1
            if (humanEnv != null) humanEnv.SetActive(_nation == 2); // Human/El Morad = 2
            
            // Open-KO birebir: s_pTbl_UPC_Looks (GameBase.h:21)
            if (KOEquipmentVisualizer.s_pTbl_UPC_Looks == null)
            {
                string looksPath = "UPC_DefaultLooks.tbl";
                KOEquipmentVisualizer.s_pTbl_UPC_Looks = KOTableReader.LoadUpcLooksTable(looksPath);
            }
            
            // Open-KO birebir: s_pTbl_Items_Basic / s_pTbl_Items_Exts
            if (KOInventory.s_pTbl_Items_Basic == null)
            {
                string dataDir = "Data";
                KOInventory.s_pTbl_Items_Basic = KOTableReader.LoadItemBasicTable(
                    Path.Combine(dataDir, "Item_Org_us.tbl"));
                KOInventory.s_pTbl_Items_Exts = KOTableReader.LoadItemExtTables(dataDir);
            }

            // FX Manager oluştur (Glow efektleri için gerekli)
            if (KO.KOFXManager.Instance == null)
            {
                var fxObj = new GameObject("KOFXManager");
                var fxMgr = fxObj.AddComponent<KO.KOFXManager>();
                fxMgr.Initialize();
            }
        }
        
        /// <summary>
        /// Open-KO birebir: cpp:128 — m_pCamera = new CN3Camera()
        /// cpp:151-165 — m_vEye/m_vAt/m_vUp nation'a göre
        /// </summary>
        private void SetupCamera()
        {
            var camObj = new GameObject("ChrSelectCamera");
            camObj.transform.SetParent(transform, false);
            _camera = camObj.AddComponent<UnityEngine.Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.fieldOfView = 0.96f * Mathf.Rad2Deg; // cpp:321 — fFOV = 0.96 radians (~55°)
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.depth = -1; // UI Canvas altında render
            
            // cpp:151-165 — nation'a göre kamera pozisyonu
            switch (_nation)
            {
                case 1: // NATION_KARUS — cpp:153-156
                    _vAt = new Vector3(0.0f, -0.4f, 0.0f);
                    camObj.transform.position = new Vector3(0.0f, -0.2f, 7.4f);
                    break;
                    
                case 2: // NATION_ELMORAD — cpp:159-160
                default:
                    _vAt = new Vector3(0.0f, -0.4f, 0.0f);
                    camObj.transform.position = new Vector3(0.0f, -0.2f, 7.0f);
                    break;
            }
            
            camObj.transform.LookAt(_vAt);
            _vEyeBackup = camObj.transform.position;
            
        }

        
        /// <summary>
        /// Open-KO birebir: cpp:174-242 — 3 spot ışık
        /// </summary>
        private void SetupLights()
        {
            // cpp:175 — D3DLIGHT_SPOT
            // cpp:176-183 — ortak parametreler
            for (int i = 0; i < 3; i++)
            {
                var lgtObj = new GameObject($"ChrSelectSpotLight_{i}");
                lgtObj.transform.SetParent(transform, false);
                
                var light = lgtObj.AddComponent<Light>();
                light.type = LightType.Spot;              // cpp:175
                light.range = 6.0f;                        // cpp:179
                light.color = Color.white;                 // cpp:180-182 — (255,255,255)
                light.intensity = 2.0f;                    // D3D Attenuation0=0.1f → Unity intensity
                
                _spotLights[i] = light;
            }
            
            Vector3 camPos = _camera.transform.position;
            
            switch (_nation)
            {
                case 1: // NATION_KARUS — cpp:188-215
                {
                    // Light 0: Merkez (kamera pozisyonundan) — cpp:197-202
                    Vector3 lgt0Pos = new Vector3(camPos.x, camPos.y + 2.0f, camPos.z);
                    Vector3 lgt0Target = new Vector3(0.0f, 0.0f, 3.5f);
                    _spotLights[0].transform.position = lgt0Pos;
                    _spotLights[0].transform.LookAt(lgt0Target);
                    _spotLights[0].spotAngle = 0.6f * Mathf.Rad2Deg * 2f; // cpp:202 — Phi
                    
                    // Light 1: Sağ — cpp:204-208
                    _spotLights[1].transform.position = new Vector3(5.87f, 2.4f, 4.73f);
                    _spotLights[1].transform.LookAt(new Vector3(2.32f, 0.0f, 2.54f));
                    _spotLights[1].spotAngle = 0.6f * Mathf.Rad2Deg * 2f;
                    
                    // Light 2: Sol — cpp:210-214
                    _spotLights[2].transform.position = new Vector3(-5.87f, 2.4f, 4.73f);
                    _spotLights[2].transform.LookAt(new Vector3(-2.32f, 0.0f, 2.54f));
                    _spotLights[2].spotAngle = 0.6f * Mathf.Rad2Deg * 2f;
                    break;
                }
                    
                case 2: // NATION_ELMORAD — cpp:217-243
                default:
                {
                    // Light 0: Merkez — cpp:225-230
                    Vector3 lgt0Pos = new Vector3(camPos.x, camPos.y + 2.0f, camPos.z);
                    Vector3 lgt0Target = new Vector3(0.0f, -0.1f, 3.0f);
                    _spotLights[0].transform.position = lgt0Pos;
                    _spotLights[0].transform.LookAt(lgt0Target);
                    _spotLights[0].spotAngle = 0.45f * Mathf.Rad2Deg * 2f;
                    
                    // Light 1: Sağ — cpp:232-236
                    _spotLights[1].transform.position = new Vector3(5.6f, 2.4f, 4.68f);
                    _spotLights[1].transform.LookAt(new Vector3(2.2f, -0.1f, 2.36f));
                    _spotLights[1].spotAngle = 0.45f * Mathf.Rad2Deg * 2f;
                    
                    // Light 2: Sol — cpp:238-242
                    _spotLights[2].transform.position = new Vector3(-5.6f, 2.4f, 4.68f);
                    _spotLights[2].transform.LookAt(new Vector3(-2.4f, -0.1f, 2.23f));
                    _spotLights[2].spotAngle = 0.45f * Mathf.Rad2Deg * 2f;
                    break;
                }
            }
            
        }
        
        // ============================================
        // AddChr — cpp:359-641
        // Open-KO birebir: CN3Chr oluştur → JointSet → AniCtrlSet → PlugSet(0) → PlugSet(1)
        // C++ hiçbir zaman .n3chr yüklemez ChrSelect'te.
        // ============================================
        
        /// <summary>
        /// Open-KO birebir: AddChr(e_ChrPos, __CharacterSelectInfo*) — cpp:359-641
        /// Race+Class'a göre doğru .n3joint/.n3anim/.n3cplug dosyalarını belirler ve yükler.
        /// Equipment item'ları varsa PartSet ile zırh görsellerini uygular — cpp:531-571.
        /// Pozisyon ve rotasyon nation+slot'a göre ayarlanır.
        /// </summary>
        public void AddChr(int slotIndex, CharacterListItem charInfo)
        {
            RemoveChr(0); // Her zaman merkezdeki (Slot 0) modeli temizliyoruz
            if (charInfo == null || string.IsNullOrEmpty(charInfo.Name)) return;
            
            byte race = charInfo.Race;
            byte charClass = charInfo.Class;
            
            // C++ birebir: race+class → joint/anim/plug dosya adları — cpp:392-524
            var selInfo = GetChrSelectInfo(race, charClass);
            if (!selInfo.IsValid)
            {
                Debug.LogWarning($"[CHRSEL3D] Race={race} Class={charClass} için ChrSelect bilgisi bulunamadı");
                return;
            }
            
            // C++ birebir: cpp:526-529
            //   m_pChrs[iPosIndex]->JointSet(szJointFN);
            //   m_pChrs[iPosIndex]->AniCtrlSet(szAniFN);
            //   m_pChrs[iPosIndex]->PlugSet(0, szPlug0FN);
            //   m_pChrs[iPosIndex]->PlugSet(1, szPlug1FN);
            string plug0 = "";
            string plug1 = "";
            int joint0 = -1;
            int joint1 = -1;


            if (charInfo.ItemRightHand != 0)
            {
                var pItem = FindItemBasic(charInfo.ItemRightHand);
                string resolved = MakePlugResourceFileName(pItem, charInfo.ItemRightHand);
                if (!string.IsNullOrEmpty(resolved))
                {
                    plug0 = resolved;
                    if (KOEquipmentVisualizer.s_pTbl_UPC_Looks != null && 
                        KOEquipmentVisualizer.s_pTbl_UPC_Looks.TryGetValue(race, out var pLooks))
                    {
                        joint0 = pLooks.iJointRH;
                    }
                    else
                    {
                        Debug.LogWarning($"[CHRSEL3D] itemR looks lookup failed or table null");
                    }
                }
            }
            if (string.IsNullOrEmpty(plug0))
            {
                plug0 = "";
                joint0 = -1;
            }

            if (charInfo.ItemLeftHand != 0)
            {
                var pItem = FindItemBasic(charInfo.ItemLeftHand);
                string resolved = MakePlugResourceFileName(pItem, charInfo.ItemLeftHand);
                if (!string.IsNullOrEmpty(resolved))
                {
                    plug1 = resolved;
                    if (KOEquipmentVisualizer.s_pTbl_UPC_Looks != null && 
                        KOEquipmentVisualizer.s_pTbl_UPC_Looks.TryGetValue(race, out var pLooks))
                    {
                        joint1 = pLooks.iJointLH;
                    }
                    else
                    {
                        Debug.LogWarning($"[CHRSEL3D] itemL looks lookup failed or table null");
                    }
                }
            }
            if (string.IsNullOrEmpty(plug1))
            {
                plug1 = "";
                joint1 = -1;
            }

            var chrObj = N3CharBuilder.BuildChrSelect(
                selInfo.JointFN, selInfo.AniFN,
                plug0, plug1, joint0, joint1);
            if (chrObj == null)
            {
                Debug.LogError($"[CHRSEL3D] Karakter modeli oluşturulamadı: {selInfo.JointFN}");
                return;
            }
            
            chrObj.transform.SetParent(transform, false);
            
            // cpp:531-571 — Equipment part/plug rendering (AddChrPart birebir)
            ApplyEquipmentParts(chrObj, charInfo);

            // Silah parlamalarını (glow) ekle
            var plugTransforms = chrObj.GetComponentsInChildren<Transform>(true);
            foreach (var t in plugTransforms)
            {
                if (t.name.StartsWith("PLUG_0_") && charInfo.ItemRightHand != 0)
                {
                    var pItem = FindItemBasic(charInfo.ItemRightHand);
                    var pItemExt = FindItemExt(pItem, charInfo.ItemRightHand);
                    if (pItemExt != null)
                    {
                        var glow = t.gameObject.AddComponent<KOWeaponGlow>();
                        glow.Initialize(pItemExt);
                    }
                }
                else if (t.name.StartsWith("PLUG_1_") && charInfo.ItemLeftHand != 0)
                {
                    var pItem = FindItemBasic(charInfo.ItemLeftHand);
                    var pItemExt = FindItemExt(pItem, charInfo.ItemLeftHand);
                    if (pItemExt != null)
                    {
                        var glow = t.gameObject.AddComponent<KOWeaponGlow>();
                        glow.Initialize(pItemExt);
                    }
                }
            }
            
            // Tek koltuk olacağı için her zaman merkez pozisyona (Slot 0) yerleştiriyoruz
            SetChrPositionAndRotation(chrObj, 0);
            
            _chrModels[0] = chrObj;
        }
        
        /// <summary>
        /// Open-KO birebir: cpp:528-571 — equipment part rendering
        /// 
        /// Sıralama (birebir):
        ///   1. PlugSet(0, szPlug0FN), PlugSet(1, szPlug1FN) — ChrSelect SABIT pluglar (silah),
        ///      BuildChrSelect'te ayrıca yükleniyor. Envanter silahları KULLANILMAZ.
        ///   2. AddChrPart(UPPER) — cpp:532
        ///   3. Robe kontrolü → LOWER — cpp:534-538
        ///   4. AddChrPart(HANDS) — cpp:540
        ///   5. AddChrPart(FEET) — cpp:542
        ///   6. Face — pLooks + iFace index — cpp:546-553
        ///   7. Hair/Helmet — 3 dallanma — cpp:555-571
        /// </summary>
        private void ApplyEquipmentParts(GameObject chrObj, CharacterListItem charInfo)
        {
            byte eRace = charInfo.Race;
            
            // cpp:385 — pLooks tablosu (varsayılan görünüm fallback)
            KOTableReader.TablePlayerLooks pLooks = null;
            if (KOEquipmentVisualizer.s_pTbl_UPC_Looks != null)
                KOEquipmentVisualizer.s_pTbl_UPC_Looks.TryGetValue(eRace, out pLooks);
            
            // ========================================
            // cpp:528-529 — PlugSet(0, szPlug0FN), PlugSet(1, szPlug1FN)
            // ChrSelect sabit silahları: BuildChrSelect'te ayrıca yükleniyor (birebir C++).
            // Envanter silahları burada KULLANILMAZ — C++ birebir.
            // ========================================
            
            // ========================================
            // cpp:531-532 — UPPER (상체)
            // ========================================
            AddChrPart(chrObj, pLooks, N3CharBuilder.PART_POS_UPPER, charInfo.ItemUpper, eRace, charInfo.Class);
            
            // ========================================
            // cpp:534-538 — LOWER (하체) — Robe kontrolü
            // ========================================
            if (charInfo.ItemUpper != 0)
            {
                var pItemUpper = FindItemBasic(charInfo.ItemUpper);
                // cpp:535 — pItemUpper && pItemUpper->byIsRobeType
                if (pItemUpper != null && pItemUpper.byIsRobeType != 0)
                {
                    // Robe giyilmişse dinamik alt parça modelini bulup yüklemeyi dene (oyun içi KOEquipmentVisualizer ile aynı mantık)
                    string lowerRobePath = GetLowerRobePath(pItemUpper, eRace);
                    if (!string.IsNullOrEmpty(lowerRobePath) && KOBinaryProvider.Exists(lowerRobePath))
                    {
                        N3CharBuilder.PartSet(chrObj, N3CharBuilder.PART_POS_LOWER, lowerRobePath);
                    }
                    else
                    {
                        N3CharBuilder.PartSet(chrObj, N3CharBuilder.PART_POS_LOWER, "");
                    }
                }
                else
                {
                    // cpp:538 — AddChrPart(LOWER, dwItemLower)
                    AddChrPart(chrObj, pLooks, N3CharBuilder.PART_POS_LOWER, charInfo.ItemLower, eRace, charInfo.Class);
                }
            }
            else
            {
                // UPPER item yok → LOWER normal
                AddChrPart(chrObj, pLooks, N3CharBuilder.PART_POS_LOWER, charInfo.ItemLower, eRace, charInfo.Class);
            }
            
            // ========================================
            // cpp:540 — HANDS (팔/장갑)
            // ========================================
            AddChrPart(chrObj, pLooks, N3CharBuilder.PART_POS_HANDS, charInfo.ItemGloves, eRace, charInfo.Class);
            
            // ========================================
            // cpp:542 — FEET (다리/신발)
            // ========================================
            AddChrPart(chrObj, pLooks, N3CharBuilder.PART_POS_FEET, charInfo.ItemShoes, eRace, charInfo.Class);
            
            // ========================================
            // cpp:546-553 — Face (얼굴)
            // pLooks->szPartFNs[FACE] + iFace index ile dosya adı oluştur
            // ========================================
            if (pLooks != null)
            {
                N3CharBuilder.InitFace(chrObj, pLooks, charInfo.Face);
            }
            
            // ========================================
            // cpp:555-571 — Hair/Helmet (머리카락/투구)
            // 3 dallanma:
            //   1. pItemHelmet && pItemHelmet->dwIDResrc → AddChrPart(HAIR_HELMET, dwItemHelmet)
            //   2. pLooks->szPartFNs[HAIR_HELMET] not empty → hair index ile varsayılan saç
            //   3. else → PartSet(HAIR_HELMET, "") — kel
            // ========================================
            if (charInfo.ItemHelmet != 0)
            {
                // cpp:556 — s_pTbl_Items_Basic.Find(dwItemHelmet)
                var pItemHelmet = FindItemBasic(charInfo.ItemHelmet);
                // cpp:557 — pItemHelmet && pItemHelmet->dwIDResrc
                if (pItemHelmet != null && pItemHelmet.dwIDResrc != 0)
                {
                    // cpp:559 — AddChrPart(HAIR_HELMET, dwItemHelmet)
                    AddChrPart(chrObj, pLooks, N3CharBuilder.PART_POS_HAIR_HELMET, charInfo.ItemHelmet, eRace, charInfo.Class);
                }
                else
                {
                    // Helmet item var ama resource yok → saç göster
                    if (pLooks != null)
                        N3CharBuilder.InitHair(chrObj, pLooks, charInfo.Hair);
                }
            }
            else
            {
                // cpp:561-567 — helmet yok → varsayılan saç
                if (pLooks != null)
                {
                    N3CharBuilder.InitHair(chrObj, pLooks, charInfo.Hair);
                }
                else
                {
                    // cpp:570 — pLooks da yok → kel
                    N3CharBuilder.PartSet(chrObj, N3CharBuilder.PART_POS_HAIR_HELMET, "");
                }
            }
        }
        
        /// <summary>
        /// Open-KO birebir: AddChrPart() — cpp:643-683
        /// dwItemID'den MakeResrcFileNameForUPC ile .n3cpart çözümle.
        /// Item yoksa (dwItemID=0) veya resource bulunamazsa → pLooks varsayılan part.
        /// </summary>
        private void AddChrPart(GameObject chrObj, KOTableReader.TablePlayerLooks pLooks,
            int ePartPos, int dwItemID, byte eRace, int charClass)
        {
            // cpp:652 — s_pTbl_Items_Basic.Find(dwItemID / 1000 * 1000)
            KOTableReader.TableItemBasic pItem = null;
            if (dwItemID != 0)
                pItem = FindItemBasic(dwItemID);
            
            // cpp:653-657 — dwItemID != 0 && pItem == null → return (hatalı item)
            if (dwItemID != 0 && pItem == null)
            {
                Debug.LogWarning($"[CHRSEL3D] AddChrPart: Item bulunamadı: {dwItemID}");
                return;
            }
            
            // cpp:668 — MakeResrcFileNameForUPC → szResrcFN
            string szResrcFN = null;
            if (pItem != null)
                szResrcFN = MakePartResourceFileName(pItem, dwItemID, eRace);
            
            // cpp:669-672 — szResrcFN boşsa pLooks default, değilse item part
            if (string.IsNullOrEmpty(szResrcFN))
            {
                // cpp:670 — pLooks->szPartFNs[ePartPos] — varsayılan part
                if (pLooks != null && ePartPos < pLooks.szPartFNs.Length)
                {
                    string defaultPart = pLooks.szPartFNs[ePartPos];
                    defaultPart = N3CharBuilder.GetDefaultPartPath(ePartPos, eRace, charClass, defaultPart);
                    if (!string.IsNullOrEmpty(defaultPart))
                        N3CharBuilder.PartSet(chrObj, ePartPos, defaultPart);
                }
            }
            else
            {
                // cpp:672 — PartSet(ePartPos, szResrcFN)
                N3CharBuilder.PartSet(chrObj, ePartPos, szResrcFN);
            }
        }
        
        /// <summary>
        /// Open-KO birebir: s_pTbl_Items_Basic.Find(dwItemID / 1000 * 1000)
        /// </summary>
        private KOTableReader.TableItemBasic FindItemBasic(int itemDefId)
        {
            if (KOInventory.s_pTbl_Items_Basic == null) return null;
            return KOTableReader.FindItemBasic(KOInventory.s_pTbl_Items_Basic, itemDefId);
        }

        private KOTableReader.TableItemExt FindItemExt(KOTableReader.TableItemBasic pItem, int itemDefId)
        {
            if (pItem == null || KOInventory.s_pTbl_Items_Exts == null) return null;
            return KOTableReader.FindItemExt(KOInventory.s_pTbl_Items_Exts, pItem.byExtIndex, itemDefId);
        }
        
        /// <summary>
        /// Open-KO birebir: MakeResrcFileNameForUPC — Plug dosya adı oluşturma
        /// GameBase.cpp satır 604-608
        /// </summary>
        private string MakePlugResourceFileName(KOTableReader.TableItemBasic pItem, int itemDefId)
        {
            if (pItem == null || pItem.dwIDResrc == 0) return null;

            uint iIDResrc = pItem.dwIDResrc;
            if (KOInventory.s_pTbl_Items_Exts != null)
            {
                var pItemExt = KOTableReader.FindItemExt(
                    KOInventory.s_pTbl_Items_Exts, pItem.byExtIndex, itemDefId);
                if (pItemExt != null && pItemExt.dwIDResrc != 0)
                    iIDResrc = pItemExt.dwIDResrc;
            }

            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = (int)((iIDResrc / 10) % 100);
            int d4 = (int)(iIDResrc % 10);

            return $"Item\\{d1}_{d2:D4}_{d3:D2}_{d4}.n3cplug";
        }

        /// <summary>
        /// Open-KO birebir: MakeResrcFileNameForUPC — Part dosya adı (race offset EKLENİR)
        /// GameBase.cpp satır 599-602
        /// </summary>
        private string MakePartResourceFileName(KOTableReader.TableItemBasic pItem, int itemDefId, byte eRace)
        {
            if (pItem == null || pItem.dwIDResrc == 0) return null;
            
            uint iIDResrc = pItem.dwIDResrc;
            if (KOInventory.s_pTbl_Items_Exts != null)
            {
                var pItemExt = KOTableReader.FindItemExt(
                    KOInventory.s_pTbl_Items_Exts, pItem.byExtIndex, itemDefId);
                if (pItemExt != null && pItemExt.dwIDResrc != 0)
                    iIDResrc = pItemExt.dwIDResrc;
            }
            
            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = (int)((iIDResrc / 10) % 100);
            int d4 = (int)(iIDResrc % 10);
            int d2WithRace = d2 + eRace;
            
            return $"Item\\{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
        }

        private string GetLowerRobePath(KOTableReader.TableItemBasic upperItem, byte eRace)
        {
            if (upperItem == null || upperItem.dwIDResrc == 0) return null;
            uint iIDResrc = upperItem.dwIDResrc;
            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = 20; // Lower part slot
            int d4 = (int)(iIDResrc % 10);

            int d2WithRace = d2 + eRace;
            if (eRace == 4)
            {
                string testPath = $"Item\\{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
                if (!KOBinaryProvider.Exists(testPath))
                {
                    d2WithRace = d2 + 13;
                }
            }

            return $"Item/{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
        }
        
        
        /// <summary>
        /// Open-KO birebir: MsgRecv_DeleteChr() sonrası — cpp:693-698
        /// Karakter modelini kaldır.
        /// </summary>
        public void RemoveChr(int slotIndex)
        {
            if (_chrModels[0] != null)
            {
                Destroy(_chrModels[0]);
                _chrModels[0] = null;
            }
        }
        
        /// <summary>
        /// Tüm modelleri temizle.
        /// </summary>
        public void ClearAll()
        {
            RemoveChr(0);
        }
        
        /// <summary>
        /// <summary>
        /// Open-KO birebir: cpp:392-524 — race+class → joint/anim/plug dosya adları
        /// C++ hiçbir zaman .n3chr yüklemez ChrSelect'te — sadece .n3joint + .n3anim + .n3cplug
        /// ayrı ayrı set edilir.
        /// </summary>
        private struct ChrSelectInfo
        {
            public string JointFN;  // .n3joint dosya adı (skeleton)
            public string AniFN;    // .n3anim dosya adı (animasyon)
            public string Plug0FN;  // .n3cplug dosya adı (sağ el silah)
            public string Plug1FN;  // .n3cplug dosya adı (sol el silah/sadak)
            public bool IsValid;
        }
        
        private ChrSelectInfo GetChrSelectInfo(byte race, byte charClass)
        {
            var info = new ChrSelectInfo();
            
            // C++ birebir: sub-class → base class grubu (GetRepresentClass mantığı)
            // WARRIOR(1/101/201) + BLADE/BERSERKER(5/105/205) + PROTECTOR/GUARDIAN(6/106/206) → 1
            // ROGUE(2/102/202) + RANGER/HUNTER(7/107/207) + ASSASSIN/PENETRATOR(8/108/208) → 2
            // WIZARD(3/103/203) + MAGE/SORCERER(9/109/209) + ENCHANTER/NECROMANCER(10/110/210) → 3
            // PRIEST(4/104/204) + CLERIC/SHAMAN(11/111/211) + DRUID/DARKPRIEST(12/112/212) → 4
            int c = charClass % 100; // nation prefix kaldır: 201→1, 105→5
            int baseClass;
            switch (c)
            {
                case 1: case 5: case 6:   baseClass = 1; break; // Warrior grubu
                case 2: case 7: case 8:   baseClass = 2; break; // Rogue grubu
                case 3: case 9: case 10:  baseClass = 3; break; // Wizard grubu
                case 4: case 11: case 12: baseClass = 4; break; // Priest grubu
                default: return info; // IsValid = false
            }
            
            switch (race)
            {
                // ============================================
                // RACE_EL_BABARIAN (11) — cpp:394-400
                // ============================================
                case 11:
                    info.JointFN = "ChrSelect\\upc_el_ba_wa.n3joint";
                    info.AniFN   = "ChrSelect\\upc_el_ba_wa.n3anim";
                    info.Plug0FN = "ChrSelect\\wea_el_great_sword.n3cplug";
                    info.Plug1FN = "";
                    info.IsValid = true;
                    break;
                    
                // ============================================
                // RACE_EL_WOMEN (13) — cpp:401-439
                // ============================================
                case 13:
                    switch (baseClass)
                    {
                        case 1: // CLASS_EL_WARRIOR (+BLADE,PROTECTOR) — cpp:405-412
                            info.JointFN = "ChrSelect\\upc_el_rf_wa.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rf_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_el_long_sword_left.n3cplug";
                            info.Plug1FN = "";
                            break;
                        case 2: // CLASS_EL_ROGUE (+RANGER,ASSASSIN) — cpp:413-420
                            info.JointFN = "ChrSelect\\upc_el_rf_rog.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rf_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_el_rf_rog_bow.n3cplug";
                            info.Plug1FN = "ChrSelect\\wea_el_quiver.n3cplug";
                            break;
                        case 3: // CLASS_EL_WIZARD (+MAGE,ENCHANTER) — cpp:421-428
                            info.JointFN = "ChrSelect\\upc_el_rf_wiz.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rf_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\upc_el_rf_wiz.n3cplug";
                            info.Plug1FN = "";
                            break;
                        case 4: // CLASS_EL_PRIEST (+CLERIC,DRUID) — cpp:429-436
                            info.JointFN = "ChrSelect\\upc_el_rf_pri.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rf_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_el_wand.n3cplug";
                            info.Plug1FN = "";
                            break;
                        default:
                            return info; // IsValid = false
                    }
                    info.IsValid = true;
                    break;
                    
                // ============================================
                // RACE_EL_MAN (12) — cpp:441-478
                // ============================================
                case 12:
                    switch (baseClass)
                    {
                        case 1: // CLASS_EL_WARRIOR (+BLADE,PROTECTOR) — cpp:445-451
                            info.JointFN = "ChrSelect\\upc_el_rm_wa.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rm_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_el_long_sword.n3cplug";
                            info.Plug1FN = "";
                            break;
                        case 2: // CLASS_EL_ROGUE (+RANGER,ASSASSIN) — cpp:452-458
                            info.JointFN = "ChrSelect\\upc_el_rm_rog.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rm_rog.n3anim";
                            info.Plug0FN = "ChrSelect\\upc_el_rm_rog_bow.n3cplug";
                            info.Plug1FN = "ChrSelect\\wea_el_quiver.n3cplug";
                            break;
                        case 3: // CLASS_EL_WIZARD (+MAGE,ENCHANTER) — cpp:460-466
                            info.JointFN = "ChrSelect\\upc_el_rm_ma.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rm_rog.n3anim";
                            info.Plug0FN = "ChrSelect\\upc_el_rm_wiz.n3cplug";
                            info.Plug1FN = "";
                            break;
                        case 4: // CLASS_EL_PRIEST (+CLERIC,DRUID) — cpp:468-474
                            info.JointFN = "ChrSelect\\upc_el_rm_pri.n3joint";
                            info.AniFN   = "ChrSelect\\upc_el_rm_rog.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_el_wand.n3cplug";
                            info.Plug1FN = "";
                            break;
                        default:
                            return info;
                    }
                    info.IsValid = true;
                    break;
                    
                // ============================================
                // RACE_KA_ARKTUAREK (1) — cpp:480-485
                // ============================================
                case 1:
                    info.JointFN = "ChrSelect\\upc_ka_at_wa.n3joint";
                    info.AniFN   = "ChrSelect\\upc_ka_at_wa.n3anim";
                    info.Plug0FN = "ChrSelect\\wea_ka_great_axe.n3cplug";
                    info.Plug1FN = "";
                    info.IsValid = true;
                    break;
                    
                // ============================================
                // RACE_KA_TUAREK (2) — cpp:487-508
                // ============================================
                case 2:
                    switch (baseClass)
                    {
                        case 2: // CLASS_KA_ROGUE (+HUNTER,PENETRATOR) — cpp:490-496
                            info.JointFN = "ChrSelect\\upc_ka_tu_rog.n3joint";
                            info.AniFN   = "ChrSelect\\upc_ka_at_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_ka_bow.n3cplug";
                            info.Plug1FN = "ChrSelect\\wea_ka_quiver.n3cplug";
                            break;
                        case 4: // CLASS_KA_PRIEST (+SHAMAN,DARKPRIEST) — cpp:498-504
                            info.JointFN = "ChrSelect\\upc_ka_tu_pri.n3joint";
                            info.AniFN   = "ChrSelect\\upc_ka_at_wa.n3anim";
                            info.Plug0FN = "ChrSelect\\wea_ka_mace.n3cplug";
                            info.Plug1FN = "";
                            break;
                        default:
                            return info;
                    }
                    info.IsValid = true;
                    break;
                    
                // ============================================
                // RACE_KA_WRINKLETUAREK (3) — cpp:510-514
                // ============================================
                case 3:
                    info.JointFN = "ChrSelect\\upc_ka_wt_ma.n3joint";
                    info.AniFN   = "ChrSelect\\upc_ka_at_wa.n3anim";
                    info.Plug0FN = "ChrSelect\\wea_ka_staff.n3cplug";
                    info.Plug1FN = "";
                    info.IsValid = true;
                    break;
                    
                // ============================================
                // RACE_KA_PURITUAREK (4) — cpp:516-520
                // ============================================
                case 4:
                    info.JointFN = "ChrSelect\\upc_el_rf_pri.n3joint";
                    info.AniFN   = "ChrSelect\\upc_el_rf_wa.n3anim";
                    info.Plug0FN = "ChrSelect\\wea_ka_mace.n3cplug";
                    info.Plug1FN = "";
                    info.IsValid = true;
                    break;
                    
                default:
                    return info;
            }
            
            return info;
        }
        
        /// <summary>
        /// Open-KO birebir: cpp:575-631 — pozisyon ve rotasyon
        /// Nation + slot index'e göre karakter konumlandırması.
        /// </summary>
        private void SetChrPositionAndRotation(GameObject chrObj, int slotIndex)
        {
            float yRot = 0f;
            Vector3 pos = Vector3.zero;
            
            switch (_nation)
            {
                case 1: // NATION_KARUS — cpp:577-600
                    switch (slotIndex)
                    {
                        case 0: // POS_CENTER — cpp:580-583
                            pos = new Vector3(0.0f, -1.16f, 2.72f);
                            yRot = 0.0f;
                            break;
                        case 1: // POS_LEFT — cpp:586-589
                            pos = new Vector3(1.86f, -1.16f, 2.1f);
                            yRot = 42.0f;
                            break;
                        case 2: // POS_RIGHT — cpp:592-595
                            pos = new Vector3(-1.9f, -1.16f, 2.1f);
                            yRot = -42.0f;
                            break;
                    }
                    break;
                    
                case 2: // NATION_ELMORAD — cpp:603-627
                default:
                    switch (slotIndex)
                    {
                        case 0: // POS_CENTER — cpp:606-609
                            pos = new Vector3(0.0f, -1.20f, 2.74f);
                            yRot = 0.0f;
                            break;
                        case 1: // POS_LEFT — cpp:612-615
                            pos = new Vector3(1.86f, -1.20f, 2.0f);
                            yRot = 42.0f;
                            break;
                        case 2: // POS_RIGHT — cpp:618-621
                            pos = new Vector3(-1.9f, -1.20f, 2.0f);
                            yRot = -46.0f; // cpp:620 — El Morad sağ slot -46 derece
                            break;
                    }
                    break;
            }
            
            chrObj.transform.position = pos;
            // cpp:582 — qt.RotationAxis(0, 1, 0, DegreesToRadians(angle))
            chrObj.transform.rotation = Quaternion.Euler(0f, yRot, 0f);
        }
        
        // ============================================
        // Kamera Orbit — cpp:1080-1101 RotateLeft/Right
        // ============================================
        
        /// <summary>
        /// Open-KO birebir: cpp:1009-1065 CheckJobState → PROCESS_ROTATEING
        /// Kamerayı m_vAt etrafında orbit ettirmeye başla.
        /// direction: +1=sola (left buton), -1=sağa (right buton)
        /// </summary>
        public void StartOrbit(int direction, System.Action onComplete = null)
        {
            // Orbit is disabled since camera is static. Instantly invoke completion.
            onComplete?.Invoke();
        }
        
        /// <summary>
        /// Open-KO birebir: cpp:848-865 CheckRotateCenterToRight/Left
        /// Kamerayı başlangıç (center) pozisyonuna geri döndür.
        /// </summary>
        public void ResetOrbit()
        {
            if (_camera == null) return;
            _camera.transform.position = _vEyeBackup;
            _camera.transform.LookAt(_vAt);
        }
        
        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
