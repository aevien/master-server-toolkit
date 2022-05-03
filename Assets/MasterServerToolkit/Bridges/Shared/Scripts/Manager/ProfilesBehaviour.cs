using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Games
{
    public class ProfilesBehaviour : BaseClientBehaviour
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
        protected ObservableProfile profile;

        protected override void OnInitialize()
        {
            base.OnInitialize();

            profile = new ObservableProfile();
        }

        /// <summary>
        /// Invokes when user profile is loaded
        /// </summary>
        public virtual void OnProfileLoaded() { }

        /// <summary>
        /// Get profile data from master
        /// </summary>
        public virtual void LoadProfile()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Loading profile... Please wait!");

            MstTimer.WaitForSeconds(0.2f, () =>
            {
                Mst.Client.Profiles.FillInProfileValues(profile, (isSuccessful, error) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        OnProfileLoadedEvent?.Invoke();
                    }
                    else
                    {
                        logger.Error($"Could not load user profile. Error: {error}");
                        OnProfileLoadFailedEvent?.Invoke();
                    }
                });
            });
        }
    }
}