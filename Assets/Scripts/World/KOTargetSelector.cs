using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using EntropyOnline.Import;
using EntropyOnline.UI;

namespace EntropyOnline.World
{
    /// <summary>
    /// Tap/Click ile entity seçme sistemi.
    /// Open-KO birebir: GameProcMain::TargetSelect (GameProcMain.cpp:5545-5601)
    ///
    /// Target bar layout: co_targetbar_us.uif binary parse'dan birebir.
    /// HP bar texture: ui_us\ui_message_us.dxt — UV koordinatları .uif'den.
    ///
    /// .uif parse sonuçları (v1264 format):
    ///   Root ID="target" Region=(0,0,324,64)
    ///     String ID="text_target" Region=(0,4,323,38) Font=Arial/14/Bold Color=0xFFFF0000
    ///     Progress ID="pro_target" Region=(93,41,242,57)
    ///       Image Reserved=0 (BKGND) UV=(0.627,0.869,0.977,0.900)
    ///       Image Reserved=1 (FRGND) UV=(0.631,0.902,0.973,0.926)
    ///     Progress ID="Progress_HP_slow" Region=(93,41,242,57)
    ///     Progress ID="Progress_HP_drop" Region=(93,41,241,57)
    ///     Progress ID="Progress_HP_lasting" Region=(93,41,241,57)
    ///
    /// Target symbol: Co_targetsymbol.n3shape (3D asset — plane + symbol mesh)
    ///   GameProcMain.cpp:4148 — LoadFromFile(pTbl->szTargetSymbolShape)
    ///   GameProcMain.cpp:717-764 — RenderTarget()
    ///   Scale: fScale = pTarget->Radius() * 2.0f, fYScale = pTarget->Height() * 1.3f
    ///   Position: target entity position
    ///
    /// Panel position: GameProcMain.cpp:3968
    ///   SetPos((iW - (rc.right - rc.left)) / 2, 0)
    ///   iW = CN3Base::s_CameraData.vp.Width (viewport genişliği)
    ///   Y = 0 (ekranın en üstü)
    /// </summary>
    public class KOTargetSelector : MonoBehaviour
    {
        // === .uif birebir sabitler ===
        // co_targetbar_us.uif parse'dan — 1431/1431 byte doğrulanmış
        private const int UIF_ROOT_W = 324;
        private const int UIF_ROOT_H = 64;

        // text_target region
        private const int UIF_TEXT_L = 0;
        private const int UIF_TEXT_T = 4;
        private const int UIF_TEXT_R = 323;
        private const int UIF_TEXT_B = 38;

        // pro_target region
        private const int UIF_PRO_L = 62;
        private const int UIF_PRO_T = 25;
        private const int UIF_PRO_R = 262;
        private const int UIF_PRO_B = 43;

        // pro_target background UV (ui_message_us.dxt — Reserved=0 BKGND)
        private const float UIF_BG_UV_L = 0.6269531f;
        private const float UIF_BG_UV_T = 0.8691406f;
        private const float UIF_BG_UV_R = 0.9765625f;
        private const float UIF_BG_UV_B = 0.9003906f;

        // pro_target foreground UV (ui_message_us.dxt — Reserved=1 FRGND)
        private const float UIF_FG_UV_L = 0.6308594f;
        private const float UIF_FG_UV_T = 0.9023438f;
        private const float UIF_FG_UV_R = 0.9726563f;
        private const float UIF_FG_UV_B = 0.9257813f;

        // === Target Bar UI ===
        private Canvas _targetBarCanvas;
        private TextMeshProUGUI _targetNameText;
        private Image _hpBarBg;
        private Image _hpBarFg;
        private RectTransform _hpBarFgRt;
        private GameObject _targetBarPanel;
        private GameObject _hpBarContainer;
        private GameObject _hpBorderObj;
        private Image _hpBorderImg;
        private System.Collections.Generic.Dictionary<string, Sprite> _slantedSpriteCache = new System.Collections.Generic.Dictionary<string, Sprite>();

        // === Modernized Target HUD UI ===
        private Sprite _spritePlayerIcon;
        private Sprite _spriteMonsterIcon;
        private Sprite _spriteFrameIcon;

        private GameObject _targetIconFrameObj;
        private Image _targetIconFrameImg;
        private GameObject _targetIconObj;
        private Image _targetIconImg;
        private Image _targetIconBgImg;
        private GameObject _targetLevelTextObj;
        private TextMeshProUGUI _targetLevelText;
        private GameObject _targetActionButtonsPanel;

        // Buttons
        private Button _btnInvite;
        private Button _btnPM;
        private Button _btnInfo;
        private Button _btnShowDrops;

        // === Target Symbol (3D) ===
        // Open-KO: Co_targetsymbol.n3shape — 2 part'lı 3D shape
        //   Part(0) = symbol mesh (oklar) — target_symbol.dxt
        //   Part(1) = plane mesh (zemin dairesi) — target_symbol_plane.dxt
        // GameProcMain.cpp:4148 — LoadFromFile(pTbl->szTargetSymbolShape)
        private GameObject _targetSymbolRoot;
        private GameObject _targetSymbolPart0; // symbol
        private GameObject _targetSymbolPart1; // plane

        // === Texture cache ===
        private Texture2D _uiMessageTex;

        // === Seçili target ===
        private KOEntity _currentTarget;
        private RemotePlayerEntity _currentTargetPlayer;

        private void Start()
        {
            LoadUITexture();
            LoadTargetSymbol();
            CreateTargetBarUI();
        }

