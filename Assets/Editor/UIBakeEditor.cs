using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.UI;

namespace EntropyOnline.Editor
{
    /// <summary>
    /// Sahnedeki aktif modernize edilmiş UI GameObject'inin (Play Mode veya Edit Mode fark etmeksizin)
    /// tüm bileşenleriyle (TMPro, ScrollRect vb.) birlikte bir kopyasını alarak 
    /// Assets/Resources/ModernUI klasörü altına yerel (native) Prefab olarak fırınlar (bake).
    /// </summary>
    public static class UIBakeEditor
    {
        [MenuItem("Entropy Online/UI/Bake Selected UI to Modern Prefab")]
        public static void BakeSelectedUI()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Bake UI Error", "Lütfen Hierarchy (Hiyerarşi) penceresinde fırınlamak istediğiniz UI GameObject'ini seçin.", "OK");
                return;
            }

            RectTransform rt = selected.GetComponentInChildren<RectTransform>();
            if (rt == null)
            {
                EditorUtility.DisplayDialog("Bake UI Error", "Seçilen nesne veya alt nesneleri bir UI elemanı (RectTransform barındırmalı) olmalıdır.", "OK");
                return;
            }

            // Hedef Klasör: Assets/Resources/ModernUI
            string dirPath = "Assets/Resources/ModernUI";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
                AssetDatabase.Refresh();
            }

            // KO_UI_ ve (Clone) gibi runtime eklerini temizle
            string cleanName = selected.name.Replace("KO_UI_", "").Replace("(Clone)", "").Trim();

            // Eğer dinamik olarak oyuncu adına göre isimlendirilmiş bir fısıltı penceresiyse, orijinal ismine (co_whisper_open_us) çevir
            if (cleanName.StartsWith("co_whisper_open_"))
            {
                cleanName = "co_whisper_open_us";
            }

            string prefabPath = Path.Combine(dirPath, cleanName + ".prefab");

            // Clone the selected GameObject to clean it up before baking
            EntropyOnline.UI.MobileSkillBar.IsBaking = true;
            GameObject clone = Object.Instantiate(selected);
            EntropyOnline.UI.MobileSkillBar.IsBaking = false;
            clone.name = cleanName;

            // Clean up runtime dynamic GameObjects from the clone
            CleanDynamicObjects(clone.transform);



            // Automatically clean up missing script behaviors to prevent save failures
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(clone);
            if (removed > 0)
            {
                Debug.Log($"[UIBake] {removed} missing script component(s) removed from the clone.");
            }

            // Save the clean clone
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            Object.DestroyImmediate(clone);

            if (prefabAsset != null)
            {
                Debug.Log($"[UIBake] '{selected.name}' başarıyla '{prefabPath}' konumuna fırınlandı!");
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Bake UI Başarılı", $"Arayüz başarıyla prefab olarak kaydedildi:\n{prefabPath}\n\nŞimdi oyunu durdurup, oluşan bu prefab üzerindeki KOWhisperPanel bileşenine ilgili Text/Button referanslarını Inspector'dan bağlayabilirsiniz!", "OK");
            }
            else
            {
                Debug.LogError($"[UIBake] '{selected.name}' prefab olarak kaydedilemedi.");
            }
        }

        private static void CleanDynamicObjects(Transform trans, bool isMobileSkillBar = false)
        {
            if (trans.name == "MobileSkillBar" || trans.name == "MobileSkillBarCanvas")
            {
                isMobileSkillBar = true;
            }

            for (int i = trans.childCount - 1; i >= 0; i--)
            {
                var child = trans.GetChild(i);
                string name = child.name;

                // Special handling for Skill Slot children to clean dynamic runtime states
                if (name == "CountText")
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                if (name == "text0" || name == "text_message")
                {
                    var txt = child.GetComponent<Text>();
                    if (txt != null) txt.text = "";
                }
                if (name == "Icon" && child.parent != null && child.parent.name == "Inner" && child.parent.parent != null && child.parent.parent.name.StartsWith("Skill_"))
                {
                    var img = child.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = null;
                        img.color = Color.clear;
                    }
                }
                if (name == "CooldownOverlay" && child.parent != null && child.parent.name == "Inner" && child.parent.parent != null && child.parent.parent.name.StartsWith("Skill_"))
                {
                    var img = child.GetComponent<Image>();
                    if (img != null)
                    {
                        img.fillAmount = 0f;
                    }
                }

                // Exclude known dynamic objects
                if (name.StartsWith("EqSlot_") || 
                    name.StartsWith("BagSlot_") || 
                    name.StartsWith("BlockRow_") || 
                    name.StartsWith("Bubble_") ||
                    (name.StartsWith("Skill_") && !isMobileSkillBar) ||
                    name.StartsWith("Row_Skill_") ||
                    name.StartsWith("Row_Stat_") ||
                    name.StartsWith("WareSlot_") ||
                    name.StartsWith("WareInvSlot_") ||
                    name.StartsWith("TradeInvSlot_") ||
                    name.StartsWith("ShopInvItem_") ||
                    name.StartsWith("ShopItem_") ||
                    name.StartsWith("UpgradeInvSlot_") ||
                    name.StartsWith("FastUpgradeSlot_") ||
                    name.StartsWith("MerchantSetupIcon_") ||
                    name.StartsWith("MerchantSetupInvIcon_") ||
                    name.StartsWith("AccessoryIcon_") ||
                    name.Contains("(Clone)") ||
                    name == "HeaderBar" ||
                    name.StartsWith("SlotIndexFrame_") ||
                    name.StartsWith("SlotSelectionOutline_") ||
                    name.StartsWith("SlotArrowBtn_") ||
                    name.StartsWith("SlotClassIcon_") ||
                    name.StartsWith("SlotNameBg_") ||
                    name == "HPBarBG" ||
                    name == "HPBarMask" ||
                    name == "HPBarBorder" ||
                    name == "HPBarSelectedOutline" ||
                    name == "MPBarBG" ||
                    name == "MPBarMask" ||
                    name == "MPBarBorder" ||
                    name == "MobileStateBarContainer")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
                else
                {
                    CleanDynamicObjects(child, isMobileSkillBar);
                }
            }
        }

    }
}
