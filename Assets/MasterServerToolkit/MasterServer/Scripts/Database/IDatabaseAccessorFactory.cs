using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class DatabaseAccessorFactory : MonoBehaviour
    {
        public abstract void CreateAccessors();
    }
}