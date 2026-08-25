#if FISHNET
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    [DisallowMultipleComponent]
    public class PlayerCharacterAvatar : PlayerCharacterBehaviour
    {
        [Header("Components"), SerializeField, Tooltip("Render objects hidden for the owning client and shown for remote clients. Assign first-person body or head meshes that would obstruct the local camera.")]
        protected GameObject[] remoteParts;

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetPartsActive(!IsOwner);
        }

        public virtual void SetPartsActive(bool value)
        {
            if (remoteParts != null)
            {
                foreach (var part in remoteParts)
                {
                    part.SetActive(value);
                }
            }
        }
    }
}
#endif
