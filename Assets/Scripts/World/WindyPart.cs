using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// CN3SPart::Tick() — RF_WINDY (0x20) birebir portu.
    /// Rüzgarla hafifçe sallanma efekti (ağaç yaprakları, çimenler vb.)
    ///
    /// C++ (N3Shape.cpp:131-173):
    ///   m_fTimeToSetWind -= s_fSecPerFrm;
    ///   if (m_fTimeToSetWind <= 0)
    ///       m_fWindFactorToReach = rand(0..1.0);
    ///       m_fTimeToSetWind = rand(0..3.0);
    ///   else if (m_fWindFactorToReach != m_fWindFactorCur)
    ///       float fFactor = s_fSecPerFrm * abs(reach - cur);
    ///       if (cur < reach) cur += fFactor;
    ///       if (cur > reach) cur -= fFactor;
    ///       if (abs(reach - cur) < fFactor) cur = reach;
    ///       vPos = pivot * mtxParent;
    ///       m_Matrix.Rotation(Vector3(0.05, 0.02, 0.05) * windFactorCur);
    ///       m_Matrix *= mtxParent;
    ///       m_Matrix.PosSet(vPos);
    /// </summary>
    public class WindyPart : MonoBehaviour
    {
        // C++ karşılıkları: m_fWindFactorToReach, m_fWindFactorCur, m_fTimeToSetWind
        private float _windFactorToReach;
        private float _windFactorCur;
        private float _timeToSetWind;

        // C++ sabit değerler (N3Shape.cpp:169)
        private static readonly Vector3 WindRotationAxis = new Vector3(0.05f, 0.02f, 0.05f);

        private void Start()
        {
            // Her part farklı bir rüzgar fazında başlasın (C++'da rand() başlangıcı)
            _windFactorCur = Random.value;
            _windFactorToReach = Random.value;
            _timeToSetWind = 3.0f * Random.value;
        }

        private void Update()
        {
            // C++ (N3Shape.cpp:133): m_fTimeToSetWind -= CN3Base::s_fSecPerFrm;
            _timeToSetWind -= Time.deltaTime;

            if (_timeToSetWind <= 0f)
            {
                // C++ (N3Shape.cpp:141-142):
                // m_fWindFactorToReach = (rand() % 100) / 100.0f;
                // m_fTimeToSetWind = 3.0f * ((rand() % 100) / 100.0f);
                _windFactorToReach = Random.value;
                _timeToSetWind = 3.0f * Random.value;
            }
            else if (!Mathf.Approximately(_windFactorToReach, _windFactorCur))
            {
                // C++ (N3Shape.cpp:148): float fFactor = s_fSecPerFrm * abs(reach - cur);
                float fFactor = Time.deltaTime * Mathf.Abs(_windFactorToReach - _windFactorCur);

                // C++ (N3Shape.cpp:156-159):
                if (_windFactorCur < _windFactorToReach)
                    _windFactorCur += fFactor;
                if (_windFactorCur > _windFactorToReach)
                    _windFactorCur -= fFactor;

                // C++ (N3Shape.cpp:164-165): snap if close enough
                if (Mathf.Abs(_windFactorToReach - _windFactorCur) < fFactor)
                    _windFactorCur = _windFactorToReach;

                // C++ (N3Shape.cpp:169): m_Matrix.Rotation(Vector3(0.05, 0.02, 0.05) * m_fWindFactorCur);
                // Rotation(Vector3) = euler rotation matrix from radians (Matrix44.inl:210-233)
                // D3D row-vector: v * Rot(euler) * ParentWorld, pos = pivot * ParentWorld
                //
                // Unity eşdeğeri: localRotation = Euler(angles_in_degrees)
                // Part zaten parent Shape'in child'ı (inherits parent TRS)
                // localRotation sadece rüzgar salınımını ekler
                Vector3 eulerRad = WindRotationAxis * _windFactorCur;
                transform.localRotation = Quaternion.Euler(
                    eulerRad.x * Mathf.Rad2Deg,
                    eulerRad.y * Mathf.Rad2Deg,
                    eulerRad.z * Mathf.Rad2Deg);
            }
        }
    }
}
