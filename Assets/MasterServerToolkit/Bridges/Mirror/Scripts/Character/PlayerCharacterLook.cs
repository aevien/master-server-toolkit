#if MIRROR
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.Mirror.Character
{
    public class PlayerCharacterLook : PlayerCharacterBehaviour
    {
        #region INSPECTOR

        [Header("Base Look Settings"), SerializeField]
        protected bool resetCameraAfterDestroy = true;

        [Header("Base Look Components"), SerializeField]
        protected Camera lookCamera;
        [SerializeField]
        protected PlayerCharacterInput inputController;

        #endregion

        public override bool IsReady => lookCamera && inputController;

        [SyncVar]
        protected bool lookIsAllowed = true;

        /// <summary>
        /// The starting parent of the camera. It is necessary to return the camera to its original place after the destruction of the current object
        /// </summary>
        protected Transform initialCameraParent;

        /// <summary>
        /// The starting position of the camera. It is necessary to return the camera to its original place after the destruction of the current object
        /// </summary>
        protected Vector3 initialCameraPosition;

        /// <summary>
        /// The starting rotation of the camera. It is necessaryto return the camera to its original angle after the destruction of the current object
        /// </summary>
        protected Quaternion initialCameraRotation;

        protected virtual void OnDestroy()
        {
            if (isLocalPlayer && resetCameraAfterDestroy)
            {
                DetachCamera();
            }
        }

        /// <summary>
        /// Clears camera object and set camera back to its original place
        /// </summary>
        public void DetachCamera()
        {
            if (isLocalPlayer && lookCamera)
            {
                if (initialCameraParent != null)
                {
                    lookCamera.transform.SetParent(initialCameraParent);
                    lookCamera.transform.localPosition = initialCameraPosition;
                    lookCamera.transform.localRotation = initialCameraRotation;
                }
                else
                {
                    lookCamera.transform.SetParent(null);
                    lookCamera.transform.position = initialCameraPosition;
                    lookCamera.transform.rotation = initialCameraRotation;
                }

                lookCamera = null;
            }
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            DetachCamera();
        }

        /// <summary>
        /// Direction to the point at what the character is looking in armed mode
        /// </summary>
        /// <returns></returns>
        public virtual Vector3 AimDirection()
        {
            if (inputController.MouseToWorldHitPoint(out RaycastHit hit))
            {
                return hit.point - (transform.position + new Vector3(0f, 1.4f, 0f));
            }
            else
            {
                return Vector3.forward;
            }
        }

        /// <summary>
        /// Gets camera rotation angle in <see cref="Quaternion"/>
        /// </summary>
        /// <returns></returns>
        public virtual Quaternion GetCameraRotation()
        {
            return lookCamera.transform.rotation;
        }
    }
}
#endif