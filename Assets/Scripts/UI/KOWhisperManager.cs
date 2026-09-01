using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using EntropyOnline.Core;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;

namespace EntropyOnline.UI
{
    public class WhisperMessage
    {
        public string SenderName;
        public string MessageText;
        public bool IsOutgoing;
    }

    public class WhisperConversation
    {
        public string PlayerName;
        public int PlayerLevel = 83; // Fallback default
        public int UnreadCount = 0;
        public List<WhisperMessage> Messages = new List<WhisperMessage>();
    }

    /// <summary>
    /// Knight Online v1298 Özel Mesaj (PM/Fısıltı) pencere yöneticisi.
    /// </summary>
    public class KOWhisperManager : MonoBehaviour
    {
        public static KOWhisperManager Instance { get; private set; }

        private Dictionary<string, KOWhisperPanel> _openPanels = new Dictionary<string, KOWhisperPanel>(System.StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, KOWhisperClosePanel> _minimizedPanels = new Dictionary<string, KOWhisperClosePanel>(System.StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, WhisperConversation> _chatHistories = new Dictionary<string, WhisperConversation>(System.StringComparer.OrdinalIgnoreCase);

        private KOWhisperDirectoryPanel _directoryPanel;
        private Vector2 _nextWindowPos = new Vector2(250f, 250f);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void ShowWhisperDirectory()
        {
            // Stop flashing PM button
            KOUIManager.Instance?.SetPMButtonBlinking(false);

            if (_directoryPanel == null)
            {
                var canvasTransform = KOUIManager.Instance?.Canvas?.transform;
                GameObject prefab = Resources.Load<GameObject>("ModernUI/co_whisper_directory");
                bool isModern = (prefab != null);

                if (isModern)
                {
                    var dirGO = Instantiate(prefab, canvasTransform);
                    dirGO.name = "co_whisper_directory";
                    _directoryPanel = dirGO.GetComponent<KOWhisperDirectoryPanel>();
                    if (_directoryPanel == null)
                    {
                        _directoryPanel = dirGO.AddComponent<KOWhisperDirectoryPanel>();
                    }
                }
                else
                {
                    // Create directory panel dynamically
                    var dirGO = new GameObject("co_whisper_directory", typeof(RectTransform));
                    if (canvasTransform != null)
                    {
                        dirGO.transform.SetParent(canvasTransform, false);
                    }
                    _directoryPanel = dirGO.AddComponent<KOWhisperDirectoryPanel>();
                }
                
                _directoryPanel.Initialize();
            }

            _directoryPanel.gameObject.SetActive(true);
            _directoryPanel.RefreshList(_chatHistories);
            _directoryPanel.transform.SetAsLastSibling();
        }

        public void ShowWhisperWindow(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return;

            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.IsPlayerBlockingMe(targetName))
            {
                KOUIManager.Instance?.AddMsgOutput("This user has blocked private messages.", KOUIManager.D3DColorToUnity(0xffffff00));
                return;
            }

            // Zaten açıksa odakla (En öne getir)
            if (_openPanels.TryGetValue(targetName, out var openPanel))
            {
                openPanel.transform.SetAsLastSibling();
                return;
            }

            // Simge durumundaysa geri yükle
            if (_minimizedPanels.ContainsKey(targetName))
            {
                RestoreWindow(targetName);
                return;
            }

            // co_whisper_open_us prefabını yükle (modern veya legacy check)
            var prefab = Resources.Load<GameObject>("ModernUI/co_whisper_open_us");
            bool isModern = (prefab != null);
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("KOPrefabs/UI/co_whisper_open_us");
            }

            if (prefab == null)
            {
                Debug.LogError("[WHISPER] 'co_whisper_open_us' prefab'ı KOPrefabs/UI veya ModernUI klasöründe bulunamadı!");
                return;
            }

            if (isModern)
            {
            }

            Transform canvasTransform = KOUIManager.Instance?.Canvas?.transform;
            if (canvasTransform == null)
            {
                Debug.LogError("[WHISPER] Canvas bulunamadı!");
                return;
            }

            var panelGO = Instantiate(prefab, canvasTransform);
            panelGO.name = $"co_whisper_open_{targetName}";
            
            var whisperPanel = panelGO.GetComponent<KOWhisperPanel>();
            if (whisperPanel == null)
            {
                whisperPanel = panelGO.AddComponent<KOWhisperPanel>();
            }
            
            if (!_chatHistories.TryGetValue(targetName, out var conv))
            {
                conv = new WhisperConversation { PlayerName = targetName };
                _chatHistories[targetName] = conv;
            }
            conv.UnreadCount = 0; // Clear unread

            whisperPanel.Initialize(targetName, conv.Messages);

            // Konumlandır (sol tarafta sabit veya merkeze yakın)
            var rt = panelGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                
                float s = KOUIManager.Instance != null ? KOUIManager.Instance.CanvasScaleFactor : 1f;
                if (s > 0f)
                {
                    rt.anchoredPosition = new Vector2(50f / s, 190f / s); // opens 50px from left edge
                }
                else
                {
                    rt.anchoredPosition = new Vector2(50f, 190f);
                }
            }

