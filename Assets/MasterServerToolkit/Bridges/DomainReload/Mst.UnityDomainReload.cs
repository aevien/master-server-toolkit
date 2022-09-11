using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public partial class Mst
    {
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RunOnStart()
        {
            Initialize();
        }
#endif
    }
}