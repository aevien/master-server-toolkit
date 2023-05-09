using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableProfilePopulator : MonoBehaviour
    {
        public abstract void Populate(ObservableProfile profile);
    }
}