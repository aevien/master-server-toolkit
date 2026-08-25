#if MIRROR
using Mirror;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class Player : PlayerBehaviour
    {
        #region INSPECTOR

        [SerializeField, Tooltip("Required local input component. The owning client reads movement and pause input from this reference; the server and remote clients do not poll it.")]
        private PlayerInput input;
        [SerializeField, Tooltip("Required networked movement component used by this player facade on the server, owning client, and remote clients.")]
        private PlayerMovement movement;
        [SerializeField, Tooltip("Required avatar component that hides first-person body parts for the owning client and shows them for remote clients.")]
        private PlayerAvatar avatar;
        [SerializeField, Tooltip("Required look component that controls the owning client's camera and synchronizes visible orientation to other peers.")]
        private PlayerLook look;
        [SerializeField, Tooltip("Required CharacterController used by client prediction and authoritative movement simulation.")]
        private CharacterController controller;

        #endregion

        public static event Action<Player> OnServerSpawned;
        public static event Action<Player> OnClientSpawned;
        public static event Action<Player> OnLocalSpawned;
        public static event Action<Player> OnDestroyed;

        public static Player Local {  get; protected set; }
        public PlayerInput Input => input;
        public PlayerMovement Movement => movement;
        public PlayerAvatar Avatar => avatar;
        public PlayerLook Look => look;
        public CharacterController Controller => controller;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorPlayModeState()
        {
            OnServerSpawned = null;
            OnClientSpawned = null;
            OnLocalSpawned = null;
            OnDestroyed = null;
            Local = null;
        }
#endif

        [ClientCallback]
        private void Update()
        {
            if (isLocalPlayer)
            {
                if (input.IsPaused())
                {
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            OnDestroyed?.Invoke(this);

            if (ReferenceEquals(Local, this))
                Local = null;
        }

        #region SERVER

        public override void OnStartServer()
        {
            base.OnStartServer();
            OnServerSpawned?.Invoke(this);
        }

        #endregion

        #region CLIENT

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            Local = this;
            OnLocalSpawned?.Invoke(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            OnClientSpawned?.Invoke(this);
        }

        #endregion
    }
}
#endif
