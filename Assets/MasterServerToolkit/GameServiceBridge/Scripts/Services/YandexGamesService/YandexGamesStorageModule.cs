using MasterServerToolkit.Json;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesStorageModule : BaseStorageModule
    {
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetPlayerData();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_SetPlayerData(string data, bool useStats);
        [DllImport("__Internal")]
        private static extern int Gb_Yg_isReady();

        private Coroutine saveDataCoroutine;
        private Coroutine initRoutine;

        public override void OnInit(IService service)
        {
            if (initRoutine != null)
                return;

            base.OnInit(service);
            IsSupported = true;
            initRoutine = StartCoroutine(InitCoroutine());
        }

        protected virtual IEnumerator InitCoroutine()
        {
            yield return null;

            float elapsed = 0f;

            while (!Service.Player.IsReady && elapsed < Service.WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            yield return null;

            RefreshData();
        }

        public override void LoadData(StorageDataHandler callback)
        {
            base.LoadData(callback);
            RefreshData();
        }

        public void RefreshData()
        {
            Gb_Yg_GetPlayerData();
        }

        public override void SaveData(MstJson data, bool saveAsStats = false)
        {
            saveDataCoroutine ??= StartCoroutine(coroutine());

            IEnumerator coroutine()
            {
                Gb_Yg_SetPlayerData((data ?? MstJson.CreateObject()).ToString(), saveAsStats);
                yield return new WaitForSecondsRealtime(Service.Options.GetField(nameof(YandexGameSdkSettings.saveInterval)).FloatValue);
                saveDataCoroutine = null;
            }
        }

        #region WEB_CALLBACKS

        protected void Yg_OnPlayerGetData(string json)
        {
            var data = new MstJson(json);

            if (data.HasField(YandexGamesKeys.Error))
            {
                Logger.Warn($"Yandex player data load failed. Error: {data[YandexGamesKeys.Error].StringValue}");
                Data = MstJson.CreateObject();
            }
            else
            {
                Data = data;
            }

            if (!IsReady)
            {
                IsReady = true;
                Logger.Debug($"Module ready");
            }

            NotifyOnLoad();
        }

        protected void Yg_OnPlayerSetData(string json)
        {
            var data = new MstJson(json);
            NotifyOnSave(
                data.HasField(YandexGamesKeys.Success) && data[YandexGamesKeys.Success].BoolValue,
                data.HasField(YandexGamesKeys.Error) ? data[YandexGamesKeys.Error].StringValue : string.Empty);
        }

        #endregion
    }
}
