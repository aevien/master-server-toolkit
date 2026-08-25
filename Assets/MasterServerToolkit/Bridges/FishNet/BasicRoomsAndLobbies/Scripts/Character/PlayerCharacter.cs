#if FISHNET
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    public class PlayerCharacter : PlayerCharacterBehaviour
    {
        public static event Action<PlayerCharacter> OnServerCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnClientCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnLocalCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnCharacterDestroyedEvent;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorPlayModeState()
        {
            OnServerCharacterSpawnedEvent = null;
            OnClientCharacterSpawnedEvent = null;
            OnLocalCharacterSpawnedEvent = null;
            OnCharacterDestroyedEvent = null;
        }
#endif

        private void OnDestroy()
        {
            OnCharacterDestroyedEvent?.Invoke(this);
        }

        #region SERVER

        public override void OnStartServer()
        {
            base.OnStartServer();
            OnServerCharacterSpawnedEvent?.Invoke(this);
        }

        #endregion

        #region CLIENT
        public override void OnStartClient()
        {
            base.OnStartClient();

            OnClientCharacterSpawnedEvent?.Invoke(this);

            if (IsOwner)
                OnLocalCharacterSpawnedEvent?.Invoke(this);
        }

        #endregion
    }
}
#endif