        private void Update()
        {
            // Target bar + symbol her zaman güncellenmeli — input'tan bağımsız
            UpdateTargetBar();

            bool tapped = false;
            Vector2 screenPos = Vector2.zero;
            int activePointerId = -1;

            // 1. Yeni Input System Dokunmatik Girişleri
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        tapped = true;
                        screenPos = touch.position.ReadValue();
                        activePointerId = touch.touchId.ReadValue();
                        break;
                    }
                }
            }

            // 2. Mouse / Editör Tıklama Girişi
            if (!tapped && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    tapped = true;
                    screenPos = Mouse.current.position.ReadValue();
                    activePointerId = -1;
                }
            }

            if (tapped)
            {
                if (!IsPointerOverUIOtherThanJoystick(activePointerId, screenPos))
                {
                    TrySelectEntity(screenPos);
                }
            }
        }

        private bool IsPointerOverUIOtherThanJoystick(int pointerId = -1, Vector2? position = null)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current);
            eventData.pointerId = pointerId;

            if (position.HasValue)
            {
                eventData.position = position.Value;
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    eventData.position = mouse.position.ReadValue();
                }
            }

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            if (results.Count > 0)
            {
                var topmost = results[0].gameObject;
                if (topmost != null)
                {
                    // En üstteki nesne joystick veya joystick'in bir parçası/konteyneri değilse hedef seçmeyi engelle
                    if (topmost.GetComponent<EntropyOnline.Input.VirtualJoystick>() == null && 
                        topmost.GetComponentInParent<EntropyOnline.Input.VirtualJoystick>() == null &&
                        topmost.GetComponentInChildren<EntropyOnline.Input.VirtualJoystick>() == null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// UI texture yükle — ui_us\ui_message_us.dxt
        /// </summary>
        private void LoadUITexture()
        {
            // Resources/KOTextures/UI_US/ altından yükle
            _uiMessageTex = Resources.Load<Texture2D>("KOTextures/UI_US/ui_Message_us");
            if (_uiMessageTex == null)
                _uiMessageTex = Resources.Load<Texture2D>("KOTextures/UI/ui_Message_us");
            if (_uiMessageTex == null)
                Debug.LogError("[TargetBar] UI texture bulunamadı: ui_Message_us");
        }

        /// <summary>
        /// Target symbol yükle — Co_targetsymbol.n3shape birebir parse.
        /// Open-KO: GameProcMain.cpp:4148 — LoadFromFile(pTbl->szTargetSymbolShape)
        /// N3ShapeParser.ParseShapeFile → CN3Shape::Load birebir
        /// N3PMeshImporter.Load → CN3PMesh::Load birebir
        /// DxtTextureImporter.Load → CN3Texture::Load birebir
        /// </summary>
        private void LoadTargetSymbol()
        {
            string basePath = "";
            string shapePath = System.IO.Path.Combine(basePath, "Misc", "Co_targetsymbol.n3shape");

            var shapeData = N3ShapeParser.ParseShapeFile(shapePath);
            if (shapeData == null || shapeData.Parts.Count < 2)
            {
                Debug.LogWarning($"[TargetBar] Co_targetsymbol.n3shape parse edilemedi veya < 2 part");
                return;
            }

            // Root GameObject — RenderTarget'da ScaleSet + PosSet uygulanacak
            _targetSymbolRoot = new GameObject("TargetSymbol");
            _targetSymbolRoot.SetActive(false);

            // Her part için: mesh yükle + texture yükle + GameObject oluştur
            for (int p = 0; p < shapeData.Parts.Count; p++)
            {
                var part = shapeData.Parts[p];

                // Mesh yükle — N3PMeshImporter birebir
                string meshFileName = part.MeshFileName;
                string meshPath = FindKOAsset(meshFileName);
                Mesh mesh = null;
                if (!string.IsNullOrEmpty(meshPath))
                {
                    var meshData = N3PMeshImporter.Load(meshPath);
                    mesh = N3PMeshImporter.CreateUnityMesh(meshData);
                }

                if (mesh == null)
                {
                    Debug.LogWarning($"[TargetBar] Part {p} mesh yüklenemedi: {meshFileName}");
                    continue;
                }

                // Texture yükle — Resources/KOTextures/ altından
                Texture2D tex = null;
                if (part.TextureFileNames != null && part.TextureFileNames.Count > 0)
                {
                    string texFileName = part.TextureFileNames[0];
                    string texBaseName = System.IO.Path.GetFileNameWithoutExtension(texFileName);
                    string[] searchDirs = { "Misc", "UI", "DTex", "Chr", "Object" };
                    foreach (var d in searchDirs)
                    {
                        tex = Resources.Load<Texture2D>($"KOTextures/{d}/{texBaseName}");
                        if (tex != null) break;
                    }
                }

                // GameObject oluştur
                var partObj = new GameObject($"Part{p}_{part.MeshFileName}");
                partObj.transform.SetParent(_targetSymbolRoot.transform, false);
                // CN3SPart pivot
                partObj.transform.localPosition = part.Pivot;

                var mf = partObj.AddComponent<MeshFilter>();
                mf.mesh = mesh;

                var mr = partObj.AddComponent<MeshRenderer>();
                // Open-KO birebir: Co_targetsymbol.n3shape material parse sonucu:
                //   RenderFlags=0x1/0x9 (RF_ALPHABLENDING), SrcBlend=2 (D3DBLEND_ONE), DstBlend=2 (D3DBLEND_ONE)
                //   → Additive blending: siyah(0,0,0) kaybolur, parlak kısımlar eklenir
                // CN3SPart::Render() → SetRenderState(D3DRS_SRCBLEND, D3DBLEND_ONE)
                // Sprites/Default shader _SrcBlend/_DstBlend property'lerini desteklemez,
                // URP Particles/Unlit veya legacy Particles/Standard Unlit shader'ı kullanılmalı.
                Material mat = null;
                var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particleShader == null)
                    particleShader = Shader.Find("Particles/Standard Unlit");
                if (particleShader != null)
                {
                    mat = new Material(particleShader);
                    // Surface = Transparent, Blending = Additive
                    mat.SetFloat("_Surface", 1); // 0=Opaque, 1=Transparent
                    mat.SetFloat("_Blend", 2);   // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.EnableKeyword("_BLENDMODE_ADD");
                    mat.renderQueue = 3001;
                    if (tex != null)
                        mat.mainTexture = tex;
                }
                else
                {
                    // Son çare: Sprites/Default (additive olmaz ama en azından alpha var)
                    mat = new Material(Shader.Find("Sprites/Default"));
                    if (tex != null)
                        mat.mainTexture = tex;
                    mat.renderQueue = 3001;
                }
                mr.material = mat;

                // Part referanslarını sakla
                if (p == 0) _targetSymbolPart0 = partObj;
                if (p == 1) _targetSymbolPart1 = partObj;

                // KO DirectX → Unity: symbol üçgeni ters → Y scale negatif
                if (p == 0)
                    partObj.transform.localScale = new Vector3(1f, -1f, 1f);
            }
        }

        /// <summary>
        /// KO asset dosyası bul — N3CharBuilder.FindAssetFile ile aynı mantık.
        /// Arama sırası: root → Misc/ → Chr/ → Item/ → Object/
        /// </summary>
        private static string FindKOAsset(string koPath)
        {
            if (string.IsNullOrEmpty(koPath)) return null;
            string normalized = koPath.Replace('\\', '/');
            string fileName = System.IO.Path.GetFileName(normalized);

            // Doğrudan root'tan
            string full = normalized;
            if (KOBinaryProvider.Exists(full)) return full;

            // Alt klasörlerde ara
            string[] searchDirs = { "Misc", "Chr", "Item", "Object" };
            foreach (var dir in searchDirs)
            {
                string path = System.IO.Path.Combine(dir, fileName);
                if (KOBinaryProvider.Exists(path)) return path;
            }

            return null;
        }

        /// <summary>
        /// Ekran pozisyonundan raycast yaparak entity seç.
        /// Open-KO: GameProcMain.cpp satır 7510-7515
        /// </summary>
        private void TrySelectEntity(Vector2 screenPos)
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;


            Ray ray = cam.ScreenPointToRay(screenPos);

            // RaycastAll — terrain arkasındaki entity collider'ları da yakala
            // C++ KO farklı picking sistemi kullanır (mesh-based), Unity'de terrain
            // collider'ı entity trigger collider'ının önüne geçebilir.
            var hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);

            // Önce entity hit'lerini ara (mesafeye göre sıralı)
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            KOEntity foundEntity = null;
            KOWorldEvent foundWorldEvent = null;
            RemotePlayerEntity foundPlayer = null;
            foreach (var hit in hits)
            {
                // Check for KOLootBox
                var lootBox = hit.collider.GetComponent<KOLootBox>();
                if (lootBox == null)
                    lootBox = hit.collider.GetComponentInParent<KOLootBox>();

                if (lootBox != null)
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player == null) player = GameObject.Find("Player");
                    if (player != null)
                    {
                        float fDist = Vector3.Distance(player.transform.position, lootBox.transform.position);
                        float fDistLimit = 8.0f; // Standard looting distance
                        if (fDist >= fDistLimit)
                        {
                            return;
                        }
                    }

                    if (LootDropUI.Instance != null)
                    {
                        LootDropUI.Instance.SendBundleOpen(lootBox.BundleID, lootBox.CorpseEntity);
                    }
                    return;
                }

                // Önce KOEntity ara (NPC/monster)
                var entity = hit.collider.GetComponent<KOEntity>();
                if (entity == null)
                    entity = hit.collider.GetComponentInParent<KOEntity>();

                if (entity != null)
                {
                    // NPC ise doğrudan tıklama/hedefleme işlemlerini tamamen iptal et
                    if (entity.IsNpc)
                    {
                        continue;
                    }
                    foundEntity = entity;
                    break;
                }

                // RemotePlayerEntity ara (player)
                var rpe = hit.collider.GetComponent<RemotePlayerEntity>();
                if (rpe == null)
                    rpe = hit.collider.GetComponentInParent<RemotePlayerEntity>();

                if (rpe != null)
                {
                    foundPlayer = rpe;
                    break;
                }

                // KOEntity yoksa KOWorldEvent ara (gate/warp point gibi world shape'ler)
                // C++ birebir: GameProcMain.cpp:7817-7843 — PickWithShape → ObjectEvent
                if (foundWorldEvent == null)
                {
                    var worldEvt = hit.collider.GetComponent<KOWorldEvent>();
                    if (worldEvt == null)
                        worldEvt = hit.collider.GetComponentInParent<KOWorldEvent>();
                    if (worldEvt != null)
                        foundWorldEvent = worldEvt;
                }
            }

            if (foundEntity != null)
            {
                    // Open-KO birebir: GameProcMain.cpp:5555-5560 — ClickCharacter
                    // if (pTarget->IsDead()) { ClickCorpse(pTarget); return; }
                    if (foundEntity.IsDead && foundEntity.DroppedItemID > 0)
                    {
                        // Open-KO birebir: mesafe kontrolü (cpp:1613-1615)
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player == null) player = GameObject.Find("Player");
                        if (player != null)
                        {
                            float fDist = Vector3.Distance(player.transform.position, foundEntity.transform.position);
                            float corpseRadius = 0.5f;
                            var corpseCol = foundEntity.GetComponent<CapsuleCollider>();
                            if (corpseCol != null)
                                corpseRadius = corpseCol.radius * foundEntity.transform.localScale.x;
                            float fDistLimit = corpseRadius * 2.0f + 6.0f;
                            if (fDist >= fDistLimit)
                            {
                                return;
                            }
                        }

                        if (LootDropUI.Instance != null)
                        {
                            LootDropUI.Instance.SendBundleOpen(foundEntity.DroppedItemID, foundEntity);
                        }
                        return;
                    }

                    // Open-KO birebir: GameProcMain.cpp:7847-7886
                    if (foundEntity == _currentTarget)
                    {
                        if (foundEntity.IsNpc)
                            InteractWithNpc(foundEntity);
                        else
                            TryStartAttack(foundEntity);
                        return;
                    }

                    SelectTarget(foundEntity);
                    return;
            }

            if (foundPlayer != null)
            {
                // If player is selling in merchant mode, click triggers opening their shop
                if (WorldBuilder.Instance != null && WorldBuilder.Instance.IsPlayerMerchant((int)foundPlayer.CharId))
                {
                    EntropyOnline.Trade.KOMerchantManager.Instance?.SendMerchantItemList((int)foundPlayer.CharId);
                    return;
                }

                SelectPlayerTarget(foundPlayer);
                return;
            }

            // ============================================
            // C++ birebir: GameProcMain.cpp:7817-7843 — World shape event
            // Sol tık → m_pObjectTarget = PickWithShape(...)
            // Sağ tık → if (pShape == m_pObjectTarget && pShape->m_iEventID)
            //           → MsgSend_ObjectEvent(eventID, npcID)
            //
            // Unity'de: tek tıkla etkileşim (mobil UX — sağ tık yok)
            // ============================================
            if (foundWorldEvent != null)
            {
                InteractWithWorldEvent(foundWorldEvent);
                return;
            }

            // Open-KO: TargetSelect(-1, false) → target bar gizle
            ClearTarget();
        }

        /// <summary>
        /// NPC etkileşimi başlat.
        /// Open-KO birebir: GameProcMain.cpp:7863-7884
        ///   1. !IsHostileTarget(pNPC) — aynı ülke kontrolü (NPC'ler her zaman friendly)
        ///   2. fD = (playerPos - npcPos).Magnitude()
        ///   3. fDLimit = (playerRadius + npcRadius) * 3.0f
        ///   4. fD > fDLimit → "NPC çok uzakta" mesajı
        ///   5. Uygunsa: ActionMove(PSM_STOP) + pNPC->RotateTo(player) + MsgSend_NPCEvent(iID)
        /// </summary>
        public void InteractWithNpc(KOEntity npc)
        {
            // Open-KO: NPC'ler her zaman friendly, IsHostileTarget kontrolü geçer

            // Mesafe kontrolü — Open-KO birebir (GameProcMain.cpp:7869-7874)
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null) return;

            float fD = Vector3.Distance(player.transform.position, npc.transform.position);

            // Open-KO birebir: fDLimit = (s_pPlayer->Radius() + pNPC->Radius()) * 3.0f
            float playerRadius = 0.5f;
            var playerCol = player.GetComponent<CapsuleCollider>();
            if (playerCol != null)
                playerRadius = playerCol.radius * player.transform.localScale.x;

            float npcRadius = 0.5f;
            var npcCol = npc.GetComponent<CapsuleCollider>();
            if (npcCol != null)
                npcRadius = npcCol.radius * npc.transform.localScale.x;

            float fDLimit = (playerRadius + npcRadius) * 3.0f;
            if (npc.ActType == 2) // Gate'ler için mesafe limiti (normal NPC'lerin 2 katı)
                fDLimit = 6.0f;

            if (fD > fDLimit)
            {
                // Open-KO birebir: IDS_ERR_REQUEST_NPC_EVENT_SO_FAR
                return;
            }

            // Open-KO birebir: pNPC->RotateTo(s_pPlayer)
            // Object NPC'ler (Magic Anvil vb.) oyuncuya dönmez
            if (!npc.IsObjectNpc)
            {
                Vector3 dirToPlayer = (player.transform.position - npc.transform.position);
                dirToPlayer.y = 0;
                if (dirToPlayer.sqrMagnitude > 0.001f)
                    npc.transform.rotation = Quaternion.LookRotation(dirToPlayer.normalized);
            }

            // Open-KO birebir: MsgSend_NPCEvent(iID) — GameProcMain.cpp:4307-4317
            if (npc.IsObjectNpc)
            {
                var worldEvent = npc.GetComponentInChildren<KOWorldEvent>();
                int eventId = worldEvent != null ? worldEvent.EventID : npc.NpcId; // fallback
                SendObjectEvent(eventId, (int)npc.ServerInstanceId);
            }
            else
            {
                if (ShopUI.Instance != null)
                    ShopUI.Instance.SetNpcId((int)npc.ServerInstanceId);

                KONpcEventHandler.SendNpcEvent(npc);
            }
        }

        /// <summary>
        /// World shape event etkileşimi.
        /// Open-KO birebir: GameProcMain.cpp:7817-7843
        ///   1. pShape = ACT_WORLD->PickWithShape(...)
        ///   2. if (pShape == m_pObjectTarget && pShape->m_iEventID)
        ///   3. fD = (playerPos - shapePos).Magnitude()
        ///   4. fDLimit = (playerRadius + shapeRadius) * 2.0f
        ///   5. OBJECT_TYPE_WARP_POINT(2) → MsgSend_ObjectEvent(eventID, npcID)
        ///   6. OBJECT_TYPE_BINDPOINT(1) → MessageBox("Bind here?")
        /// </summary>
        private void InteractWithWorldEvent(KOWorldEvent worldEvent)
        {
            if (worldEvent == null || worldEvent.EventID == 0) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null) return;

            // C++ birebir mesafe kontrolü: GameProcMain.cpp:7821-7831
            float fD = Vector3.Distance(player.transform.position, worldEvent.transform.position);

            // C++ birebir: fDLimit = (s_pPlayer->Radius() + pShape->Radius()) * 2.0f
            float playerRadius = 0.5f;
            var playerCol = player.GetComponent<CapsuleCollider>();
            if (playerCol != null)
                playerRadius = playerCol.radius * player.transform.localScale.x;

            // Shape radius: mesh bounds'tan tahmin
            float shapeRadius = 3.0f; // Gate'ler büyük yapılar — varsayılan geniş
            var renderers = worldEvent.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                shapeRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }

            float fDLimit = (playerRadius + shapeRadius) * 2.0f;
            // Gate'ler için minimum mesafe limiti
            fDLimit = Mathf.Max(fDLimit, 15.0f);

            if (fD > fDLimit)
            {
                // C++ birebir: IDS_ERR_REQUEST_OBJECT_EVENT_SO_FAR
                return;
            }

            // C++ birebir: GameProcMain.cpp:7839-7841
            // OBJECT_TYPE_WARP_POINT(5) → MsgSend_ObjectEvent(eventID, npcID)
            if (worldEvent.EventType == 5) // OBJECT_TYPE_WARP_POINT / OBJECT_TYPE_WARP_GATE
            {
                SendObjectEvent(worldEvent.EventID, worldEvent.NPC_ID);
            }
            else if (worldEvent.EventType == 0) // OBJECT_TYPE_BINDPOINT / OBJECT_TYPE_BIND
            {
                if (KOMessageBox.Instance != null)
                {
                    // C++ birebir: GameProcMain.cpp:7830-7835: MessageBoxPost(szMsg, "", MB_YESNO, BEHAVIOR_REQUEST_BINDPOINT)
                    KOMessageBox.Instance.ShowYesNo(
                        "Burayı yeniden doğma noktanız olarak kaydetmek istiyor musunuz?",
                        "",
                        MsgBoxBehavior.BEHAVIOR_REQUEST_BINDPOINT,
                        () => SendObjectEvent(worldEvent.EventID, worldEvent.NPC_ID),
                        null
                    );
                }
                else
                {
                    SendObjectEvent(worldEvent.EventID, worldEvent.NPC_ID);
                }
            }
            else
            {
                // Bilinmeyen event type — yine de gönder
                SendObjectEvent(worldEvent.EventID, worldEvent.NPC_ID);
            }
        }

        /// <summary>
        /// Open-KO birebir: GameProcMain::MsgSend_ObjectEvent (GameProcMain.cpp:4322-4333)
        ///   CAPISocket::MP_AddByte(byBuff, iOffset, WIZ_OBJECT_EVENT);
        ///   CAPISocket::MP_AddShort(byBuff, iOffset, iEventID);
        ///   CAPISocket::MP_AddShort(byBuff, iOffset, iNPC_ID);
        ///   s_pSocket->Send(byBuff, iOffset);
        /// </summary>
        private void SendObjectEvent(int eventID, int npcID)
        {
            var netMgr = EntropyOnline.Network.KO.KONetworkManager.Instance;
            if (netMgr == null || !netMgr.IsConnected) return;

            using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                EntropyOnline.Network.KO.WizOpcode.WIZ_OBJECT_EVENT);
            pkt.WriteInt16((short)eventID);
            pkt.WriteInt16((short)npcID);
            netMgr.SendPacket(pkt);

        }

        /// <summary>
        /// Saldırı başlat.
        /// Open-KO birebir: GameProcMain::TryStartAttack (GameProcMain.cpp:7581-7609)
        ///   - IsAttackableTarget(pTarget, false) → menzildeyse:
        ///     - CommandMove(MD_STOP, true)
        ///     - CommandEnableAttackContinous(true, pTarget) → StartAutoAttack
        ///   - Menzilde değilse:
        ///     - CommandMove(MD_FORWARD, true)
        ///     - SetMoveTargetID(m_iIDTarget) → hedefe yürü, menzile girince saldır
        /// </summary>
        private void TryStartAttack(KOEntity target)
        {
            // C++ birebir: IsHostileTarget kontrolü (PlayerBase.cpp:2532)
            // NPC (IsNpc=true) → her zaman etkileşim, saldırı yok
            if (target == null || target.IsNpc) return;

            // C++ birebir: Nation kontrolü — aynı nation hostile değil
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null && target.Nation != 0 && target.Nation == gm.Nation) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null) return;

            var playerCtrl = player.GetComponent<EntropyOnline.Character.PlayerController>();
            if (playerCtrl == null) return;

            // Open-KO birebir: IsAttackableTarget(pTarget, false) — cpp:7592
            float fD = Vector3.Distance(player.transform.position, target.transform.position);

            // Open-KO birebir: AttackableDistance — PlayerMySelf.cpp:730-743
            float playerRadius = 0.5f;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) playerRadius = cc.radius;

            float targetRadius = 0.5f;
            var targetCol = target.GetComponent<CapsuleCollider>();
            if (targetCol != null)
                targetRadius = targetCol.radius * target.transform.localScale.x;

            float fDistLimit = (playerRadius + targetRadius) / 2.0f;  // PlayerMySelf.cpp:735

            // Open-KO birebir: AttackableDistance — PlayerMySelf.cpp:736-740
            // m_pItemPlugBasics[0] (sağ el) varsa → siAttackRange / 10.0f
            // yoksa m_pItemPlugBasics[1] (sol el) varsa VE byAttachPoint == ITEM_POS_TWOHANDLEFT → siAttackRange / 10.0f
            // m_pItemPlugBasics = s_pTbl_Items_Basic.Find(dwItemID / 1000 * 1000)
            var koInv = EntropyOnline.UI.KOInventory.Instance;
            if (koInv != null && EntropyOnline.UI.KOInventory.s_pTbl_Items_Basic != null)
            {
                var tbl = EntropyOnline.UI.KOInventory.s_pTbl_Items_Basic;

                // cpp:736-737: m_pItemPlugBasics[0] (sağ el — PLUG_POS_RIGHTHAND)
                var rhSlot = koInv.m_pMySlot[EntropyOnline.UI.KOInventory.ITEM_SLOT_HAND_RIGHT];
                if (rhSlot != null && !rhSlot.IsEmpty
                    && tbl.TryGetValue((uint)(rhSlot.itemId / 1000 * 1000), out var rhBasic))
                {
                    fDistLimit += rhBasic.siAttackRange / 10.0f;
                }
                else
                {
                    // cpp:738-740: m_pItemPlugBasics[1] (sol el — PLUG_POS_LEFTHAND)
                    // && ITEM_POS_TWOHANDLEFT == m_pItemPlugBasics[1]->byAttachPoint
                    var lhSlot = koInv.m_pMySlot[EntropyOnline.UI.KOInventory.ITEM_SLOT_HAND_LEFT];
                    if (lhSlot != null && !lhSlot.IsEmpty
                        && tbl.TryGetValue((uint)(lhSlot.itemId / 1000 * 1000), out var lhBasic)
                        && lhBasic.byAttachPoint == EntropyOnline.UI.KOInventory.ITEM_ATTACH_POS_TWOHAND_LEFT)
                    {
                        fDistLimit += lhBasic.siAttackRange / 10.0f;
                    }
                }
            }

            if (fD <= fDistLimit)
            {
                // Open-KO birebir: cpp:7594-7595 — menzilde → dur + saldır
                playerCtrl.StartAutoAttack(target);
            }
            else
            {
                // Open-KO birebir: cpp:7599-7600 — uzakta → hedefe yürü
                playerCtrl.SetMoveTargetID(target);
            }
        }

        /// <summary>
        /// Target seç.
        /// Open-KO birebir: GameProcMain::TargetSelect(CPlayerNPC*) (satır 5545-5601)
        /// </summary>
        private void SelectTarget(KOEntity target)
        {
            if (Character.PlayerController.Instance != null && Character.PlayerController.Instance.IsBlinded)
                return;

            if (_currentTarget == target)
                return;

            _currentTarget = target;
            _currentTargetPlayer = null; // Clear player target

            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                gm.TargetId = target.ServerInstanceId;
                gm.TargetIsPlayer = false; // Monster/NPC
            }

            if (_targetBarPanel != null)
                _targetBarPanel.SetActive(true);

            // Configure Monster Icon
            if (_targetIconImg != null)
            {
                _targetIconImg.sprite = _spriteMonsterIcon;
                _targetIconImg.color = target.IsNpc ? Color.white : Color.red;
            }
            Color bgColor = target.IsNpc ? new Color(0.03f, 0.11f, 0.05f, 0.95f) : new Color(0.24f, 0.03f, 0.03f, 0.95f);
            Color frameColor = new Color(0.6f, 0.48f, 0.22f, 1f); // Always bronze-gold border!

            if (_targetIconBgImg != null)
            {
                _targetIconBgImg.color = bgColor;
            }
            if (_targetIconFrameImg != null)
            {
                _targetIconFrameImg.color = frameColor;
            }

            // Configure Level text
            if (_targetLevelText != null)
            {
                if (target.IsNpc)
                {
                    _targetLevelText.text = ""; // NPCs don't show level
                }
                else
                {
                    var monster = target.GetComponent<EntropyOnline.Combat.MonsterEntity>();
                    _targetLevelText.text = monster != null ? $"Lv.{monster.Level}" : "Lv.1";
                }
            }

            // Configure buttons
            if (_targetActionButtonsPanel != null)
            {
                if (target.IsNpc)
                {
                    _targetActionButtonsPanel.SetActive(false); // Hide buttons for NPCs
                }
                else
                {
                    _btnInvite.gameObject.SetActive(false);
                    _btnPM.gameObject.SetActive(false);
                    _btnInfo.gameObject.SetActive(false);
                    _btnShowDrops.gameObject.SetActive(true);
                    _targetActionButtonsPanel.SetActive(true);
                }
            }

            if (_targetNameText != null)
            {
                _targetNameText.text = target.EntityName;
                _targetNameText.color = target.GetTargetColor();

                Canvas.ForceUpdateCanvases();

            }

            if (target.ServerInstanceId >= 0)
            {
                using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                    EntropyOnline.Network.KO.WizOpcode.WIZ_TARGET_HP);
                pkt.WriteInt16((short)target.ServerInstanceId);
                pkt.WriteByte((byte)1); // 1=npc
                EntropyOnline.Network.KO.KONetworkManager.Instance?.SendPacket(pkt);
            }

            UpdateHPBar();
            UpdateTargetSymbol();
        }

        private void SelectPlayerTarget(RemotePlayerEntity player)
        {
            if (Character.PlayerController.Instance != null && Character.PlayerController.Instance.IsBlinded)
                return;

            if (player == null) return;
            if (_currentTargetPlayer == player)
                return;

            _currentTargetPlayer = player;
            _currentTarget = null; // Clear NPC/monster target

            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                gm.TargetId = player.CharId;
                gm.TargetIsPlayer = true;
            }

            if (_targetBarPanel != null)
                _targetBarPanel.SetActive(true);

            // Configure Player Icon
            if (_targetIconImg != null)
            {
                int classBase = player.CharClass % 100;
                int mainClass = classBase switch
                {
                    1 or 5 or 6 => 1,   // Warrior
                    2 or 7 or 8 => 2,   // Rogue
                    3 or 9 or 10 => 3,  // Mage
                    4 or 11 or 12 => 4, // Priest
                    _ => 1
                };
                Sprite classSprite = Resources.Load<Sprite>($"UI/class_icon_{mainClass}");
                if (classSprite != null)
                {
                    _targetIconImg.sprite = classSprite;
                    _targetIconImg.color = Color.white;
                }
                else
                {
                    _targetIconImg.sprite = _spritePlayerIcon;
                    _targetIconImg.color = Color.green;
                }
                // Set dynamic background and frame colors based on friendly/enemy faction
                bool isEnemy = gm != null && gm.ZoneAbilityType != 0 && gm.Nation != player.Nation;
                Color bgColor = isEnemy ? new Color(0.24f, 0.03f, 0.03f, 0.95f) : new Color(0.03f, 0.11f, 0.05f, 0.95f);
                Color frameColor = new Color(0.6f, 0.48f, 0.22f, 1f); // Always bronze-gold border!

                if (_targetIconBgImg != null)
                {
                    _targetIconBgImg.color = bgColor;
                }
                if (_targetIconFrameImg != null)
                {
                    _targetIconFrameImg.color = frameColor;
                }


            }

            // Configure Level text
            if (_targetLevelText != null)
            {
                _targetLevelText.text = $"Lv.{player.Level}";
            }

            // Configure buttons
            if (_targetActionButtonsPanel != null)
            {
                _btnInvite.gameObject.SetActive(true);
                _btnPM.gameObject.SetActive(true);
                _btnInfo.gameObject.SetActive(true);
                _btnShowDrops.gameObject.SetActive(false);
                _targetActionButtonsPanel.SetActive(true);
            }

            if (_targetNameText != null)
            {
                _targetNameText.text = player.PlayerName;
                
                bool nameIsEnemy = gm != null && gm.Nation != player.Nation;
                _targetNameText.color = nameIsEnemy ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 0.5f, 1f);

                Canvas.ForceUpdateCanvases();
            }

            if (player.CharId >= 0)
            {
                using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                    EntropyOnline.Network.KO.WizOpcode.WIZ_TARGET_HP);
                pkt.WriteInt16((short)player.CharId);
                pkt.WriteByte(0); // 0=user (Player)
                EntropyOnline.Network.KO.KONetworkManager.Instance?.SendPacket(pkt);
            }

            UpdateHPBar();
            UpdateTargetSymbol();
        }

        public void ClearTarget()
        {
            _currentTarget = null;
            _currentTargetPlayer = null;

            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                gm.TargetId = -1;
                gm.TargetIsPlayer = false;
            }

            var pc = Object.FindAnyObjectByType<EntropyOnline.Character.PlayerController>();
            if (pc != null)
            {
                pc.StopAutoAttack();
                pc.CancelMoveToTarget();
            }

            if (_targetBarPanel != null)
                _targetBarPanel.SetActive(false);

            if (_targetActionButtonsPanel != null)
                _targetActionButtonsPanel.SetActive(false);

            if (_targetSymbolRoot != null)
                _targetSymbolRoot.SetActive(false);
        }

        private void UpdateTargetBar()
        {
            // Unity-safe null check: Destroy() sonrası C# referans null olmaz,
            // ama Unity'nin == operator override'ı "== null" true döndürür.
            // ReferenceEquals yerine implicit bool kullanıyoruz.
            if ((_currentTarget == null || !_currentTarget) && (_currentTargetPlayer == null || !_currentTargetPlayer))
            {
                ClearTarget();
                return;
            }

            UpdateHPBar();
            UpdateTargetSymbol();
        }

        /// <summary>
        /// HP bar güncelleme.
        /// Open-KO birebir: N3UIProgress::UpdateFrGndImage() LEFT2RIGHT (N3UIProgress.cpp:162-177)
        ///   rcRegion.right = m_rcRegion.left + (int)((m_rcRegion.right - m_rcRegion.left) * fPercentage)
        ///   frcUVRect.right = frcUVRect.left + (frcUVRect.right - frcUVRect.left) * fraction
        ///
        /// Open-KO: UITargetBar::UpdateHP (UITargetBar.cpp:36-51)
        ///   iPercentage = iHP * 100 / iHPMax
        ///   m_pProgress_HP->SetRange(0, 100) — range 0-100
        /// </summary>
        private void UpdateHPBar()
        {
            if (_hpBarFgRt == null)
                return;

            if (_currentTarget != null)
            {
                if (_currentTarget.MaxHP > 0)
                {
                    float ratio = Mathf.Clamp01((float)_currentTarget.CurrentHP / _currentTarget.MaxHP);
                    SetHPBarRatio(ratio);
                }
                else
                {
                    SetHPBarRatio(1f);
                }
            }
            else if (_currentTargetPlayer != null)
            {
                if (_currentTargetPlayer.MaxHp > 0)
                {
                    float ratio = Mathf.Clamp01((float)_currentTargetPlayer.CurrentHp / _currentTargetPlayer.MaxHp);
                    SetHPBarRatio(ratio);
                }
                else
                {
                    SetHPBarRatio(1f);
                }
            }
        }

        private void SetHPBarRatio(float ratio)
        {
            if (_hpBarFgRt != null)
            {
                _hpBarFgRt.anchorMax = new Vector2(ratio, 1f);
            }
        }

        /// <summary>
        /// Target symbol (selection highlight) güncelle.
        /// Open-KO birebir: GameProcMain::RenderTarget() (GameProcMain.cpp:717-764)
        ///   pTarget = CharacterGetByID(m_iIDTarget, false)
        ///   fScale = pTarget->Radius() * 2.0f
        ///   fYScale = pTarget->Height() * 1.3f
        ///   m_pTargetSymbol->ScaleSet(fScale, fYScale, fScale)
        ///   m_pTargetSymbol->PosSet(pTarget->Position())
        /// </summary>
        private void UpdateTargetSymbol()
        {
            if (_targetSymbolRoot == null) return;

            Transform targetTransform = null;
            CapsuleCollider col = null;

            if (_currentTarget != null)
            {
                targetTransform = _currentTarget.transform;
                col = _currentTarget.GetComponent<CapsuleCollider>();
            }
            else if (_currentTargetPlayer != null)
            {
                targetTransform = _currentTargetPlayer.transform;
                col = _currentTargetPlayer.GetComponent<CapsuleCollider>();
                if (col == null)
                    col = _currentTargetPlayer.GetComponentInChildren<CapsuleCollider>();
            }

            bool hideTargetFX = false;
            if (EntropyOnline.UI.GameOptionsManager.Instance != null && EntropyOnline.UI.GameOptionsManager.Instance.Effect_HideTargetFX)
            {
                hideTargetFX = true;
            }

            if (targetTransform == null || hideTargetFX)
            {
                _targetSymbolRoot.SetActive(false);
                return;
            }

            _targetSymbolRoot.SetActive(true);

            float fScale = 1f;
            float fYScale = 1f;

            if (col != null)
            {
                float scaleY = targetTransform.localScale.y;
                fYScale = (col.height * scaleY) * 1.3f;
                
                // Calculate selection circle radius using mesh renderers to avoid capsule height clamping on flat/wide monsters
                var renderers = targetTransform.GetComponentsInChildren<Renderer>();
                float maxBoundRadius = 0f;
                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    Bounds b = r.bounds;
                    float rad = Mathf.Max(b.extents.x, b.extents.z);
                    if (rad > maxBoundRadius) maxBoundRadius = rad;
                }
                
                float scaleX = targetTransform.localScale.x;
                float boundsScale = maxBoundRadius * 1.7f;
                float colliderScale = (col.radius * scaleX) * 2.0f;
                fScale = Mathf.Max(boundsScale, colliderScale);
            }

            _targetSymbolRoot.transform.localScale = new Vector3(fScale, fYScale, fScale);
            Vector3 targetPos = targetTransform.position;
            if (col != null)
            {
                Vector3 worldCenter = col.transform.TransformPoint(col.center);
                targetPos.x = worldCenter.x;
                targetPos.z = worldCenter.z;
            }
            _targetSymbolRoot.transform.position = targetPos;
        }

        /// <summary>
        /// Target bar UI oluştur — co_targetbar_us.uif birebir.
        ///
        /// Panel pozisyonu — GameProcMain.cpp:3968 birebir:
        ///   SetPos((iW - (rc.right - rc.left)) / 2, 0)
        ///   iW = CN3Base::s_CameraData.vp.Width — viewport genişliği
        ///   Y = 0 — ekranın en üstü
        ///
        /// .uif root'ta arka plan image child YOK. Sadece:
        ///   1x CN3UIString (text_target) + 4x CN3UIProgress (pro_target + slow/drop/lasting)
        /// </summary>
        private void CreateTargetBarUI()
        {
            // Load custom UI sprites
            _spritePlayerIcon = LoadSpriteFromTexture("UI/target_icon_player");
            _spriteMonsterIcon = LoadSpriteFromTexture("UI/target_icon_monster");
            _spriteFrameIcon = LoadSpriteFromTexture("UI/target_icon_frame");

            // Screen Space Canvas
            var canvasObj = new GameObject("TargetBarCanvas");
            canvasObj.transform.SetParent(null);
            canvasObj.transform.localScale = Vector3.one;
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 105; // KOUIManager canvas'ı (100) üstünde render et
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Root panel — .uif "target" ID, Region=(0,0,324,64)
            _targetBarPanel = new GameObject("TargetBarPanel");
            _targetBarPanel.transform.SetParent(canvasObj.transform, false);
            var panelRt = _targetBarPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 1f);
            panelRt.anchorMax = new Vector2(0.5f, 1f);
            panelRt.pivot = new Vector2(0.5f, 1f);
            panelRt.sizeDelta = new Vector2(UIF_ROOT_W, UIF_ROOT_H);
            panelRt.anchoredPosition = new Vector2(12.5f, 0f); // Shifted by 12.5f to visually center the diamond + HP bar block on the screen!

            // === target_icon_frame ===
            // Diamond Frame on the left
            _targetIconFrameObj = new GameObject("TargetIconFrame");
            _targetIconFrameObj.transform.SetParent(_targetBarPanel.transform, false);
            _targetIconFrameImg = _targetIconFrameObj.AddComponent<Image>();
            _targetIconFrameImg.color = new Color(0.6f, 0.48f, 0.22f, 1f); // Gold border color
            _targetIconFrameImg.raycastTarget = false;
            _targetIconFrameObj.transform.localEulerAngles = new Vector3(0, 0, 45); // Rotate 45 degrees for diamond!

            var frameRt = _targetIconFrameObj.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 1f);
            frameRt.anchorMax = new Vector2(0.5f, 1f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.anchoredPosition = new Vector2(-111f, -34f); // Centered vertically with HP bar, overlays HP bar left edge (which starts at -100)
            frameRt.sizeDelta = new Vector2(28f, 28f);

            // TargetIconBG (child of Frame)
            var bgObj = new GameObject("TargetIconBG");
            bgObj.transform.SetParent(_targetIconFrameObj.transform, false);
            _targetIconBgImg = bgObj.AddComponent<Image>();
            
            // Create a solid 2x2 white texture for solid masking of the diamond shape
            Texture2D texBg = new Texture2D(2, 2);
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    texBg.SetPixel(x, y, Color.white);
                }
            }
            texBg.Apply();
            _targetIconBgImg.sprite = Sprite.Create(texBg, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _targetIconBgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f); // Dark background inside border
            
            // Add Mask component to clip the icon to the rotated diamond shape
            bgObj.AddComponent<Mask>().showMaskGraphic = true;

            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(24f, 24f);

            // === target_icon ===
            // Target Type Icon inside Frame (counter-rotated to stay upright)
            _targetIconObj = new GameObject("TargetIcon");
            _targetIconObj.transform.SetParent(bgObj.transform, false);
            _targetIconImg = _targetIconObj.AddComponent<Image>();
            _targetIconImg.sprite = _spritePlayerIcon;
            _targetIconImg.color = Color.white;
            _targetIconImg.raycastTarget = false;
            _targetIconObj.transform.localEulerAngles = new Vector3(0, 0, -45); // Counter-rotate to stay upright

            var iconRt = _targetIconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(28f, 28f); // Larger to completely fill the 24x24 diamond mask!

            // === text_target ===
            var nameObj = new GameObject("text_target");
            nameObj.transform.SetParent(_targetBarPanel.transform, false);
            _targetNameText = nameObj.AddComponent<TextMeshProUGUI>();
            _targetNameText.alignment = TextAlignmentOptions.Center;
            _targetNameText.fontSize = 16;
            _targetNameText.fontStyle = FontStyles.Bold;
            _targetNameText.textWrappingMode = TextWrappingModes.NoWrap;
            _targetNameText.overflowMode = TextOverflowModes.Overflow;
            _targetNameText.raycastTarget = false;
            _targetNameText.color = Color.white;
            _targetNameText.outlineWidth = 0.2f;
            _targetNameText.outlineColor = Color.black;
            var nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.5f, 1f);
            nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -4f);
            nameRt.sizeDelta = new Vector2(300f, 20f);

            // === pro_target — HP Bar ===
            _hpBarContainer = new GameObject("pro_target");
            _hpBarContainer.transform.SetParent(_targetBarPanel.transform, false);
            var hpContainerRt = _hpBarContainer.AddComponent<RectTransform>();
            hpContainerRt.anchorMin = new Vector2(0.5f, 1f);
            hpContainerRt.anchorMax = new Vector2(0.5f, 1f);
            hpContainerRt.pivot = new Vector2(0.5f, 1f);
            hpContainerRt.anchoredPosition = new Vector2(0f, -UIF_PRO_T);
            hpContainerRt.sizeDelta = new Vector2(UIF_PRO_R - UIF_PRO_L, UIF_PRO_B - UIF_PRO_T);

            int barW = (int)(UIF_PRO_R - UIF_PRO_L); // 149
            int barH = (int)(UIF_PRO_B - UIF_PRO_T); // 16

            // Background slanted image
            var hpBgObj = new GameObject("HPBarBG");
            hpBgObj.transform.SetParent(_hpBarContainer.transform, false);
            _hpBarBg = hpBgObj.AddComponent<Image>();
            var hpBgRt = hpBgObj.GetComponent<RectTransform>();
            hpBgRt.anchorMin = Vector2.zero;
            hpBgRt.anchorMax = Vector2.one;
            hpBgRt.offsetMin = Vector2.zero;
            hpBgRt.offsetMax = Vector2.zero;
            _hpBarBg.sprite = GetArrowSprite("tgt_hp_bar_bg_no_border", barW, barH, 0, 7, new Color(0.12f, 0.02f, 0.02f, 0.85f), Color.clear, 0);

            // HP Bar Mask Parent
            var maskObj = new GameObject("HPBarMask");
            maskObj.transform.SetParent(_hpBarContainer.transform, false);
            var maskRt = maskObj.AddComponent<RectTransform>();
            maskRt.anchorMin = Vector2.zero;
            maskRt.anchorMax = Vector2.one;
            maskRt.offsetMin = Vector2.zero;
            maskRt.offsetMax = Vector2.zero;
            var maskImg = maskObj.AddComponent<Image>();
            maskImg.sprite = GetArrowSprite("tgt_hp_bar_bg_mask_shape", barW, barH, 0, 7, Color.white, Color.clear, 0);
            maskObj.AddComponent<Mask>().showMaskGraphic = false;

            // Foreground image (child of Mask)
            var fgObj = new GameObject("HPBarFG");
            fgObj.transform.SetParent(maskObj.transform, false);
            _hpBarFg = fgObj.AddComponent<Image>();
            _hpBarFgRt = fgObj.GetComponent<RectTransform>();
            _hpBarFgRt.anchorMin = Vector2.zero;
            _hpBarFgRt.anchorMax = Vector2.one;
            _hpBarFgRt.pivot = new Vector2(0f, 0.5f);
            _hpBarFgRt.offsetMin = new Vector2(2f, 2f); // Padding
            _hpBarFgRt.offsetMax = new Vector2(-2f, -2f);
            _hpBarFg.sprite = GetArrowSprite("tgt_hp_fill_slanted_white", barW - 4, barH - 4, 0, 5, Color.white, Color.clear, 0, horizontalGradient: true);
            _hpBarFg.color = new Color(0.796f, 0.004f, 0.035f, 0.95f); // #cb0109

            // Golden Border (drawn on top)
            _hpBorderObj = new GameObject("HPBorder");
            _hpBorderObj.transform.SetParent(_hpBarContainer.transform, false);
            var borderRt = _hpBorderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            _hpBorderImg = _hpBorderObj.AddComponent<Image>();
            _hpBorderImg.sprite = GetArrowSprite("tgt_hp_bar_border", barW, barH, 0, 7, Color.clear, new Color(0.6f, 0.48f, 0.22f, 0.9f), 2);

            // Make sure the diamond frame renders on top of the HP bar container and level text
            if (_targetIconFrameObj != null)
            {
                _targetIconFrameObj.transform.SetAsLastSibling();
            }

            // === text_level ===
            // Target Level text (parented to HP bar container to overlay on top)
            _targetLevelTextObj = new GameObject("text_level");
            _targetLevelTextObj.transform.SetParent(_hpBarContainer.transform, false);
            _targetLevelText = _targetLevelTextObj.AddComponent<TextMeshProUGUI>();
            _targetLevelText.alignment = TextAlignmentOptions.Left;
            _targetLevelText.fontSize = 11;
            _targetLevelText.fontStyle = FontStyles.Bold;
            _targetLevelText.color = new Color(0.2f, 1f, 0.2f); // Light green
            _targetLevelText.outlineWidth = 0.2f;
            _targetLevelText.outlineColor = Color.black;
            _targetLevelText.text = "Lv.83";
            _targetLevelText.raycastTarget = false;

            var lvlRt = _targetLevelTextObj.GetComponent<RectTransform>();
            lvlRt.anchorMin = new Vector2(0f, 0.5f);
            lvlRt.anchorMax = new Vector2(0f, 0.5f);
            lvlRt.pivot = new Vector2(0f, 0.5f);
            lvlRt.anchoredPosition = new Vector2(11f, 0f); // 11px padding inside HP bar left edge (shifted 5px right)
            lvlRt.sizeDelta = new Vector2(50f, 16f);

            _targetActionButtonsPanel = new GameObject("TargetActionButtonsPanel");
            _targetActionButtonsPanel.transform.SetParent(_targetBarPanel.transform, false);
            var buttonsRt = _targetActionButtonsPanel.AddComponent<RectTransform>();
            buttonsRt.anchorMin = new Vector2(0.5f, 1f);
            buttonsRt.anchorMax = new Vector2(0.5f, 1f);
            buttonsRt.pivot = new Vector2(0.5f, 1f);
            buttonsRt.anchoredPosition = new Vector2(0f, -42f);
            buttonsRt.sizeDelta = new Vector2(190f, 26f);

            var layout = _targetActionButtonsPanel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Generate capsule background sprites for buttons
            Sprite btnBg = CreateCapsuleSprite(62, 24, 11f, new Color(0.08f, 0.15f, 0.28f, 0.85f));
            Sprite btnBgDrops = CreateCapsuleSprite(90, 24, 11f, new Color(0.08f, 0.15f, 0.28f, 0.85f));

            // Create buttons
            _btnInvite = CreateActionButton("Btn_Invite", "Invite", btnBg, 62f);
            _btnPM = CreateActionButton("Btn_PM", "PM", btnBg, 62f);
            _btnInfo = CreateActionButton("Btn_Info", "Info", btnBg, 62f);
            _btnShowDrops = CreateActionButton("Btn_ShowDrops", "Show Drops", btnBgDrops, 90f);

            // Add click listeners
            _btnInvite.onClick.AddListener(OnInviteClicked);
            _btnPM.onClick.AddListener(OnPMClicked);
            _btnInfo.onClick.AddListener(OnInfoClicked);
            _btnShowDrops.onClick.AddListener(OnShowDropsClicked);

            _targetBarPanel.SetActive(false);
            _targetActionButtonsPanel.SetActive(false);
        }

        private Sprite CreateCapsuleSprite(int w, int h, float r, Color color)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = x;
                    float cy = y;
                    float targetX = -1f;
                    float targetY = -1f;

                    if (x < r) targetX = r;
                    else if (x > w - 1 - r) targetX = w - 1 - r;

                    if (y < r) targetY = r;
                    else if (y > h - 1 - r) targetY = h - 1 - r;

                    if (targetX >= 0f && targetY >= 0f)
                    {
                        float dx = cx - targetX;
                        float dy = cy - targetY;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float aa = 1.5f;
                        if (dist > r + aa / 2f) tex.SetPixel(x, y, Color.clear);
                        else if (dist < r - aa / 2f) tex.SetPixel(x, y, color);
                        else
                        {
                            float alpha = 0.5f - (dist - r) / aa;
                            tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(alpha)));
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, color);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        private Button CreateActionButton(string name, string label, Sprite bg, float width = 62f)
        {
            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(_targetActionButtonsPanel.transform, false);
            
            var rt = btnObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 24f);

            var btn = btnObj.AddComponent<Button>();
            StyleActionButton(btn, label, bg);
            return btn;
        }

        private void StyleActionButton(Button btn, string label, Sprite bgSprite)
        {
            var img = btn.gameObject.GetComponent<Image>() ?? btn.gameObject.AddComponent<Image>();
            img.sprite = bgSprite;
            img.color = Color.white;
            btn.targetGraphic = img;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btn.transform, false);
            var rt = txtObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.color = Color.white;
            txt.fontSize = 11;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
        }

        private void OnInviteClicked()
        {
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm == null || gm.TargetId < 0) return;

            if (PartyUI.Instance != null)
            {
                PartyUI.Instance.SendPartyInvite(gm.TargetId);
            }
        }

        private void OnPMClicked()
        {
            string targetName = _currentTargetPlayer != null ? _currentTargetPlayer.PlayerName : (_currentTarget != null ? _currentTarget.EntityName : "");

            if (!string.IsNullOrEmpty(targetName) && EntropyOnline.UI.KOWhisperManager.Instance != null)
            {
                EntropyOnline.UI.KOWhisperManager.Instance.ShowWhisperWindow(targetName);
            }
        }

        private void OnInfoClicked()
        {
            string targetName = _currentTargetPlayer != null ? _currentTargetPlayer.PlayerName : (_currentTarget != null ? _currentTarget.EntityName : "");

            if (_currentTargetPlayer != null)
            {
                if (EntropyOnline.UI.KOInspectManager.Instance != null)
                {
                    EntropyOnline.UI.KOInspectManager.Instance.RequestInspect(_currentTargetPlayer.CharId);
                }
            }
            else
            {
                EntropyOnline.UI.KOUIManager.Instance?.ShowToast("You can only inspect other players.");
            }
        }

        private void OnShowDropsClicked()
        {
            string targetName = _currentTarget != null ? _currentTarget.EntityName : "";

            if (_currentTarget != null && !_currentTarget.IsNpc)
            {
                if (KODropSearchUI.Instance != null)
                {
                    KODropSearchUI.Instance.OpenForMonster(_currentTarget.NpcId);
                }
            }
        }

        // Public erişim
        public static KOTargetSelector Instance { get; private set; }
        public KOEntity CurrentTarget => _currentTarget;
        public KOEntity SelectedEntity => _currentTarget;
        public RemotePlayerEntity CurrentTargetPlayer => _currentTargetPlayer;

        /// <summary>
        /// Open-KO birebir: GameProcMain::TargetSelect(int iID, bool bMustAlive)
        /// GameProcMain.cpp:5539-5543
        ///
        /// CPlayerNPC* pTarget = s_pOPMgr->CharacterGetByID(iID, bMustAlive);
        /// this->TargetSelect(pTarget);
        ///
        /// C++'da CPlayerOther ve CPlayerNPC aynı base class'tan türer.
        /// Unity'de KOEntity (monster/NPC) ve RemotePlayerEntity (uzak oyuncu) ayrı sınıflar.
        /// İkisini de taramamız gerekiyor — birebir CharacterGetByID karşılığı.
        /// </summary>
        public void SelectTargetByID(long entityId, bool bMustAlive)
        {
            // Open-KO birebir: s_pOPMgr->CharacterGetByID(iID, bMustAlive)
            // 1. KOEntity (monster/NPC) — ServerInstanceId ile eşleşme
            var koEntities = FindObjectsByType<KOEntity>(FindObjectsInactive.Exclude);
            foreach (var e in koEntities)
            {
                if (e.ServerInstanceId == entityId)
                {
                    if (bMustAlive && e.IsDead) continue;
                    SelectTarget(e);
                    return;
                }
            }

            // 2. RemotePlayerEntity (uzak oyuncu) — CharId ile eşleşme
            // C++'da CPlayerOther da CPlayerNPC* olarak döner.
            var rpes = FindObjectsByType<RemotePlayerEntity>(FindObjectsInactive.Exclude);
            foreach (var rpe in rpes)
            {
                if (rpe.CharId == entityId)
                {
                    if (bMustAlive && !rpe.IsAlive) continue;

                    SelectPlayerTarget(rpe);
                    return;
                }
            }

            // Open-KO birebir: pTarget == nullptr → TargetSelect(-1, false)
            // cpp:5591 — m_iIDTarget = -1, target bar gizle
            ClearTarget();
        }

        public void TargetNearestMonster()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null) return;

            if (EntityManager.Instance == null) return;

            // Z-Fix: If we already have a target, cycle to the next nearest
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.PK_ZFix && _currentTarget != null)
            {
                var candidates = new System.Collections.Generic.List<KOEntity>();
                foreach (var kvp in EntityManager.Instance.Monsters)
                {
                    var mv = kvp.Value;
                    if (mv == null || mv.Root == null) continue;
                    var entity = mv.Root.GetComponent<KOEntity>();
                    if (entity == null || entity.IsDead || entity.IsNpc) continue;

                    float dist = Vector3.Distance(player.transform.position, mv.Root.transform.position);
                    if (dist < 40f)
                    {
                        candidates.Add(entity);
                    }
                }

                if (candidates.Count > 1)
                {
                    candidates.Sort((a, b) => Vector3.Distance(player.transform.position, a.transform.position)
                        .CompareTo(Vector3.Distance(player.transform.position, b.transform.position)));

                    int currentIndex = candidates.FindIndex(x => x == _currentTarget);
                    if (currentIndex != -1)
                    {
                        int nextIndex = (currentIndex + 1) % candidates.Count;
                        SelectTarget(candidates[nextIndex]);
                        return;
                    }
                }
            }

            KOEntity closest = null;
            float minDist = 40f; // Range limit

            foreach (var kvp in EntityManager.Instance.Monsters)
            {
                var mv = kvp.Value;
                if (mv == null || mv.Root == null) continue;

                var entity = mv.Root.GetComponent<KOEntity>();
                if (entity == null || entity.IsDead || entity.IsNpc) continue;

                float dist = Vector3.Distance(player.transform.position, mv.Root.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = entity;
                }
            }

            if (closest != null)
            {
                SelectTarget(closest);
            }
        }

        public void TargetNearestEnemy()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null) return;

            if (EntityManager.Instance == null) return;

            // Z-Fix: If we already have a target, cycle to the next nearest (monsters & remote players unified)
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.PK_ZFix && (_currentTarget != null || _currentTargetPlayer != null))
            {
                var candidates = new System.Collections.Generic.List<GameObject>();
                // Scan monsters
                foreach (var kvp in EntityManager.Instance.Monsters)
                {
                    var mv = kvp.Value;
                    if (mv == null || mv.Root == null) continue;
                    var entity = mv.Root.GetComponent<KOEntity>();
                    if (entity == null || entity.IsDead || entity.IsNpc) continue;

                    float dist = Vector3.Distance(player.transform.position, mv.Root.transform.position);
                    if (dist < 40f) candidates.Add(mv.Root);
                }
                // Scan remote players (enemies only)
                var gm = EntropyOnline.Core.GameManager.Instance;
                if (gm != null)
                {
                    foreach (var kvp in EntityManager.Instance.GetAllRemotePlayers())
                    {
                        var rpv = kvp.Value;
                        if (rpv == null || rpv.Root == null || rpv.CurrentHp <= 0 || rpv.Nation == gm.Nation) continue;
                        var rpe = rpv.Root.GetComponent<RemotePlayerEntity>();
                        if (rpe == null) continue;

                        float dist = Vector3.Distance(player.transform.position, rpv.Root.transform.position);
                        if (dist < 40f) candidates.Add(rpv.Root);
                    }
                }

                if (candidates.Count > 1)
                {
                    candidates.Sort((a, b) => Vector3.Distance(player.transform.position, a.transform.position)
                        .CompareTo(Vector3.Distance(player.transform.position, b.transform.position)));

                    GameObject currentGO = _currentTarget != null ? _currentTarget.gameObject : _currentTargetPlayer.gameObject;
                    int currentIndex = candidates.FindIndex(x => x == currentGO);
                    if (currentIndex != -1)
                    {
                        int nextIndex = (currentIndex + 1) % candidates.Count;
                        var nextGO = candidates[nextIndex];
                        var nextMonster = nextGO.GetComponent<KOEntity>();
                        var nextPlayer = nextGO.GetComponent<RemotePlayerEntity>();
                        if (nextPlayer != null)
                        {
                            SelectTargetByID(nextPlayer.CharId, true);
                        }
                        else if (nextMonster != null)
                        {
                            SelectTarget(nextMonster);
                        }
                        return;
                    }
                }
            }

            float minDist = 40f; // Range limit
            KOEntity closestMonster = null;
            RemotePlayerEntity closestPlayer = null;

            // 1. Scan monsters
            foreach (var kvp in EntityManager.Instance.Monsters)
            {
                var mv = kvp.Value;
                if (mv == null || mv.Root == null) continue;

                var entity = mv.Root.GetComponent<KOEntity>();
                if (entity == null || entity.IsDead || entity.IsNpc) continue;

                float dist = Vector3.Distance(player.transform.position, mv.Root.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestMonster = entity;
                    closestPlayer = null;
                }
            }

            // 2. Scan remote players (enemies only)
            var gm2 = EntropyOnline.Core.GameManager.Instance;
            if (gm2 != null)
            {
                foreach (var kvp in EntityManager.Instance.GetAllRemotePlayers())
                {
                    var rpv = kvp.Value;
                    if (rpv == null || rpv.Root == null || rpv.CurrentHp <= 0 || rpv.Nation == gm2.Nation) continue;

                    var rpe = rpv.Root.GetComponent<RemotePlayerEntity>();
                    if (rpe == null) continue;

                    float dist = Vector3.Distance(player.transform.position, rpv.Root.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestPlayer = rpe;
                        closestMonster = null;
                    }
                }
            }

            if (closestMonster != null)
            {
                SelectTarget(closestMonster);
            }
            else if (closestPlayer != null)
            {
                SelectTargetByID(closestPlayer.CharId, true);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        private Sprite GetArrowSprite(string key, int w, int h, int leftSlant, int rightArrowSlant, Color fillColor, Color borderColor, int borderWidth, bool horizontalGradient = false)
        {
            string cacheKey = $"{key}_{w}_{h}_{leftSlant}_{rightArrowSlant}_{fillColor}_{borderColor}_{borderWidth}_{horizontalGradient}";
            if (_slantedSpriteCache.TryGetValue(cacheKey, out Sprite cached))
            {
                if (cached != null) return cached;
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            int absLeftSlant = Mathf.Abs(leftSlant);
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                float startX = (leftSlant >= 0) ? (t * absLeftSlant) : ((1 - t) * absLeftSlant);
                float endX = w - 2f * Mathf.Abs(t - 0.5f) * rightArrowSlant;

                for (int x = 0; x < w; x++)
                {
                    Color pixelColor = Color.clear;
                    if (x >= startX && x <= endX)
                    {
                        bool isBorder = false;
                        if (x < startX + borderWidth || y < borderWidth || y >= h - borderWidth)
                        {
                            isBorder = true;
                        }
                        else
                        {
                            float rightBorderLimit = endX - borderWidth;
                            if (x > rightBorderLimit)
                            {
                                isBorder = true;
                            }
                        }

                        if (isBorder && borderWidth > 0)
                        {
                            pixelColor = borderColor;
                        }
                        else
                        {
                            if (horizontalGradient)
                            {
                                float horizontalT = (float)x / (w - 1);
                                Color leftColor = new Color(fillColor.r * 0.5f, fillColor.g * 0.5f, fillColor.b * 0.5f, fillColor.a);
                                pixelColor = Color.Lerp(leftColor, fillColor, horizontalT);
                            }
                            else
                            {
                                pixelColor = fillColor;
                            }
                        }
                    }
                    tex.SetPixel(x, y, pixelColor);
                }
            }

            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _slantedSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        private Sprite GetSlantedSprite(string key, int w, int h, int slant, Color fillColor, Color borderColor, int borderWidth)
        {
            string cacheKey = $"{key}_{w}_{h}_{slant}_{fillColor}_{borderColor}_{borderWidth}";
            if (_slantedSpriteCache.TryGetValue(cacheKey, out Sprite cached))
            {
                if (cached != null) return cached;
            }

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            int absSlant = Mathf.Abs(slant);
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                float startX = 0f;
                float endX = 0f;

                if (slant >= 0)
                {
                    startX = t * absSlant;
                    endX = w - (absSlant - t * absSlant);
                }
                else
                {
                    startX = (1 - t) * absSlant;
                    endX = w - t * absSlant;
                }

                for (int x = 0; x < w; x++)
                {
                    Color pixelColor = Color.clear;
                    if (x >= startX && x <= endX)
                    {
                        if (x < startX + borderWidth || x > endX - borderWidth || y < borderWidth || y >= h - borderWidth)
                        {
                            pixelColor = borderColor;
                        }
                        else
                        {
                            pixelColor = fillColor;
                        }
                    }
                    tex.SetPixel(x, y, pixelColor);
                }
            }

            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _slantedSpriteCache[cacheKey] = sprite;
            return sprite;
        }

        private Sprite LoadSpriteFromTexture(string path)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[TARGET] Could not load texture from Resources: {path}");
                return null;
            }
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}
