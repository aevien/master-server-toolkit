using MasterServerToolkit.Json;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class VkGamesStorageModule : BaseStorageModule
    {
        private const float LoadRetryDelaySeconds = 5f;

        [DllImport("__Internal")] private static extern void Gb_Vk_GetPlayerData(string key);
        [DllImport("__Internal")] private static extern void Gb_Vk_SetPlayerData(string key, string data);

        private Coroutine initRoutine;
        private Coroutine loadRetryRoutine;
        private Coroutine saveRoutine;
        private MstJson pendingData;
        private MstJson savingData;
        private bool hasLoadedRemoteData;
        private bool flushWhenPossible;
        private bool isLoadInProgress;
        private bool isSaveInProgress;

        public override void OnInit(IService service)
        {
            if (initRoutine != null)
                return;

            base.OnInit(service);
            IsSupported = true;
            Service.OnPauseEvent += Service_OnPauseEvent;
            initRoutine = StartCoroutine(InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            yield return null;
            float elapsed = 0f;
            while (!Service.Player.IsReady && elapsed < Service.WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            RefreshData();
        }

        public override void LoadData(StorageDataHandler callback)
        {
            base.LoadData(callback);
            RefreshData();
        }

        public void RefreshData()
        {
            if (isLoadInProgress)
                return;

            if (loadRetryRoutine != null)
            {
                StopCoroutine(loadRetryRoutine);
                loadRetryRoutine = null;
            }

            isLoadInProgress = true;
            Gb_Vk_GetPlayerData(GetStorageKey());
        }

        public override void SaveData(MstJson data, bool saveAsStats = false)
        {
            pendingData = (data ?? MstJson.CreateObject()).Copy();

            if (!hasLoadedRemoteData)
            {
                ScheduleLoadRetry();
                return;
            }

            ScheduleSave();
        }

        private void ScheduleSave()
        {
            if (saveRoutine == null && !isSaveInProgress && pendingData != null)
                saveRoutine = StartCoroutine(SaveCoroutine());
        }

        private IEnumerator SaveCoroutine()
        {
            yield return new WaitForSecondsRealtime(Service.Options[nameof(VkGamesSdkSettings.saveInterval)].FloatValue);
            saveRoutine = null;
            FlushPendingData();
        }

        private void FlushPendingData()
        {
            if (!hasLoadedRemoteData || isSaveInProgress || pendingData == null)
                return;

            if (saveRoutine != null)
            {
                StopCoroutine(saveRoutine);
                saveRoutine = null;
            }

            savingData = pendingData;
            pendingData = null;
            isSaveInProgress = true;
            Gb_Vk_SetPlayerData(GetStorageKey(), savingData.ToString());
        }

        private void ScheduleLoadRetry()
        {
            if (!hasLoadedRemoteData && !isLoadInProgress && loadRetryRoutine == null)
                loadRetryRoutine = StartCoroutine(LoadRetryCoroutine());
        }

        private IEnumerator LoadRetryCoroutine()
        {
            yield return new WaitForSecondsRealtime(LoadRetryDelaySeconds);
            loadRetryRoutine = null;
            RefreshData();
        }

        private string GetStorageKey() => Service.Options[nameof(VkGamesSdkSettings.storageKey)].StringValue;

        protected void Vk_OnPlayerGetData(string json)
        {
            bool isValidResponse = MstJson.IsJson(json);
            var response = isValidResponse ? new MstJson(json) : MstJson.CreateObject();
            isLoadInProgress = false;

            bool hasData = response.HasField("data") && response["data"].IsObject;
            if (!isValidResponse || response.HasField("error") || !hasData)
            {
                string error = response.HasField("error")
                    ? response["error"].StringValue
                    : "invalid_response";
                Logger.Warn($"VK Storage load failed. Error: {error}");
                ScheduleLoadRetry();

                if (!IsReady)
                {
                    IsReady = true;
                    initRoutine = null;
                }

                return;
            }

            MstJson loadedData = response["data"];

            if (pendingData != null)
            {
                MergeTopLevelFields(loadedData, pendingData);
                pendingData = loadedData.Copy();
            }

            Data = loadedData;
            hasLoadedRemoteData = true;
            IsReady = true;
            initRoutine = null;
            NotifyOnLoad();

            if (hasLoadedRemoteData && pendingData != null)
            {
                if (flushWhenPossible)
                    FlushPendingData();
                else
                    ScheduleSave();
            }
        }

        protected void Vk_OnPlayerSetData(string json)
        {
            var response = MstJson.IsJson(json) ? new MstJson(json) : MstJson.CreateObject();
            bool success = response.HasField("success") && response["success"].BoolValue;
            string error = response.HasField("error") ? response["error"].StringValue : string.Empty;

            isSaveInProgress = false;
            if (!success && pendingData == null)
                pendingData = savingData;

            savingData = null;
            NotifyOnSave(success, error);

            if (pendingData != null)
            {
                if (success && flushWhenPossible)
                    FlushPendingData();
                else
                    ScheduleSave();
            }
            else
            {
                flushWhenPossible = false;
            }
        }

        private static void MergeTopLevelFields(MstJson target, MstJson source)
        {
            if (!target.IsObject || !source.IsObject || source.Keys == null)
                return;

            foreach (string key in source.Keys)
                target.SetField(key, source[key].Copy());
        }

        private void Service_OnPauseEvent(bool isPaused)
        {
            if (isPaused)
            {
                flushWhenPossible = true;
                FlushPendingData();
            }
            else
            {
                flushWhenPossible = false;
            }
        }

        private void OnDestroy()
        {
            if (Service != null)
                Service.OnPauseEvent -= Service_OnPauseEvent;
        }
    }
}
