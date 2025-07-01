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
        private static extern void Gb_Yg_initSdk();
        [DllImport("__Internal")]
        private static extern int Gb_Yg_isReady();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_SetApiReady();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GameStart();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GameStop();
        [DllImport("__Internal")]
        private static extern string Gb_Yg_Environment();
        [DllImport("__Internal")]
        private static extern string Gb_Yg_Device();
#endif

        protected MstJson environment = MstJson.NullObject;
        protected string device = string.Empty;
        protected bool apiReadyWasSent = false;

        public override string AppId
        {
            get
            {
                ParseEnvironment();
                return environment["app"]["id"].StringValue;
            }
        }

        public override string Lang
        {
            get
            {
                ParseEnvironment();
                return environment["i18n"]["lang"].StringValue;
            }
        }

        public override string DeviceType
        {
            get
            {
                if (string.IsNullOrEmpty(device))
                {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                    device = Gb_Yg_Device();
#endif
                }

                return device;
            }
        }

        public override MstJson Payload
        {
            get
            {
                ParseEnvironment();
                return environment.HasField("payload") ? environment["payload"] : MstJson.EmptyObject;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            Id = GameServiceId.YandexGames;
            Player = new PlayerInfo();
            IsInAppPurchaseSupported = true;
            IsAdSupported = true;
            IsLeaderboardSupported = true;
        }

        #region SERVICE

        public override void Init(MstJson options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            if (Gb_Yg_isReady() != 1)
            {
                base.Init(options);
                StartCoroutine(InitCoroutine());
            }
#else
            StartCoroutine(InitCoroutine());
#endif
        }

        public override void GameLoaded(MstJson options)
        {
            if (!apiReadyWasSent)
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
               Gb_Yg_SetApiReady();
#endif
                apiReadyWasSent = true;
            }
        }

        public override void GameStart(MstJson options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
               Gb_Yg_GameStart();
#endif
        }

        public override void GameStop(MstJson options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
               Gb_Yg_GameStop();
#endif
        }

        private IEnumerator InitCoroutine()
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_initSdk();

            while (Gb_Yg_isReady() != 1)
            {
                yield return new WaitForEndOfFrame();
            }

            Gb_Yg_GetPlayer();
#endif
            Mst.Localization.Lang = Lang;

            yield return new WaitForSecondsRealtime(0.1f);

            GameStart();
            GameStop();
        }

        private void ParseEnvironment()
        {
            if (environment.IsNull)
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                environment = new MstJson(Gb_Yg_Environment());
#endif
            }
        }
        #endregion
    }
}