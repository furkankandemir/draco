using UnityEngine;
using EntropyOnline.Network;
using EntropyOnline.Network.KO;
using EntropyOnline.World;

namespace EntropyOnline.UI
{
    /// <summary>
    /// Open-KO birebir: UIWarp.cpp + MsgSend_Warp (GameProcMain.cpp:4344-4365)
    /// 
    /// Warp paket handler + gönderici.
    /// UI artık KOUIManager tarafından el_warp_us.uif'den yükleniyor.
    /// Bu sınıf warp paketlerini işler ve MsgSend_Warp karşılığını sağlar.
    /// 
    /// C++ akış:
    ///   1. Sunucu WIZ_WARP_LIST (type=1) gönderir → MsgRecv_WarpList → m_pUIWarp->InfoAdd + UpdateList + SetVisible
    ///   2. Kullanıcı OK veya çift tıklama → MsgSend_Warp → WIZ_WARP_LIST + [warpId: short]
    ///   3. Sunucu WIZ_WARP_LIST (type=2) gönderir → MsgRecv_WarpList_Error → mesaj göster
    /// </summary>
    public class WarpUI : MonoBehaviour
    {
        public static WarpUI Instance { get; private set; }

        /// <summary>
        /// C++ m_szWarpDestination (GameProcMain.h:155) — son seçilen warp hedef adı.
        /// Başarı mesajında kullanılır.
        /// </summary>
        private string _szWarpDestination;

        /// <summary>
        /// C++ birebir: m_sEventNid (User.h) — son etkileşime girilen NPC'nin runtime ID'si.
        /// WIZ_WARP_LIST gönderilirken npcId olarak kullanılır.
        /// SelectWarpList (User.cpp:3917) → pkt >> npcid >> warpid
        /// </summary>
        private ushort _lastEventNpcId;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            KOPacketHandler.OnWarpList += HandleWarpList_KO;
            // NOT: OnZoneChange artık GameSceneController tarafından handle ediliyor (C++ GameProcMain birebir)
            // Duplicate handler kaldırıldı — ChangeZone() ve ZONE_CHANGE_LOADING 2 kez çağrılıyordu
            KOPacketHandler.OnObjectEvent += HandleObjectEvent_KO;
        }

