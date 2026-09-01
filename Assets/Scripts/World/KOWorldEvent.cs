using UnityEngine;

namespace EntropyOnline.World
{
    /// <summary>
    /// Open-KO birebir: CN3Shape event bilgisi (N3Shape.h:56-58)
    ///   m_iEventID   — sunucuya gönderilen event ID
    ///   m_iEventType — 0=none, 1=bindpoint, 2=warp_point
    ///   m_iNPC_ID    — ilişkili NPC'nin ID'si
    ///
    /// C++ akışı (GameProcMain.cpp:7817-7843):
    ///   1. Sol tık → m_pObjectTarget = ACT_WORLD->PickWithShape(...)
    ///   2. Sağ tık → pShape == m_pObjectTarget && pShape->m_iEventID
    ///      → MsgSend_ObjectEvent(pShape->m_iEventID, pShape->m_iNPC_ID)
    ///
    /// Unity'de: KOTargetSelector raycast hit'te bu component'ı arar.
    /// </summary>
    public class KOWorldEvent : MonoBehaviour
    {
        /// <summary>C++ CN3Shape::m_iEventID — sunucuya gönderilen event kimliği</summary>
        public int EventID;

        /// <summary>
        /// C++ CN3Shape::m_iEventType — globals.h:370-379 birebir
        ///   0 = OBJECT_TYPE_BIND / OBJECT_TYPE_BINDPOINT
        ///   5 = OBJECT_TYPE_WARP_GATE / OBJECT_TYPE_WARP_POINT
        ///   7 = OBJECT_TYPE_REMOVE_BIND
        /// </summary>
        public int EventType;

        /// <summary>C++ CN3Shape::m_iNPC_ID — ilişkili NPC'nin sunucu ID'si</summary>
        public int NPC_ID;
    }
}