            _openPanels[targetName] = whisperPanel;
        }

        public void MinimizeWindow(string targetName)
        {
            if (!_openPanels.TryGetValue(targetName, out var openPanel)) return;

            // Yok et
            Destroy(openPanel.gameObject);
            _openPanels.Remove(targetName);

            // Switch back to general directory panel
            ShowWhisperDirectory();
        }

        public void RestoreWindow(string targetName)
        {
            if (!_minimizedPanels.TryGetValue(targetName, out var closePanel)) return;

            Destroy(closePanel.gameObject);
            _minimizedPanels.Remove(targetName);
            
            RepositionMinimizedPanels();

            ShowWhisperWindow(targetName);
        }

        public void CloseWindow(string targetName)
        {
            if (_openPanels.TryGetValue(targetName, out var openPanel))
            {
                Destroy(openPanel.gameObject);
                _openPanels.Remove(targetName);
            }

            if (_minimizedPanels.TryGetValue(targetName, out var closePanel))
            {
                Destroy(closePanel.gameObject);
                _minimizedPanels.Remove(targetName);
                RepositionMinimizedPanels();
            }

            // Remove from histories list to close/delete conversation entirely
            if (_chatHistories.ContainsKey(targetName))
            {
                _chatHistories.Remove(targetName);
            }

            if (_directoryPanel != null && _directoryPanel.gameObject.activeSelf)
            {
                _directoryPanel.RefreshList(_chatHistories);
            }

            // Switch back to general directory panel
            ShowWhisperDirectory();
        }

        public void RemoveAllConversations()
        {
            // Close all open panels
            foreach (var kvp in _openPanels)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _openPanels.Clear();

            foreach (var kvp in _minimizedPanels)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _minimizedPanels.Clear();

            _chatHistories.Clear();

            if (_directoryPanel != null && _directoryPanel.gameObject.activeSelf)
            {
                _directoryPanel.RefreshList(_chatHistories);
            }

            KOUIManager.Instance?.SetPMButtonBlinking(false);
        }

        public void ReceivePrivateMessage(string senderName, string message)
        {
            if (string.IsNullOrEmpty(senderName)) return;

            // If we receive the block feedback back, it means we are blocked by the target player
            if (message == "This user has blocked private messages.")
            {
                KOUIManager.Instance?.AddMsgOutput("This user has blocked private messages.", KOUIManager.D3DColorToUnity(0xffffff00));
                GameOptionsManager.Instance?.AddPlayerWhoBlockedMe(senderName);
                return;
            }

            // Discard incoming messages if player is blocked, and reply silently to notify them
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.IsPlayerBlocked(senderName))
            {
                SendPrivateMessageSilent(senderName, "This user has blocked private messages.");
                return;
            }

            // Get or create conversation history
            if (!_chatHistories.TryGetValue(senderName, out var conv))
            {
                conv = new WhisperConversation { PlayerName = senderName };
                _chatHistories[senderName] = conv;
            }

            // Since we received a normal PM from them, they are definitely not blocking us
            GameOptionsManager.Instance?.RemovePlayerWhoBlockedMe(senderName);