        private void OnDisable()
        {
            KOPacketHandler.OnWarpList -= HandleWarpList_KO;
            KOPacketHandler.OnObjectEvent -= HandleObjectEvent_KO;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ============================
        // PACKET HANDLERS
        // ============================

        /// <summary>
        /// Open-KO birebir: MsgRecv_WarpList (GameProcMain.cpp:6775-6804)
        ///   m_pUIWarp->Reset()
        ///   for (i=0..count) m_pUIWarp->InfoAdd(WI)
        ///   m_pUIWarp->UpdateList()
        ///   m_pUIWarp->SetVisible(true)
        /// </summary>
        /// <summary>KO wrapper — WIZ_WARP_LIST response</summary>
        private void HandleWarpList_KO(byte[] rawData)
        {
            // Open-KO birebir: MsgRecv_WarpList (GameProcMain.cpp:6763-6805)
            // Wire: [opcode][subOpcode:byte]
            //   subOpcode=2 → error path: [errorCode:byte][...]
            //   subOpcode=1 → [count:int16][count × WarpInfo]
            var r = new KOPacketReader(rawData);
            byte subOpcode = r.ReadByte();

            if (subOpcode == 2)
            {
                // C++ cpp:6807: MsgRecv_WarpList_Error
                byte errorCode = r.ReadByte();
                HandleWarpListError(errorCode, $"Warp hatası: code={errorCode}");
                return;
            }

            if (subOpcode != 1) return;

            short count = r.ReadInt16();
            if (count <= 0) return;

            var entries = new WarpListEntry[count];
            for (int i = 0; i < count; i++)
            {
                // openko-ref birebir: User.cpp:9908-9923 — GetWarpList()
                // Wire: [warpId:int16][name:KOString][announce:KOString][zone:int16][maxUser:int16][pay:uint32][x:int16][z:int16][y:int16]
                entries[i] = new WarpListEntry
                {
                    WarpId = r.ReadInt16(),
                    Name = r.ReadKOString(),
                    Agreement = r.ReadKOString(),
                    TargetZoneId = r.ReadInt16(),
                    MaxUser = r.ReadInt16(),
                    GoldCost = (int)r.ReadUInt32(),
                    PosX = r.ReadInt16(),
                    PosZ = r.ReadInt16(),
                    PosY = r.ReadInt16()
                };
            }

            HandleWarpList(entries);
        }

        /// <summary>KO wrapper — WIZ_ZONE_CHANGE</summary>
        private void HandleZoneChange_KO(byte[] rawData)
        {
            // openko-ref birebir: User.cpp:2751-2758
            // Server sends: WIZ_ZONE_CHANGE << uint8(ZONE_CHANGE_TELEPORT)
            //               << uint8(zoneId) << uint8(subzone) << uint16(x*10) << uint16(z*10) << int16(y*10) << uint8(victory)
            var r = new KOPacketReader(rawData);
            byte subOpcode = r.ReadByte();

            const byte ZONE_CHANGE_TELEPORT = 1;
            const byte ZONE_CHANGE_LOADED = 3;

            if (subOpcode == ZONE_CHANGE_TELEPORT)
            {
                // openko-ref birebir: User.cpp:2753-2758
                byte zoneId = r.ReadByte();          // SetByte(m_pUserData->m_bZone)
                byte zoneSub = r.ReadByte();         // SetByte(0) — subzone
                float fX = r.ReadUInt16() / 10.0f;  // SetShort(x*10)
                float fZ = r.ReadUInt16() / 10.0f;  // SetShort(z*10)
                float fY = r.ReadInt16() / 10.0f;   // SetShort(y*10)
                byte victoryNation = r.ReadByte();   // SetByte(victory)

                HandleZoneChange(true, (short)zoneId, fX, fY, fZ);
            }
            else if (subOpcode == ZONE_CHANGE_LOADED)
            {
            }
        }

        private void HandleWarpList(WarpListEntry[] warpPoints)
        {

            // C++ cpp:6775-6804: KOUIManager'dan warp panelini göster ve listeyi doldur
            if (KOUIManager.Instance != null)
            {
                KOUIManager.Instance.PopulateWarpList(warpPoints);
                KOUIManager.Instance.ShowWarp(true);
            }
        }

        /// <summary>
        /// Open-KO birebir: MsgRecv_WarpList_Error (GameProcMain.cpp:6807-6856)
        /// errorCode'a göre doğru mesajı gösterir.
        /// </summary>
        private void HandleWarpListError(byte errorCode, string rawMessage)
        {
            // C++ birebir: MsgRecv_WarpList_Error (GameProcMain.cpp:6812-6855)
            string szMsg;
            switch (errorCode)
            {
                case 1: // WARP_LIST_ERROR_SUCCESS
                    // C++ cpp:6815: IDS_WARP_ARRIVED_AT → "{destination}'a ulaştınız"
                    szMsg = !string.IsNullOrEmpty(_szWarpDestination)
                        ? $"Arrived at {_szWarpDestination}."
                        : "Teleport successful.";
                    break;
                case 2: // WARP_LIST_ERROR_MIN_LEVEL
                    szMsg = "Your level is too low for this warp.";
                    break;
                case 3: // WARP_LIST_ERROR_NOT_DURING_CSW
                    szMsg = "You cannot warp during castle siege.";
                    break;
                case 4: // WARP_LIST_ERROR_NOT_DURING_WAR
                    szMsg = "You cannot warp during war.";
                    break;
                case 5: // WARP_LIST_ERROR_NEED_LOYALTY
                    szMsg = "You need more loyalty points to warp.";
                    break;
                case 6: // WARP_LIST_ERROR_WRONG_LEVEL_DLW
                    szMsg = "You must be between level 30 and 50.";
                    break;
                case 7: // WARP_LIST_ERROR_DO_NOT_QUALIFY
                    szMsg = "You do not qualify for this warp.";
                    break;
                default:
                    szMsg = $"Warp error: code={errorCode}";
                    break;
            }


            if (KOUIManager.Instance != null)
            {
                // C++ cpp:6816,6824,6830,6835,6840: MsgOutput(szMsg, 0xFFFFFF00)
                KOUIManager.Instance.AddMsgOutput(szMsg, KOUIManager.D3DColorToUnity(0xFFFFFF00));
            }
        }

        private void HandleZoneChange(bool success, short zoneId, float spawnX, float spawnY, float spawnZ)
        {
            if (!success)
            {
                Debug.LogWarning("[WARP] Zone değişimi başarısız!");
                return;
            }


            // C++ birebir: InitZone() (GameProcMain.cpp:4430-4520)

            // 1. C++ cpp:4475 — s_pOPMgr->Release() — tüm entity'leri temizle
            if (EntityManager.Instance != null)
                EntityManager.Instance.ClearAll();

            // C++ cpp:4464 — s_pPlayer->m_InfoExt.iZoneCur = iZone
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null)
            {
                gm.CurrentZoneId = zoneId;
            }

            // 2. C++ cpp:4476 — s_pWorldMgr->InitWorld(iZone) — terrain/shape yeniden yükle
            if (WorldBuilder.Instance != null)
                WorldBuilder.Instance.ChangeZone(zoneId);

            // 3. C++ cpp:4519 — InitPlayerPosition(vPosPlayer) — oyuncu pozisyonunu ayarla
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player != null)
            {
                // C++ birebir: sunucu fX/fY/fZ — KO koordinat sistemi
                float terrainY = spawnY;
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    float sampledY = terrain.SampleHeight(new Vector3(spawnX, 0, spawnZ)) + terrain.transform.position.y;
                    terrainY = Mathf.Max(spawnY, sampledY);
                }
                player.transform.position = new Vector3(spawnX, terrainY, spawnZ);

