#if MIRROR
using Mirror;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class PlayerCharacterLook : PlayerCharacterBehaviour
    {
        #region INSPECTOR

        [Header("Base Components"), SerializeField]
        protected Camera lookCamera;
        [SerializeField]
        protected PlayerCharacterInput inputController;
        [SerializeField]
        protected PlayerCharacterMovement movementController = null;

        [Header("Base Settings"), SerializeField]
        protected bool resetCameraAfterDestroy = true;

        [Header("Base Rotation Settings"), SerializeField, Range(1f, 10f)]
        protected float rotationSencitivity = 3f;

        [Header("Base Collision Settings"), SerializeField, Range(1f, 15f)]
        protected float collisionDstanceSmoothTime = 5f;
        [SerializeField]
        protected bool useCollisionDetection = true;

        #endregion

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

        /// <summary>
        /// Check if camera is collided with something
        /// </summary>
        protected bool isCameraCollided = false;

        /// <summary>
        /// Check if camera is collided with something
        /// </summary>
        protected float cameraCollisionDistance = 0f;

        public override bool IsReady => base.IsReady
            && inputController
            && movementController
            && NetworkClient.ready;

        protected virtual void Update() { }

        public override void OnStartLocalPlayer() { }

        /// <summary>
        /// Clears camera object and set camera back to its original place
        /// </summary>
        protected void DetachCamera()
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

        protected virtual void CreateCameraControls() { }
        protected virtual void UpdateCameraDistance() { }
        protected virtual void UpdateCameraPosition() { }
        protected virtual void UpdateCameraRotation() { }

        /// <summary>
        /// Direction to the point at what the character is looking in armed mode
        /// </summary>
        /// <returns></returns>
        public virtual Vector3 AimDirection()
        {
            return lookCamera.transform.forward;
        }

        /// <summary>
        /// Gets camera root rotation angle in <see cref="Quaternion"/>
        /// </summary>
        /// <returns></returns>
        public virtual Quaternion GetCameraRotation()
        {
            return lookCamera.transform.rotation;
        }
    }
}
#endif