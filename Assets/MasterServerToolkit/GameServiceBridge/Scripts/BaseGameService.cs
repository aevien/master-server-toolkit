using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public abstract class BaseGameService : IGameService
    {
        private readonly List<UnityAction> readyCallbacks = new List<UnityAction>();
        private bool isReady = false;

        public GameServiceId Id { get; protected set; } = GameServiceId.Self;
        public PlayerInfo Player { get; protected set; } = new PlayerInfo();
        public IEnumerable<ProductInfo> Products { get; protected set; } = Enumerable.Empty<ProductInfo>();
        public MstJson Purchases { get; protected set; } = MstJson.EmptyArray;
        public bool IsAdSupported { get; protected set; }

        public abstract IEnumerator Init();
        public abstract IEnumerator Authenticate(SuccessCallback callback);
        public abstract IEnumerator GetProducts(SuccessCallback callback);
        public abstract IEnumerator GetProductPurchases(SuccessCallback callback);

        protected void NotifyOnReady()
        {
            isReady = true;

            foreach (var callback in readyCallbacks)
            {
                callback?.Invoke();
            }

            readyCallbacks.Clear();
        }

        public void OnReady(UnityAction callback)
        {
            if (isReady)
            {
                callback?.Invoke();
            }
            else
            {
                readyCallbacks.Add(callback);
            }
        }

        #region STARAGE

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

        public virtual void SetBool(string key, bool value)
        {
            PlayerPrefs.SetString(key, value.ToString());
            PlayerPrefs.Save();
        }

        public virtual IEnumerator GetString(string key, UnityAction<bool, string> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, PlayerPrefs.GetString(key));
            }
            else
            {
                callback?.Invoke(false, string.Empty);
            }

            yield return new WaitForEndOfFrame();
        }

        public virtual IEnumerator GetFloat(string key, UnityAction<bool, float> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, PlayerPrefs.GetFloat(key));
            }
            else
            {
                callback?.Invoke(false, 0);
            }

            yield return new WaitForEndOfFrame();
        }

        public virtual IEnumerator GetInt(string key, UnityAction<bool, int> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, PlayerPrefs.GetInt(key));
            }
            else
            {
                callback?.Invoke(false, 0);
            }

            yield return new WaitForEndOfFrame();
        }

        public virtual IEnumerator GetBool(string key, UnityAction<bool, bool> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, bool.Parse(PlayerPrefs.GetString(key)));
            }
            else
            {
                callback?.Invoke(false, false);
            }

            yield return new WaitForEndOfFrame();
        }

        #endregion
    }
}