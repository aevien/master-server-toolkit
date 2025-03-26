using MasterServerToolkit.Json;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern bool MstIsMobile();
        [DllImport("__Internal")]
        private static extern string MstGetQueryString();
        [DllImport("__Internal")]
        private static extern string MstGetBrowserLang();
#endif

        private bool isReady = false;

        protected MstJson options = MstJson.EmptyObject;

        /// <summary>
        /// Current service the game runs on
        /// </summary>
        public GameServiceId Id { get; protected set; } = GameServiceId.Self;
        /// <summary>
        /// Game id
        /// </summary>
        public virtual string AppId { get; protected set; } = "5749032816215847";
        /// <summary>
        /// Current language
        /// </summary>
        public virtual string Lang
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                return MstGetBrowserLang();
#else
                return "en";
#endif
            }
        }
        /// <summary>
        /// The device that the game is currently running on
        /// </summary>
        public virtual string DeviceType
        {
            get
            {
                return IsMobile ? "mobile" : "desktop";
            }
        }
        /// <summary>
        /// Useful data that is transmitted to the game at startup, if the game is web, 
        /// then the data can be transmitted from the browser line 
        /// of the service on which the game is running
        /// </summary>
        public virtual MstJson Payload
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                return new MstJson(MstGetQueryString());
#else
                return MstJson.EmptyObject;
#endif
            }
        }
        /// <summary>
        /// Is the game running on a mobile device
        /// </summary>
        public virtual bool IsMobile
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
                return MstIsMobile();
#else
                return Application.isMobilePlatform;
#endif
            }
        }

        public bool IsReady => isReady;

        protected virtual void Awake() { }

        #region SERVICE

        public virtual void Init()
        {
            Init(MstJson.EmptyObject);
        }

        public virtual void Init(MstJson options)
        {
            this.options = options;
        }

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game is ready to interact with the player. 
        /// For these purposes, call this method.
        /// </summary>
        public virtual void GameLoaded()
        {
            GameLoaded(MstJson.EmptyObject);
        }

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game is ready to interact with the player. 
        /// For these purposes, call this method.
        /// </summary>
        /// <param name="options"></param>
        public virtual void GameLoaded(MstJson options) { }

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has started. For these purposes, call this method.
        /// </summary>
        public virtual void GameStart()
        {
            GameStart(MstJson.EmptyObject);
        }

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has started. For these purposes, call this method.
        /// </summary>
        /// <param name="options"></param>
        public virtual void GameStart(MstJson options) { }

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has stopped. For these purposes, call this method.
        /// </summary>
        public virtual void GameStop()
        {
            GameStop(MstJson.EmptyObject);
        }

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has stopped. For these purposes, call this method.
        /// </summary>
        /// <param name="options"></param>
        public virtual void GameStop(MstJson options) { }

        #endregion
    }
}