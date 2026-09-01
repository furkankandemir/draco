using EntropyOnline.Network.KO;
using EntropyOnline.UI;
using EntropyOnline.Network;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using EntropyOnline.Import;

namespace EntropyOnline.World
{
    public class KOEquipmentVisualizer : MonoBehaviour
    {
        public const int PREVIEW_LAYER = 9;

        public const int ITEM_SLOT_POS_HAND_RIGHT = 6;
        public const int ITEM_SLOT_POS_HAND_LEFT = 8;

        public const int PLUG_POS_RIGHTHAND = 0;
        public const int PLUG_POS_LEFTHAND = 1;

        public const int ITEM_TYPE_PLUG = 1;
        public const int ITEM_TYPE_PART = 2;

        private string[] _currentPartFiles = new string[N3CharBuilder.PART_POS_COUNT];

        private string _currentRHPlugFile;
        private string _currentLHPlugFile;

        private GameObject _characterModel;
        private bool _initialCleanupDone = false;

        private KOTableReader.TableItemBasic[] m_pItemPartBasics = new KOTableReader.TableItemBasic[N3CharBuilder.PART_POS_COUNT];
        private KOTableReader.TableItemExt[] m_pItemPartExts = new KOTableReader.TableItemExt[N3CharBuilder.PART_POS_COUNT];

        public static Dictionary<uint, KOTableReader.TablePlayerLooks> s_pTbl_UPC_Looks;

        private void Awake()
        {
        }

        private void OnEnable()
        {
            KOInventory.OnEquipmentChanged += HandleEquipmentChanged;
            KOPacketHandler.OnUserLookChange += HandleUserLookChange_KO;
            KOPacketHandler.OnInventoryData += HandleInventoryData_KO;
            KOPacketHandler.OnMyInfo += HandleMyInfo_KO;
        }

        private void OnDisable()
        {
            KOInventory.OnEquipmentChanged -= HandleEquipmentChanged;
            KOPacketHandler.OnUserLookChange -= HandleUserLookChange_KO;
            KOPacketHandler.OnInventoryData -= HandleInventoryData_KO;
            KOPacketHandler.OnMyInfo -= HandleMyInfo_KO;
        }

        public void InitEquipment()
        {
            EnsureLooksLoaded();
            EnsureCharacterModel();
            if (_characterModel == null) return;

            if (!_initialCleanupDone)
            {
                CleanupNonPrefixedParts();
                _initialCleanupDone = true;
            }

            HandleItemMoveResult(1);
        }

        private void HandleEquipmentChanged(byte direction, int srcIndex, int dstIndex)
        {
            EnsureLooksLoaded();
            if (KOInventory.Instance == null) return;
            EnsureCharacterModel();
            if (_characterModel == null) return;

            if (!_initialCleanupDone)
            {
                CleanupNonPrefixedParts();
                _initialCleanupDone = true;
            }

            HandleItemMoveResult(1);
        }

        private void HandleUserLookChange_KO(byte[] rawData)
        {
        }

        private void HandleInventoryData_KO(byte[] rawData)
        {
            StartCoroutine(ApplyEquipmentNextFrame());
        }

        private System.Collections.IEnumerator ApplyEquipmentNextFrame()
        {
            yield return null;
            if (KOInventory.Instance == null) yield break;
            EnsureCharacterModel();
            if (_characterModel == null) yield break;
            HandleItemMoveResult(1);
        }

        private void HandleMyInfo_KO(byte[] rawData)
        {
            StartCoroutine(ApplyEquipmentAfterMyInfo());
        }

        private System.Collections.IEnumerator ApplyEquipmentAfterMyInfo()
        {
            yield return null;
            yield return null;
            if (KOInventory.Instance == null) yield break;
            EnsureCharacterModel();
            if (_characterModel == null) yield break;

            if (!_initialCleanupDone)
            {
                CleanupNonPrefixedParts();
                _initialCleanupDone = true;
            }
            HandleItemMoveResult(1);
            var pc = GetComponent<EntropyOnline.Character.PlayerController>();
            if (pc != null)
                pc.ForcePlayIdle();
        }

