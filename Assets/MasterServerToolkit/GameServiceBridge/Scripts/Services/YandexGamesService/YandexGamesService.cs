using MasterServerToolkit.Json;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    [Serializable]
    public class YandexGameSdkSettings
    {
        [Range(6f, 60f)]
        [Tooltip("Realtime seconds between automatic player-data saves. Valid Inspector range: 6 to 60 seconds.")]
        public float saveInterval = 6f;
        [Range(180f, 600f)]
        [Tooltip("Minimum realtime seconds between interstitial-ad requests. Valid Inspector range: 180 to 600 seconds.")]
        public float interstitialAdInterval = 180f;
        [Tooltip("Automatically calls the Yandex GameplayAPI ready signal after the bridge initializes. Disable only when the game sends readiness at a later controlled point.")]
        public bool autoSendApiReady = true;
        [Range(6f, 60f)]
        [Tooltip("Realtime cache lifetime in seconds for the current player's leaderboard entry. Valid Inspector range: 6 to 60 seconds.")]
        public float leaderboardPlayerEntryInterval = 6f;
        [Range(16f, 60f)]
        [Tooltip("Realtime cache lifetime in seconds for leaderboard entry lists. Valid Inspector range: 16 to 60 seconds.")]
        public float leaderboardEntriesInterval = 16f;

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.SetField(nameof(saveInterval), saveInterval);
            json.SetField(nameof(interstitialAdInterval), interstitialAdInterval);
            json.SetField(nameof(autoSendApiReady), autoSendApiReady);
            json.SetField(nameof(leaderboardPlayerEntryInterval), leaderboardPlayerEntryInterval);
            json.SetField(nameof(leaderboardEntriesInterval), leaderboardEntriesInterval);
            return json;
        }
    }

    public partial class YandexGamesService : BaseService
    {
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
        [DllImport("__Internal")]
        private static extern double Gb_Yg_serverTime();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetRemoteFlags(string defaultFlagsJson, string clientFeaturesJson);

        private MstJson environment = MstJson.CreateNull();
        private bool environmentWarningWasLogged;
        private Coroutine remoteFlagsRoutine;
        protected bool apiReadyWasSent = false;

        public override bool IsReady =>
            !environment.IsNull &&
            Gb_Yg_isReady() == 1 &&
            base.IsReady;

        public override long ServerTime
        {
            get
            {
                return (long)Gb_Yg_serverTime();
            }
        }

        public override ServiceDeviceType Device
        {
            get
            {
                if (Enum.TryParse<ServiceDeviceType>(Gb_Yg_Device(), true, out var result))
                {
                    return result;
                }

                return base.Device;
            }
        }

        public override MstJson RemoteFlags { get; protected set; } = MstJson.CreateObject();

        public override void OnBeforeInit(MstJson options)
        {
            Id = GameServiceId.YandexGames;
            environment = MstJson.CreateNull();
            environmentWarningWasLogged = false;
            AppId = null;
            Lang = null;
            Payload = MstJson.CreateObject();
            Referrer = new ReferrerInfo();

            Player = AddModule<YandexGamesPlayerModule>();
            IAP = AddModule<YandexGamesInAppPurchaseModule>();
            Leaderboards = AddModule<YandexGamesLeaderboardsModule>();
            Ad = AddModule<YandexGamesAdvertisementModule>();
            Analytics = AddModule<YandexGamesAnalyticsModule>();
            Storage = AddModule<YandexGamesStorageModule>();
            Share = AddModule<YandexGamesShareModule>();

            base.OnBeforeInit(options);

            Gb_Yg_initSdk();
        }

        public override void OnInit()
        {
            if (remoteFlagsRoutine == null)
                remoteFlagsRoutine = StartCoroutine(LoadRemoteFlagsCoroutine());

            base.OnInit();
        }

        public override void OnAfterInit()
        {
            base.OnAfterInit();
        }

        public override void GameLoaded(MstJson options)
        {
            if (!apiReadyWasSent &&
                Options.HasField(nameof(YandexGameSdkSettings.autoSendApiReady)) &&
                Options[nameof(YandexGameSdkSettings.autoSendApiReady)].BoolValue)
            {
                Gb_Yg_SetApiReady();
                apiReadyWasSent = true;
            }
        }

        public override void GameStart(MstJson options)
        {
            Gb_Yg_GameStart();
        }

        public override void GameStop(MstJson options)
        {
            Gb_Yg_GameStop();
        }

        private IEnumerator LoadRemoteFlagsCoroutine()
        {
            yield return null;
            float elapsed = 0f;

            while (!TryLoadEnvironment() && elapsed < WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (!environment.IsNull)
            {
                Gb_Yg_GetRemoteFlags(MstJson.CreateObject().ToString(), MstJson.CreateArray().ToString());
            }
            else
            {
                Logger.Warn("Yandex environment was not loaded and remote flags were not requested before the service timeout.");
            }

            remoteFlagsRoutine = null;
        }

        private bool TryLoadEnvironment()
        {
            if (!environment.IsNull)
                return true;

            if (Gb_Yg_isReady() != 1)
                return false;

            try
            {
                string environmentJson = Gb_Yg_Environment();

                if (!MstJson.IsJson(environmentJson))
                {
                    WarnEnvironmentLoadFailure("SDK returned invalid JSON");
                    return false;
                }

                var parsedEnvironment = new MstJson(environmentJson);

                if (!parsedEnvironment.IsObject)
                {
                    WarnEnvironmentLoadFailure("SDK returned a non-object JSON value");
                    return false;
                }

                environment = parsedEnvironment;
                environmentWarningWasLogged = false;
            }
            catch (Exception e)
            {
                WarnEnvironmentLoadFailure(e.Message);
                return false;
            }

            if (environment.HasField("app"))
            {
                MstJson app = environment["app"];

                if (app.IsObject && app.HasField("id"))
                    AppId = app["id"].StringValue;
            }

            if (environment.HasField("i18n"))
            {
                MstJson i18n = environment["i18n"];

                if (i18n.IsObject && i18n.HasField("lang"))
                    Lang = i18n["lang"].StringValue;
            }

            Payload = environment.HasField("payload")
                ? environment["payload"]
                : MstJson.CreateObject();

            Referrer = environment.HasField("referrer")
                ? ReferrerInfo.FromJson(environment["referrer"])
                : new ReferrerInfo();

            return true;
        }

        private void WarnEnvironmentLoadFailure(string error)
        {
            if (environmentWarningWasLogged)
                return;

            environmentWarningWasLogged = true;
            Logger?.Warn($"Failed to load Yandex Games environment. Error: {error}");
        }

        #region WEB_CALLBACKS

        protected void Yg_OnGameApiPause(int result)
        {
            NotifyOnPause(result != 0);
        }

        protected void Yg_OnAccountSelectionDialog(int result)
        {
            bool opened = result != 0;
            NotifyOnAccountSelectionDialog(opened);

            if (!opened)
            {
                if (Player is YandexGamesPlayerModule playerModule)
                    playerModule.RefreshInfo();

                if (Storage is YandexGamesStorageModule storageModule)
                    storageModule.RefreshData();
            }
        }

        protected void Yg_OnGetRemoteFlags(string json)
        {
            var data = new MstJson(json);

            if (data.HasField(YandexGamesKeys.Error))
            {
                Logger.Warn($"Yandex remote flags load failed. Error: {data[YandexGamesKeys.Error].StringValue}");
                RemoteFlags = MstJson.CreateObject();
                return;
            }

            RemoteFlags = data;
        }

        #endregion
    }
}
