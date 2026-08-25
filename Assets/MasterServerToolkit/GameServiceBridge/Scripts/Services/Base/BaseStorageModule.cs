using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.GameService
{
    public abstract class BaseStorageModule : BaseServiceModule, IStorageModule
    {
        protected StorageDataHandler playerDataCallback;

        public event StorageDataHandler OnLoadEvent;
        public event SuccessCallback OnSaveEvent;

        public MstJson Data { get; protected set; } = MstJson.CreateObject();

        protected void NotifyOnLoad()
        {
            playerDataCallback?.Invoke(Data);
            OnLoadEvent?.Invoke(Data);
            playerDataCallback = null;
        }

        protected void NotifyOnSave(bool isSuccess, string error)
        {
            OnSaveEvent?.Invoke(isSuccess, error);
        }

        public void SetString(string key, string value)
        {
            if (!Data.HasField(key))
            {
                Data.AddField(key, value);
            }
            else
            {
                Data.SetField(key, value);
            }

            SaveData(Data, false);
        }

        public void SetFloat(string key, float value)
        {
            if (!Data.HasField(key))
            {
                Data.AddField(key, value);
            }
            else
            {
                Data.SetField(key, value);
            }

            SaveData(Data, false);
        }

        public void SetInt(string key, int value)
        {
            if (!Data.HasField(key))
            {
                Data.AddField(key, value);
            }
            else
            {
                Data.SetField(key, value);
            }

            SaveData(Data, false);
        }

        public void SetBool(string key, bool value)
        {
            if (!Data.HasField(key))
            {
                Data.AddField(key, value);
            }
            else
            {
                Data.SetField(key, value);
            }

            SaveData(Data, false);
        }

        public string GetString(string key, string defaultValue = "")
        {
            if (Data.HasField(key))
            {
                return Data[key].StringValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public float GetFloat(string key, float defaultValue = 0f)
        {
            if (Data.HasField(key))
            {
                return Data[key].FloatValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            if (Data.HasField(key))
            {
                return Data[key].IntValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (Data.HasField(key))
            {
                return Data[key].BoolValue;
            }
            else
            {
                return defaultValue;
            }
        }

        public virtual void SaveData(MstJson data, bool saveAsStats = false) { }

        public virtual void LoadData(StorageDataHandler callback)
        {
            playerDataCallback = callback;
        }
    }
}