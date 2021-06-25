using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class DatabaseAccessorFactory : MonoBehaviour
    {
        /// <summary>
        /// Creates database or web/rest api accessor to communicate with them.
        /// </summary>
        public abstract void CreateAccessors();
    }
}