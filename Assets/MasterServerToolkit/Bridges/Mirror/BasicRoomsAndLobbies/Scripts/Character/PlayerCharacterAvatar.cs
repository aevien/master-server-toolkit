#if MIRROR
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    public class PlayerCharacterAvatar : PlayerCharacterBehaviour
    {
        [Header("Components"), SerializeField]
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