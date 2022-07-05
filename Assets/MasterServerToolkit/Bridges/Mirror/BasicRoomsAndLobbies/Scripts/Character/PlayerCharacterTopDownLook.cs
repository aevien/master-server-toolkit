#if MIRROR
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterInput))]
    public class PlayerCharacterTopDownLook : PlayerCharacterLook
    {
        #region INSPECTOR

        [Header("TD Look Settings"), SerializeField]
        private Vector3 lookAtPoint = Vector3.zero;
        [SerializeField, Range(1f, 5f)]
        private float followSmoothTime = 2f;

        [Header("TD Distance Settings"), SerializeField, Range(5f, 100f)]
        private float minDistance = 5f;
        [SerializeField, Range(5f, 100f)]
        private float maxDistance = 15f;
        [SerializeField, Range(5f, 100f)]
        private float startDistance = 15f;
        [SerializeField, Range(1f, 15f)]
        private float distanceSmoothTime = 5f;
        [SerializeField, Range(0.01f, 1f)]
        private float distanceScrollPower = 1f;
        [SerializeField]
        private bool applyOffsetDistance = true;
        [SerializeField, Range(1f, 25f)]
        private float maxOffsetDistance = 5f;

        [Header("TD Rotation Settings"), SerializeField, Range(35f, 90f)]
        private float pitchAngle = 65f;

        [Header("TD Screen Padding Settings"), SerializeField, Range(5f, 300f)]
        private float minHorizontalPadding = 100f;
        [SerializeField, Range(5f, 300f)]
        private float minVerticalPadding = 100f;
        [SerializeField, Range(5f, 300f)]
        private float maxHorizontalPadding = 100f;
        [SerializeField, Range(5f, 300f)]
        private float maxVerticalPadding = 100f;
        [SerializeField]
        private bool usePadding = false;

        #endregion

        private GameObject cameraRoot = null;
        private GameObject cameraYPoint = null;
        private GameObject cameraXPoint = null;

        private float currentCameraDistance = 0f;
        private float currentCameraYawAngle = 0f;

        /// <summary>
        /// 
        /// </summary>
        protected Vector3 cameraOffsetPosition = Vector3.zero;

        public override bool IsReady => base.IsReady
            && cameraRoot
            && cameraYPoint
            && cameraXPoint;

        protected override void Update()
        {
            if (isLocalPlayer && IsReady)
            {
                UpdateCameraDistance();
                UpdateCameraPosition();
                UpdateCameraOffset();
                UpdateCameraRotation();
                UpdateCameraCollision();
            }
        }

        /// <summary>
        /// When local player is created
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            RecalculateDistance();
            CreateCameraControls();
        }

        /// <summary>
        /// Create camera control elements
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
            }
            else
            {
                initialCameraPosition = lookCamera.transform.position;
                initialCameraRotation = lookCamera.transform.rotation;
            }

            cameraRoot = new GameObject("--PLAYER_CAMERA_ROOT");

            cameraYPoint = new GameObject("CamYPoint");
            cameraYPoint.transform.SetParent(cameraRoot.transform);
            cameraYPoint.transform.localPosition = Vector3.zero;
            cameraYPoint.transform.localRotation = Quaternion.identity;

            cameraXPoint = new GameObject("CamXPoint");
            cameraXPoint.transform.SetParent(cameraYPoint.transform);
            cameraXPoint.transform.localPosition = Vector3.zero;
            cameraXPoint.transform.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);

            lookCamera.transform.SetParent(cameraXPoint.transform);
            lookCamera.transform.localPosition = new Vector3(0f, 0f, startDistance * -1f);
            lookCamera.transform.localRotation = Quaternion.identity;

            cameraRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);

            cameraOffsetPosition = transform.position;
        }

        /// <summary>
        /// Just calculates the distance. If max distance less than min
        /// </summary>
        protected void RecalculateDistance()
        {
            if (minDistance >= maxDistance)
            {
                maxDistance = minDistance + 1f;
            }

            currentCameraDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        }

        /// <summary>
        /// 
        /// </summary>
        protected void UpdateCameraCollision()
        {
            if (!useCollisionDetection) return;

            Vector3 startPoint = transform.position + lookAtPoint;

            var ray = new Ray(startPoint, lookCamera.transform.forward * maxDistance * -1f);
            Debug.DrawRay(startPoint, lookCamera.transform.forward * maxDistance * -1f);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance))
            {
                cameraCollisionDistance = Vector3.Distance(startPoint, hitInfo.point);

                isCameraCollided = true;
            }
            else
            {
                isCameraCollided = false;
            }
        }

        /// <summary>
        /// Updates camera offset in front of character
        /// </summary>
        protected void UpdateCameraOffset()
        {
            if (applyOffsetDistance && inputController.IsMoving() && !IsCharacterOutOfBounds() && !isCameraCollided)
            {
                var newCameraOffsetPosition = transform.forward * maxOffsetDistance + transform.position;
                cameraOffsetPosition = Vector3.Lerp(cameraOffsetPosition, newCameraOffsetPosition, Time.deltaTime * 3f);
            }
            else
            {
                cameraOffsetPosition = Vector3.Lerp(cameraOffsetPosition, transform.position, Time.deltaTime * 3f);
            }
        }

        /// <summary>
        /// Updates camera position
        /// </summary>
        protected override void UpdateCameraPosition()
        {
            if (applyOffsetDistance && !isCameraCollided)
            {
                cameraRoot.transform.position = Vector3.Lerp(cameraRoot.transform.position, cameraOffsetPosition + lookAtPoint, Time.deltaTime * followSmoothTime);
            }
            else
            {
                cameraRoot.transform.position = transform.position + lookAtPoint;
            }
        }

        /// <summary>
        /// Updates camera rotation
        /// </summary>
        protected override void UpdateCameraRotation()
        {
            // Если нажата кнопка мыши вращения камеры
            if (inputController.IsRotateCameraMode())
            {
                // Устанавливаем новый угол камеры
                currentCameraYawAngle += inputController.MouseX() * rotationSencitivity;
            }

            // Интерполируем угол камеры плавно
            float newAngle = Mathf.LerpAngle(cameraRoot.transform.rotation.eulerAngles.y, currentCameraYawAngle, Time.deltaTime * 4f);
            cameraRoot.transform.rotation = Quaternion.Euler(0f, newAngle, 0f);
        }

        /// <summary>
        /// Updates distance between camera and character
        /// </summary>
        protected override void UpdateCameraDistance()
        {
            if (isCameraCollided && cameraCollisionDistance < currentCameraDistance)
            {
                lookCamera.transform.localPosition = Vector3.Lerp(lookCamera.transform.localPosition, new Vector3(0f, 0f, (cameraCollisionDistance - 0.5f) * -1f), Time.deltaTime * collisionDstanceSmoothTime);
            }
            else
            {
                currentCameraDistance = Mathf.Clamp(currentCameraDistance - (inputController.MouseVerticalScroll() * distanceScrollPower), minDistance, maxDistance);
                lookCamera.transform.localPosition = Vector3.Lerp(lookCamera.transform.localPosition, new Vector3(0f, 0f, currentCameraDistance * -1f), Time.deltaTime * distanceSmoothTime);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool IsCharacterOutOfBounds()
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            if (usePadding) return (screenPos.x <= minHorizontalPadding || screenPos.y <= minVerticalPadding || screenPos.x >= Screen.width - maxHorizontalPadding || screenPos.y >= Screen.height - maxVerticalPadding);
            else return false;
        }

        /// <summary>
        /// Gets camera root rotation angle in <see cref="Quaternion"/>
        /// </summary>
        /// <returns></returns>
        public override Quaternion GetCameraRotation()
        {
            return cameraRoot.transform.rotation;
        }

        /// <summary>
        /// Direction to the point at what the character is looking in armed mode
        /// </summary>
        /// <returns></returns>
        public override Vector3 AimDirection()
        {
            if (inputController.MouseToWorldHitPoint(out RaycastHit hit))
            {
                return hit.point - transform.position;
            }
            else
            {
                return Vector3.forward;
            }
        }
    }
}
#endif