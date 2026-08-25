using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public abstract class BaseService : MonoBehaviour, IService
    {
        private Coroutine initRoutine;

        public Logging.Logger Logger { get; set; }
        public GameServiceId Id { get; protected set; }
        public virtual string AppId { get; protected set; }
        public virtual string Lang { get; protected set; }
        public virtual long ServerTime { get; protected set; }
        public virtual ServiceDeviceType Device => ServiceDeviceType.Desktop;
        public virtual MstJson Payload { get; protected set; } = MstJson.CreateObject();
        public virtual ReferrerInfo Referrer { get; protected set; } = new ReferrerInfo();
        public virtual MstJson RemoteFlags { get; protected set; } = MstJson.CreateObject();

        public virtual bool IsReady => Player.IsReady &&
                    IAP.IsReady &&
                    Leaderboards.IsReady &&
                    Ad.IsReady &&
                    Analytics.IsReady &&
                    Storage.IsReady &&
                    Share.IsReady;

        public IPlayerModule Player { get; protected set; }
        public IInAppPurchaseModule IAP { get; protected set; }
        public ILeaderboardsModule Leaderboards { get; protected set; }
        public IAdvertisementModule Ad { get; protected set; }
        public IAnalyticsModule Analytics { get; protected set; }
        public IStorageModule Storage { get; protected set; }
        public IShareModule Share { get; protected set; }
        public MstJson Options { get; protected set; }
        public float WaitForReadyTime { get; set; }

        public event ReadyHandler OnReadyEvent;
        public event PauseHandler OnPauseEvent;
        public event AccountSelectionDialogHandler OnAccountSelectionDialogEvent;

        public void OnBeforeInit()
        {
            OnBeforeInit(MstJson.CreateObject());
        }

        public virtual void OnBeforeInit(MstJson options)
        {
            Options = options;

            foreach (var module in GetComponents<BaseServiceModule>())
                module.OnBeforeInit(this);
        }

        public virtual void OnInit()
        {
            if (initRoutine != null)
                return;

            foreach (var module in GetComponents<BaseServiceModule>())
                module.OnInit(this);

            initRoutine = StartCoroutine(InitCoroutine());
        }

        public virtual void OnAfterInit()
        {
            foreach (var module in GetComponents<BaseServiceModule>())
                module.OnAfterInit(this);
        }

        public void OnReady()
        {
            OnReadyEvent?.Invoke(IsReady);
        }

        protected virtual IEnumerator InitCoroutine()
        {
            yield return null;

            float elapsed = 0f;

            while (!IsReady && elapsed < WaitForReadyTime)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Mst.Localization.Lang = Lang;

            yield return new WaitForEndOfFrame();

            if (IsReady)
            {
                foreach (var module in GetComponents<BaseServiceModule>())
                    module.OnReady(this);

                Logger.Info($"Service {Id} started and ready to be used");
            }
            else
            {
                Logger.Error($"Service {Id} could not be started");
            }

            OnReady();
            initRoutine = null;
        }

        public virtual void GameLoaded()
        {
            GameLoaded(MstJson.CreateObject());
        }

        public virtual void GameLoaded(MstJson options)
        {
            Logger.Info($"Game loaded with options: {options}");
        }

        public virtual void GameStart()
        {
            GameStart(MstJson.CreateObject());
        }

        public virtual void GameStart(MstJson options)
        {
            Logger.Info($"Game start with options: {options}");
        }

        public virtual void GameStop()
        {
            GameStop(MstJson.CreateObject());
        }

        public virtual void GameStop(MstJson options)
        {
            Logger.Info($"Game stop with options: {options}");
        }

        protected T AddModule<T>() where T : BaseServiceModule
        {
            return gameObject.AddComponent<T>();
        }

        protected void NotifyOnPause(bool state)
        {
            OnPauseEvent?.Invoke(state);
        }

        protected void NotifyOnAccountSelectionDialog(bool opened)
        {
            OnAccountSelectionDialogEvent?.Invoke(opened);
        }
    }
}
