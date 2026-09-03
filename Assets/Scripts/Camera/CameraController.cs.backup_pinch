using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using EntropyOnline.UI;

namespace EntropyOnline.Camera
{
    /// <summary>
    /// Entropy Online — 3. Şahıs Kamera Kontrolcüsü
    /// Sağ ekranı sürükleyerek kamera döndürülür.
    /// Open-KO'daki serbest kamera sisteminin mobil versiyonu.
    /// Yeni Input System kullanır.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Hedef")]
        [SerializeField] private Transform target;

        [Header("Kamera Ayarları")]
        [SerializeField] private float distance = 9f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private float height = 1.6f; // Orijinal Knight Online odak yüksekliği (fHeightPlayer * 0.8)
        [SerializeField] private float rotationSpeed = 3f;


        [Header("Açı Sınırları")]
        [SerializeField] private float minVerticalAngle = 10f;
        [SerializeField] private float maxVerticalAngle = 65f;

        private float _currentX = 0f;
        private float _currentY = 25f;
        private int _dragFingerId = -1;
        private bool _isRotating180 = false;
        private bool _isMouseRotationBlocked = false;
        private float _rotate180Duration = 0.36f; // Yarı yarıya yavaşlatılmış (0.36 saniye)
        private float _rotate180Elapsed = 0f;
        private float _rotate180Start = 0f;
        private float _rotate180Target = 0f;
        private EntropyOnline.Input.VirtualJoystick _rightJoystick;

        public float CurrentYaw => _currentX;

        public float Distance
        {
            get => distance;
            set => distance = Mathf.Clamp(value, minDistance, maxDistance);
        }

        public static float GetDistanceForZoomLimit(int zoomVal)
        {
            if (zoomVal >= 0)
            {
                return Mathf.Clamp(9f - (zoomVal * 0.6f), 3f, 25f);
            }
            else
            {
                return Mathf.Clamp(9f - (zoomVal * 1.6f), 3f, 25f);
            }
        }

        public void SetZoomLimit(int zoomVal)
        {
            distance = GetDistanceForZoomLimit(zoomVal);
        }

        public void SetTarget(Transform t)
        {
            target = t;
        }

        /// <summary>
        /// Teleport sonrası kamerayı anında hedef konuma taşır (Lerp yok).
        /// </summary>
        public void SnapToTarget()
        {
        }

        private void Start()
        {
            minVerticalAngle = 1f; // Force programmatically to override Unity serialized field limits

            // Eğer varsayılan yakın değerlerde (5f) kaldıysa, Knight Online açısına (9f) zorlayalım
            if (distance == 5f)
            {
                distance = 9f;
                maxDistance = 25f;
                height = 1.6f;
            }

            // Load initial zoom from settings
            if (GameOptionsManager.Instance != null)
            {
                distance = GetDistanceForZoomLimit(GameOptionsManager.Instance.Graphic_CameraZoom);
            }
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            if (_isRotating180)
            {
                _rotate180Elapsed += Time.deltaTime;
                float t = _rotate180Elapsed / _rotate180Duration;
                if (t >= 1f)
                {
                    _currentX = _rotate180Target;
                    _isRotating180 = false;
                }
                else
                {
                    // Mathf.LerpAngle kullanarak yörüngesel (dairesel) hızlı dönüş sağlıyoruz
                    _currentX = Mathf.LerpAngle(_rotate180Start, _rotate180Target, t);
                }

                // Açı normalizasyonu
                if (_currentX > 360f) _currentX -= 360f;
                else if (_currentX < 0f) _currentX += 360f;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            UpdateCameraPosition();
        }

        private void HandleInput()
        {
            if (_isRotating180) return;

            // Lag spike koruması: Eğer bu karede bir donma (stutter) yaşandıysa, 
            // birikmiş olan fare/dokunmatik delta değerlerini yoksayarak kameranın anlık zıplamasını engelle!
            if (Time.unscaledDeltaTime > 0.08f)
            {
                _dragFingerId = -1;
                _isMouseRotationBlocked = true;
                return;
            }

            // Seçenekler panelindeki kamera hassasiyeti çarpanını hesapla
            float sensMultiplier = 1f;
            if (GameOptionsManager.Instance != null)
            {
                float sensVal = GameOptionsManager.Instance.Graphic2_CameraSens;
                // 0.5f varsayılan değerini 1.0x çarpanına eşitleyen hassas Lerp eşleme
                sensMultiplier = sensVal < 0.5f 
                    ? Mathf.Lerp(0.2f, 1.0f, sensVal * 2f) 
                    : Mathf.Lerp(1.0f, 3.0f, (sensVal - 0.5f) * 2f);
            }
            float actualRotationSpeed = rotationSpeed * sensMultiplier;

            // --- Dokunmatik Giriş (Mobil) ---
            // İki parmakla yakınlaştırma/uzaklaştırma (Pinch-to-Zoom)
            if (Touch.activeTouches.Count == 2)
            {
                var touch0 = Touch.activeTouches[0];
                var touch1 = Touch.activeTouches[1];

                if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Moved ||
                    touch1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    float currentDist = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);
                    Vector2 prevPos0 = touch0.screenPosition - touch0.delta;
                    Vector2 prevPos1 = touch1.screenPosition - touch1.delta;
                    float prevDist = Vector2.Distance(prevPos0, prevPos1);

                    float delta = currentDist - prevDist;
                    // Hassasiyet: 0.015f
                    distance -= delta * 0.015f;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }
            else
            {
                // Tek parmakla kamera döndürme
                foreach (var touch in Touch.activeTouches)
                {
                    // Sadece ekranın sağ yarısındaki dokunuşlar (joystick alanıyla çakışmasın)
                    if (touch.screenPosition.x < Screen.width * 0.4f) continue;

                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        var es = UnityEngine.EventSystems.EventSystem.current;
                        bool isOverUI = es != null && es.IsPointerOverGameObject(touch.touchId);
                        if (isOverUI)
                        {
                            // Arayüz elemanına tıklandıysa bu parmağı döndürme için kullanma
                            continue;
                        }

                        if (_dragFingerId < 0)
                            _dragFingerId = touch.touchId;
                    }

                    if (touch.touchId == _dragFingerId)
                    {
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                        {
                            _currentX += touch.delta.x * actualRotationSpeed * 0.1f;
                            // Dikey kaydırma mobilde devre dışı bırakıldı (sadece ok tuşlarıyla dikey açı ayarlanır)
                            // _currentY -= touch.delta.y * actualRotationSpeed * 0.1f;
                            // _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
                        }

                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                            touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        {
                            _dragFingerId = -1;
                        }
                    }
                }
            }

