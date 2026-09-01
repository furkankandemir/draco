// ===================================================================================
// Open-KO birebir: e_Ani index → Unity AnimationClip name resolver
//
// C++ referans:
//   GameDef.h satır 127-303  — e_Ani enum, klip isimleri
//   N3CPartImporter.AnimData — .n3anim dosyasındaki klip isimleri
//   PlayerBase.cpp:1613-1616 — JudgetAnimationSpellMagic() = (e_Ani)(m_iMagicAni)
//
// .n3anim dosyasındaki klip isimleri tam olarak e_Ani sırasına karşılık gelir.
// Bu resolver, bir e_Ani index'ini animasyon klip adına çevirir.
//
// NOT: Klip isimleri .n3anim dosyasında tanımlanır ve N3AnimBuilder tarafından
// Unity AnimationClip.name olarak atanır. Bu resolver, Animation component
// üzerindeki clip ismiyle eşleştirme yapar.
// ===================================================================================

using UnityEngine;

namespace EntropyOnline.Character
{
    /// <summary>
    /// Open-KO birebir: e_Ani index → Unity AnimationClip name resolver.
    ///
    /// KO animasyon sistemi .n3anim dosyasında animasyonları index sırasıyla listeler.
    /// e_Ani enum değeri doğrudan .n3anim'deki klip index'ine karşılık gelir.
    /// Klip isimleri karakter modeline (.n3anim dosyasına) göre değişir.
    ///
    /// Örnek (upc_el_rm.n3anim — El Morad right-hand male):
    ///   Index 0  = "breath"
    ///   Index 1  = "walk"
    ///   Index 2  = "run"
    ///   Index 3  = "walk_reverse"
    ///   ...
    ///
    /// Bu sınıf, bir Animation component üzerindeki klip listesini tarayarak
    /// e_Ani index'ini klip adına çevirir.
    /// </summary>
    public class KOAnimResolver
    {
        // Cached clip names by index — Animation component'taki klip sırasını tutar
        private string[] _clipNamesByIndex;
        private Animation _animComponent;

        /// <summary>
        /// Sıralı AnimationClip listesinden klip index listesini oluşturur.
        /// Open-KO birebir: CN3CPartImporter::LoadAnimControl → AnimData listesi
        /// Klip isimleri sırasıyla index'e karşılık gelir.
        ///
        /// ÖNEMLİ: Unity AnimationState foreach sırası GARANTI DEĞİLDİR.
        /// Bu yüzden N3CharBuilder'dan gelen sıralı clip listesini kullanmalıyız.
        /// </summary>
        public bool Initialize(System.Collections.Generic.List<AnimationClip> orderedClips, Animation anim = null)
        {
            _animComponent = anim;

            if (orderedClips == null || orderedClips.Count == 0)
                return false;

            _clipNamesByIndex = new string[orderedClips.Count];
            for (int i = 0; i < orderedClips.Count; i++)
                _clipNamesByIndex[i] = orderedClips[i].name;

            // Debug: TÜM clip index → isim eşlemesini logla
            return true;
        }

        /// <summary>
        /// N3AnimClipRegistry'deki sıralı clip isimleri dizisinden klip listesini oluşturur.
        /// </summary>
        public bool Initialize(string[] clipNames, Animation anim = null)
        {
            _animComponent = anim;

            if (clipNames == null || clipNames.Length == 0)
                return false;

            _clipNamesByIndex = new string[clipNames.Length];
            System.Array.Copy(clipNames, _clipNamesByIndex, clipNames.Length);

            return true;
        }

        /// <summary>
        /// Animation component'tan klip index listesini oluşturur.
        /// UYARI: Unity AnimationState foreach sırası garanti değildir!
        /// Mümkünse Initialize(List&lt;AnimationClip&gt;) kullanın.
        /// </summary>
        public bool Initialize(Animation anim)
        {
            if (anim == null) return false;
            _animComponent = anim;

            var clipList = new System.Collections.Generic.List<string>();
            foreach (AnimationState state in anim)
            {
                clipList.Add(state.name);
            }

            _clipNamesByIndex = clipList.ToArray();

            // Debug: TÜM clip index → isim eşlemesini logla (foreach dalı)
            return _clipNamesByIndex.Length > 0;
        }

        /// <summary>
        /// e_Ani index → klip adı çevirisi.
        ///
        /// Open-KO birebir: PlayerBase.cpp:1613-1616
        ///   JudgetAnimationSpellMagic() → return (e_Ani)(m_iMagicAni)
        ///   AniCurSet(eAni, ...) → animasyon oynatma
        ///
        /// .n3anim'deki klip index'i doğrudan e_Ani enum değerine karşılık gelir.
        /// </summary>
        public string GetClipName(KOAni aniIndex)
        {
            int idx = (int)aniIndex;
            if (idx < 0 || _clipNamesByIndex == null) return null;
            if (idx >= _clipNamesByIndex.Length) return null;
            return _clipNamesByIndex[idx];
        }

        /// <summary>
        /// Klip adı ile arama (fallback — isim bilinen durumlarda).
        /// </summary>
        public string FindClipByName(string partialName)
        {
            if (_clipNamesByIndex == null) return null;
            string lower = partialName.ToLowerInvariant();
            foreach (var name in _clipNamesByIndex)
            {
                if (name.ToLowerInvariant().Contains(lower))
                    return name;
            }
            return null;
        }

        /// <summary>
        /// Belirtilen e_Ani index'inin geçerli bir klip'e karşılık gelip gelmediğini kontrol eder.
        /// </summary>
        public bool HasClip(KOAni aniIndex)
        {
            return GetClipName(aniIndex) != null;
        }

        /// <summary>
        /// Toplam yüklenmiş klip sayısı.
        /// </summary>
        public int ClipCount => _clipNamesByIndex?.Length ?? 0;
    }
}
