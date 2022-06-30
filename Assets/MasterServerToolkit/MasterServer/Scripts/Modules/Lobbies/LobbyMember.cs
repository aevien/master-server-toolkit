namespace MasterServerToolkit.MasterServer
{
    public class LobbyMember
    {
        /// <summary>
        /// True, if member is ready to play
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Lobby user extension assigned to this lobby member
        /// </summary>
        public LobbyUserPeerExtension Extension { get; private set; }

        /// <summary>
        /// Player's properties
        /// </summary>
        public MstProperties Properties { get; protected set; }

        /// <summary>
        /// Current lobby member username
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// A lobby team, to which this member belongs
        /// </summary>
        public virtual LobbyTeam Team { get; set; }

        public LobbyMember(string username, LobbyUserPeerExtension ext)
        {
            Username = username;
            Extension = ext;
            Properties = new MstProperties();
        }

        /// <summary>
        /// Changes property value of the player
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetProperty(string key, string value)
        {
            Properties.Set(key, value);
        }

        /// <summary>
        /// Retrieves a property value of current member
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetProperty(string key)
        {
            return Properties.AsString(key);
        }

        /// <summary>
        /// Creates a lobby member data packet
        /// </summary>
        /// <returns></returns>
        public virtual LobbyMemberData GenerateDataPacket()
        {
            return new LobbyMemberData()
            {
                IsReady = IsReady,
                Username = Username,
                Properties = Properties,
                Team = Team != null ? Team.Name : ""
            };
        }
    }
}