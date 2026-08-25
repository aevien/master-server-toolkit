#if MIRROR
using Mirror;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class PlayerLook : PlayerBehaviour
    {
        #region INSPECTOR

        [Header("Base Components")]
        [SerializeField, Tooltip("Camera controlled only by the owning client. Assign the gameplay camera used by this player.")]
        protected Camera lookCamera;
        [SerializeField, Tooltip("Required player facade that provides input, movement, avatar, and CharacterController references.")]
        protected Player player;
        [SerializeField, Tooltip("Required pivot whose position and rotation define the owning client's camera point and synchronized look direction.")]
        protected Transform cameraContainer;

        [Header("Base Settings")]
        [SerializeField, Tooltip("Returns the owning client's camera to its original parent and transform when this network player is destroyed.")]
        protected bool resetCameraAfterDestroy = true;

        [Header("Input Settings")]
        [SerializeField, Tooltip("Horizontal and vertical look rotation applied per legacy input-axis unit. X controls yaw and Y controls pitch on the owning client.")]
        protected Vector2Int lookSensitivity = new Vector2Int(8, 8);
        [SerializeField, Range(-90, 0), Tooltip("Lowest camera pitch in degrees. -90 allows looking straight down; 0 prevents downward rotation.")]
        protected float minLookAngle = -60f;
        [SerializeField, Range(0, 90), Tooltip("Highest camera pitch in degrees. 90 allows looking straight up; 0 prevents upward rotation.")]
        protected float maxLookAngle = 60f;

        [Header("Smoothness Settings")]
        [SerializeField, Tooltip("Smooths the owning client's camera rotation when enabled. Disable for immediate response to look input.")]
        protected bool useSmoothness = true;
        [SerializeField, Range(0.01f, 1f), Tooltip("Approximate SmoothDamp response time in seconds. Lower values follow input faster; higher values feel softer.")]
        protected float smoothnessTime = 0.1f;

        [Header("Network Optimization")]
        [SerializeField, Tooltip("Minimum look-angle change in degrees before the owning client sends another rotation update. 0 sends every detected change.")]
        protected float networkUpdateThreshold = 1f;
        [SerializeField, Range(1f, 50f), Tooltip("Remote-client rotation interpolation responsiveness. Higher values converge to the synchronized rotation faster.")]
        protected float networkInterpolationSpeed = 15f;

        #endregion

        #region NETWORK DATA STRUCTURES

        /// <summary>
        /// Packed camera X rotation for network (2 bytes instead of 16)
        /// Only vertical camera rotation, horizontal handled by NetworkTransform
        /// </summary>
        [System.Serializable]
        public struct PackedCameraRotation
        {
            public short x; // -32768 to 32767 mapping to -90 to 90 degrees (camera X rotation only)

            public PackedCameraRotation(float xAngle)
            {
                // Convert X angle (-90 to 90) to short (-32768 to 32767)
                x = (short)(Mathf.Clamp(xAngle, -90f, 90f) * (32767f / 90f));
            }

            public readonly float ToXAngle()
            {
                // Convert short back to X angle
                return (float)x * (90f / 32767f);
            }

            public readonly Quaternion ToCameraRotation()
            {
                return Quaternion.Euler(ToXAngle(), 0f, 0f);
            }

            public static bool operator ==(PackedCameraRotation a, PackedCameraRotation b)
            {
                return a.x == b.x;
            }

            public static bool operator !=(PackedCameraRotation a, PackedCameraRotation b)
            {
                return !(a == b);
            }

            public override readonly bool Equals(object obj)
            {
                return obj is PackedCameraRotation other && this == other;
            }

            public override int GetHashCode()
            {
                return x.GetHashCode();
            }
        }

        #endregion

        #region PRIVATE VARIABLES

        // Network sync variable - only for camera X rotation
        [SyncVar]
        private PackedCameraRotation cameraXRotation = new PackedCameraRotation();

        // Camera rotation data (X-axis: vertical)
        private float currentCameraXRotation;
        private float smoothedCameraXRotation;
        private float cameraXRotationVelocity;

        // Player rotation data (Y-axis: horizontal)
        private float currentPlayerYRotation;
        private float smoothedPlayerYRotation;
        private float playerYRotationVelocity;

        // Network optimization
        private PackedCameraRotation lastSentXRotation;

        // Camera restoration data
        protected Transform initialCameraParent;
        protected Vector3 initialCameraPosition;
        protected Quaternion initialCameraRotation;

        #endregion

        #region PUBLIC PROPERTIES

        /// <summary>
        /// Current camera container position
        /// </summary>
        public Vector3 Point => cameraContainer != null ? cameraContainer.position : transform.position;

        /// <summary>
        /// Current camera container forward direction
        /// </summary>
        public Vector3 Direction => cameraContainer != null ? cameraContainer.forward : transform.forward;

        /// <summary>
        /// Current camera container rotation
        /// </summary>
        public Quaternion Rotation => cameraContainer != null ? cameraContainer.rotation : transform.rotation;

        #endregion

        #region UNITY LIFECYCLE

        protected virtual void Update()
        {
            if (isLocalPlayer)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                    return;

                CalculateCameraRotation();
            }
            else
            {
                // Interpolate only camera X rotation, NetworkTransform handles Y rotation
                if (cameraContainer != null)
                {
                    float targetXRotation = cameraXRotation.ToXAngle();
                    float currentX = cameraContainer.localRotation.eulerAngles.x;

                    // Normalize current X angle to -90 to 90 range
                    if (currentX > 180f) currentX -= 360f;

                    float lerpedX = Mathf.LerpAngle(currentX, targetXRotation, Time.deltaTime * networkInterpolationSpeed);
                    cameraContainer.localRotation = Quaternion.Euler(lerpedX, 0f, 0f);
                }
            }
        }

        protected virtual void OnDrawGizmos()
        {
            // Draw camera container position and direction
            if (cameraContainer != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(cameraContainer.position, 0.1f);
                Gizmos.DrawRay(cameraContainer.position, cameraContainer.forward);
            }
            else
            {
                // Fallback if container is not assigned
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(transform.position, 0.1f);
                Gizmos.DrawRay(transform.position, transform.forward);
            }
        }

        #endregion

        #region FISHNET LIFECYCLE

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (isLocalPlayer)
                AttachCamera();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (isLocalPlayer && resetCameraAfterDestroy)
                DetachCamera();
        }

        #endregion

        #region CAMERA MANAGEMENT

        /// <summary>
        /// Attaches the camera to the camera container
        /// </summary>
        protected void AttachCamera()
        {
            if (cameraContainer == null)
            {
                Debug.LogError("Camera Container is not assigned! Please assign a Transform to cameraContainer field.");
                return;
            }

            if (lookCamera == null)
                lookCamera = Camera.main;

            if (lookCamera == null)
            {
                // Create new camera if none exists
                var cameraObject = new GameObject("--PlayerCamera");
                cameraObject.transform.SetParent(cameraContainer);
                cameraObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var cameraComponent = cameraObject.AddComponent<Camera>();
                lookCamera = cameraComponent;
            }
            else
            {
                // Store initial camera state for restoration
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

                // Attach camera to container
                lookCamera.transform.SetParent(cameraContainer);
                lookCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
        }

        /// <summary>
        /// Clears camera object and detaches it from camera container
        /// </summary>
        protected void DetachCamera()
        {
            if (isLocalPlayer && lookCamera && cameraContainer != null)
            {
                // Detach camera from container and restore to original state
                if (initialCameraParent != null)
                {
                    lookCamera.transform.SetParent(initialCameraParent);
                    lookCamera.transform.SetLocalPositionAndRotation(initialCameraPosition, initialCameraRotation);
                }
                else
                {
                    lookCamera.transform.SetParent(null);
                    lookCamera.transform.SetPositionAndRotation(initialCameraPosition, initialCameraRotation);
                }

                lookCamera = null;
            }
        }

        #endregion

        #region INPUT AND ROTATION

        /// <summary>
        /// Updates camera container rotation based on player input
        /// Handles both X rotation (camera vertical) and Y rotation (player horizontal)
        /// Only X rotation is synchronized over network, Y rotation handled by NetworkTransform
        /// </summary>
        protected void CalculateCameraRotation()
        {
            // Update X rotation (vertical camera movement) - this will be synchronized
            currentCameraXRotation = Mathf.Clamp(currentCameraXRotation - player.Input.MouseY() * lookSensitivity.y, minLookAngle, maxLookAngle);

            // Update Y rotation (horizontal player rotation) - this will be handled by NetworkTransform
            currentPlayerYRotation += player.Input.MouseX() * lookSensitivity.x;

            if (useSmoothness)
            {
                // Apply smoothed X rotation to camera container
                smoothedCameraXRotation = Mathf.SmoothDampAngle(smoothedCameraXRotation, currentCameraXRotation, ref cameraXRotationVelocity, smoothnessTime);
                cameraContainer.localRotation = Quaternion.Euler(smoothedCameraXRotation, 0f, 0f);

                // Apply smoothed Y rotation to player transform
                smoothedPlayerYRotation = Mathf.SmoothDampAngle(smoothedPlayerYRotation, currentPlayerYRotation, ref playerYRotationVelocity, smoothnessTime);
                transform.rotation = Quaternion.Euler(0f, smoothedPlayerYRotation, 0f);
            }
            else
            {
                // Apply direct rotations
                cameraContainer.localRotation = Quaternion.Euler(currentCameraXRotation, 0f, 0f);
                transform.rotation = Quaternion.Euler(0f, currentPlayerYRotation, 0f);
            }

            // Send only X rotation to network, Y rotation will be synchronized by NetworkTransform
            SetCameraXRotationOptimized(useSmoothness ? smoothedCameraXRotation : currentCameraXRotation);
        }

        #endregion

        #region NETWORK OPTIMIZATION

        /// <summary>
        /// Optimized method for sending camera X rotation to network
        /// Sends data only when there's a significant change
        /// </summary>
        private void SetCameraXRotationOptimized(float xAngle)
        {
            PackedCameraRotation newRotation = new PackedCameraRotation(xAngle);

            // Check if angle changed enough to send update
            if (ShouldSendXRotationUpdate(newRotation))
            {
                Cmd_SetCameraXRotation(newRotation);
                lastSentXRotation = newRotation;
            }
        }

        /// <summary>
        /// Checks if X rotation update should be sent
        /// </summary>
        private bool ShouldSendXRotationUpdate(PackedCameraRotation newRotation)
        {
            if (lastSentXRotation == default(PackedCameraRotation))
                return true; // First send

            float lastAngle = lastSentXRotation.ToXAngle();
            float newAngle = newRotation.ToXAngle();

            float deltaX = Mathf.Abs(Mathf.DeltaAngle(lastAngle, newAngle));

            return deltaX >= networkUpdateThreshold;
        }

        [Command]
        private void Cmd_SetCameraXRotation(PackedCameraRotation value)
        {
            cameraXRotation = value;
        }

        #endregion

        #region PUBLIC API

        /// <summary>
        /// Direction to the point at what the character is looking in armed mode
        /// </summary>
        /// <returns>Aim direction vector</returns>
        public virtual Vector3 AimDirection()
        {
            return cameraContainer != null ? cameraContainer.forward : transform.forward;
        }

        /// <summary>
        /// Gets camera container rotation angle in Quaternion
        /// </summary>
        /// <returns>Camera rotation quaternion</returns>
        public virtual Quaternion GetCameraRotation()
        {
            return cameraContainer != null ? cameraContainer.rotation : transform.rotation;
        }

        /// <summary>
        /// Gets packed camera rotation (for debugging/UI)
        /// </summary>
        /// <returns>Packed camera X rotation data</returns>
        public PackedCameraRotation GetPackedCameraRotation()
        {
            return cameraXRotation;
        }

        /// <summary>
        /// Gets current camera X rotation angle
        /// </summary>
        /// <returns>Camera X rotation in degrees</returns>
        public float GetCameraXRotation()
        {
            return isLocalPlayer ? (useSmoothness ? smoothedCameraXRotation : currentCameraXRotation) : cameraXRotation.ToXAngle();
        }

        /// <summary>
        /// Gets current player Y rotation angle
        /// </summary>
        /// <returns>Player Y rotation in degrees</returns>
        public float GetPlayerYRotation()
        {
            return useSmoothness ? smoothedPlayerYRotation : currentPlayerYRotation;
        }

        /// <summary>
        /// Sets network update sensitivity
        /// </summary>
        /// <param name="threshold">Minimum angle change in degrees</param>
        public void SetNetworkUpdateThreshold(float threshold)
        {
            networkUpdateThreshold = Mathf.Clamp(threshold, 0.1f, 10f);
        }

        /// <summary>
        /// Sets network interpolation speed
        /// </summary>
        /// <param name="speed">Interpolation speed multiplier</param>
        public void SetNetworkInterpolationSpeed(float speed)
        {
            networkInterpolationSpeed = Mathf.Clamp(speed, 1f, 50f);
        }

        /// <summary>
        /// Sets look sensitivity for both axes
        /// </summary>
        /// <param name="x">Horizontal sensitivity</param>
        /// <param name="y">Vertical sensitivity</param>
        public void SetLookSensitivity(int x, int y)
        {
            lookSensitivity = new Vector2Int(Mathf.Clamp(x, 1, 20), Mathf.Clamp(y, 1, 20));
        }

        /// <summary>
        /// Sets vertical look angle limits
        /// </summary>
        /// <param name="min">Minimum look angle (negative)</param>
        /// <param name="max">Maximum look angle (positive)</param>
        public void SetLookAngleLimits(float min, float max)
        {
            minLookAngle = Mathf.Clamp(min, -90f, 0f);
            maxLookAngle = Mathf.Clamp(max, 0f, 90f);
        }

        /// <summary>
        /// Toggles rotation smoothness
        /// </summary>
        /// <param name="enable">Enable or disable smoothness</param>
        public void SetSmoothness(bool enable)
        {
            useSmoothness = enable;
        }

        /// <summary>
        /// Sets smoothness time
        /// </summary>
        /// <param name="time">Smoothness time in seconds</param>
        public void SetSmoothnessTime(float time)
        {
            smoothnessTime = Mathf.Clamp(time, 0.01f, 1f);
        }

        /// <summary>
        /// Sets current camera X rotation directly (useful for spawning/teleporting)
        /// </summary>
        /// <param name="xRotation">X rotation angle in degrees</param>
        public void SetCameraXRotationDirect(float xRotation)
        {
            currentCameraXRotation = Mathf.Clamp(xRotation, minLookAngle, maxLookAngle);
            smoothedCameraXRotation = currentCameraXRotation;

            if (cameraContainer != null)
            {
                cameraContainer.localRotation = Quaternion.Euler(currentCameraXRotation, 0f, 0f);
            }

            // Force network update
            if (isLocalPlayer)
            {
                Cmd_SetCameraXRotation(new PackedCameraRotation(currentCameraXRotation));
                lastSentXRotation = new PackedCameraRotation(currentCameraXRotation);
            }
        }

        /// <summary>
        /// Sets current player Y rotation directly (useful for spawning/teleporting)
        /// </summary>
        /// <param name="yRotation">Y rotation angle in degrees</param>
        public void SetPlayerYRotationDirect(float yRotation)
        {
            currentPlayerYRotation = yRotation;
            smoothedPlayerYRotation = currentPlayerYRotation;

            transform.rotation = Quaternion.Euler(0f, currentPlayerYRotation, 0f);
        }

        /// <summary>
        /// Sets both rotations directly (useful for spawning/teleporting)
        /// </summary>
        /// <param name="xRotation">Camera X rotation in degrees</param>
        /// <param name="yRotation">Player Y rotation in degrees</param>
        public void SetRotationsDirect(float xRotation, float yRotation)
        {
            SetCameraXRotationDirect(xRotation);
            SetPlayerYRotationDirect(yRotation);
        }

        /// <summary>
        /// Resets both rotations to zero
        /// </summary>
        public void ResetRotations()
        {
            SetRotationsDirect(0f, 0f);
        }

        /// <summary>
        /// Gets both current rotations
        /// </summary>
        /// <returns>Vector2 with X (camera) and Y (player) rotations</returns>
        public Vector2 GetCurrentRotations()
        {
            return new Vector2(GetCameraXRotation(), GetPlayerYRotation());
        }

        #endregion
    }
}
#endif
