using MasterServerToolkit.MasterServer;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class ProfileLoaderBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        [SerializeField]
        private ObservablePropertyPopulatorsDatabase populatorsDatabase;

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

            if (Mst.Client.Profiles.HasProfile == false)
            {
                Profile = CreateProfile();
            }
            else
            {
                Profile = Mst.Client.Profiles.Current;
            }
        }

        /// <summary>
        /// Invokes when user profile is loaded
        /// </summary>
        protected virtual void OnProfileLoaded() { }
        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnProfileLoadFailed() { }

        protected ObservableProfile CreateProfile()
        {
            var profile = new ObservableProfile();

            foreach (var populator in populatorsDatabase.Populators)
            {
                profile.Add(populator.Populate());
            }

            return profile;
        }

        /// <summary>
        /// Get profile data from master
        /// </summary>
        public virtual void LoadProfile()
        {
            if (Mst.Client.Profiles.HasProfile == false)
            {
                Mst.Client.Profiles.FillInProfileValues(Profile, (isSuccessful, error) =>
                {
                    if (isSuccessful)
                    {
                        //Debug.Log(Profile.ToJson());
                        Logger.Info($"Profile is loaded");
                        OnProfileLoaded();
                        OnProfileLoadedEvent?.Invoke();
                    }
                    else
                    {
                        Logger.Error($"Could not load user profile. Error: {error}");
                        OnProfileLoadFailed();
                        OnProfileLoadFailedEvent?.Invoke();
                    }
                });
            }
        }
    }
}