        private void HandleInventoryData(InventoryItemData[] items)
        {
            EnsureLooksLoaded();
            EnsureCharacterModel();
            if (_characterModel == null) return;

            if (!_initialCleanupDone)
            {
                CleanupNonPrefixedParts();
                _initialCleanupDone = true;
            }

            string rhPlugFile = null;
            string lhPlugFile = null;
            bool lhIsShield = false;
            KOTableReader.TableItemExt rhItemExt = null;
            KOTableReader.TableItemExt lhItemExt = null;

            var newPartBasics = new KOTableReader.TableItemBasic[N3CharBuilder.PART_POS_COUNT];
            var newPartExts = new KOTableReader.TableItemExt[N3CharBuilder.PART_POS_COUNT];
            string[] newPartFiles = new string[N3CharBuilder.PART_POS_COUNT];

            byte eRace = Core.GameManager.Instance?.Race ?? 0;

            foreach (var item in items)
            {
                if (!item.IsEquipped) continue;

                var pItem = FindItemBasic(item.ItemDefId);
                if (pItem == null) continue;

                var pItemExt = FindItemExt(pItem, item.ItemDefId);

                int eItemType = KOInventory.MakeResrcFileNameForUPC(
                    pItem, null,
                    out int ePartPos, out int ePlugPos,
                    eRace);

                if (eItemType == ITEM_TYPE_PLUG)
                {
                    string plugFileName = MakePlugResourceFileName(pItem, item.ItemDefId);
                    if (string.IsNullOrEmpty(plugFileName)) continue;

                    if (ePlugPos == PLUG_POS_RIGHTHAND)
                    {
                        rhPlugFile = plugFileName;
                        rhItemExt = pItemExt;
                    }
                    else if (ePlugPos == PLUG_POS_LEFTHAND)
                    {
                        lhPlugFile = plugFileName;
                        lhItemExt = pItemExt;
                        lhIsShield = (pItem.byClass == (byte)EntropyOnline.Character.KOItemClass.ITEM_CLASS_SHIELD);
                    }
                }
                else if (eItemType == ITEM_TYPE_PART)
                {
                    if (ePartPos >= 0 && ePartPos < N3CharBuilder.PART_POS_COUNT)
                    {
                        string partFileName = MakePartResourceFileName(pItem, item.ItemDefId, eRace);
                        if (!string.IsNullOrEmpty(partFileName))
                        {
                            newPartFiles[ePartPos] = partFileName;
                            newPartBasics[ePartPos] = pItem;
                            newPartExts[ePartPos] = pItemExt;
                        }
                    }
                }
            }

            UpdatePlug(PLUG_POS_RIGHTHAND, rhPlugFile, ref _currentRHPlugFile, "PLUG_RH", rhItemExt);
            UpdatePlug(PLUG_POS_LEFTHAND, lhPlugFile, ref _currentLHPlugFile, "PLUG_LH", lhItemExt, lhIsShield);

            ApplyPartsDirect(newPartFiles, newPartBasics, newPartExts, eRace);
        }

