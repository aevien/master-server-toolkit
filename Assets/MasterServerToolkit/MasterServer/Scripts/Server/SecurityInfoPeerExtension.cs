using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class SecurityInfoPeerExtension : IPeerExtension
    {
        public int PermissionLevel { get; set; }
        public string AesKey { get; set; }
        public byte[] AesKeyEncrypted { get; set; }
        public Guid UniqueGuid { get; set; }
        public IPeer Peer { get; private set; }
        public SecurityInfoPeerExtension() { }
    }
}