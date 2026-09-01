using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO birebir: GameProcMain::MsgSend_NPCEvent (GameProcMain.cpp:4307-4317)
    ///
    /// Client sadece WIZ_NPC_EVENT + short(npcSid) gönderir.
    /// NPC tipine göre ne yapılacağına SUNUCU karar verir (User.cpp:4911-5060).
    /// Client, sunucunun yanıtını (WIZ_TRADE_NPC, WIZ_WAREHOUSE, WIZ_CLIENT_EVENT vb.)
    /// alıp ilgili handler'da işler.
    /// </summary>
    public static class KONpcEventHandler
    {
        public static long LastInteractedNpcInstanceId;
        public static int LastInteractedNpcTemplateId;

        /// <summary>
        /// Open-KO birebir: openko-ref GameProcMain.cpp:4307-4317
        ///   CAPISocket::MP_AddByte(byBuff, iOffset, WIZ_NPC_EVENT);
        ///   CAPISocket::MP_AddShort(byBuff, iOffset, siIDTarget);
        ///   s_pSocket->Send(byBuff, iOffset);
        ///
        /// Sunucu tarafı (openko-ref User.cpp:4921):
        ///   nid = GetShort(pBuf, index);  // sadece int16!
        /// </summary>
        public static void SendNpcEvent(KOEntity npc)
        {
            if (npc == null) return;

            LastInteractedNpcInstanceId = npc.ServerInstanceId;
            LastInteractedNpcTemplateId = npc.NpcId;

            var netMgr = EntropyOnline.Network.KO.KONetworkManager.Instance;
            if (netMgr != null && netMgr.IsConnected)
            {
                // openko-ref birebir: sadece WIZ_NPC_EVENT + int16(npcId)
                using var pkt = new EntropyOnline.Network.KO.KOPacketWriter(
                    EntropyOnline.Network.KO.WizOpcode.WIZ_NPC_EVENT);
                pkt.WriteInt16((short)npc.ServerInstanceId); // sNpcID — int16
                netMgr.SendPacket(pkt);

                // WarpUI'a son etkileşilen NPC ID'sini bildir
                if (EntropyOnline.UI.WarpUI.Instance != null)
                    EntropyOnline.UI.WarpUI.Instance.SetLastEventNpcId((ushort)npc.ServerInstanceId);

            }
            else
            {
            }
        }
    }
}

