using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using UnityEngine.Events;

namespace MasterServerToolkit.Games
{
    public class ProfileLoaderBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Invokes when profile is loaded
        /// </summary>
        public UnityEvent OnProfileLoadedEvent;

        /// <summary>
        /// Invokes when profile is not loaded successfully
        /// </summary>
        public UnityEvent OnProfileLoadFailedEvent;

        #endregion

        /// <summary>
        /// The loaded profile of client
        /// </summary>
        public ObservableProfile Profile { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public bool HasProfile => Profile != null;

        protected override void Awake()
        {
            base.Awake();
            Profile = new ObservableProfile();
        }

        /// <summary>
        /// Invokes when user profile is loaded
        /// </summary>
        protected virtual void OnProfileLoaded() { }
        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnProfileLoadFailed() { }

        /// <summary>
        /// Get profile data from master
        /// </summary>
        public virtual void LoadProfile()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Loading profile... Please wait!");

            Tweener.DelayedCall(0.1f, () =>
            {
                Mst.Client.Profiles.FillInProfileValues(Profile, (isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        OnProfileLoaded();
                        OnProfileLoadedEvent?.Invoke();
                    }
                    else
                    {
                        logger.Error($"Could not load user profile. Error: {error}");

                        OnProfileLoadFailed();
                        OnProfileLoadFailedEvent?.Invoke();
                    }
                });
            });
        }
    }
}