            var msgObj = new WhisperMessage { SenderName = senderName, MessageText = message, IsOutgoing = false };
            conv.Messages.Add(msgObj);

            // Eğer pencere açık ise doğrudan ekle ve okundu say
            if (_openPanels.TryGetValue(senderName, out var openPanel))
            {
                openPanel.AddMessage(senderName, message, false);
                openPanel.transform.SetAsLastSibling(); // Ön plana al
            }
            else
            {
                // Increment unread count
                conv.UnreadCount++;
                
                // Sol alttaki PM butonunu yanıp söndürmeye başla
                KOUIManager.Instance?.SetPMButtonBlinking(true);
            }

            // Genel listeyi yenile
            if (_directoryPanel != null && _directoryPanel.gameObject.activeSelf)
            {
                _directoryPanel.RefreshList(_chatHistories);
            }
        }

        public void SendPrivateMessageSilent(string targetName, string message)
        {
            if (string.IsNullOrEmpty(targetName) || string.IsNullOrEmpty(message)) return;

            // 1. Sunucuya hedef belirleme paketi gönder
            KONetworkManager.Instance?.SendChatSelectTarget(targetName);

            // 2. Fısıltı mesaj paketini gönder
            using (var packet = new KOPacketWriter(WizOpcode.WIZ_CHAT))
            {
                packet.WriteByte(2); 
                packet.WriteString(message);
                KONetworkManager.Instance?.SendPacket(packet);
            }
        }

        public void SendPrivateMessage(string targetName, string message)
        {
            if (string.IsNullOrEmpty(targetName) || string.IsNullOrEmpty(message)) return;

            // Prevent sending PMs if target is in local block list
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.IsPlayerBlocked(targetName))
            {
                KOUIManager.Instance?.AddMsgOutput("Blocked Private messages.", KOUIManager.D3DColorToUnity(0xffffff00));
                return;
            }

            // 0. Karşı tarafın engeli kaldırıp kaldırmadığını test etmek için engellenme durumunu yerelde geçici olarak sil
            if (GameOptionsManager.Instance != null)
            {
                GameOptionsManager.Instance.RemovePlayerWhoBlockedMe(targetName);
            }

            // 1. Sunucuya hedef belirleme paketi gönder
            KONetworkManager.Instance?.SendChatSelectTarget(targetName);

            // 2. Fısıltı mesaj paketini gönder
            using (var packet = new KOPacketWriter(WizOpcode.WIZ_CHAT))
            {
                packet.WriteByte(2); 
                packet.WriteString(message);
                KONetworkManager.Instance?.SendPacket(packet);
            }

            string myName = GameManager.Instance?.CharacterName ?? "You";

            if (!_chatHistories.TryGetValue(targetName, out var conv))
            {
                conv = new WhisperConversation { PlayerName = targetName };
                _chatHistories[targetName] = conv;
            }

            var msgObj = new WhisperMessage { SenderName = myName, MessageText = message, IsOutgoing = true };
            conv.Messages.Add(msgObj);

            if (_openPanels.TryGetValue(targetName, out var openPanel))
            {
                openPanel.AddMessage(myName, message, true);
            }
        }

        private void RepositionMinimizedPanels()
        {
            float startX = 20f;
            float y = 50f;
            float spacingX = 160f;

            int index = 0;
            foreach (var kvp in _minimizedPanels)
            {
                var rt = kvp.Value.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                    rt.anchoredPosition = new Vector2(startX + (index * spacingX), y);
                }
                index++;
            }
        }

        public void UpdatePanelsScaleAndPosition(float s)
        {
            if (s <= 0f) return;

            if (_directoryPanel != null && _directoryPanel.gameObject.activeSelf)
            {
                var rt = _directoryPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(20f / s, 190f / s);
                }
            }

            foreach (var kvp in _openPanels)
            {
                if (kvp.Value != null)
                {
                    var rt = kvp.Value.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchoredPosition = new Vector2(20f / s, 190f / s);
                    }
                }
            }
        }

        public void UpdateWhisperPanelBlockState(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return;
            if (_openPanels.TryGetValue(targetName, out var openPanel))
            {
                openPanel.UpdateBlockStateVisual();
            }
        }
    }
}
