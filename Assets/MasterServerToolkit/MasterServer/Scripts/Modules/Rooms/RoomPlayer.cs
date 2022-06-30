using System;

namespace MasterServerToolkit.MasterServer
{
    public class RoomPlayer
    {
        public RoomPlayer() { }

        public RoomPlayer(int masterPeerId, int roomPeer, string userId, string username, MstProperties customOptions)
        {
            MasterPeerId = masterPeerId;
            RoomPeerId = roomPeer;
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            CustomOptions = customOptions ?? throw new ArgumentNullException(nameof(customOptions));
            Profile = new ObservableServerProfile(UserId);
        }

        /// <summary>
        /// Master server connection id
        /// </summary>
        public int MasterPeerId { get; set; }

        /// <summary>
        /// Room server connection id
        /// </summary>
        public int RoomPeerId { get; set; }

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
            options.Add("RoomPeerId", RoomPeerId);
            options.Add("MasterPeerId", MasterPeerId);
            options.Append(Profile.ToStringsDictionary());
            options.Append(CustomOptions);
            return options.ToReadableString();
        }
    }
}