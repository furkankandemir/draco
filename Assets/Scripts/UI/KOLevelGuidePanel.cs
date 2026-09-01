using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using EntropyOnline.Core;
using EntropyOnline.Import;
using EntropyOnline.Services;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir CUILevelGuide (UILevelGuide.cpp) Portu
    /// Seviye Kılavuzu (Level Guide) Arayüz Paneli
    /// </summary>
    public class KOLevelGuidePanel : MonoBehaviour
    {
        private const int MAX_SEARCH_LEVEL_RANGE = 5;
        private const int MAX_QUESTS_PER_PAGE = 3;
        private const int MAX_LEVEL = 80;

        // UI Element referansları
        private InputField _editLevel;
        private Text _textPage;
        private Text _textLevel;
        private Button _btnCheck;
        private Button _btnUp;
        private Button _btnDown;
        private Button _btnCancel;

        // Görev slotları
        private readonly Text[] _textTitle = new Text[MAX_QUESTS_PER_PAGE];
        private readonly Text[] _textGuide = new Text[MAX_QUESTS_PER_PAGE];
        private readonly GameObject[] _scrollGuide = new GameObject[MAX_QUESTS_PER_PAGE];

        // Durum (State) değişkenleri
        private int _searchLevel = 0;
        private int _pageNo = 0;
        private bool _isInitialized = false;

        // Scroll durumları
        private readonly string[][] _slotLines = new string[MAX_QUESTS_PER_PAGE][];
        private readonly int[] _slotStartLine = new int[MAX_QUESTS_PER_PAGE];

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            SetVisible(true);
        }

        private void OnDisable()
        {
            if (_editLevel != null && _editLevel.isFocused)
            {
                _editLevel.DeactivateInputField();
            }
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            // Elementleri bul ve bağla
            _editLevel = KOUIRenderer.FindChildByID(transform, "edit_level")?.GetComponent<InputField>();
            if (_editLevel != null)
            {
                if (_editLevel.textComponent != null)
                {
                    _editLevel.textComponent.verticalOverflow = VerticalWrapMode.Overflow;
                }
                _editLevel.lineType = InputField.LineType.SingleLine;
            }
            _textPage = KOUIRenderer.FindChildText(transform, "text_page");
            _textLevel = KOUIRenderer.FindChildText(transform, "text_level");
            _btnCheck = KOUIRenderer.FindChildButton(transform, "btn_check");
            _btnUp = KOUIRenderer.FindChildButton(transform, "btn_up");
            _btnDown = KOUIRenderer.FindChildButton(transform, "btn_down");
            _btnCancel = KOUIRenderer.FindChildButton(transform, "btn_cancel");

            for (int i = 0; i < MAX_QUESTS_PER_PAGE; i++)
            {
                _textTitle[i] = KOUIRenderer.FindChildText(transform, $"text_title{i}");
                _textGuide[i] = KOUIRenderer.FindChildText(transform, $"text_guide{i}");
                
                var scrollTr = KOUIRenderer.FindChildByID(transform, $"scroll_guide{i}");
                if (scrollTr != null)
                {
                    _scrollGuide[i] = scrollTr.gameObject;
                    SetupScrollButtons(i, _scrollGuide[i]);
                }
            }

            // Buton listener'larını tanımla
            if (_btnCancel != null) _btnCancel.onClick.AddListener(() => SetVisible(false));
            if (_btnUp != null) _btnUp.onClick.AddListener(() => SetPageNo(_pageNo + 1));
            if (_btnDown != null) _btnDown.onClick.AddListener(() => SetPageNo(_pageNo - 1));
            if (_btnCheck != null) _btnCheck.onClick.AddListener(SearchQuests);

            _isInitialized = true;
        }

        public void SetVisible(bool bVisible)
        {
            gameObject.SetActive(bVisible);

            if (bVisible)
            {
                _searchLevel = 0;
                SetPageNo(0);
                if (_editLevel != null)
                {
                    _editLevel.ActivateInputField();
                }
            }
            else
            {
                if (_editLevel != null && _editLevel.isFocused)
                {
                    _editLevel.DeactivateInputField();
                }
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                SetVisible(false);
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                if (_editLevel != null && _editLevel.isFocused)
                {
                    SearchQuests();
                }
            }
        }

        /// <summary>
        /// C++ CUILevelGuide::SearchQuests (UILevelGuide.cpp:98-124) Portu
        /// </summary>
        public void SearchQuests()
        {
            if (_editLevel == null) return;

            string szSearchLevel = _editLevel.text;
            if (string.IsNullOrEmpty(szSearchLevel)) return;

            if (!int.TryParse(szSearchLevel, out int iSearchLevel) || iSearchLevel == 0)
                return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            int maxSearch = gm.Level + MAX_SEARCH_LEVEL_RANGE;
            if (maxSearch < iSearchLevel)
            {
                // IDS_QUEST_SEARCH_LEVEL_ERROR = 10100
                string rawMsg = StringTableService.Get(10100);
                if (string.IsNullOrEmpty(rawMsg) || rawMsg == "[#10100]")
                {
                    rawMsg = "You can only search up to +%d levels from your current level.";
                }

                string formattedMsg = rawMsg.Replace("%d", MAX_SEARCH_LEVEL_RANGE.ToString());

                if (KOMessageBox.Instance != null)
                {
                    KOMessageBox.Instance.ShowOk(formattedMsg, "");
                }
                else
                {
                    Debug.LogWarning($"[LevelGuide] Warning: {formattedMsg}");
                }

                iSearchLevel = maxSearch;
            }

            _searchLevel = iSearchLevel;
            SetPageNo(0);

            _editLevel.text = "";
            _editLevel.ActivateInputField();
        }

        /// <summary>
        /// C++ CUILevelGuide::SetPageNo (UILevelGuide.cpp:126-209) Portu
        /// </summary>
        public void SetPageNo(int iPageNo)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            int iSearchLevel = 0;
            if (_searchLevel <= 0)
                iSearchLevel = gm.Level;
            else
                iSearchLevel = _searchLevel;

            if (_textLevel != null)
            {
                _textLevel.text = iSearchLevel.ToString();
            }

            iSearchLevel = Mathf.Clamp(iSearchLevel, 1, MAX_LEVEL);

            if (_editLevel != null)
            {
                _editLevel.ActivateInputField();
            }

            // Oyuncunun represent sınıfı
            int eCR = GetRepresentClass(gm.CharClass);

            // eligibleQuests filtresi
            var eligibleQuests = new List<KOTableReader.TableHelp>();
            if (KOInventory.s_pTbl_Help != null)
            {
                foreach (var quest in KOInventory.s_pTbl_Help.Values)
                {
                    if (iSearchLevel < quest.iMinLevel || iSearchLevel > quest.iMaxLevel)
                        continue;

                    // reqClass == -1 veya 100 (unknown) ise genel görevdir, yoksa sınıfa özeldir
                    if (quest.iReqClass == -1 || quest.iReqClass == 100 || quest.iReqClass == eCR)
                    {
                        eligibleQuests.Add(quest);
                    }
                }
            }

            int iPageCount = (eligibleQuests.Count + MAX_QUESTS_PER_PAGE - 1) / MAX_QUESTS_PER_PAGE;

            if (iPageNo >= iPageCount)
                iPageNo = iPageCount - 1;

            if (iPageNo < 0)
                iPageNo = 0;

            _pageNo = iPageNo;

            int iStartIndex = iPageNo * MAX_QUESTS_PER_PAGE;
            int iVisibleIndex = 0;

            for (int i = iStartIndex; i < eligibleQuests.Count && iVisibleIndex < MAX_QUESTS_PER_PAGE; i++)
            {
                var quest = eligibleQuests[i];

                if (_textTitle[iVisibleIndex] != null)
                {
                    _textTitle[iVisibleIndex].text = quest.szQuestName;
                }

                // Scroll ve metin satırlama ayarı
                string desc = quest.szQuestDesc;
                if (_textGuide[iVisibleIndex] != null)
                {
                    SetupQuestDescription(iVisibleIndex, desc);
                }

                iVisibleIndex++;
            }

            // Geriye kalan slotları temizle
            for (int i = iVisibleIndex; i < MAX_QUESTS_PER_PAGE; i++)
            {
                if (_textTitle[i] != null) _textTitle[i].text = "";
                if (_textGuide[i] != null) _textGuide[i].text = "";
                if (_scrollGuide[i] != null) _scrollGuide[i].SetActive(false);
                _slotLines[i] = null;
                _slotStartLine[i] = 0;
            }

            if (_textPage != null)
            {
                _textPage.text = (_pageNo + 1).ToString();
            }
        }

        private void SetupQuestDescription(int index, string desc)
        {
            if (string.IsNullOrEmpty(desc))
            {
                _textGuide[index].text = "";
                if (_scrollGuide[index] != null) _scrollGuide[index].SetActive(false);
                _slotLines[index] = null;
                _slotStartLine[index] = 0;
                return;
            }

            // Satırları ayır
            string[] rawLines = desc.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var filteredLines = new List<string>();
            foreach (var line in rawLines)
            {
                // Çok uzun satırlar varsa Unity Text component'inin sarmalaması nedeniyle
                // satır bazlı kaydırma için onları elle bölüyoruz.
                // 12pt fontta yaklaşık 35-40 karakter sığar
                int maxCharsPerLine = 35; 
                if (line.Length > maxCharsPerLine)
                {
                    int start = 0;
                    while (start < line.Length)
                    {
                        int len = Mathf.Min(maxCharsPerLine, line.Length - start);
                        filteredLines.Add(line.Substring(start, len));
                        start += len;
                    }
                }
                else
                {
                    filteredLines.Add(line);
                }
            }

            _slotLines[index] = filteredLines.ToArray();
            _slotStartLine[index] = 0;

            if (_slotLines[index].Length > 4)
            {
                if (_scrollGuide[index] != null) _scrollGuide[index].SetActive(true);
            }
            else
            {
                if (_scrollGuide[index] != null) _scrollGuide[index].SetActive(false);
            }

            UpdateDescriptionText(index);
        }

        private void UpdateDescriptionText(int index)
        {
            if (_textGuide[index] == null || _slotLines[index] == null) return;

            int start = _slotStartLine[index];
            int count = Mathf.Min(4, _slotLines[index].Length - start);

            if (count > 0)
            {
                var displayLines = new string[count];
                Array.Copy(_slotLines[index], start, displayLines, 0, count);
                _textGuide[index].text = string.Join("\n", displayLines);
            }
            else
            {
                _textGuide[index].text = "";
            }
        }

        private void SetupScrollButtons(int index, GameObject scrollObj)
        {
            if (scrollObj == null) return;

            // scrollbar altındaki butonları bul (yukarıda sorted Y koordinat mantığı)
            var buttons = scrollObj.GetComponentsInChildren<Button>(true);
            if (buttons.Length >= 2)
            {
                Array.Sort(buttons, (a, b) =>
                {
                    float yA = a.GetComponent<RectTransform>().anchoredPosition.y;
                    float yB = b.GetComponent<RectTransform>().anchoredPosition.y;
                    return yB.CompareTo(yA); // Descending (en üstteki ilk)
                });

                Button btnUp = buttons[0];
                Button btnDown = buttons[buttons.Length - 1];

                btnUp.onClick.RemoveAllListeners();
                btnUp.onClick.AddListener(() => ScrollQuestDescription(index, -1));

                btnDown.onClick.RemoveAllListeners();
                btnDown.onClick.AddListener(() => ScrollQuestDescription(index, 1));
            }
        }

        private void ScrollQuestDescription(int index, int dir)
        {
            if (_slotLines[index] == null || _slotLines[index].Length <= 4) return;

            int maxStartLine = _slotLines[index].Length - 4;
            int newStartLine = Mathf.Clamp(_slotStartLine[index] + dir, 0, maxStartLine);

            if (newStartLine != _slotStartLine[index])
            {
                _slotStartLine[index] = newStartLine;
                UpdateDescriptionText(index);
            }
        }

        /// <summary>
        /// Open-KO GetRepresentClass() satır 398-427 birebir portu
        /// </summary>
        private int GetRepresentClass(byte eClass)
        {
            int baseClass = eClass % 100;
            return baseClass switch
            {
                1 or 5 or 6 => 0,   // CLASS_REPRESENT_WARRIOR
                2 or 7 or 8 => 1,   // CLASS_REPRESENT_ROGUE
                3 or 9 or 10 => 2,  // CLASS_REPRESENT_WIZARD
                4 or 11 or 12 => 3, // CLASS_REPRESENT_PRIEST
                _ => 100            // CLASS_REPRESENT_UNKNOWN
            };
        }
    }
}
