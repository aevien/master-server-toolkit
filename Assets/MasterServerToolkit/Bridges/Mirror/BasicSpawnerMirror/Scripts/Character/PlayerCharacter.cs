#if MIRROR
using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.Bridges.MirrorNetworking.Character
{
    public class PlayerCharacter : PlayerCharacterBehaviour
    {
        protected override void Awake()
        {
            base.Awake();

            tag = "Client";
        }

        public override void OnStartLocalPlayer()
        {
            tag = "Player";

            base.OnStartLocalPlayer();

            // Notify listeners about that player character is in game
            Mst.Events.Invoke(MstEventKeys.playerStartedGame, this);
        }

        private void OnDestroy()
        {
            if (isLocalPlayer)
            {
                Mst.Events.Invoke(MstEventKeys.playerFinishedGame);
            }
        }
    }
}
#endif