        private void HandleItemMoveResult(byte result)
        {
            EnsureLooksLoaded();
            if (result == 0) return;

            if (KOInventory.Instance == null) return;

            EnsureCharacterModel();
            if (_characterModel == null) return;

            if (!_initialCleanupDone)
            {
                CleanupNonPrefixedParts();
                _initialCleanupDone = true;
            }

            string rhPlugFile = null;
            string lhPlugFile = null;
            bool lhIsShield = false;
            KOTableReader.TableItemExt rhItemExt = null;
            KOTableReader.TableItemExt lhItemExt = null;

            var newPartBasics = new KOTableReader.TableItemBasic[N3CharBuilder.PART_POS_COUNT];
            var newPartExts = new KOTableReader.TableItemExt[N3CharBuilder.PART_POS_COUNT];
            string[] newPartFiles = new string[N3CharBuilder.PART_POS_COUNT];

            byte eRace = Core.GameManager.Instance?.Race ?? 0;

            for (int slot = 0; slot < KOInventory.ITEM_SLOT_COUNT; slot++)
            {
                var slotItem = KOInventory.Instance.m_pMySlot[slot];
                if (slotItem == null || slotItem.IsEmpty) continue;

                int itemDefId = slotItem.itemId;
                var pItem = FindItemBasic(itemDefId);
                if (pItem == null) continue;

                var pItemExt = FindItemExt(pItem, itemDefId);

                int eItemType = KOInventory.MakeResrcFileNameForUPC(
                    pItem, null,
                    out int ePartPos, out int ePlugPos,
                    eRace);

                if (eItemType == ITEM_TYPE_PLUG)
                {
                    string plugFileName = MakePlugResourceFileName(pItem, itemDefId);
                    if (string.IsNullOrEmpty(plugFileName)) continue;

                    if (slot == KOInventory.ITEM_SLOT_HAND_RIGHT)
                    {
                        ePlugPos = PLUG_POS_RIGHTHAND;
                    }
                    else if (slot == KOInventory.ITEM_SLOT_HAND_LEFT)
                    {
                        ePlugPos = PLUG_POS_LEFTHAND;
                    }

                    if (ePlugPos == PLUG_POS_RIGHTHAND)
                    {
                        rhPlugFile = plugFileName;
                        rhItemExt = pItemExt;
                    }
                    else if (ePlugPos == PLUG_POS_LEFTHAND)
                    {
                        lhPlugFile = plugFileName;
                        lhItemExt = pItemExt;
                        lhIsShield = (pItem.byClass == (byte)EntropyOnline.Character.KOItemClass.ITEM_CLASS_SHIELD);
                    }
                }
                else if (eItemType == ITEM_TYPE_PART)
                {
                    if (ePartPos >= 0 && ePartPos < N3CharBuilder.PART_POS_COUNT)
                    {
                        string partFileName = MakePartResourceFileName(pItem, itemDefId, eRace);
                        if (!string.IsNullOrEmpty(partFileName))
                        {
                            newPartFiles[ePartPos] = partFileName;
                            newPartBasics[ePartPos] = pItem;
                            newPartExts[ePartPos] = pItemExt;
                        }
                    }
                }
            }

            UpdatePlug(PLUG_POS_RIGHTHAND, rhPlugFile, ref _currentRHPlugFile, "PLUG_RH", rhItemExt);
            UpdatePlug(PLUG_POS_LEFTHAND, lhPlugFile, ref _currentLHPlugFile, "PLUG_LH", lhItemExt, lhIsShield);

            ApplyPartsDirect(newPartFiles, newPartBasics, newPartExts, eRace);
        }

