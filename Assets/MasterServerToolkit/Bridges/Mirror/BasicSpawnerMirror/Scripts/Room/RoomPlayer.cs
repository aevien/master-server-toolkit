#if MIRROR
using MasterServerToolkit.MasterServer;
using Mirror;
using System;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomPlayer
    {
        public RoomPlayer()
        {
        }

        public RoomPlayer(int msfPeerId, NetworkConnection mirrorPeer, string userId, string username, MstProperties customOptions)
        {
            MasterPeerId = msfPeerId;
            MirrorPeer = mirrorPeer ?? throw new ArgumentNullException(nameof(mirrorPeer));
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            CustomOptions = customOptions ?? throw new ArgumentNullException(nameof(customOptions));
            Profile = new ObservableServerProfile(UserId);
        }

        /// <summary>
        /// Id of the Masterserver connection
        /// </summary>
        public int MasterPeerId { get; set; }

        /// <summary>
        /// Connection of mirror
        /// </summary>
        public NetworkConnection MirrorPeer { get; set; }

        /// <summary>
        /// Username
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// UserId
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Custom options user can use in game
        /// </summary>
        public MstProperties CustomOptions { get; set; }

        /// <summary>
        /// Player profile
        /// </summary>
        public ObservableServerProfile Profile { get; set; }

        public override string ToString()
        {
            MstProperties options = new MstProperties();
            options.Add("Username", Username);
            options.Add("MirrorPeerId", MirrorPeer.connectionId);
            options.Add("MsfPeerId", MasterPeerId);
            options.Append(Profile.ToStringsDictionary());
            options.Append(CustomOptions);

            return options.ToReadableString();
        }
    }
}
#endif