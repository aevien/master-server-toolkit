using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public class GameBridge : SingletonBehaviour<GameBridge>
    {
        private IGameService _service;
        public static IGameService Service => Instance._service;
        public static PlayerInfo Player => Service.Player;
        public static IEnumerable<ProductInfo> Products => Service.Products;

        protected override void Awake()
        {
            base.Awake();
            var json = MstWebBrowser.GetQueryStringData();

#if UNITY_EDITOR
            string pw3Auth = Mst.Args.AsString(GameServiceArgNames.PW3_AUTH_KEY);

            if (!string.IsNullOrEmpty(pw3Auth))
            {
                json.SetField("pw3_auth", pw3Auth);
            }
#endif

            if (json.HasField("pw3_auth"))
            {
                _service = new PlayWeb3Service();
            }
            else
            {
                _service = new SelfService();
            }

            Instance.StartCoroutine(_service.Init());
        }

        #region EVENTS

        public static void OnReady(UnityAction callback)
        {
            Service.OnReady(callback);
        } 

        #endregion

        #region ACCOUNT

        public static void Authenticate(SuccessCallback callback)
        {
            Instance.StartCoroutine(Service.Authenticate(callback));
        }

        #endregion

        #region PURCHASE

        public static void GetProducts(SuccessCallback callback)
        {
            Instance.StartCoroutine(Service.GetProducts(callback));
        }

        public static void GetProductPurchases(SuccessCallback callback)
        {
            Instance.StartCoroutine(Service.GetProductPurchases(callback));
        } 

        #endregion

        #region STORAGE

        public static void SetString(string key, string value)
        {
            Service.SetString(key, value);
        }

        public static void SetFloat(string key, float value)
        {
            Service.SetFloat(key, value);
        }

        public static void SetInt(string key, int value)
        {
            Service.SetInt(key, value);
        }

        public static void SetBool(string key, bool value)
        {
            Service.SetBool(key, value);
        }

        public static void GetString(string key, UnityAction<bool, string> callback)
        {
            Instance.StartCoroutine(Service.GetString(key, callback));
        }

        public static void GetFloat(string key, UnityAction<bool, float> callback)
        {
            Instance.StartCoroutine(Service.GetFloat(key, callback));
        }

        public static void GetInt(string key, UnityAction<bool, int> callback)
        {
            Instance.StartCoroutine(Service.GetInt(key, callback));
        }

        public static void GetBool(string key, UnityAction<bool, bool> callback)
        {
            Instance.StartCoroutine(Service.GetBool(key, callback));
        }

        #endregion
    }
}