        private void ApplyPartsDirect(
            string[] newPartFiles,
            KOTableReader.TableItemBasic[] newPartBasics,
            KOTableReader.TableItemExt[] newPartExts,
            byte eRace)
        {
            _myRace = eRace; // Record race for temporary helmet calculations
            KOTableReader.TablePlayerLooks pLooks = null;
            if (s_pTbl_UPC_Looks != null)
                s_pTbl_UPC_Looks.TryGetValue(eRace, out pLooks);

            int charClass = Core.GameManager.Instance?.CharClass ?? 0;

            for (int ePos = 0; ePos < N3CharBuilder.PART_POS_COUNT; ePos++)
            {
                m_pItemPartBasics[ePos] = newPartBasics[ePos];
                m_pItemPartExts[ePos] = newPartExts[ePos];

                string targetFile = newPartFiles[ePos];

                if (ePos == N3CharBuilder.PART_POS_LOWER && eRace == 13)
                {
                    var upperItem = newPartBasics[N3CharBuilder.PART_POS_UPPER];
                    if (upperItem != null)
                    {
                        uint upperBaseId = upperItem.dwID / 1000;
                        if (upperBaseId == 263001 || upperBaseId == 264001 || upperBaseId == 265001)
                        {
                            targetFile = null;
                            UpdatePartVisual(ePos, null);
                            continue;
                        }
                    }
                }

                if (string.IsNullOrEmpty(targetFile))
                {
                    if (ePos == N3CharBuilder.PART_POS_HAIR_HELMET)
                    {
                        if (IsStealthActive)
                        {
                            // Eğer stealth aktifse, saç yerine geçici kaskı tak
                            int d1 = 2;
                            int d2 = 4300;
                            int d3 = 30;
                            int d4 = 0;
                            int d2WithRace = d2 + eRace;
                            string tempHelmetFile = $"Item/{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
                            
                            Import.N3CharBuilder.PartSet(_characterModel, ePos, tempHelmetFile);
                            _currentPartFiles[ePos] = tempHelmetFile;
                            continue;
                        }

                        if (pLooks != null)
                        {
                            byte iHair = Core.GameManager.Instance?.PlayerHairColor ?? 0;
                            N3CharBuilder.InitHair(_characterModel, pLooks, iHair);
                            _currentPartFiles[ePos] = $"HAIR_{iHair}";
                        }
                        continue;
                    }
                    else if (ePos == N3CharBuilder.PART_POS_FACE)
                    {
                        if (pLooks != null)
                        {
                            byte iFace = Core.GameManager.Instance?.PlayerFace ?? 0;
                            N3CharBuilder.InitFace(_characterModel, pLooks, iFace);
                            _currentPartFiles[ePos] = $"FACE_{iFace}";
                        }
                        continue;
                    }
                    else
                    {
                        if (pLooks != null && ePos < pLooks.szPartFNs.Length)
                        {
                            string defaultPart = pLooks.szPartFNs[ePos];
                            defaultPart = N3CharBuilder.GetDefaultPartPath(ePos, eRace, charClass, defaultPart);
                            if (!string.IsNullOrEmpty(defaultPart))
                                targetFile = defaultPart;
                        }
                    }
                }

                UpdatePartVisual(ePos, targetFile);
            }
        }

        private string MakePlugResourceFileName(KOTableReader.TableItemBasic pItem, int itemDefId)
        {
            if (pItem == null || pItem.dwIDResrc == 0) return null;

            // ==========================================
            // [Araya Girme / Interception - Dark Vane]
            // Dark Vane (Base ID: 119101000) için Shard modelinden ayırıp 
            // kendine has bir override model adı dönmesini sağlıyoruz.
            // ==========================================
            if (itemDefId / 1000 * 1000 == 119101000)
            {
                string dvFileName = "Item/1_1910_11_1.n3cplug";
                return dvFileName;
            }

            uint iIDResrc = pItem.dwIDResrc;
            var pItemExt = FindItemExt(pItem, itemDefId);
            if (pItemExt != null && pItemExt.dwIDResrc != 0)
            {
                iIDResrc = pItemExt.dwIDResrc;
            }

            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = (int)((iIDResrc / 10) % 100);
            int d4 = (int)(iIDResrc % 10);

            string fileName = $"Item/{d1}_{d2:D4}_{d3:D2}_{d4}.n3cplug";

            return fileName;
        }

        private string MakePartResourceFileName(KOTableReader.TableItemBasic pItem, int itemDefId, byte eRace)
        {
            if (pItem == null || pItem.dwIDResrc == 0) return null;

            uint iIDResrc = pItem.dwIDResrc;
            var pItemExt = FindItemExt(pItem, itemDefId);
            if (pItemExt != null && pItemExt.dwIDResrc != 0)
            {
                iIDResrc = pItemExt.dwIDResrc;
            }

            int d1 = (int)(iIDResrc / 10000000);
            int d2 = (int)((iIDResrc / 1000) % 10000);
            int d3 = (int)((iIDResrc / 10) % 100);
            int d4 = (int)(iIDResrc % 10);

            int d2WithRace = d2 + eRace;
            if (eRace == 4)
            {
                string testPath = $"Item\\{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
                bool exists = KOBinaryProvider.Exists(testPath);
                Debug.Log($"[DEBUG_VIS] itemDefId={itemDefId}, d1={d1}, d2={d2}, d2WithRace={d2WithRace}, d3={d3}, d4={d4}, testPath={testPath}, exists={exists}");
                if (!exists)
                {
                    d2WithRace = d2 + 13;
                }
            }

            string fileName = $"Item/{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";

            return fileName;
        }

