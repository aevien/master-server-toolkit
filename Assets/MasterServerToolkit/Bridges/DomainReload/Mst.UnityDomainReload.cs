using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
#if UNITY_EDITOR
    public partial class Mst
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RunOnStart()
        {
            Initialize();
        }
    }
#endif
}