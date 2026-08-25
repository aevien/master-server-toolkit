#if MIRROR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class PlayerCharacter : PlayerCharacterBehaviour
    {
        public static event Action<PlayerCharacter> OnServerCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnClientCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnLocalCharacterSpawnedEvent;

        public static event Action<PlayerCharacter> OnCharacterDestroyedEvent;

        public static List<PlayerCharacter> Players = new();
        public static PlayerCharacter Local { get; private set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorPlayModeState()
        {
            OnServerCharacterSpawnedEvent = null;
            OnClientCharacterSpawnedEvent = null;
            OnLocalCharacterSpawnedEvent = null;
            OnCharacterDestroyedEvent = null;
            Players.Clear();
            Local = null;
        }
#endif

        private void OnDestroy()
        {
            OnCharacterDestroyedEvent?.Invoke(this);
            Players.Remove(this);

            if (ReferenceEquals(Local, this))
                Local = null;
        }

        #region SERVER

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (!Players.Contains(this))
                Players.Add(this);

            OnServerCharacterSpawnedEvent?.Invoke(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Players.Remove(this);
        }

        #endregion

        #region CLIENT

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            Local = this;
            OnLocalCharacterSpawnedEvent?.Invoke(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!Players.Contains(this))
                Players.Add(this);

            OnClientCharacterSpawnedEvent?.Invoke(this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            Players.Remove(this);
        }

        #endregion
    }
}
#endif
