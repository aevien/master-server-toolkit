using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesPlayerModule : BasePlayerModule
    {
        [DllImport("__Internal")]
        private static extern void Gb_Yg_AuthPlayer();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetPlayer();
        [DllImport("__Internal")]
        private static extern int Gb_Yg_isReady();

        private Coroutine initRoutine;
        private bool authenticationPending;
        private bool notifyAuthenticatedOnPlayerInfo;

        public override void OnInit(IService service)
        {
            if (initRoutine != null)
                return;

            base.OnInit(service);
            IsSupported = true;
            IsAuthenticationSupported = true;
            initRoutine = StartCoroutine(InitCoroutine());
        }

        protected virtual IEnumerator InitCoroutine()
        {
            yield return null;
            float elapsed = 0f;

            while (Gb_Yg_isReady() != 1 && elapsed < Service.WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            RefreshInfo();
        }

        public void RefreshInfo()
        {
            Gb_Yg_GetPlayer();
        }

        public override void Authenticate(SuccessCallback callback)
        {
            if (authenticationPending)
            {
                callback?.Invoke(false, "authentication_in_progress");
                return;
            }

            base.Authenticate(callback);

            if (!IsGuest)
            {
                NotifyOnAuthenticated(true, string.Empty);
                NotifyOnInfoChanged();
                return;
            }

            authenticationPending = true;
            Gb_Yg_AuthPlayer();
        }

        #region WEB_CALLBACKS

        protected void Yg_OnAuthPlayer(string json)
        {
            if (!MstJson.IsJson(json))
            {
                authenticationPending = false;
                notifyAuthenticatedOnPlayerInfo = false;
                Logger.Error("Yandex authentication returned invalid JSON");
                NotifyOnAuthenticated(false, "invalid_yandex_auth_response");
                return;
            }

            var data = new MstJson(json);

            if (data.HasField(YandexGamesKeys.Success) && data[YandexGamesKeys.Success].BoolValue)
            {
                notifyAuthenticatedOnPlayerInfo = true;
                RefreshInfo();
            }
            else
            {
                authenticationPending = false;
                notifyAuthenticatedOnPlayerInfo = false;
                NotifyOnAuthenticated(false, data.HasField(YandexGamesKeys.Error) ? data[YandexGamesKeys.Error].StringValue : "unknown_error");
            }
        }

        protected void Yg_OnGetPlayer(string json)
        {
            if (!MstJson.IsJson(json))
            {
                bool shouldNotifyAuthentication = notifyAuthenticatedOnPlayerInfo;
                authenticationPending = false;
                notifyAuthenticatedOnPlayerInfo = false;
                IsGuest = true;
                IsReady = true;
                Logger.Error("Yandex player info returned invalid JSON");
                NotifyOnInfoChanged();

                if (shouldNotifyAuthentication)
                    NotifyOnAuthenticated(false, "invalid_yandex_player_response");

                return;
            }

            var data = new MstJson(json);

            if (data.HasField(YandexGamesKeys.Error))
                Logger.Warn($"Yandex player info failed. Error: {data[YandexGamesKeys.Error].StringValue}");

            Id = data.HasField(YandexGamesKeys.Id) ? data[YandexGamesKeys.Id].StringValue : string.Empty;
            Name = data.HasField(YandexGamesKeys.Name) ? data[YandexGamesKeys.Name].StringValue : string.Empty;
            IsGuest = data.HasField(YandexGamesKeys.IsGuest) ? data[YandexGamesKeys.IsGuest].BoolValue : true;
            Avatar = data.HasField(YandexGamesKeys.Avatar) ? data[YandexGamesKeys.Avatar].StringValue : string.Empty;
            Extra = data.HasField(YandexGamesKeys.Extra) ? data[YandexGamesKeys.Extra] : MstJson.CreateObject();

            if (!IsReady)
            {
                IsReady = true;
            }

            NotifyOnInfoChanged();

            if (notifyAuthenticatedOnPlayerInfo)
            {
                authenticationPending = false;
                notifyAuthenticatedOnPlayerInfo = false;
                bool identityConfirmed = !IsGuest && !string.IsNullOrWhiteSpace(Id);
                NotifyOnAuthenticated(
                    identityConfirmed,
                    identityConfirmed ? string.Empty : "yandex_identity_unavailable");
            }
        }

        #endregion
    }
}
