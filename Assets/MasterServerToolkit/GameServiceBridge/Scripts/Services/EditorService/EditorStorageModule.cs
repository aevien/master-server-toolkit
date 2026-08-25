using MasterServerToolkit.Json;
using System;
using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class EditorStorageModule : BaseStorageModule
    {
        private const string StorageKeyPrefix = "editorStorage:";

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            LoadCurrentPlayerData();
            StartCoroutine(NotifyCoroutine());
        }

        public override void LoadData(StorageDataHandler callback)
        {
            base.LoadData(callback);
            LoadCurrentPlayerData();
            NotifyOnLoad();
        }

        public override void SaveData(MstJson data, bool saveAsStats = false)
        {
            base.SaveData(data, saveAsStats);
            Data = data ?? MstJson.CreateObject();

            try
            {
                PlayerPrefs.SetString(GetStorageKey(), Data.ToString());
                PlayerPrefs.Save();
                NotifyOnSave(true, string.Empty);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to save Editor storage for player '{Service.Player.Id}': {exception}");
                NotifyOnSave(false, exception.Message);
            }
        }

        private IEnumerator NotifyCoroutine()
        {
            yield return null;
            IsReady = true;
            NotifyOnLoad();
        }

        private void LoadCurrentPlayerData()
        {
            try
            {
                string rawData = PlayerPrefs.GetString(GetStorageKey(), string.Empty);
                bool hasValidData = !string.IsNullOrWhiteSpace(rawData) && MstJson.IsJson(rawData);
                Data = hasValidData ? new MstJson(rawData) : MstJson.CreateObject();

                if (!string.IsNullOrWhiteSpace(rawData) && !hasValidData)
                    Logger.Warn($"Ignored invalid Editor storage data for player '{Service.Player.Id}'");
            }
            catch (Exception exception)
            {
                Data = MstJson.CreateObject();
                Logger.Error($"Failed to load Editor storage for player '{Service.Player.Id}': {exception}");
            }
        }

        private string GetStorageKey() => $"{StorageKeyPrefix}{Service.Player.Id ?? string.Empty}";
    }
}
