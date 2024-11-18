using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public abstract class BaseGameService : MonoBehaviour, IGameService
    {
        [DllImport("__Internal")]
        private static extern bool MstIsMobile();

        private readonly List<UnityAction> readyCallbacks = new List<UnityAction>();
        private bool isReady = false;

        public GameServiceId Id { get; protected set; } = GameServiceId.Self;
        public bool IsMobile
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return MstIsMobile();
#else
                return Application.isMobilePlatform;
#endif
                //return true;
            }
        }

        protected virtual void Awake() { }

        #region AUTHENTICATION

        public PlayerInfo Player { get; protected set; } = new PlayerInfo();
        public abstract void Authenticate(SuccessCallback callback);

        #endregion

        #region PURCHASES

        public bool IsInAppPurchaseSupported { get; protected set; } = false;
        public IEnumerable<ProductInfo> Products { get; protected set; } = Enumerable.Empty<ProductInfo>();
        public MstJson Purchases { get; protected set; } = MstJson.EmptyArray;
        public abstract void GetProducts(SuccessCallback callback);
        public abstract void Purchase(string productId, SuccessCallback callback);
        public abstract void GetProductPurchases(SuccessCallback callback);

        #endregion

        #region ADDVERTISEMENT

        public bool IsAdSupported { get; protected set; }

        #endregion

        public abstract void Init();

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

        public virtual void GetString(string key, UnityAction<bool, string> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, PlayerPrefs.GetString(key));
            }
            else
            {
                callback?.Invoke(false, string.Empty);
            }
        }

        public virtual void GetFloat(string key, UnityAction<bool, float> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, PlayerPrefs.GetFloat(key));
            }
            else
            {
                callback?.Invoke(false, 0);
            }
        }

        public virtual void GetInt(string key, UnityAction<bool, int> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, PlayerPrefs.GetInt(key));
            }
            else
            {
                callback?.Invoke(false, 0);
            }
        }

        public virtual void GetBool(string key, UnityAction<bool, bool> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                callback?.Invoke(true, bool.Parse(PlayerPrefs.GetString(key)));
            }
            else
            {
                callback?.Invoke(false, false);
            }
        }

        #endregion
    }
}