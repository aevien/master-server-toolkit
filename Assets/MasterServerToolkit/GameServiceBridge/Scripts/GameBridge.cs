using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using System.Runtime.InteropServices;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public class GameBridge : SingletonBehaviour<GameBridge>
    {
        [DllImport("__Internal")]
        private static extern IntPtr MstGetPlatformId();

        private IGameService _service;
        public static IGameService Service => Instance._service;

        protected override void Awake()
        {
            base.Awake();

            var serviceId = GetPlatform();

            switch (serviceId)
            {
                case GameServiceId.PlayWeb3:
                    _service = gameObject.AddComponent<PlayWeb3Service>();
                    break;
                default:
                    _service = gameObject.AddComponent<SelfService>();
                    break;
            }

            _service.Init();
        }

        private GameServiceId GetPlatform()
        {
            GameServiceId platformId = GameServiceId.Self;

#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr ptr = MstGetPlatformId();
            string platformIdStr = Marshal.PtrToStringUTF8(ptr);
            platformId = Enum.Parse<GameServiceId>(platformIdStr);
#else
            string pw3Auth = Mst.Args.AsString(GameServiceArgNames.PW3_AUTH_KEY);

            if (!string.IsNullOrEmpty(pw3Auth))
            {
                platformId = GameServiceId.PlayWeb3;
            }
#endif
            return platformId;
        }

        #region EVENTS

        public static void OnReady(UnityAction callback)
        {
            Service.OnReady(callback);
        }

        #endregion
    }
}