                // C++ birebir: cpp:4451-4452 — s_pPlayer->m_bMoveContinous = true → STOP
                var cc = player.GetComponent<UnityEngine.CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                    player.transform.position = new Vector3(spawnX, terrainY, spawnZ);
                    cc.enabled = true;
                }

            }

            // 4. C++ birebir: cpp:5009-5013 — WIZ_ZONE_CHANGE + ZONE_CHANGE_LOADING gönder
            // Sunucuya "yükleme başladı" bildirimi
            var netMgr = KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                const byte ZONE_CHANGE_LOADING = 2;
                using var pkt = new KOPacketWriter(WizOpcode.WIZ_ZONE_CHANGE);
                pkt.WriteByte(ZONE_CHANGE_LOADING);
                netMgr.SendPacket(pkt);
            }

            // 5. Warp UI'ı kapat, target seçimini temizle
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowWarp(false, true);
            if (KOTargetSelector.Instance != null)
                KOTargetSelector.Instance.ClearTarget();

        }

        // ============================
        // PACKET SEND
        // ============================

        /// <summary>
        /// Open-KO birebir: MsgSend_Warp (GameProcMain.cpp:4344-4365)
        /// 
        /// C++ mantık:
        ///   1. m_pUIWarp->InfoGetCur(WI) — seçili warp bilgisini al
        ///   2. WI.szName.empty() → return
        ///   3. m_szWarpDestination = WI.szName (başarı mesajı için sakla)
        ///   4. Gold kontrolü: s_pPlayer->m_InfoExt.iGold < WI.iGold → hata mesajı + return
        ///   5. WIZ_WARP_LIST + [warpId: int16] gönder — npcId GÖNDERİLMEZ
        /// 
        /// Wire: [WIZ_WARP_LIST(0x4B)] [warpId: int16]
        /// </summary>
        public void SendWarpSelect(short warpId, string warpName, int goldCost)
        {
            // C++ cpp:4347: WI.szName.empty() → return
            if (string.IsNullOrEmpty(warpName))
                return;
            
            // C++ cpp:4353: m_szWarpDestination = WI.szName
            _szWarpDestination = warpName;
            
            // C++ cpp:4355-4359: Gold ön-kontrolü (client-side)
            var gm = EntropyOnline.Core.GameManager.Instance;
            if (gm != null && gm.Gold < goldCost)
            {
                // C++ birebir: GameProcMain.cpp:4357-4358
                // MsgOutput(IDS_TELEPORT_TO_X_NEED_Y_COINS, 0xFFFF3B3B)
                Debug.LogWarning($"[WARP] Yetersiz gold! {warpName} için {goldCost} gold gerekli.");
                if (KOUIManager.Instance != null)
                    KOUIManager.Instance.AddMsgOutput($"You need {goldCost} coins to teleport to {warpName}.", KOUIManager.D3DColorToUnity(0xFFFF3B3B));
                return;
            }
            
            // openko-ref birebir: User.cpp:9755-9766 — SelectWarpList()
            // warpid = GetShort(pBuf, index);  — sunucu SADECE warpId bekler, npcId YOK
            using var pkt = new KOPacketWriter(WizOpcode.WIZ_WARP_LIST);
            pkt.WriteInt16(warpId);             // openko-ref birebir: sadece warpid (int16)
            KONetworkManager.Instance?.SendPacket(pkt);


            // C++ UIWarp.cpp:51: this->SetVisible(false) — panel kapat
            if (KOUIManager.Instance != null)
                KOUIManager.Instance.ShowWarp(false, true);
        }

        // ============================
        // OBJECT EVENT HANDLER
        // ============================

        /// <summary>
        /// C++ birebir: User.cpp:4023-4035 — BindObjectEvent() / SendGateFlag()
        /// WIZ_OBJECT_EVENT handler — gate open/close, bind point, vs.
        /// 
        /// S→C formatı:
        ///   [objectType: uint8] [result: uint8]
        ///   result=1 başarı, result=0 hata
        ///   Gate flag extended: [objectType: uint8] [result: uint8] [npcId: uint16] [isOpen: uint8]
        /// </summary>
        private void HandleObjectEvent_KO(byte[] rawData)
        {
            var r = new KOPacketReader(rawData);
            byte objectType = r.ReadByte();
            byte result = r.ReadByte();


            // Open-KO v1298 birebir: OBJECT_TYPE_ANVIL = 8 (defined in globals.h:382)
            if (objectType == 8)
            {
                short npcId = r.ReadInt16();
                PlayAnvilEffect(npcId, result == 1);
                return;
            }

            if (result == 0)
            {
                Debug.LogWarning($"[OBJECT_EVENT] İşlem başarısız: objectType={objectType}");
                return;
            }

            // Başarılı — objectType'a göre işlem
            switch (objectType)
            {
                case 1: // OBJECT_GATE
                case 2: // OBJECT_BIND
                case 3: // OBJECT_REMOVE_BIND
                    // Bind point güncellendi — şimdilik log
                    break;

                default:
                    break;
            }
        }

        private void PlayAnvilEffect(short npcId, bool success)
        {
            var entity = EntityManager.Instance?.GetEntityByInstanceId(npcId);
            if (entity == null)
            {
                Debug.LogWarning($"[ANVIL_EFFECT] Anvil NPC with ID {npcId} not found in EntityManager!");
                return;
            }

            int fxId = success ? 10101 : 10100;

            KO.KOFXManager.Instance?.TriggerBundle(npcId, 0, fxId, npcId, 0);
        }

        /// <summary>
        /// NPC event gönderildiğinde çağrılır — son etkileşilen NPC ID'sini saklar.
        /// WIZ_WARP_LIST gönderilirken npcId olarak kullanılır.
        /// </summary>
        public void SetLastEventNpcId(ushort npcId)
        {
            _lastEventNpcId = npcId;
        }
    }
}