        private GameObject GetPlugObject(string tag)
        {
            if (_characterModel == null) return null;
            var allTransforms = _characterModel.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith(tag + "_"))
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        private void UpdatePlug(int plugPos, string newPlugFile, ref string currentPlugFile, string tag,
            KOTableReader.TableItemExt pItemExt = null, bool isShield = false)
        {
            if (newPlugFile == currentPlugFile)
            {
                if (!string.IsNullOrEmpty(newPlugFile))
                {
                    var plugObj = GetPlugObject(tag);
                    if (plugObj != null)
                    {
                        var glow = plugObj.GetComponent<KOWeaponGlow>();
                        if (glow != null)
                        {
                            glow.Initialize(pItemExt);
                        }
                        else if (pItemExt != null)
                        {
                            var newGlow = plugObj.AddComponent<KOWeaponGlow>();
                            newGlow.Initialize(pItemExt);
                        }
                    }
                }
                return;
            }
 
            if (string.IsNullOrEmpty(newPlugFile))
            {
                N3CharBuilder.PlugRemove(_characterModel, tag);
                currentPlugFile = null;
            }
            else
            {
                int jointIndex = -1;
                byte eRace = Core.GameManager.Instance?.Race ?? 0;
                if (s_pTbl_UPC_Looks != null && s_pTbl_UPC_Looks.TryGetValue(eRace, out var pLooks))
                {
                    if (plugPos == PLUG_POS_RIGHTHAND)
                        jointIndex = pLooks.iJointRH;
                    else if (plugPos == PLUG_POS_LEFTHAND)
                        jointIndex = isShield ? pLooks.iJointLH2 : pLooks.iJointLH;
                }
 
                var plugObj = N3CharBuilder.PlugSet(
                    _characterModel, newPlugFile, jointIndex, tag);

                if (plugObj != null)
                {
                    currentPlugFile = newPlugFile;

                    ApplyPlugEnchantTrailColor(plugObj, pItemExt);

                    if (pItemExt != null)
                    {
                        var glow = plugObj.AddComponent<KOWeaponGlow>();
                        glow.Initialize(pItemExt);
                    }

                }
                else
                {
                    Debug.LogWarning($"[EQUIP-VIS] ❌ Plug yüklenemedi: {newPlugFile}");
                }
            }

            // If stealth is active, refresh transparency for newly equipped weapons (plugs)
            if (IsStealthActive && GameHUD.Instance != null)
            {
                bool isMe = gameObject.name == "Player" || gameObject.CompareTag("Player") || GetComponent<EntropyOnline.Character.PlayerController>() != null;
                GameHUD.Instance.RefreshStealthTransparency(gameObject, isMe, true, true);
            }
        }

        private void ApplyPlugEnchantTrailColor(GameObject plugObj, KOTableReader.TableItemExt pItemExt)
        {
            if (plugObj == null || pItemExt == null) return;
            var trail = plugObj.GetComponent<KOWeaponTrail>() ?? plugObj.GetComponentInChildren<KOWeaponTrail>();
            if (trail == null) return;

            const int LIMIT_FX_DAMAGE = 64;
            const int ITEM_ATTRIB_UNIQUE = 4;

            if ((pItemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && pItemExt.byDamageFire > 0)
                     || (pItemExt.byDamageFire >= LIMIT_FX_DAMAGE))
            {
                trail.crTrace = 0xffff0000;
            }
            else if ((pItemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && pItemExt.byDamageIce > 0)
                     || (pItemExt.byDamageIce >= LIMIT_FX_DAMAGE))
            {
                trail.crTrace = 0xff0000ff;
            }
            else if ((pItemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && pItemExt.byDamageThuner > 0)
                     || (pItemExt.byDamageThuner >= LIMIT_FX_DAMAGE))
            {
                trail.crTrace = 0xffffffff;
            }
            else if ((pItemExt.byMagicOrRare == ITEM_ATTRIB_UNIQUE && pItemExt.byDamagePoison > 0)
                     || (pItemExt.byDamagePoison >= LIMIT_FX_DAMAGE))
            {
                trail.crTrace = 0xffff00ff;
            }
        }

