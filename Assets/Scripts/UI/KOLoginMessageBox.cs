using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Shared Message Box Helper specifically for the LoginScene (Login, Character Select, Character Create).
    /// Creates a pixel-perfect, centered dialog box procedurally in C# code with zero prefab dependencies.
    /// Matches the exact design of the gameplay/disconnect dialog box.
    /// </summary>
    public static class KOLoginMessageBox
    {
        private static readonly Color ColorBorder = new Color(0.36f, 0.29f, 0.13f, 0.9f);
        private static readonly Color ColorTextLight = new Color(0.9f, 0.85f, 0.75f, 1f);

        public static GameObject Show(Canvas canvas, string message, Action onOk = null)
        {
            if (canvas == null)
            {
                Debug.LogError("[KOLoginMessageBox] Cannot show messagebox: Canvas is null!");
                return null;
            }

            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // 1. Root Dialog Panel (Centered, 360x160)
            GameObject dialogObj = new GameObject("LoginMessageBox_Ok", typeof(RectTransform));
            dialogObj.transform.SetParent(canvas.transform, false);

            var rt = dialogObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 160f);
            rt.anchoredPosition3D = Vector3.zero;
            rt.localScale = Vector3.one;

            // Blocker plane behind the dialog box to intercept other clicks
            var blockerImg = dialogObj.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.6f); // Dark translucent background overlay
            rt.sizeDelta = new Vector2(2000f, 2000f); // Make the block plane full screen

            // 2. Physical Dialog Box Frame (Centered inside the blocker)
            GameObject frameObj = new GameObject("Frame", typeof(RectTransform));
            frameObj.transform.SetParent(dialogObj.transform, false);
            var frameRt = frameObj.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 0.5f);
            frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(360f, 160f);
            frameRt.anchoredPosition3D = Vector3.zero;

            var bgImg = frameObj.AddComponent<Image>();
            bgImg.sprite = CreatePanelBgSprite("dialog_bg", 360, 160,
                new Color(0.12f, 0.10f, 0.08f, 0.98f),
                new Color(0.04f, 0.04f, 0.04f, 0.98f),
                ColorBorder,
                2);
            bgImg.color = Color.white;

            // 3. Message Text (Upper half of the dialog)
            GameObject txtObj = new GameObject("MessageText", typeof(RectTransform));
            txtObj.transform.SetParent(frameObj.transform, false);
            var txtComp = txtObj.AddComponent<TextMeshProUGUI>();
            txtComp.font = fontAsset;
            txtComp.text = message;
            txtComp.fontSize = 14;
            txtComp.alignment = TextAlignmentOptions.Center;
            txtComp.color = ColorTextLight;
            txtComp.fontStyle = FontStyles.Bold;

            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.5f, 1f);
            txtRt.anchorMax = new Vector2(0.5f, 1f);
            txtRt.pivot = new Vector2(0.5f, 1f);
            txtRt.sizeDelta = new Vector2(320f, 75f);
            txtRt.anchoredPosition = new Vector2(0f, -25f);

            // 4. OK Button (Centered bottom)
            GameObject okBtnObj = new GameObject("Btn_OK", typeof(RectTransform));
            okBtnObj.transform.SetParent(frameObj.transform, false);
            var okBtnRt = okBtnObj.GetComponent<RectTransform>();
            okBtnRt.anchorMin = new Vector2(0.5f, 0f);
            okBtnRt.anchorMax = new Vector2(0.5f, 0f);
            okBtnRt.pivot = new Vector2(0.5f, 0.5f);
            okBtnRt.sizeDelta = new Vector2(104f, 28f);
            okBtnRt.anchoredPosition = new Vector2(0f, 35f);

            var okImg = okBtnObj.AddComponent<Image>();
            okImg.sprite = CreateHexagonSprite("dialog_btn_bg", 104, 28,
                new Color(0.96f, 0.80f, 0.22f, 0.98f),
                new Color(0.68f, 0.48f, 0.08f, 0.98f),
                new Color(0.85f, 0.65f, 0.15f, 0.98f),
                new Color(1.00f, 0.95f, 0.72f, 0.98f));
            okImg.color = Color.white;

            var okTextObj = new GameObject("Text", typeof(RectTransform));
            okTextObj.transform.SetParent(okBtnObj.transform, false);
            var okTxtComp = okTextObj.AddComponent<TextMeshProUGUI>();
            okTxtComp.font = fontAsset;
            okTxtComp.text = "OK";
            okTxtComp.fontSize = 13;
            okTxtComp.fontStyle = FontStyles.Bold;
            okTxtComp.color = Color.black;
            okTxtComp.alignment = TextAlignmentOptions.Center;

            var okTxtRt = okTextObj.GetComponent<RectTransform>();
            okTxtRt.anchorMin = Vector2.zero;
            okTxtRt.anchorMax = Vector2.one;
            okTxtRt.offsetMin = Vector2.zero;
            okTxtRt.offsetMax = Vector2.zero;

            var btnComp = okBtnObj.AddComponent<Button>();
            btnComp.onClick.AddListener(() => {
                UnityEngine.Object.Destroy(dialogObj);
                onOk?.Invoke();
            });

            return dialogObj;
        }

        public static GameObject ShowYesNo(Canvas canvas, string message, Action onYes, Action onNo = null)
        {
            if (canvas == null)
            {
                Debug.LogError("[KOLoginMessageBox] Cannot show messagebox: Canvas is null!");
                return null;
            }

            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // 1. Root Dialog Panel (Centered, 360x160)
            GameObject dialogObj = new GameObject("LoginMessageBox_YesNo", typeof(RectTransform));
            dialogObj.transform.SetParent(canvas.transform, false);

            var rt = dialogObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2000f, 2000f); // Blocker plane size
            rt.anchoredPosition3D = Vector3.zero;
            rt.localScale = Vector3.one;

            var blockerImg = dialogObj.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.6f);

            // 2. Physical Dialog Box Frame (Centered inside the blocker)
            GameObject frameObj = new GameObject("Frame", typeof(RectTransform));
            frameObj.transform.SetParent(dialogObj.transform, false);
            var frameRt = frameObj.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 0.5f);
            frameRt.anchorMax = new Vector2(0.5f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(360f, 160f);
            frameRt.anchoredPosition3D = Vector3.zero;

            var bgImg = frameObj.AddComponent<Image>();
            bgImg.sprite = CreatePanelBgSprite("dialog_bg", 360, 160,
                new Color(0.12f, 0.10f, 0.08f, 0.98f),
                new Color(0.04f, 0.04f, 0.04f, 0.98f),
                ColorBorder,
                2);
            bgImg.color = Color.white;

            // 3. Message Text (Upper half of the dialog)
            GameObject txtObj = new GameObject("MessageText", typeof(RectTransform));
            txtObj.transform.SetParent(frameObj.transform, false);
            var txtComp = txtObj.AddComponent<TextMeshProUGUI>();
            txtComp.font = fontAsset;
            txtComp.text = message;
            txtComp.fontSize = 14;
            txtComp.alignment = TextAlignmentOptions.Center;
            txtComp.color = ColorTextLight;
            txtComp.fontStyle = FontStyles.Bold;

            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0.5f, 1f);
            txtRt.anchorMax = new Vector2(0.5f, 1f);
            txtRt.pivot = new Vector2(0.5f, 1f);
            txtRt.sizeDelta = new Vector2(320f, 75f);
            txtRt.anchoredPosition = new Vector2(0f, -25f);

            // 4. YES Button (Left bottom)
            GameObject yesBtnObj = new GameObject("Btn_Yes", typeof(RectTransform));
            yesBtnObj.transform.SetParent(frameObj.transform, false);
            var yesBtnRt = yesBtnObj.GetComponent<RectTransform>();
            yesBtnRt.anchorMin = new Vector2(0.5f, 0f);
            yesBtnRt.anchorMax = new Vector2(0.5f, 0f);
            yesBtnRt.pivot = new Vector2(0.5f, 0.5f);
            yesBtnRt.sizeDelta = new Vector2(104f, 28f);
            yesBtnRt.anchoredPosition = new Vector2(-65f, 35f);

            var yesImg = yesBtnObj.AddComponent<Image>();
            yesImg.sprite = CreateHexagonSprite("dialog_btn_bg", 104, 28,
                new Color(0.96f, 0.80f, 0.22f, 0.98f),
                new Color(0.68f, 0.48f, 0.08f, 0.98f),
                new Color(0.85f, 0.65f, 0.15f, 0.98f),
                new Color(1.00f, 0.95f, 0.72f, 0.98f));
            yesImg.color = Color.white;

            var yesTextObj = new GameObject("Text", typeof(RectTransform));
            yesTextObj.transform.SetParent(yesBtnObj.transform, false);
            var yesTxtComp = yesTextObj.AddComponent<TextMeshProUGUI>();
            yesTxtComp.font = fontAsset;
            yesTxtComp.text = "YES";
            yesTxtComp.fontSize = 13;
            yesTxtComp.fontStyle = FontStyles.Bold;
            yesTxtComp.color = Color.black;
            yesTxtComp.alignment = TextAlignmentOptions.Center;

            var yesTxtRt = yesTextObj.GetComponent<RectTransform>();
            yesTxtRt.anchorMin = Vector2.zero;
            yesTxtRt.anchorMax = Vector2.one;
            yesTxtRt.offsetMin = Vector2.zero;
            yesTxtRt.offsetMax = Vector2.zero;

            var yesBtnComp = yesBtnObj.AddComponent<Button>();
            yesBtnComp.onClick.AddListener(() => {
                UnityEngine.Object.Destroy(dialogObj);
                onYes?.Invoke();
            });

            // 5. NO Button (Right bottom)
            GameObject noBtnObj = new GameObject("Btn_No", typeof(RectTransform));
            noBtnObj.transform.SetParent(frameObj.transform, false);
            var noBtnRt = noBtnObj.GetComponent<RectTransform>();
            noBtnRt.anchorMin = new Vector2(0.5f, 0f);
            noBtnRt.anchorMax = new Vector2(0.5f, 0f);
            noBtnRt.pivot = new Vector2(0.5f, 0.5f);
            noBtnRt.sizeDelta = new Vector2(104f, 28f);
            noBtnRt.anchoredPosition = new Vector2(65f, 35f);

            var noImg = noBtnObj.AddComponent<Image>();
            noImg.sprite = CreateHexagonSprite("dialog_btn_bg", 104, 28,
                new Color(0.24f, 0.22f, 0.20f, 0.98f),
                new Color(0.12f, 0.10f, 0.08f, 0.98f),
                new Color(0.20f, 0.18f, 0.16f, 0.98f),
                new Color(0.35f, 0.32f, 0.28f, 0.98f));
            noImg.color = Color.white;

            var noTextObj = new GameObject("Text", typeof(RectTransform));
            noTextObj.transform.SetParent(noBtnObj.transform, false);
            var noTxtComp = noTextObj.AddComponent<TextMeshProUGUI>();
            noTxtComp.font = fontAsset;
            noTxtComp.text = "NO";
            noTxtComp.fontSize = 13;
            noTxtComp.fontStyle = FontStyles.Bold;
            noTxtComp.color = new Color(0.9f, 0.85f, 0.75f, 1f);
            noTxtComp.alignment = TextAlignmentOptions.Center;

            var noTxtRt = noTextObj.GetComponent<RectTransform>();
            noTxtRt.anchorMin = Vector2.zero;
            noTxtRt.anchorMax = Vector2.one;
            noTxtRt.offsetMin = Vector2.zero;
            noTxtRt.offsetMax = Vector2.zero;

            var noBtnComp = noBtnObj.AddComponent<Button>();
            noBtnComp.onClick.AddListener(() => {
                UnityEngine.Object.Destroy(dialogObj);
                onNo?.Invoke();
            });

            return dialogObj;
        }

        // ============================================
        // Procedural Sprite Generation Helpers
        // ============================================

        private static Sprite CreatePanelBgSprite(string name, int w, int h, Color topColor, Color bottomColor, Color borderColor, int borderWidth)
        {
            int scale = 2;
            int sw = w * scale;
            int sh = h * scale;
            int sborderWidth = borderWidth * scale;

            Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < sh; y++)
            {
                float t = (float)y / sh;
                Color fillColor = Color.Lerp(bottomColor, topColor, t);

                for (int x = 0; x < sw; x++)
                {
                    bool isBorder = false;
                    if (sborderWidth > 0)
                    {
                        if (x < sborderWidth || x >= sw - sborderWidth || y < sborderWidth || y >= sh - sborderWidth)
                            isBorder = true;
                    }
                    tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateHexagonSprite(string name, int w, int h, Color centerColor, Color edgeColor, Color borderColor, Color innerGlowColor)
        {
            int scale = 4;
            int sw = w * scale;
            int sh = h * scale;

            Texture2D tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cy = sh / 2f;
            float indent = sh * 0.4f;
            float cosTheta = 0.78f;

            float resScale = sh / 36f;
            float borderOuter = 1.5f * resScale;
            float borderInner = 3.0f * resScale;

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    float leftBound = Mathf.Abs(y - cy) * (indent / cy);
                    float rightBound = sw - leftBound;

                    float distToLeft = (x - leftBound) * cosTheta;
                    float distToRight = (rightBound - x) * cosTheta;
                    float distToTop = (sh - 1) - y;
                    float distToBottom = y;
                    float minDist = Mathf.Min(Mathf.Min(distToLeft, distToRight), Mathf.Min(distToTop, distToBottom));

                    if (minDist < 0f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (minDist < borderOuter)
                    {
                        tex.SetPixel(x, y, borderColor);
                    }
                    else if (minDist < borderInner)
                    {
                        float t = (minDist - borderOuter) / (borderInner - borderOuter);
                        tex.SetPixel(x, y, Color.Lerp(borderColor, innerGlowColor, t));
                    }
                    else
                    {
                        float t = (float)x / sw;
                        Color col = Color.Lerp(centerColor, edgeColor, t);
                        tex.SetPixel(x, y, col);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f));
        }
    }
}
