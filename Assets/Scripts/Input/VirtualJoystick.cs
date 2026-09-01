using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using EntropyOnline.Combat;
using EntropyOnline.World;
using EntropyOnline.Core;

namespace EntropyOnline.Input
{
    /// <summary>
    /// Entropy Online — Mobil Dinamik ve Kilitlenebilir (Oto-Koşu) Sanal Joystick
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("UI References")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private RectTransform lockIndicator;

        [Header("Settings")]
        [SerializeField] private float handleRange = 1f;
        [SerializeField] private float lockDistanceRatio = 2.0f;
        [HideInInspector] public bool isCameraJoystick = false;

        private Vector2 _inputVector = Vector2.zero;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private bool _isLocked = false;
        private GameObject _forwardedTarget = null;

        /// <summary>Normalize edilmiş hareket yönü (-1 ile 1 arası).</summary>
        public Vector2 Direction => _inputVector;

        /// <summary>Joystick aktif mi? (Dokunuluyor veya kilitli)</summary>
        public bool IsActive => _isLocked || _inputVector.sqrMagnitude > 0.01f;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            lockDistanceRatio = 2.0f; // Kilit ok simgesini biraz daha yukarıya konumlandırmak için oran 2.0f yapıldı
            
            // Joystick alanını ekranın sol yarısını kaplayacak şekilde kodla ayarla
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0.5f, 1f); // Sol yarısı komple
                rect.pivot = new Vector2(0, 0);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
            }

            // Background üzerinde CanvasGroup var mı kontrol et (Fading için)
            if (background != null)
            {
                // Konumlandırma hatasını engellemek için anchor ve pivot değerlerini kodla eşitle
                background.anchorMin = new Vector2(0, 0);
                background.anchorMax = new Vector2(0, 0);
                background.pivot = new Vector2(0.5f, 0.5f);

                _canvasGroup = background.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = background.gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Kilit indikatörünü gizle
            if (lockIndicator != null)
            {
                lockIndicator.gameObject.SetActive(false);
            }

            // Varsayılan olarak görünmez yap
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            // Eğer background veya handle sprite'ları atanmamışsa dinamik olarak yuvarlak yapalım
            SetupDefaultGraphics();
        }

        public void InitializeAsCameraJoystick()
        {
            isCameraJoystick = true;
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(1f, 1f); // Right half
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
            }
            if (lockIndicator != null)
            {
                lockIndicator.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (!isCameraJoystick)
            {
                // Deactivate gameObject temporarily to prevent Awake/Start from running on clone immediately
                bool originalActive = gameObject.activeSelf;
                gameObject.SetActive(false);

                // Create the camera joystick clone dynamically (it will be inactive initially)
                GameObject rightJoystickObj = Instantiate(gameObject, transform.parent);
                rightJoystickObj.name = "RightVirtualJoystick";
                
                var rightJoystick = rightJoystickObj.GetComponent<VirtualJoystick>();
                rightJoystick.isCameraJoystick = true;

                // Re-activate both and initialize the copy's position
                gameObject.SetActive(originalActive);
                rightJoystickObj.SetActive(true);
                rightJoystick.InitializeAsCameraJoystick();
            }

            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

        private void SetupDefaultGraphics()
        {
            // Background için yuvarlak sprite
            if (background != null)
            {
                var bgImage = background.GetComponent<Image>();
                if (bgImage != null && bgImage.sprite == null)
                {
                    bgImage.sprite = CreateCircleSprite(256, new Color(1, 1, 1, 0.2f), 0.75f); // Halka şeklinde
                    bgImage.type = Image.Type.Simple;
                }
            }

            // Handle için yuvarlak sprite
            if (handle != null)
            {
                var handleImage = handle.GetComponent<Image>();
                if (handleImage != null && handleImage.sprite == null)
                {
                    handleImage.sprite = CreateCircleSprite(128, new Color(1, 1, 1, 0.6f)); // Dolu yuvarlak
                    handleImage.type = Image.Type.Simple;
                }
            }

            // Lock Indicator için kilit/oto-koşu simgesi
            if (lockIndicator != null)
            {
                var lockImage = lockIndicator.GetComponent<Image>();
                if (lockImage != null && lockImage.sprite == null)
                {
                    lockImage.sprite = CreateLockSprite(64, Color.white);
                    lockImage.type = Image.Type.Simple;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _forwardedTarget = null;

            // 1. UI Raycast Kontrolü (Bizden başka bir UI elemanı tıklandı mı?)
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var r in results)
            {
                if (r.gameObject != null && r.gameObject.GetComponentInParent<VirtualJoystick>() == null)
                {
                    _forwardedTarget = r.gameObject;
                    break;
                }
            }

            if (_forwardedTarget != null)
            {
                ExecuteEvents.Execute(_forwardedTarget, eventData, ExecuteEvents.pointerDownHandler);
                return;
            }



            // Eğer zaten kilitliyse, tekrar dokunulduğunda kilidi aç
            if (_isLocked)
            {
                // Ekstra kontrol: eventData null değilse ve kilitliyken dokunulduysa
                UnlockJoystick();
            }


            // Dokunulan konumu joystick merkezi yap (Dynamic / Floating)
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform as RectTransform, 
                eventData.position, 
                null, // ScreenSpaceOverlay için kamera her zaman null olmalıdır
                out localPoint
            );

            if (background != null)
            {
                background.anchoredPosition = localPoint;
            }
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            // Joystick'i görünür yap
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }

            // Kilit indikatörünü joystick'in hemen üzerinde göster
            if (lockIndicator != null && background != null)
            {
                if (isCameraJoystick)
                {
                    lockIndicator.gameObject.SetActive(false);
                }
                else
                {
                    lockIndicator.gameObject.SetActive(true);
                    float bgRadius = background.sizeDelta.y * 0.5f;
                    lockIndicator.anchoredPosition = new Vector2(0, bgRadius * lockDistanceRatio);
                    var lockImage = lockIndicator.GetComponent<Image>();
                    if (lockImage != null)
                    {
                        lockImage.color = new Color(1, 1, 1, 0.4f); // Yarı saydam
                    }
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_forwardedTarget != null)
            {
                ExecuteEvents.Execute(_forwardedTarget, eventData, ExecuteEvents.dragHandler);
                return;
            }

            if (background == null || handle == null) return;

            Vector2 position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, null, out position);

            float bgSizeX = background.sizeDelta.x;
            float bgSizeY = background.sizeDelta.y;

            // Normalize edilmiş input vektörü
            position.x = position.x / (bgSizeX * 0.5f);
            position.y = position.y / (bgSizeY * 0.5f);

            if (isCameraJoystick)
            {
                _inputVector = new Vector2(position.x, 0f);
            }
            else
            {
                _inputVector = new Vector2(position.x, position.y);
            }
            
            // Maksimum sürükleme mesafesi kontrolü
            float magnitude = _inputVector.magnitude;
            if (magnitude > 1f)
            {
                _inputVector = _inputVector.normalized;
            }

            // Handle'ın konumunu ayarla
            handle.anchoredPosition = new Vector2(
                _inputVector.x * (bgSizeX * 0.5f) * handleRange,
                _inputVector.y * (bgSizeY * 0.5f) * handleRange
            );

            // Kilit mesafesi kontrolü (Yukarı doğru sürükleme kontrolü)
            if (!isCameraJoystick && lockIndicator != null)
            {
                float handleDistance = Vector2.Distance(handle.anchoredPosition, Vector2.zero);
                float maxHandleDistance = (bgSizeY * 0.5f) * handleRange;

                // Kilit alanı yakınına gelindi mi? (yukarı yönde ve maksimuma yakın)
                bool isNearLock = _inputVector.y > 0.7f && (handleDistance / maxHandleDistance) > 0.8f;
                var lockImage = lockIndicator.GetComponent<Image>();
                if (lockImage != null)
                {
                    if (isNearLock)
                    {
                        lockImage.color = Color.green; // Kilitlenmeye hazır olduğunu belirt
                    }
                    else
                    {
                        lockImage.color = new Color(1, 1, 1, 0.4f);
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_forwardedTarget != null)
            {
                ExecuteEvents.Execute(_forwardedTarget, eventData, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(_forwardedTarget, eventData, ExecuteEvents.pointerClickHandler);
                _forwardedTarget = null;
                return;
            }

            if (background == null || handle == null) return;

            // Yukarı doğru yeterince sürüklenip bırakıldı mı?
            float bgSizeY = background.sizeDelta.y;
            float handleDistance = Vector2.Distance(handle.anchoredPosition, Vector2.zero);
            float maxHandleDistance = (bgSizeY * 0.5f) * handleRange;

            if (isCameraJoystick)
            {
                ResetJoystick();
                return;
            }

            bool shouldLock = _inputVector.y > 0.7f && (handleDistance / maxHandleDistance) > 0.8f;

            if (shouldLock)
            {
                LockJoystick();
            }
            else
            {
                ResetJoystick();
            }
        }

        private void LockJoystick()
        {
            _isLocked = true;
            _inputVector = Vector2.up; // İleri doğru tam güç gitmesini sağla

            // Görsel olarak kilitli olduğunu göster
            if (background != null && handle != null)
            {
                handle.anchoredPosition = new Vector2(0, (background.sizeDelta.y * 0.5f) * handleRange);
                var handleImage = handle.GetComponent<Image>();
                if (handleImage != null)
                {
                    handleImage.color = Color.green;
                }
            }

            if (lockIndicator != null)
            {
                lockIndicator.gameObject.SetActive(true);
                var lockImage = lockIndicator.GetComponent<Image>();
                if (lockImage != null)
                {
                    lockImage.color = Color.green;
                }
            }
        }

        private void UnlockJoystick()
        {
            _isLocked = false;
            ResetJoystick();
        }

        private void ResetJoystick()
        {
            _isLocked = false;
            _inputVector = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
                var handleImage = handle.GetComponent<Image>();
                if (handleImage != null)
                {
                    handleImage.color = new Color(1, 1, 1, 0.6f);
                }
            }

            if (lockIndicator != null)
            {
                lockIndicator.gameObject.SetActive(false);
            }

            // Görünmez yap
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }

        // --- Yuvarlak ve Kilit Sembolü Spriteları Üreten Metotlar ---
        private static Sprite CreateCircleSprite(int size, Color color, float innerRadiusPercent = 0f)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2.0f;
            float radius = size / 2.0f;
            float innerRadius = radius * innerRadiusPercent;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                    if (dist <= radius && dist >= innerRadius)
                    {
                        // Kenar yumuşatma (antialiasing)
                        float alpha = 1f;
                        if (dist > radius - 1.5f) alpha = (radius - dist) / 1.5f;
                        if (innerRadius > 0 && dist < innerRadius + 1.5f) alpha = Mathf.Min(alpha, (dist - innerRadius) / 1.5f);

                        tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateLockSprite(int size, Color color)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            // Tüm pikselleri şeffaf yap
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }

            // Çift yukarı ok (Oto-koşu sembolü)
            float center = size / 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dy1 = y - (center - 4f);
                    float dx = Mathf.Abs(x - center);
                    
                    // Alt ok
                    bool inArrow1 = (dy1 >= -dx - 3f && dy1 <= -dx + 3f) && (dx < size * 0.25f) && (y > center - 12f) && (y < center);
                    
                    // Üst ok
                    float dy2 = y - (center + 6f);
                    bool inArrow2 = (dy2 >= -dx - 3f && dy2 <= -dx + 3f) && (dx < size * 0.25f) && (y > center - 2f) && (y < center + 10f);

                    if (inArrow1 || inArrow2)
                    {
                        tex.SetPixel(x, y, color);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
