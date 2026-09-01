using UnityEngine;

namespace EntropyOnline.Import
{
    /// <summary>
    /// Open-KO birebir: CN3AnimControl::m_Datas (vector&lt;__AnimData&gt;) karşılığı.
    /// C++'da AniCurSet(int iAni) → m_pAniCtrlRef→DataGet(iAni) ile index erişimi yapılır.
    /// Unity Animation component'ında foreach sırası garanti değildir,
    /// bu yüzden clip isimlerini dosya sırasıyla saklıyoruz.
    /// </summary>
    public class N3AnimClipRegistry : MonoBehaviour
    {
        /// <summary>
        /// .n3anim dosyasındaki sırayla clip isimleri.
        /// Index = e_Ani enum değeri.
        /// </summary>
        public string[] ClipNames;

        /// <summary>
        /// .n3anim dosyasındaki sırayla animasyon geçiş süreleri (fTimeBlend).
        /// Index = e_Ani enum değeri.
        /// </summary>
        public float[] BlendTimes;
    }
}