        private void UpdatePartVisual(int partIndex, string newPartFile)
        {
            // HAIR_HELMET ve FACE için C++ CPlayerBase::PartSet(szFN) boşsa veya default ise
            // InitHair() veya InitFace() çağrılarak style/color eklenmiş dosya yüklenir.
            if (partIndex == N3CharBuilder.PART_POS_HAIR_HELMET &&
                (string.IsNullOrEmpty(newPartFile) || newPartFile.EndsWith("hair.n3cpart", System.StringComparison.OrdinalIgnoreCase)))
            {
                // Eğer bu karakter Stealth aktifse, saç yerine geçici kaskı tak!
                if (IsStealthActive)
                {
                    byte iHair = Core.GameManager.Instance?.PlayerHairColor ?? 0;
                    _savedHairFile = $"HAIR_{iHair}"; // Saçı yedekle
                    
                    // Irka göre geçici bir kask tak (Rogue Plate Helmet - ResrcID: 24300300)
                    byte eRace = _myRace != 0 ? _myRace : (byte)(Core.GameManager.Instance?.Race ?? 0);
                    int d1 = 2;
                    int d2 = 4300;
                    int d3 = 30;
                    int d4 = 0;
                    int d2WithRace = d2 + eRace;
                    string tempHelmetFile = $"Item/{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
                    
                    // Doğrudan kaskı tak
                    Import.N3CharBuilder.PartSet(_characterModel, partIndex, tempHelmetFile);
                    _currentPartFiles[partIndex] = tempHelmetFile;
                    return;
                }

                if (s_pTbl_UPC_Looks != null && s_pTbl_UPC_Looks.TryGetValue((uint)(Core.GameManager.Instance?.Race ?? 0), out var pLooks))
                {
                    byte iHair = Core.GameManager.Instance?.PlayerHairColor ?? 0;
                    N3CharBuilder.InitHair(_characterModel, pLooks, iHair);
                    _currentPartFiles[partIndex] = $"HAIR_{iHair}";
                }
                else
                {
                    N3CharBuilder.PartRemove(_characterModel, partIndex);
                    _currentPartFiles[partIndex] = null;
                }
                return;
            }

            if (partIndex == N3CharBuilder.PART_POS_FACE &&
                (string.IsNullOrEmpty(newPartFile) || newPartFile.EndsWith("face.n3cpart", System.StringComparison.OrdinalIgnoreCase)))
            {
                if (s_pTbl_UPC_Looks != null && s_pTbl_UPC_Looks.TryGetValue((uint)(Core.GameManager.Instance?.Race ?? 0), out var pLooks))
                {
                    byte iFace = Core.GameManager.Instance?.PlayerFace ?? 0;
                    N3CharBuilder.InitFace(_characterModel, pLooks, iFace);
                    _currentPartFiles[partIndex] = $"FACE_{iFace}";
                }
                else
                {
                    N3CharBuilder.PartRemove(_characterModel, partIndex);
                    _currentPartFiles[partIndex] = null;
                }
                return;
            }

            // CN3Chr::PartSet birebir: aynı dosyaysa dokunma
            if (newPartFile == _currentPartFiles[partIndex]) return;

            if (string.IsNullOrEmpty(newPartFile))
            {
                // Part kaldırıldı
                N3CharBuilder.PartRemove(_characterModel, partIndex);
                _currentPartFiles[partIndex] = null;
            }
            else
            {
                // Yeni part — yükle ve ekle
                var partObj = N3CharBuilder.PartSet(
                    _characterModel, partIndex, newPartFile);

                if (partObj != null)
                {
                    _currentPartFiles[partIndex] = newPartFile;
                }
                else
                {
                    // Yükleme başarısız! Önbelleği temizle ki bir sonraki giyme denemesinde takılmasın.
                    _currentPartFiles[partIndex] = null;
                    Debug.LogWarning($"[EQUIP-VIS] ❌ Part yüklenemedi: {newPartFile}");
                }
            }

            // If stealth is active, refresh transparency for newly equipped parts
            if (IsStealthActive && GameHUD.Instance != null)
            {
                bool isMe = gameObject.name == "Player" || gameObject.CompareTag("Player") || GetComponent<EntropyOnline.Character.PlayerController>() != null;
                GameHUD.Instance.RefreshStealthTransparency(gameObject, isMe, true, true);
            }
        }

