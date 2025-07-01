using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    public partial class YandexGamesService : BaseGameService
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern int Gb_Yg_GetPlayer();
        [DllImport("__Internal")]
        private static extern int Gb_Yg_AuthPlayer();
        [DllImport("__Internal")]
        private static extern int Gb_Yg_GetPlayerData();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_SetPlayerData(string data, bool useStats);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ReviewGame();
#endif

        private Coroutine saveDataCoroutine;

        public override void Authenticate(SuccessCallback callback)
        {
            if (Player.IsGuest)
            {
                base.Authenticate(callback);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_AuthPlayer();
#endif
            }
            else
            {
                NotifyOnAuthenticated(true, string.Empty);
            }
        }

        public override void LoadPlayerData(PlayerDataHandler callback)
        {
            base.LoadPlayerData(callback);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_GetPlayerData();
#endif
        }

        public override void SavePlayerData(MstJson data, bool saveAsStats = false, SuccessCallback callback = null)
        {
            if (saveDataCoroutine == null)
            {
                saveDataCoroutine = StartCoroutine(coroutine());
            }

            IEnumerator coroutine()
            {
                base.SavePlayerData(data, saveAsStats, callback);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                Gb_Yg_SetPlayerData(data.ToString(), saveAsStats);
#endif

                yield return new WaitForSecondsRealtime(options.GetField(GameServiceOptionKeys.YG_SAVE_DATA_INTERVAL).FloatValue);
                saveDataCoroutine = null;
            }
        }

        public override void ReviewGame()
        {
            base.ReviewGame();

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_ReviewGame();
#endif
        }
    }
}