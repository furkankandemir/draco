using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace EntropyOnline.Import
{
    /// <summary>
    /// KO UI Renderer — UIFNode tree'yi Unity UI elementlerine dönüştürür.
    /// 
    /// KO'nun UI'ında TÜM koordinatlar ABSOLUTE (ekran piksel baz) dır.
    /// Ama Unity'de child RectTransform'lar parent'a GÖREDİR.
    /// Bu yüzden her child'ın KO absolut koordinatlarını,
    /// parent'ın KO absolut koordinatlarına göre normalize etmeliyiz.
    /// </summary>
    public static class KOUIRenderer
    {
        // KO orijinal ekran boyutu
        private const float KO_SCREEN_W = 1024f;
        private const float KO_SCREEN_H = 768f;

        // (No skip list — render all KO elements faithfully)

        // Texture cache
        private static readonly Dictionary<string, Texture2D> _textureCache = new();

        /// <summary>
        /// UIF dosyasını yükleyip Unity Canvas child'ı olarak oluşturur.
        /// </summary>
        public static GameObject LoadUI(string uifPath, Transform canvasParent)
        {
            return LoadUI(uifPath, canvasParent, null);
        }

        /// <summary>
        /// UIF dosyasını yükler — opsiyonel override root region ile.
        /// C++ SetRegion() karşılığı: root region'ı zorla değiştirmek için kullanılır.
        /// Ör: Login .uif → SetRegion(0, 0, 1024, 768) → tam ekran.
        /// </summary>
        public static GameObject LoadUI(string uifPath, Transform canvasParent,
            UIFImporter.Rect? overrideRootRegion)
        {
            string baseName = Path.GetFileNameWithoutExtension(uifPath);

            // Resources/KOPrefabs/UI/ altından prefab yükle
            var prefab = Resources.Load<GameObject>($"KOPrefabs/UI/{baseName}");
            if (prefab == null)
            {
                Debug.LogError($"[KOUI] UI prefab bulunamadı: KOPrefabs/UI/{baseName}");
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, canvasParent);
            instance.name = $"KO_UI_{baseName}";

            // C++ birebir: CN3UIString::WordWrap() — prefab'larda baked Overflow → runtime Wrap fix
            // Prefab'lar eski Overflow ayarıyla build edilmiş olabilir.
            // Region genişliği olan multiline text'ler Wrap moduna alınır.
            PostProcessTextComponents(instance);

            return instance;
        }

        /// <summary>
        /// Runtime post-process: Prefab'daki Text component'lerinin wrap modunu düzeltir.
        /// C++ birebir: CN3UIString her zaman WordWrap() çağırır (multiline default).
        /// SINGLELINE olan text'ler (buton label'ları, kısa ID'ler) Overflow kalır.
        /// </summary>
        public static void PostProcessTextComponents(GameObject root)
        {
            if (root == null) return;

            var texts = root.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
            {
                var rt = text.GetComponent<RectTransform>();
                if (rt == null) continue;

                float w = rt.sizeDelta.x;

                // Buton label'ı mı? (parent'ta veya kendisinde Button var)
                bool isButtonLabel = text.GetComponent<Button>() != null ||
                                     text.GetComponentInParent<Button>() != null;

                // C++ birebir: CN3UIString — region genişliği > 30px ve buton değilse → multiline wrap
                // Buton label'ları ve çok dar alanlar singleline kalır
                if (w > 30f && !isButtonLabel)
                {
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                }
            }
        }

        /// <summary>
        /// Tek bir UIFNode'u Unity UI elementine dönüştürür (recursive).
        /// parentKORegion: parent'ın KO absolute koordinatları — child'lar buna göre konumlanır.
        /// </summary>
        private static void RenderNodeRecursive(
            UIFImporter.UIFNode node, Transform parent,
            UIFImporter.Rect parentKORegion)
        {


            string objName = !string.IsNullOrEmpty(node.ID) ? node.ID :
                             !string.IsNullOrEmpty(node.Name) ? node.Name :
                             $"ui_{node.Type}_{node.GetHashCode():X}";

            var obj = new GameObject(objName);
            obj.transform.SetParent(parent, false);

            var rt = obj.AddComponent<RectTransform>();

            // KO absolute koordinatları → parent'a göre relative anchor
            SetRectRelativeToParent(rt, node.Region, parentKORegion);

            // Type-specific rendering
            switch (node.Type)
            {
                case UIFImporter.UIType.Image:
                    // C++ CN3UIImage::Render (satır 167-173):
                    // Animate image kendi texture'unu render ETMEZ, sadece child frame gösterir
                    bool isAnimate = (node.Style & 0x00010000) != 0 && node.Children.Count > 0;
                    if (!isAnimate)
                        CreateImage(obj, node);
                    break;
                case UIFImporter.UIType.Progress:
                    CreateProgress(obj, node);
                    break;
                case UIFImporter.UIType.String:
                    // Statik "Total Members" UIF kalıntısı düğümü oyun yüklenirken HİÇ oluşturulmasın
                    if (!string.IsNullOrEmpty(node.Text) && node.Text.Contains("Total Members") && node.ID != "Text_clan_MemberCount")
                    {
                        break;
                    }
                    CreateString(obj, node);
                    break;
                case UIFImporter.UIType.Button:
                    CreateButton(obj, node);
                    break;
                case UIFImporter.UIType.Edit:
                    CreateEdit(obj, node);
                    break;
                case UIFImporter.UIType.Area:
                    // Open-KO birebir: CN3UIArea — m_eAreaType sakla
                    var areaComp = obj.AddComponent<KOUIArea>();
                    areaComp.AreaType = node.AreaType;
                    obj.AddComponent<CanvasRenderer>();
                    break;
            }

            // Children render (recursive)
            // C++ CN3UIButton::Render (N3UIButton.cpp:99-111) birebir:
            //   Image child'lar (m_ImageRef[] — button state sprites) CreateButton tarafından handle ediliyor
            //   NON-Image child'lar (String, Base vb.) normal render edilir
            // C++ CN3UIProgress::Render birebir: child image'lar CreateProgress tarafından handle
            // Animate Image child'ları KOAnimatedImage tarafından yönetiliyor
            bool childrenHandledByType =
                node.Type == UIFImporter.UIType.Progress;

            // C++ CN3UIButton::Render satır 99-111 birebir:
            // for (children) { if (child == m_ImageRef[i]) skip; else child->Render(); }
            // → Image child'lar skip, diğerleri render
            //
            // C++ CN3UIImage::Render satır 189 birebir:
            // CN3UIBase::Render() → Image'ın child'larını (String vb.) render eder
            // → reserved=0 (BS_NORMAL) Image'ın String child'ları = buton text label'ları
            if (node.Type == UIFImporter.UIType.Button)
            {
                UIFImporter.Rect btnChildRegion = node.Region;
                if (btnChildRegion.Width <= 0 || btnChildRegion.Height <= 0)
                    btnChildRegion = parentKORegion;

                foreach (var child in node.Children)
                {
                    if (child.Type == UIFImporter.UIType.Image)
                    {
                        // C++ birebir: CN3UIImage::Render satır 189 → CN3UIBase::Render()
                        // reserved=0 (BS_NORMAL) Image'ın non-Image child'ları render edilir
                        // Bu child'lar genellikle String (buton yazısı) içerir
                        if (child.Reserved == 0)
                        {
                            foreach (var imgChild in child.Children)
                            {
                                if (imgChild.Type == UIFImporter.UIType.Image)
                                    continue; // Image child'ın Image child'ı skip
                                
                                RenderNodeRecursive(imgChild, obj.transform, btnChildRegion);
                                
                                // C++ birebir: CN3UIImage::SetRegion satır 132-133
                                // Image'ın child String'i Image'ın region'ını kaplar
                                if (imgChild.Type == UIFImporter.UIType.String)
                                {
                                    var lastChild = obj.transform.GetChild(obj.transform.childCount - 1);
                                    var childRT = lastChild.GetComponent<RectTransform>();
                                    var childText = lastChild.GetComponent<Text>();
                                    if (childRT != null)
                                    {
                                        childRT.anchorMin = Vector2.zero;
                                        childRT.anchorMax = Vector2.one;
                                        childRT.offsetMin = Vector2.zero;
                                        childRT.offsetMax = Vector2.zero;
                                        childRT.pivot = new Vector2(0.5f, 0.5f);
                                        childRT.sizeDelta = Vector2.zero;
                                        childRT.anchoredPosition = Vector2.zero;
                                    }
                                    if (childText != null)
                                    {
                                        childText.alignment = TextAnchor.MiddleCenter;
                                        childText.raycastTarget = false;
                                    }
                                }
                            }
                        }
                        // reserved!=0 Image'lar (BS_DOWN, BS_ON, BS_DISABLE) tam skip
                        continue;
                    }
                    
                    // Non-Image child'lar (String vb.) → render (butonun doğrudan String child'ı)
                    RenderNodeRecursive(child, obj.transform, btnChildRegion);
                    
                    if (child.Type == UIFImporter.UIType.String)
                    {
                        var lastChild = obj.transform.GetChild(obj.transform.childCount - 1);
                        var childRT = lastChild.GetComponent<RectTransform>();
                        var childText = lastChild.GetComponent<Text>();
                        if (childRT != null)
                        {
                            childRT.anchorMin = Vector2.zero;
                            childRT.anchorMax = Vector2.one;
                            childRT.offsetMin = Vector2.zero;
                            childRT.offsetMax = Vector2.zero;
                            childRT.pivot = new Vector2(0.5f, 0.5f);
                            childRT.sizeDelta = Vector2.zero;
                            childRT.anchoredPosition = Vector2.zero;
                        }
                        if (childText != null)
                        {
                            childText.alignment = TextAnchor.MiddleCenter;
                            childText.raycastTarget = false;
                        }
                    }
                }
                childrenHandledByType = true;
            }

            // C++ UISTYLE_IMAGE_ANIMATE (0x00010000) — N3UIImage.cpp:150-191
            bool isAnimateImage = node.Type == UIFImporter.UIType.Image &&
                                  (node.Style & 0x00010000) != 0 &&
                                  node.Children.Count > 0;

            if (isAnimateImage)
            {
                // Child Image'ları frame olarak oluştur
                var frames = new System.Collections.Generic.List<RawImage>();
                UIFImporter.Rect childParentRegion = node.Region;
                if (childParentRegion.Width <= 0 || childParentRegion.Height <= 0)
                    childParentRegion = parentKORegion;

                foreach (var child in node.Children)
                {
                    if (child.Type == UIFImporter.UIType.Image)
                    {
                        RenderNodeRecursive(child, obj.transform, childParentRegion);
                        // Son oluşturulan child'ın RawImage'ını al
                        var lastChild = obj.transform.GetChild(obj.transform.childCount - 1);
                        var ri = lastChild.GetComponent<RawImage>();
                        if (ri != null)
                        {
                            ri.enabled = false; // Başta gizli
                            frames.Add(ri);
                        }
                    }
                }

                if (frames.Count > 0)
                {
                    var anim = obj.AddComponent<KOAnimatedImage>();
                    anim.Frames = frames.ToArray();
                    anim.FrameRate = node.AnimFrame; // m_fAnimFrame
                    frames[0].enabled = true; // İlk frame görünür
                }
                childrenHandledByType = true; // Children handled
            }

            if (!childrenHandledByType)
            {
                UIFImporter.Rect childParentRegion = node.Region;
                if (childParentRegion.Width <= 0 || childParentRegion.Height <= 0)
                    childParentRegion = parentKORegion;

                foreach (var child in node.Children)
                {
                    RenderNodeRecursive(child, obj.transform, childParentRegion);
                }
            }
        }

        #region Element Creators

        /// <summary>
        /// KO Image → Unity RawImage with UV cropping
        /// </summary>
        private static void CreateImage(GameObject obj, UIFImporter.UIFNode node)
        {
            if (string.IsNullOrEmpty(node.TextureFileName)) return;

            var tex = LoadKOTexture(node.TextureFileName);
            if (tex == null) return;

            var rawImg = obj.AddComponent<RawImage>();
            rawImg.texture = tex;

            // UV rect — C++ CN3UIImage::SetVB birebir (satır 105-112)
            // C++: vertex UV = (left, top) → (right, bottom) — DirectX UV
            // DxtTextureImporter flipY=true ile yüklediği için texture zaten doğru yönde
            // Unity RawImage.uvRect = (x, y, width, height) — y=top
            if (node.UVRect.Right > 0 || node.UVRect.Bottom > 0)
            {
                rawImg.uvRect = new UnityEngine.Rect(
                    node.UVRect.Left,
                    1f - node.UVRect.Bottom,
                    node.UVRect.Right - node.UVRect.Left,
                    node.UVRect.Bottom - node.UVRect.Top
                );
            }

            rawImg.raycastTarget = false;
        }

        /// <summary>
        /// KO Progress → İki RawImage (background + foreground fill)
        /// HP/MP/EXP barları bu şekilde çalışır.
        /// </summary>
        private static void CreateProgress(GameObject obj, UIFImporter.UIFNode node)
        {
            // Progress child'larından background ve foreground image'ı bul
            // C++: reserved=0 → IMAGETYPE_BKGND, reserved=1 → IMAGETYPE_FRGND
            UIFImporter.UIFNode bkgndNode = null;
            UIFImporter.UIFNode frgndNode = null;

            foreach (var child in node.Children)
            {
                if (child.Type == UIFImporter.UIType.Image)
                {
                    if (child.Reserved == 0) // IMAGETYPE_BKGND
                        bkgndNode = child;
                    else if (child.Reserved == 1) // IMAGETYPE_FRGND
                        frgndNode = child;
                }
            }

            // Background
            if (bkgndNode != null && !string.IsNullOrEmpty(bkgndNode.TextureFileName))
            {
                var bgObj = new GameObject("bkgnd");
                bgObj.transform.SetParent(obj.transform, false);
                var bgRT = bgObj.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero;
                bgRT.offsetMax = Vector2.zero;

                var bgImg = bgObj.AddComponent<RawImage>();
                bgImg.texture = LoadKOTexture(bkgndNode.TextureFileName);
                if (bkgndNode.UVRect.Right > 0)
                {
                    bgImg.uvRect = new UnityEngine.Rect(
                        bkgndNode.UVRect.Left,
                        1f - bkgndNode.UVRect.Bottom,
                        bkgndNode.UVRect.Right - bkgndNode.UVRect.Left,
                        bkgndNode.UVRect.Bottom - bkgndNode.UVRect.Top
                    );
                }
                bgImg.raycastTarget = false;
            }

            // Foreground (fill bar)
            if (frgndNode != null && !string.IsNullOrEmpty(frgndNode.TextureFileName))
            {
                var fgObj = new GameObject("frgnd_fill");
                fgObj.transform.SetParent(obj.transform, false);
                var fgRT = fgObj.AddComponent<RectTransform>();
                fgRT.anchorMin = Vector2.zero;
                fgRT.anchorMax = Vector2.one;
                fgRT.offsetMin = Vector2.zero;
                fgRT.offsetMax = Vector2.zero;

                var fgImg = fgObj.AddComponent<RawImage>();
                fgImg.texture = LoadKOTexture(frgndNode.TextureFileName);
                if (frgndNode.UVRect.Right > 0)
                {
                    fgImg.uvRect = new UnityEngine.Rect(
                        frgndNode.UVRect.Left,
                        1f - frgndNode.UVRect.Bottom,
                        frgndNode.UVRect.Right - frgndNode.UVRect.Left,
                        frgndNode.UVRect.Bottom - frgndNode.UVRect.Top
                    );
                }
                fgImg.raycastTarget = false;

                // Fill amount component — GameHUD bunu kontrol edecek
                var fillCtrl = fgObj.AddComponent<KOProgressFill>();
                fillCtrl.FillImage = fgImg;
                fillCtrl.OriginalUV = fgImg.uvRect;

                // Progress direction (C++ style flags)
                bool isVertical = (node.Style & 0x40000000) != 0 || (node.Style & 0x80000000) != 0;
                fillCtrl.IsVertical = isVertical;
                fillCtrl.IsReverse = (node.Style & 0x20000000) != 0 || (node.Style & 0x80000000) != 0;
            }
        }

        /// <summary>
        /// KO String → Unity Text
        /// C++ CN3UIString::Load + CN3UIString::Render birebir.
        /// </summary>
        private static void CreateString(GameObject obj, UIFImporter.UIFNode node)
        {
            var text = obj.AddComponent<Text>();
            text.text = node.Text ?? "";
            text.fontSize = Mathf.Max(node.FontSize, 12);

            // C++ CN3UIString — font: built-in font kullan (platform-safe)
            // Unity 6000: LegacyRuntime.ttf → eski sürümlerde Arial.ttf
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", text.fontSize);
            if (text.font == null)
            {
                // Son çare: sistemdeki ilk kullanılabilir font
                string[] fallbackFonts = Font.GetOSInstalledFontNames();
                if (fallbackFonts != null && fallbackFonts.Length > 0)
                    text.font = Font.CreateDynamicFontFromOSFont(fallbackFonts[0], text.fontSize);
            }

            // Color from D3DCOLOR (ARGB format)
            // C++ CN3UIString birebir: rengi UIF'den olduğu gibi kullan
            // KO gölge efektini ayrı siyah String node ile yapar (ör: ExitMenu)
            if (node.FontColor != 0)
            {
                byte a = (byte)((node.FontColor >> 24) & 0xFF);
                byte r = (byte)((node.FontColor >> 16) & 0xFF);
                byte g = (byte)((node.FontColor >> 8) & 0xFF);
                byte b = (byte)(node.FontColor & 0xFF);
                text.color = new Color32(r, g, b, a == 0 ? (byte)255 : a);
            }
            else
            {
                text.color = Color.white;
            }

            // C++ birebir: CN3UIString::WordWrap() — N3UIString.cpp:130-345
            // UISTYLE_STRING_SINGLELINE (0x00100000) → tek satır, taşan kısım kesilir
            // Yoksa (MULTILINE default) → region genişliğine göre word wrap yapılır
            bool isSingleLine = (node.Style & 0x00100000) != 0;
            if (isSingleLine)
            {
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            else
            {
                // C++ birebir: multiline — region içinde wrap
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }

            // C++ birebir: alignment flags
            // UISTYLE_STRING_ALIGNCENTER (0x00800000) / ALIGNRIGHT (0x00400000) / ALIGNLEFT (0x00200000)
            // UISTYLE_STRING_ALIGNVCENTER (0x04000000) / ALIGNBOTTOM (0x02000000) / ALIGNTOP (0x01000000)
            bool alignCenter  = (node.Style & 0x00800000) != 0;
            bool alignRight   = (node.Style & 0x00400000) != 0;
            bool alignVCenter = (node.Style & 0x04000000) != 0;
            bool alignBottom  = (node.Style & 0x02000000) != 0;

            TextAnchor anchor = TextAnchor.UpperLeft; // default
            if (alignVCenter)
            {
                if (alignCenter) anchor = TextAnchor.MiddleCenter;
                else if (alignRight) anchor = TextAnchor.MiddleRight;
                else anchor = TextAnchor.MiddleLeft;
            }
            else if (alignBottom)
            {
                if (alignCenter) anchor = TextAnchor.LowerCenter;
                else if (alignRight) anchor = TextAnchor.LowerRight;
                else anchor = TextAnchor.LowerLeft;
            }
            else // top
            {
                if (alignCenter) anchor = TextAnchor.UpperCenter;
                else if (alignRight) anchor = TextAnchor.UpperRight;
                else anchor = TextAnchor.UpperLeft;
            }
            text.alignment = anchor;
            text.raycastTarget = false;

            // C++ CN3UIString: her element kendi region (piksel) boyutuna sahiptir.
            // Unity'de anchor-stretch layout'ta Text componenti bazen boyutsuz kalabilir
            // (parent layout pass'ından önce sizeDelta=0). KO region'dan explicit boyut ver.
            var rt = obj.GetComponent<RectTransform>();
            if (rt != null && node.Region.Width > 0 && node.Region.Height > 0)
            {
                // Anchor-stretch → explicit boyut dönüşümü
                // Mevcut anchor konumunun sol-üst köşesini pivot noktası yap
                float anchorX = rt.anchorMin.x;
                float anchorY = rt.anchorMax.y; // Unity Y flip: top = anchorMax.y
                rt.anchorMin = new Vector2(anchorX, anchorY);
                rt.anchorMax = new Vector2(anchorX, anchorY);
                rt.pivot = new Vector2(0, 1); // top-left (KO convention)
                rt.sizeDelta = new Vector2(node.Region.Width, node.Region.Height);
                rt.anchoredPosition = Vector2.zero;
            }

            // C++ birebir: KO gölge efektini UIF'de ayrı siyah String node ile yapar
            // Unity Shadow component eklenmez (uydurma idi)
        }

        /// <summary>
        /// KO Button → Unity Button with image states.
        /// Butonun child Image'ları reserved değerine göre normal/down/on/disable state'leri temsil eder.
        /// Sadece reserved=0 (normal state) gösterilir.
        /// </summary>
        private static void CreateButton(GameObject obj, UIFImporter.UIFNode node)
        {
            // C++ CN3UIButton::Load satır 292-300 birebir:
            // Image child'lar reserved değerine göre m_ImageRef[BS_NORMAL/DOWN/ON/DISABLE] olur
            // CN3UIButton::Render satır 73-75: normal state'de m_ImageRef[BS_NORMAL] render edilir
            UIFImporter.UIFNode imgNode = null;
            int imageChildCount = 0;
            foreach (var child in node.Children)
            {
                if (child.Type == UIFImporter.UIType.Image)
                {
                    imageChildCount++;
                    if (!string.IsNullOrEmpty(child.TextureFileName) && child.Reserved == 0)
                    {
                        imgNode = child;
                    }
                }
            }



            Graphic targetGraphic = null;

            if (imgNode != null)
            {
                var tex = LoadKOTexture(imgNode.TextureFileName);

                if (tex != null)
                {
                    var rawImg = obj.AddComponent<RawImage>();
                    rawImg.texture = tex;
                    rawImg.raycastTarget = true;
                    if (imgNode.UVRect.Right > 0)
                    {
                        rawImg.uvRect = new UnityEngine.Rect(
                            imgNode.UVRect.Left,
                            1f - imgNode.UVRect.Bottom,
                            imgNode.UVRect.Right - imgNode.UVRect.Left,
                            imgNode.UVRect.Bottom - imgNode.UVRect.Top
                        );
                    }
                    targetGraphic = rawImg;
                }
            }

            // C++ CN3UIButton::MouseProc satır 159: PtInRect(&m_rcClick, ptCur)
            // Button tıklanabilirlik için en az bir Graphic gerekli
            if (targetGraphic == null)
            {

                var fallbackImg = obj.AddComponent<Image>();
                fallbackImg.color = new Color(0, 0, 0, 0);
                fallbackImg.raycastTarget = true;
                targetGraphic = fallbackImg;
            }

            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = targetGraphic;
            btn.transition = Selectable.Transition.ColorTint;
        }

        /// <summary>
        /// KO Edit → Unity InputField
        /// CN3UIEdit: text input box with background
        /// </summary>
        private static void CreateEdit(GameObject obj, UIFImporter.UIFNode node)
        {
            // C++ birebir: CN3UIEdit inherits CN3UIStatic
            // Edit element'in arka planı UIF Image child'ından gelir (recursive render)
            // String child'ı UIF'de tanımlı (text input alanı)
            // Sadece InputField component'i ekle ve mevcut child'ları kullan

            // UIF Image child arka planı raycast target olarak kullanılır
            // Eğer UIF'de Image child yoksa, şeffaf Image ekle (raycast için)
            var existingImg = obj.GetComponent<Image>();
            if (existingImg == null)
            {
                var img = obj.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0); // Tamamen şeffaf — sadece raycast
            }

            // C++ birebir: CN3UIEdit mevcut String child'ını text olarak kullanır
            // UIF'deki String child recursive render ile zaten oluşturulmuş olacak
            // Şimdilik boş Text oluştur — BindCountableEdit bunu bağlayacak
            var textObj = new GameObject("EditText");
            textObj.transform.SetParent(obj.transform, false);
            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            text.fontSize = node.FontSize > 0 ? node.FontSize : 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(2, 0);
            textRt.offsetMax = new Vector2(-2, 0);

            // InputField component
            var inputField = obj.AddComponent<InputField>();
            inputField.textComponent = text;
            inputField.characterLimit = 20;

            // C++ birebir: CN3UIEdit click sound, typing sound — UIF'den gelir (skip)
        }

        #endregion

        #region Coordinate Mapping

        /// <summary>
        /// KO absolute region → parent-relative Unity anchors.
        /// 
        /// KO'da tüm koordinatlar ABSOLUTE ekran pikselidir.
        /// Unity'de child RectTransform parent'a göredir.
        /// 
        /// Formül:
        ///   anchorMin.x = (child.left - parent.left) / parent.width
        ///   anchorMax.x = (child.right - parent.left) / parent.width
        ///   anchorMin.y = 1 - (child.bottom - parent.top) / parent.height   (Y flip)
        ///   anchorMax.y = 1 - (child.top - parent.top) / parent.height       (Y flip)
        /// </summary>
        private static void SetRectRelativeToParent(RectTransform rt,
            UIFImporter.Rect childRegion, UIFImporter.Rect parentRegion)
        {
            float pw = parentRegion.Width;
            float ph = parentRegion.Height;

            // Parent region geçersizse, KO ekran boyutunu kullan
            if (pw <= 0) pw = KO_SCREEN_W;
            if (ph <= 0) ph = KO_SCREEN_H;

            float left = parentRegion.Left;
            float top = parentRegion.Top;

            float relLeft = (childRegion.Left - left) / pw;
            float relRight = (childRegion.Right - left) / pw;
            float relTop = (childRegion.Top - top) / ph;
            float relBottom = (childRegion.Bottom - top) / ph;

            // Clamp
            relLeft = Mathf.Clamp01(relLeft);
            relRight = Mathf.Clamp01(relRight);
            relTop = Mathf.Clamp01(relTop);
            relBottom = Mathf.Clamp01(relBottom);

            // Unity: Y flip (KO top=0 → Unity top=1)
            rt.anchorMin = new Vector2(relLeft, 1f - relBottom);
            rt.anchorMax = new Vector2(relRight, 1f - relTop);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        #endregion

        #region Texture Loading

        /// <summary>
        /// KO texture dosya yolundan Unity Texture2D yükler.
        /// Cache kullanır — aynı dosya tekrar yüklenmez.
        /// </summary>
        private static Texture2D LoadKOTexture(string koTexPath)
        {
            if (string.IsNullOrEmpty(koTexPath)) return null;

            // Cache check
            if (_textureCache.TryGetValue(koTexPath, out var cached))
                return cached;

            string normalizedPath = koTexPath.Replace('\\', '/').Replace("//", "/");
            string baseName = Path.GetFileNameWithoutExtension(normalizedPath);

            // Resources/KOTextures/ altında ara
            string[] texDirs = { "UI", "UI_US", "DTex", "Chr", "Item", "Misc", "Object" };
            foreach (var dir in texDirs)
            {
                var tex = Resources.Load<Texture2D>($"KOTextures/{dir}/{baseName}");
                if (tex != null)
                {
                    _textureCache[koTexPath] = tex;
                    return tex;
                }
            }

            return null;
        }

        /// <summary>Texture cache'i temizler.</summary>
        public static void ClearCache()
        {
            foreach (var tex in _textureCache.Values)
            {
                if (tex != null)
                    UnityEngine.Object.Destroy(tex);
            }
            _textureCache.Clear();
        }

        #endregion

        #region Child Lookup — C++ GetChildByID<T> karşılığı

        /// <summary>
        /// Recursive olarak child GameObject'i ID (isim) ile bulur.
        /// C++ karşılığı: CN3UIBase::GetChildByID
        /// </summary>
        public static Transform FindChildByID(Transform root, string id)
        {
            if (root == null || string.IsNullOrEmpty(id)) return null;

            // Doğrudan çocuklarda ara
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == id) return child;
            }

            // Recursive derin arama
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildByID(root.GetChild(i), id);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// ID ile Text component'i bulur.
        /// C++ karşılığı: GetChildByID&lt;CN3UIString&gt;("id")
        /// </summary>
        public static Text FindChildText(Transform root, string id)
        {
            var tr = FindChildByID(root, id);
            return tr != null ? tr.GetComponent<Text>() : null;
        }

        /// <summary>
        /// ID ile Button component'i bulur.
        /// C++ karşılığı: GetChildByID&lt;CN3UIButton&gt;("id")
        /// </summary>
        public static Button FindChildButton(Transform root, string id)
        {
            var tr = FindChildByID(root, id);
            return tr != null ? tr.GetComponent<Button>() : null;
        }

        /// <summary>
        /// ID ile KOProgressFill component'i bulur.
        /// C++ karşılığı: GetChildByID&lt;CN3UIProgress&gt;("id")
        /// </summary>
        public static KOProgressFill FindChildProgress(Transform root, string id)
        {
            var tr = FindChildByID(root, id);
            if (tr == null) return null;

            // Progress node'un child'ında frgnd_fill objesi var, orada KOProgressFill bulunur
            var fill = tr.GetComponentInChildren<KOProgressFill>(true);
            return fill;
        }

        /// <summary>
        /// Open-KO birebir: CN3UIWndBase::GetChildAreaByiOrder (N3UIWndBase.cpp)
        /// Belirli area type'daki i. area'nın RectTransform'ını döndürür.
        /// eUI_AREA_TYPE değerleri (N3UIArea.h:13-29):
        ///   0=NONE, 1=SLOT, 2=INV, 3=TRADE_NPC, 4=PER_TRADE_MY,
        ///   5=PER_TRADE_OTHER, 6=DROP_ITEM, 7=SKILL_TREE, 8=SKILL_HOTKEY,
        ///   9=REPAIR_INV, 10=REPAIR_NPC, 11=TRADE_MY, 12=PER_TRADE_INV
        /// </summary>
        public static RectTransform FindChildAreaByiOrder(Transform root, int areaType, int iOrder)
        {
            if (root == null) return null;

            // C++ birebir: N3UIWndBase.cpp:70-80 — GetChildAreaByiOrder
            // string szID = to_string(iOrder);
            // for (pChild : m_Children)  ← sadece doğrudan çocuklar
            //     if (pChild->UIType() == UI_TYPE_AREA
            //         && pChild->m_eAreaType == eUAT
            //         && pChild->GetID() == szID)
            string szID = iOrder.ToString();
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var area = child.GetComponent<KOUIArea>();
                if (area != null && area.AreaType == areaType && child.name == szID)
                    return child.GetComponent<RectTransform>();
            }

            return null;
        }

        /// <summary>
        /// FindChildButton — GameObject overload.
        /// </summary>
        public static Button FindChildButton(GameObject root, string id)
        {
            return root != null ? FindChildButton(root.transform, id) : null;
        }

        /// <summary>
        /// ID ile child Text component'inin text'ini ayarlar.
        /// Open-KO birebir: CN3UIString::SetString() — UIMessageBox.cpp:61-64
        /// </summary>
        public static void SetChildText(GameObject root, string id, string text)
        {
            if (root == null) return;
            var t = FindChildText(root.transform, id);
            if (t != null)
            {
                t.text = text;
                // C++ birebir: SetString → WordWrap()
                // Region genişliği varsa wrap modu uygula
                var rt = t.GetComponent<RectTransform>();
                if (rt != null && rt.sizeDelta.x > 10f)
                {
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
                }
            }
        }

        #endregion
    }
}
