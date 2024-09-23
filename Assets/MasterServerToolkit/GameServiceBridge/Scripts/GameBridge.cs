using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public class GameBridge : SingletonBehaviour<GameBridge>
    {
        private IGameService _service;
        public static IGameService Service => Instance._service;

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
        }

        public static void Authenticate(UnityAction<bool> callback)
        {
            Instance.StartCoroutine(Service.Authenticate(callback));
        }

        public static void GetProducts(UnityAction<bool> callback)
        {
            Instance.StartCoroutine(Service.GetProducts(callback));
        }

        public static void GetProductPurchases(UnityAction<bool> callback)
        {
            Instance.StartCoroutine(Service.GetProductPurchases(callback));
        }
    }
}