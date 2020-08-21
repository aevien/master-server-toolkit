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
        /// Username of the client, who's profile this is
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Peer of the owner
        /// </summary>
        public IPeer ClientPeer { get; set; }

        /// <summary>
        /// Should this profile to be saved to database
        /// </summary>
        public bool ShouldBeSavedToDatabase { get; set; } = true;

        /// <summary>
        /// When profile modified in server
        /// </summary>
        public event Action<ObservableServerProfile> OnModifiedInServerEvent;
        public event Action<ObservableServerProfile> OnDisposedEvent;

        public ObservableServerProfile(string username)
        {
            Username = username;
        }

        public ObservableServerProfile(string username, IPeer peer)
        {
            Username = username;
            ClientPeer = peer;
        }

        protected override void OnDirtyProperty(IObservableProperty property)
        {
            base.OnDirtyProperty(property);
            OnModifiedInServerEvent?.Invoke(this);
        }

        protected void Dispose()
        {
            if (OnDisposedEvent != null)
            {
                Dispose();
            }

            OnModifiedInServerEvent = null;
            OnDisposedEvent = null;

            UnsavedProperties.Clear();
            ClearUpdates();
        }
    }
}