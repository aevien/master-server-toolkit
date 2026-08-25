#if MIRROR
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    public class PlayerAvatar : PlayerBehaviour
    {
        [Header("Components"), SerializeField, Tooltip("Render objects that are hidden for the owning client and shown for remote clients. Assign first-person body or head meshes that would obstruct the local camera.")]
        protected GameObject[] remoteParts;

        public override void OnStartClient()
        {
            if (!isLocalPlayer)
            {
                SetPartsActive(true);
            }
        }

        public override void OnStartLocalPlayer()
        {
            SetPartsActive(false);
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