            // --- Mouse Giriş (Editor/PC) ---
            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool leftPressed = mouse.leftButton.isPressed;
                if (leftPressed)
                {
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        var es = UnityEngine.EventSystems.EventSystem.current;
                        _isMouseRotationBlocked = es != null && es.IsPointerOverGameObject();
                    }

                    if (!_isMouseRotationBlocked)
                    {
                        // Sadece ekranın sağ yarısındaki tıklamalar (joystick alanıyla çakışmasın)
                        if (mouse.position.ReadValue().x >= Screen.width * 0.4f)
                        {
                            var delta = mouse.delta.ReadValue();
                            _currentX += delta.x * actualRotationSpeed * 0.05f;
                            // Dikey kaydırma devre dışı bırakıldı (sadece ok tuşlarıyla dikey açı ayarlanır)
                            // _currentY -= delta.y * actualRotationSpeed * 0.05f;
                            // _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
                        }
                    }
                }
                else
                {
                    _isMouseRotationBlocked = false;
                }
            }

            // --- Right/Camera Joystick Input ---
            if (_rightJoystick == null)
            {
                var joysticks = Object.FindObjectsByType<EntropyOnline.Input.VirtualJoystick>(FindObjectsInactive.Exclude);
                foreach (var js in joysticks)
                {
                    if (js != null && js.isCameraJoystick)
                    {
                        _rightJoystick = js;
                        break;
                    }
                }
            }

            if (_rightJoystick != null && _rightJoystick.IsActive)
            {
                float joystickInput = _rightJoystick.Direction.x;
                _currentX += joystickInput * actualRotationSpeed * 40f * Time.deltaTime;
            }

            // PC fare orta tuşu (scroll click) ile 180 derece dönüş (C++ ile birebir)
            if (mouse != null && mouse.middleButton.wasPressedThisFrame)
            {
                Rotate180();
            }

            // Scroll ile zoom
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distance -= scroll * 0.01f;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }
        }

        private void UpdateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(_currentY, _currentX, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -distance);
            Vector3 targetPos = target.position + Vector3.up * height;

            transform.position = targetPos + offset;
            transform.rotation = rotation;
        }

        public void Rotate180()
        {
            if (_isRotating180) return;

            _rotate180Start = _currentX;
            _rotate180Target = _currentX + 180f;
            _rotate180Elapsed = 0f;
            _isRotating180 = true;
        }

        public void TiltUp(float amount = 5f)
        {
            _currentY += amount;
            _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
        }

        public void TiltDown(float amount = 5f)
        {
            _currentY -= amount;
            _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);
        }

        public void ResetCamera()
        {
            _currentY = 25f;
            if (GameOptionsManager.Instance != null)
            {
                distance = GetDistanceForZoomLimit(GameOptionsManager.Instance.Graphic_CameraZoom);
            }
            else
            {
                distance = 9f;
            }
        }
    }
}
