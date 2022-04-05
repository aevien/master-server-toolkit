#if MIRROR
using Mirror;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacterInput), typeof(CharacterController))]
    public class PlayerCharacterMovement : PlayerCharacterBehaviour
    {
        #region INSPECTOR

        [Header("Gravity Settings"), SerializeField]
        protected float gravityMultiplier = 3f;
        [SerializeField, Range(0, 100)]
        protected float stickToGroundPower = 5f;

        [Header("Movement Settings"), SerializeField, Range(0, 100)]
        protected float walkSpeed = 5f;
        [SerializeField, Range(0, 100)]
        protected float runSpeed = 10f;

        [Header("Jump Settings"), SerializeField]
        protected bool jumpIsAllowed = true;
        [SerializeField, Range(0, 100)]
        protected float jumpPower = 8f;
        [SerializeField, Range(0, 100)]
        protected float jumpRate = 1f;

        [Header("Components"), SerializeField]
        protected PlayerCharacterInput inputController;
        [SerializeField]
        protected CharacterController characterController;
        [SerializeField]
        protected PlayerCharacterLook lookController;

        [Header("Rotation Settings"), SerializeField, Range(5f, 20f)]
        protected float rotationSmoothTime = 5f;

        #endregion

        /// <summary>
        /// The direction to which the character is required to look
        /// </summary>
        protected Quaternion playerTargetDirectionAngle;

        /// <summary>
        /// Check if running mode is allowed for character
        /// </summary>
        [SyncVar]
        protected bool runningIsAllowed = true;

        /// <summary>
        /// Check if movement mode is allowed for character
        /// </summary>
        [SyncVar]
        protected bool movementIsAllowed = true;

        /// <summary>
        /// Current calculated movement direction
        /// </summary>
        protected Vector3 calculatedMovementDirection = new Vector3();

        /// <summary>
        /// Current calculated movement direction
        /// </summary>
        protected Vector3 calculatedInputDirection = new Vector3();

        /// <summary>
        /// Next allowed jump time
        /// </summary>
        protected float nextJumpTime = 0f;

        /// <summary>
        /// Check if this behaviour is ready
        /// </summary>
        public override bool IsReady => inputController && characterController && lookController && NetworkClient.ready;

        /// <summary>
        /// Speed of the character
        /// </summary>
        public float CurrentMovementSpeed { get; protected set; }

        /// <summary>
        /// Check if jumping is available for the character
        /// </summary>
        public bool IsJumpAvailable { get; protected set; }

        /// <summary>
        /// If character is currently walking
        /// </summary>
        public bool IsWalking { get; protected set; }

        /// <summary>
        /// If character is currently running
        /// </summary>
        public bool IsRunning { get; protected set; }

        protected void Update()
        {
            if (isLocalPlayer && IsReady)
            {
                UpdateJumpAvailability();
                UpdateMovementStates();
                UpdateMovement();
            }
        }

        protected virtual void UpdateJumpAvailability()
        {
            if (!movementIsAllowed) return;

            if (jumpIsAllowed)
            {
                IsJumpAvailable = Time.time >= nextJumpTime;
            }
            else
            {
                IsJumpAvailable = jumpIsAllowed;
            }
        }

        /// <summary>
        /// Update movement state on client
        /// </summary>
        protected virtual void UpdateMovementStates()
        {
            IsWalking = inputController.IsMoving() && movementIsAllowed;
            IsRunning = IsWalking && inputController.IsRunnning() && runningIsAllowed;

            // Send state update to server
            CmdUpdateMovementState(IsWalking, IsRunning);

            if (IsRunning)
            {
                CurrentMovementSpeed = runSpeed;
            }
            else if (IsWalking)
            {
                CurrentMovementSpeed = walkSpeed;
            }
            else
            {
                CurrentMovementSpeed = 0f;
            }
        }

        /// <summary>
        /// Update movement state on server
        /// </summary>
        /// <param name="isWalking"></param>
        /// <param name="isRunning"></param>
        [Command]
        private void CmdUpdateMovementState(bool isWalking, bool isRunning)
        {
            IsWalking = isWalking;
            IsRunning = isRunning;
        }

        protected virtual void UpdateMovement()
        {
            if (characterController.isGrounded)
            {
                calculatedInputDirection = transform.forward * inputController.Vertical() + transform.right * inputController.Horizontal();

                calculatedMovementDirection.y = -stickToGroundPower;
                calculatedMovementDirection.x = calculatedInputDirection.x * CurrentMovementSpeed;
                calculatedMovementDirection.z = calculatedInputDirection.z * CurrentMovementSpeed;

                if (inputController.IsJump() && IsJumpAvailable)
                {
                    calculatedMovementDirection.y = jumpPower;
                    nextJumpTime = Time.time + jumpRate;
                }
            }
            else
            {
                calculatedMovementDirection += Physics.gravity * gravityMultiplier * Time.deltaTime;
            }

            characterController.Move(calculatedMovementDirection * Time.deltaTime);
        }

        /// <summary>
        /// Enable or disable running mode
        /// </summary>
        /// <param name="value"></param>
        public void AllowRunning(bool value)
        {
            if (isServer)
            {
                runningIsAllowed = value;
            }
        }

        /// <summary>
        /// Enable or disable movement mode
        /// </summary>
        /// <param name="value"></param>
        public void AllowMoving(bool value)
        {
            if (isServer)
            {
                movementIsAllowed = value;
            }
        }
    }
}
#endif