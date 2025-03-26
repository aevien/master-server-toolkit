using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
        #region STORAGE

        public virtual void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public virtual void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }

        public virtual void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public virtual string GetString(string key, string defaultValue = "")
        {
            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetString(key);
            else
                return defaultValue;
        }

        public virtual float GetFloat(string key, float defaultValue = 0f)
        {
            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetFloat(key);
            else
                return defaultValue;
        }

        public virtual int GetInt(string key, int defaultValue = 0)
        {
            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetInt(key);
            else 
                return defaultValue;
        }

        #endregion
    }
}