        private void EnsureCharacterModel()
        {
            if (_characterModel != null && _characterModel != gameObject) return;
 
            // Karakter modelini bul: içinde Animation component'i olan çocuk nesneyi bul
            var anim = GetComponentInChildren<Animation>();
            if (anim != null && anim.gameObject != gameObject)
            {
                _characterModel = anim.gameObject;
            }
            else
            {
                _characterModel = gameObject;
            }
        }

        private void CleanupNonPrefixedParts()
        {
            if (_characterModel == null) return;

            for (int i = _characterModel.transform.childCount - 1; i >= 0; i--)
            {
                var child = _characterModel.transform.GetChild(i);
                if (child.GetComponent<SkinnedMeshRenderer>() == null &&
                    child.GetComponent<MeshRenderer>() == null)
                    continue;

                if (child.name.StartsWith("PART_") || child.name.StartsWith("PLUG_"))
                    continue;

                DestroyImmediate(child.gameObject);
            }
        }

        private KOTableReader.TableItemBasic FindItemBasic(int itemDefId)
        {
            if (KOInventory.s_pTbl_Items_Basic == null) return null;
            uint baseId = (uint)(itemDefId / 1000 * 1000);
            KOInventory.s_pTbl_Items_Basic.TryGetValue(baseId, out var basic);
            return basic;
        }

        private KOTableReader.TableItemExt FindItemExt(KOTableReader.TableItemBasic pItem, int itemDefId)
        {
            if (KOInventory.s_pTbl_Items_Exts == null || pItem == null) return null;
            if (pItem.byExtIndex < 0 || pItem.byExtIndex >= KOInventory.s_pTbl_Items_Exts.Length) return null;
            var extDict = KOInventory.s_pTbl_Items_Exts[pItem.byExtIndex];
            if (extDict == null) return null;
            uint extKey = (uint)(itemDefId % 1000);
            extDict.TryGetValue(extKey, out var ext);
            return ext;
        }
 
        private void EnsureLooksLoaded()
        {
            if (s_pTbl_UPC_Looks == null)
            {
                string looksPath = "UPC_DefaultLooks.tbl";
                s_pTbl_UPC_Looks = KOTableReader.LoadUpcLooksTable(looksPath);
            }
        }

        public bool IsStealthActive { get; set; }
        private byte _myRace = 0;
        private string _savedHairFile = null;

        public void SetStealthHelmet(bool active)
        {
            int partIndex = Import.N3CharBuilder.PART_POS_HAIR_HELMET;
            if (active)
            {
                // Eğer şu an takılı olan kask değil de saç ise (HAIR_ ile başlıyorsa)
                if (_currentPartFiles[partIndex] != null && _currentPartFiles[partIndex].StartsWith("HAIR_"))
                {
                    _savedHairFile = _currentPartFiles[partIndex]; // Saçı yedekle
                    
                    // Irka göre geçici bir kask tak (Rogue Plate Helmet - ResrcID: 24300300)
                    byte eRace = _myRace != 0 ? _myRace : (byte)(Core.GameManager.Instance?.Race ?? 0);
                    int d1 = 2;
                    int d2 = 4300;
                    int d3 = 30;
                    int d4 = 0;
                    int d2WithRace = d2 + eRace;
                    string tempHelmetFile = $"Item/{d1}_{d2WithRace:D4}_{d3:D2}_{d4}.n3cpart";
                    
                    UpdatePartVisual(partIndex, tempHelmetFile);
                }
            }
            else
            {
                // Stealth bittiğinde, eğer yedeklenmiş saç varsa geri yükle
                if (!string.IsNullOrEmpty(_savedHairFile))
                {
                    UpdatePartVisual(partIndex, null); // null verilince InitHair çağrılır
                    _savedHairFile = null;
                }
            }
        }
    }
}
