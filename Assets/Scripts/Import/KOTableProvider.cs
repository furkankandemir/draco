using System.IO;
using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// .tbl/.fxb dosyalarını Resources/KOData/ altından yükleyen merkezi yardımcı.
    /// KOBinaryProvider'ın tablo versiyonu.
    ///
    /// Dosya isimlendirme: baseName_ext.bytes
    /// Örnek: Item_Org_us.tbl → Item_Org_us_tbl.bytes
    /// </summary>
    public static class KOTableProvider
    {
        /// <summary>
        /// .tbl dosyasını Resources/KOData/ altından yükle ve decrypt et.
        /// </summary>
        /// <param name="tblPath">Orijinal .tbl dosya yolu veya sadece dosya adı</param>
        /// <returns>Decrypt edilmiş byte[], bulunamazsa null</returns>
        public static byte[] LoadDecryptedTbl(string tblPath)
        {
            var raw = LoadRaw(tblPath);
            if (raw == null) return null;
            return KOTableReader.DecryptTblPublic(raw);
        }

        /// <summary>
        /// Herhangi bir binary dosyayı Resources/KOData/ altından yükle (decrypt yok).
        /// .fxb gibi dosyalar için.
        /// </summary>
        public static byte[] LoadRaw(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string resourceName = string.IsNullOrEmpty(ext) ? baseName : baseName + "_" + ext;

            var textAsset = Resources.Load<TextAsset>($"KOData/{resourceName}");
            if (textAsset != null)
            {
                byte[] data = textAsset.bytes;
                Resources.UnloadAsset(textAsset);
                return data;
            }

            return null;
        }

        /// <summary>
        /// Dosyanın Resources/KOData/ altında var olup olmadığını kontrol et.
        /// </summary>
        public static bool Exists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;

            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string ext = Path.GetExtension(filePath).TrimStart('.');
            string resourceName = string.IsNullOrEmpty(ext) ? baseName : baseName + "_" + ext;

            var textAsset = Resources.Load<TextAsset>($"KOData/{resourceName}");
            if (textAsset != null)
            {
                Resources.UnloadAsset(textAsset);
                return true;
            }
            return false;
        }
    }
}
