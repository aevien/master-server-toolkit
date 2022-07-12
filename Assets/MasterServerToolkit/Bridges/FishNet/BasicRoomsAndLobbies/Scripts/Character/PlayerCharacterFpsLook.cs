#if FISHNET
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterInput))]
    public class PlayerCharacterFpsLook : PlayerCharacterLook
    {
        #region INSPECTOR

        [Header("Positioning"), SerializeField]
        protected Vector3 cameraPoint = new Vector3(0, 1.75f, 0.15f);

        [Header("Input Settings"), SerializeField]
        protected Vector2Int lookSensitivity = new Vector2Int(8, 8);
        [SerializeField, Range(-90, 0)]
        protected float minLookAngle = -60f;
        [SerializeField, Range(0, 90)]
        protected float maxLookAngle = 60f;

        [Header("Smoothness Settings"), SerializeField]
        protected bool useSmoothness = true;
        [SerializeField, Range(0.01f, 1f)]
        protected float smoothnessTime = 0.1f;

        #endregion

        /// <summary>
        /// Current camera and character rotation
        /// </summary>
        private Vector3 cameraRotation;

        /// <summary>
        /// Velocity of smoothed rotation vector
        /// </summary>
        private Vector3 currentCameraRotationVelocity;

        /// <summary>
        /// Smoothed rotation vector
        /// </summary>
        private Vector3 smoothedCameraRotation;

        /// <summary>
        /// Check if this behaviour is ready
        /// </summary>
        public override bool IsReady => lookCamera && inputController;

        protected override void Update()
        {
            if (base.IsOwner && IsReady)
            {
                UpdateCameraPosition();
                UpdateCameraRotation();
            }
        }

        protected virtual void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position + transform.rotation * cameraPoint, 0.1f);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner)
                CreateCameraControls();
        }

        /// <summary>
        /// Setup player camera to <see cref="lookCamera"/> field
        /// </summary>
        protected override void CreateCameraControls()
        {
            if (lookCamera == null)
                lookCamera = Camera.main;

            if (lookCamera == null)
            {
                var cameraObject = new GameObject("--PlayerCamera");
                var cameraComponent = cameraObject.AddComponent<Camera>();

                lookCamera = cameraComponent;
            }

            if (lookCamera.transform.parent != null)
            {
                initialCameraPosition = lookCamera.transform.localPosition;
                initialCameraRotation = lookCamera.transform.localRotation;

                initialCameraParent = lookCamera.transform.parent;
                lookCamera.transform.SetParent(null);
            }
            else
            {
                initialCameraPosition = lookCamera.transform.position;
                initialCameraRotation = lookCamera.transform.rotation;
            }
        }

        protected override void UpdateCameraPosition()
        {
            Vector3 newCameraPosition = transform.position + transform.rotation * cameraPoint;
            lookCamera.transform.position = newCameraPosition;
        }

        protected override void UpdateCameraRotation()
        {
            cameraRotation.y += inputController.MouseX() * lookSensitivity.x;
            cameraRotation.x = Mathf.Clamp(cameraRotation.x - inputController.MouseY() * lookSensitivity.y, minLookAngle, maxLookAngle);

            if (useSmoothness)
            {
                transform.rotation = Quaternion.Euler(0f, smoothedCameraRotation.y, 0f);
                smoothedCameraRotation = Vector3.SmoothDamp(smoothedCameraRotation, cameraRotation, ref currentCameraRotationVelocity, smoothnessTime);
                lookCamera.transform.rotation = Quaternion.Euler(smoothedCameraRotation.x, smoothedCameraRotation.y, 0f);
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, cameraRotation.y, 0f);
                lookCamera.transform.rotation = Quaternion.Euler(cameraRotation.x, cameraRotation.y, 0f);
            }
        }
    }
}
#endif