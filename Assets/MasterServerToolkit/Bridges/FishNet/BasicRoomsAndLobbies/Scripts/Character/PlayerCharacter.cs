#if FISHNET
using System;

namespace MasterServerToolkit.Bridges.FishNetworking.Character
{
    public class PlayerCharacter : PlayerCharacterBehaviour
    {
        public static event Action<PlayerCharacter> OnServerCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnClientCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnLocalCharacterSpawnedEvent;
        public static event Action<PlayerCharacter> OnCharacterDestroyedEvent;

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