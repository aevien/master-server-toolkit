using System;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public struct PermissionEntry
    {
        public string key;
        public int permissionLevel;
    }
}