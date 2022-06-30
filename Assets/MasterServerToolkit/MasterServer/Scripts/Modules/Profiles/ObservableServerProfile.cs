using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents clients profile, which emits events about changes.
    /// Client, game server and master servers will create a similar
    /// object.
    /// </summary>
    public class ObservableServerProfile : ObservableProfile
    {
        /// <summary>
        /// User id of the client, who's profile this is
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// Peer of the owner
        /// </summary>
        public IPeer ClientPeer { get; set; }

        /// <summary>
        /// Should this profile to be saved to database
        /// </summary>
        public bool ShouldBeSavedToDatabase { get; set; } = true;

        /// <summary>
        /// Fires when profile modified in server
        /// </summary>
        public event Action<ObservableServerProfile> OnModifiedInServerEvent;

        /// <summary>
        /// Fires when profile is destroyed
        /// </summary>
        public event Action<ObservableServerProfile> OnDisposedEvent;

        /// <summary>
        /// Creates new instance of obsrvable profile
        /// </summary>
        /// <param name="username"></param>
        public ObservableServerProfile(string userId)
        {
            UserId = userId;
        }

        /// <summary>
        /// Creates new instance of obsrvable profile
        /// </summary>
        /// <param name="username"></param>
        /// <param name="peer"></param>
        public ObservableServerProfile(string userId, IPeer peer)
        {
            UserId = userId;
            ClientPeer = peer;
        }

        /// <summary>
        /// Destroys this instance of profile
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    OnDisposedEvent?.Invoke(this);
                    OnModifiedInServerEvent = null;
                    OnDisposedEvent = null;
                    ClearUpdates();
                }
            }

            isDisposed = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="property"></param>
        protected override void OnDirtyPropertyEventHandler(IObservableProperty property)
        {
            //Logs.Info($"<color=#FF0000>{GetType().Name} OnDirtyPropertyEventHandler for {property.Key}</color>");
            base.OnDirtyPropertyEventHandler(property);
            OnModifiedInServerEvent?.Invoke(this);
        }
    }
}