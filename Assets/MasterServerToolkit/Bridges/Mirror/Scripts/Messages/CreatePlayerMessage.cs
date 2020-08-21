#if MIRROR
using Mirror;

namespace MasterServerToolkit.Bridges.Mirror
{
    public class CreatePlayerMessage : IMessageBase
    {
        public void Deserialize(NetworkReader reader) { }
        public void Serialize(NetworkWriter writer) { }
    }
}

#endif