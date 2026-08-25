#if MIRROR
using Mirror;
using UnityEngine;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput), typeof(CharacterController))]
    public class PlayerMovement : PlayerBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField, Tooltip("Required player facade that provides the owning client's input and CharacterController for prediction and server reconciliation.")]
        protected Player player;

        [Header("Gravity Settings"), SerializeField, Tooltip("Multiplier applied to Physics.gravity while simulated movement is airborne. 0 disables airborne gravity in this demo controller.")]
        protected float gravityMultiplier = 3f;
        [SerializeField, Range(0, 100), Tooltip("Downward speed in world units per second applied while grounded to keep the CharacterController attached to slopes. 0 removes this grounding force.")]
        protected float stickToGroundPower = 5f;

        [Header("Movement Settings"), SerializeField, Range(0, 100), Tooltip("Walking speed in world units per second used by client prediction and authoritative server simulation.")]
        protected float walkSpeed = 5f;
        [SerializeField, Range(0, 100), Tooltip("Running speed in world units per second used by client prediction and authoritative server simulation.")]
        protected float runSpeed = 10f;

        [Header("Jump Settings"), SerializeField, Tooltip("Allows jump input to produce upward movement. Server movement permissions remain independent.")]
        protected bool jumpIsAllowed = true;
        [SerializeField, Range(0, 100), Tooltip("Initial upward jump speed in world units per second. 0 produces no upward lift.")]
        protected float jumpPower = 8f;
        [SerializeField, Range(0, 100), Tooltip("Minimum delay between jumps in seconds. 0 allows another jump as soon as the controller is grounded.")]
        protected float jumpRate = 1f;

        [Header("Client Prediction"), SerializeField, Tooltip("Position error in world units that triggers reconciliation against the server state. 0 corrects every non-zero difference.")]
        protected float reconciliationThreshold = 0.1f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Interval in seconds between movement inputs sent to the authoritative server. Lower values increase update frequency and bandwidth.")]
        protected float serverTickRate = 0.1f;
        [SerializeField, Range(1, 100), Tooltip("Maximum number of predicted input and state samples retained for rollback. Larger values cover more latency and use more memory.")]
        protected int maxStoredInputs = 60;
        [SerializeField, Tooltip("Interpolates remote movement and server corrections when enabled. Disable to apply authoritative positions immediately.")]
        protected bool enableSmoothing = true;
        [SerializeField, Range(0.1f, 2f), Tooltip("Correction and remote-movement interpolation responsiveness. Higher values converge to the server state faster.")]
        protected float smoothingSpeed = 1f;

        [Header("Debug"), SerializeField, Tooltip("Draws prediction position, velocity, and reconciliation threshold gizmos in the Unity Scene view. This does not affect networking.")]
        protected bool showPredictionGizmos = false;

        #endregion

        #region INPUT AND STATE STRUCTURES

        /// <summary>
        /// Input data for a single tick
        /// </summary>
        [System.Serializable]
        public struct MovementInput
        {
            public uint tick;
            public float horizontal;
            public float vertical;
            public bool isRunning;
            public bool isJumping;
            public float deltaTime;

            public MovementInput(uint tick, float horizontal, float vertical, bool isRunning, bool isJumping, float deltaTime)
            {
                this.tick = tick;
                this.horizontal = horizontal;
                this.vertical = vertical;
                this.isRunning = isRunning;
                this.isJumping = isJumping;
                this.deltaTime = deltaTime;
            }
        }

        /// <summary>
        /// Movement state for reconciliation
        /// </summary>
        [System.Serializable]
        public struct MovementState
        {
            public uint tick;
            public Vector3 position;
            public Vector3 velocity;
            public bool isGrounded;
            public bool isWalking;
            public bool isRunning;
            public float nextJumpTime;

            public MovementState(uint tick, Vector3 position, Vector3 velocity, bool isGrounded, bool isWalking, bool isRunning, float nextJumpTime)
            {
                this.tick = tick;
                this.position = position;
                this.velocity = velocity;
                this.isGrounded = isGrounded;
                this.isWalking = isWalking;
                this.isRunning = isRunning;
                this.nextJumpTime = nextJumpTime;
            }
        }

        #endregion

        #region PRIVATE VARIABLES

        // Client Prediction
        private readonly Queue<MovementInput> inputBuffer = new Queue<MovementInput>();
        private readonly Queue<MovementState> stateBuffer = new Queue<MovementState>();
        private uint clientTick = 0;
        private float serverTickTimer = 0f;

        // Movement state
        private Vector3 velocity = Vector3.zero;
        private Vector3 lastServerPosition;
        private Vector3 correctionVelocity = Vector3.zero;
        private bool needsReconciliation = false;

        // Cached components
        private Transform cachedTransform;

        // Movement properties
        [SyncVar]
        protected bool runningIsAllowed = true;
        [SyncVar]
        protected bool movementIsAllowed = true;

        protected float nextJumpTime = 0f;

        #endregion

        #region PUBLIC PROPERTIES

        /// <summary>
        /// Check if this behaviour is ready
        /// </summary>
        public override bool IsReady => player != null && player.Input && player.Controller && NetworkClient.ready;

        /// <summary>
        /// Speed of the character
        /// </summary>
        public float CurrentMovementSpeed { get; private set; }

        /// <summary>
        /// Check if jumping is available for the character
        /// </summary>
        public bool IsJumpAvailable { get; private set; }

        /// <summary>
        /// If character is currently walking
        /// </summary>
        public bool IsWalking { get; private set; }

        /// <summary>
        /// If character is currently running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Current velocity vector
        /// </summary>
        public Vector3 Velocity => velocity;

        /// <summary>
        /// Is character grounded
        /// </summary>
        public bool IsGrounded => player.Controller != null && player.Controller.isGrounded;

        #endregion

        #region UNITY LIFECYCLE

        protected void Start()
        {
            cachedTransform = transform;
            lastServerPosition = cachedTransform.position;
        }

        protected void Update()
        {
            if (!IsReady) return;

            if (isLocalPlayer)
            {
                UpdateClientPrediction();
            }
            else
            {
                UpdateRemotePlayer();
            }
        }

        #endregion

        #region CLIENT PREDICTION

        /// <summary>
        /// Updates client-side prediction
        /// </summary>
        private void UpdateClientPrediction()
        {
            clientTick++;

            // Collect input for this tick
            MovementInput input = CollectInput();

            // Store input for potential rollback
            StoreInput(input);

            // Apply movement locally (prediction)
            ApplyMovement(input);

            // Send input to server periodically
            serverTickTimer += Time.deltaTime;

            if (serverTickTimer >= serverTickRate)
            {
                Cmd_ProcessMovementInput(input);
                serverTickTimer = 0f;
            }

            // Apply any pending corrections
            if (needsReconciliation)
            {
                ApplyServerCorrection();
            }
        }

        /// <summary>
        /// Updates remote player movement (interpolation)
        /// </summary>
        private void UpdateRemotePlayer()
        {
            // Simple interpolation for remote players
            // In a full implementation, you'd use a more sophisticated interpolation system
            if (enableSmoothing && correctionVelocity.magnitude > 0.01f)
            {
                cachedTransform.position += correctionVelocity * Time.deltaTime;
                correctionVelocity = Vector3.Lerp(correctionVelocity, Vector3.zero, Time.deltaTime * smoothingSpeed);
            }
        }

        /// <summary>
        /// Collects input for current tick
        /// </summary>
        private MovementInput CollectInput()
        {
            return new MovementInput(
                clientTick,
                player.Input.Horizontal(),
                player.Input.Vertical(),
                player.Input.IsRunnning() && runningIsAllowed,
                player.Input.IsJump(),
                Time.deltaTime
            );
        }

        /// <summary>
        /// Stores input in buffer for potential rollback
        /// </summary>
        private void StoreInput(MovementInput input)
        {
            inputBuffer.Enqueue(input);

            // Store current state
            MovementState state = new MovementState(
                clientTick,
                cachedTransform.position,
                velocity,
                IsGrounded,
                IsWalking,
                IsRunning,
                nextJumpTime
            );
            stateBuffer.Enqueue(state);

            // Limit buffer size
            while (inputBuffer.Count > maxStoredInputs)
            {
                inputBuffer.Dequeue();
                stateBuffer.Dequeue();
            }
        }

        /// <summary>
        /// Applies movement based on input
        /// </summary>
        private void ApplyMovement(MovementInput input)
        {
            if (!movementIsAllowed) return;

            // Update movement states
            UpdateMovementStates(input);

            // Calculate movement
            Vector3 moveVector = CalculateMovement(input);

            // Apply movement
            player.Controller.Move(moveVector * input.deltaTime);
            velocity = moveVector;
        }

        /// <summary>
        /// Updates movement states based on input
        /// </summary>
        private void UpdateMovementStates(MovementInput input)
        {
            bool hasMovementInput = Mathf.Abs(input.horizontal) > 0.01f || Mathf.Abs(input.vertical) > 0.01f;

            IsWalking = hasMovementInput && movementIsAllowed;
            IsRunning = IsWalking && input.isRunning;

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

            // Update jump availability
            IsJumpAvailable = jumpIsAllowed && Time.time >= nextJumpTime;
        }

        /// <summary>
        /// Calculates movement vector based on input
        /// </summary>
        private Vector3 CalculateMovement(MovementInput input)
        {
            Vector3 moveVector = velocity;

            if (IsGrounded)
            {
                // Calculate input direction
                Vector3 inputDirection = cachedTransform.forward * input.vertical + cachedTransform.right * input.horizontal;

                moveVector.y = -stickToGroundPower;
                moveVector.x = inputDirection.x * CurrentMovementSpeed;
                moveVector.z = inputDirection.z * CurrentMovementSpeed;

                // Handle jumping
                if (input.isJumping && IsJumpAvailable)
                {
                    moveVector.y = jumpPower;
                    nextJumpTime = Time.time + jumpRate;
                }
            }
            else
            {
                // Apply gravity
                moveVector += gravityMultiplier * input.deltaTime * Physics.gravity;
            }

            return moveVector;
        }

        #endregion

        #region SERVER RECONCILIATION

        /// <summary>
        /// Processes movement input on server
        /// </summary>
        [Command]
        private void Cmd_ProcessMovementInput(MovementInput input)
        {
            // Apply movement on server
            ApplyMovement(input);

            // Send authoritative state back to client
            Rpc_ReceiveServerState(new MovementState(
                input.tick,
                cachedTransform.position,
                velocity,
                IsGrounded,
                IsWalking,
                IsRunning,
                nextJumpTime
            ));
        }

        /// <summary>
        /// Receives authoritative state from server
        /// </summary>
        [ClientRpc]
        private void Rpc_ReceiveServerState(MovementState serverState)
        {
            if (!isLocalPlayer)
            {
                // For remote players, just apply the state
                ApplyRemoteState(serverState);
                return;
            }

            // For local player, check if reconciliation is needed
            MovementState clientState = FindClientState(serverState.tick);

            if (clientState.tick != 0)
            {
                float positionError = Vector3.Distance(clientState.position, serverState.position);

                if (positionError > reconciliationThreshold)
                {
                    // Need to reconcile
                    PerformReconciliation(serverState);
                }
            }
        }

        /// <summary>
        /// Applies state for remote players
        /// </summary>
        private void ApplyRemoteState(MovementState state)
        {
            Vector3 targetPosition = state.position;
            Vector3 currentPosition = cachedTransform.position;

            if (enableSmoothing)
            {
                // Calculate correction velocity for smooth interpolation
                correctionVelocity = (targetPosition - currentPosition) * smoothingSpeed;
            }
            else
            {
                // Direct application
                cachedTransform.position = targetPosition;
            }

            velocity = state.velocity;
            IsWalking = state.isWalking;
            IsRunning = state.isRunning;
            nextJumpTime = state.nextJumpTime;
        }

        /// <summary>
        /// Finds client state for given tick
        /// </summary>
        private MovementState FindClientState(uint tick)
        {
            foreach (MovementState state in stateBuffer)
            {
                if (state.tick == tick)
                {
                    return state;
                }
            }
            return new MovementState(); // Default state if not found
        }

        /// <summary>
        /// Performs client-side reconciliation
        /// </summary>
        private void PerformReconciliation(MovementState serverState)
        {
            // Set position to server's authoritative position
            cachedTransform.position = serverState.position;
            velocity = serverState.velocity;
            IsWalking = serverState.isWalking;
            IsRunning = serverState.isRunning;
            nextJumpTime = serverState.nextJumpTime;

            // Re-apply all inputs after the server state
            List<MovementInput> inputsToReapply = new List<MovementInput>();

            foreach (MovementInput input in inputBuffer)
            {
                if (input.tick > serverState.tick)
                {
                    inputsToReapply.Add(input);
                }
            }

            // Re-simulate from server state
            foreach (MovementInput input in inputsToReapply)
            {
                ApplyMovement(input);
            }

            needsReconciliation = false;
        }

        /// <summary>
        /// Applies pending server corrections
        /// </summary>
        private void ApplyServerCorrection()
        {
            Vector3 currentPosition = cachedTransform.position;
            Vector3 correctedPosition = Vector3.Lerp(currentPosition, lastServerPosition, Time.deltaTime * smoothingSpeed);

            if (Vector3.Distance(correctedPosition, lastServerPosition) < 0.01f)
            {
                needsReconciliation = false;
            }
            else
            {
                cachedTransform.position = correctedPosition;
            }
        }

        #endregion

        #region PUBLIC API

        /// <summary>
        /// Enable or disable running mode
        /// </summary>
        /// <param name="value"></param>
        [Server]
        public void AllowRunning(bool value)
        {
            runningIsAllowed = value;
        }

        /// <summary>
        /// Enable or disable movement mode
        /// </summary>
        /// <param name="value"></param>
        [Server]
        public void AllowMoving(bool value)
        {
            movementIsAllowed = value;
        }

        /// <summary>
        /// Sets reconciliation threshold
        /// </summary>
        /// <param name="threshold">Threshold in world units</param>
        public void SetReconciliationThreshold(float threshold)
        {
            reconciliationThreshold = Mathf.Clamp(threshold, 0.01f, 1f);
        }

        /// <summary>
        /// Sets server tick rate
        /// </summary>
        /// <param name="rate">Tick rate in seconds</param>
        public void SetServerTickRate(float rate)
        {
            serverTickRate = Mathf.Clamp(rate, 0.05f, 1f);
        }

        /// <summary>
        /// Gets current client tick
        /// </summary>
        /// <returns>Current tick number</returns>
        public uint GetClientTick()
        {
            return clientTick;
        }

        /// <summary>
        /// Gets input buffer size
        /// </summary>
        /// <returns>Number of stored inputs</returns>
        public int GetInputBufferSize()
        {
            return inputBuffer.Count;
        }

        /// <summary>
        /// Clears prediction buffers (useful for teleporting)
        /// </summary>
        public void ClearPredictionBuffers()
        {
            inputBuffer.Clear();
            stateBuffer.Clear();
            clientTick = 0;
            velocity = Vector3.zero;
            needsReconciliation = false;
        }

        #endregion

        #region GIZMOS

        private void OnDrawGizmos()
        {
            if (!showPredictionGizmos) return;

            // Draw current position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw velocity
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, velocity.normalized * 2f);

            // Draw reconciliation threshold
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, reconciliationThreshold);
        }

        #endregion
    }
}
#endif
