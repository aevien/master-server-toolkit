using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class GlobalDynamicSingletonBehaviour<T> : SingletonBehaviour<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            //isGlobal = true;
            base.Awake();
        }
    }
}