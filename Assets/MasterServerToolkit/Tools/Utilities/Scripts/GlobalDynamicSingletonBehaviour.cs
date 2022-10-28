using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class GlobalDynamicSingletonBehaviour<T> : DynamicSingletonBehaviour<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            //isGlobal = true;
            base.Awake();
        }
    }
}