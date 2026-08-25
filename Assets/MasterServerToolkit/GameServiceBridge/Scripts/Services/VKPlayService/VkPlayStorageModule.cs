using MasterServerToolkit.Json;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class VkPlayStorageModule : BaseStorageModule
    {
        private const string storageKeyPrefix = "vkplayStorage";

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsSupported = true;
            LoadFromPlayerPrefs();
            IsReady = true;
            NotifyOnLoad();
        }

        public override void LoadData(StorageDataHandler callback)
        {
            base.LoadData(callback);
            LoadFromPlayerPrefs();
            NotifyOnLoad();
        }

        public override void SaveData(MstJson data, bool saveAsStats = false)
        {
            Data = data ?? MstJson.CreateObject();
            PlayerPrefs.SetString(StorageKey, Data.ToString());
            PlayerPrefs.Save();
            NotifyOnSave(true, string.Empty);
        }

        private void LoadFromPlayerPrefs()
        {
            string rawData = PlayerPrefs.GetString(StorageKey, string.Empty);
            Data = string.IsNullOrWhiteSpace(rawData) ? MstJson.CreateObject() : new MstJson(rawData);
        }

        private string StorageKey
        {
            get
            {
                string playerId = Service?.Player?.Id;
                return string.IsNullOrWhiteSpace(playerId) ? storageKeyPrefix : $"{storageKeyPrefix}:{playerId}";
            }
        }
    }
}
