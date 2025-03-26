using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public delegate void PlayerAction(PlayerInfo player);

    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
        #region PLAYER

        public PlayerInfo Player { get; protected set; } = new PlayerInfo();
        public MstJson Data { get; protected set; } = MstJson.NullObject;

        public virtual void Authenticate(SuccessCallback callback)
        {
            authenticateCallback = callback;
        }

        public virtual void LoadPlayerData(PlayerDataHandler callback)
        {
            playerDataCallback = callback;
        }

        public virtual void SavePlayerData(string key, string value, SuccessCallback callback = null)
        {
            var data = MstJson.EmptyObject;
            data.AddField(key, value);
            SavePlayerData(data, false, callback);
        }

        public virtual void SavePlayerData(string key, int value, bool saveAsStats = false, SuccessCallback callback = null)
        {
            var data = MstJson.EmptyObject;
            data.AddField(key, value);
            SavePlayerData(data, saveAsStats, callback);
        }

        public virtual void SavePlayerData(MstJson data, bool saveAsStats = false, SuccessCallback callback = null)
        {
            setPlayerDataCallback = callback;
        }

        #endregion
    }
}