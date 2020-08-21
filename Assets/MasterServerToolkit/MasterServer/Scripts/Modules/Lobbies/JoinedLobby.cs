using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public delegate void PlayerPropertyChangedHandler(LobbyMemberData member, string propertyKey, string propertyValue);
    public delegate void LobbyPropertyChangeHandler(string property, string key);

    /// <summary>
    /// Represents a joined lobby. When player joins a lobby,
    /// an instance of this class is created. It acts as a convenient way
    /// to manage lobby state from player perspective
    /// </summary>
    public class JoinedLobby
    {
        public LobbyDataPacket Data { get; private set; }
        private readonly IClientSocket _connection;

        public Dictionary<string, string> Properties { get; private set; }
        public Dictionary<string, LobbyMemberData> Members { get; private set; }
        public Dictionary<string, LobbyTeamData> Teams { get; private set; }

        public ILobbyListener Listener { get; private set; }

        public string LobbyName { get { return Data.LobbyName; } }

        public int Id { get { return Data.LobbyId; } }

        public LobbyState State { get { return Data.LobbyState; } }

        public bool HasLeft { get; private set; }

        public JoinedLobby(LobbyDataPacket data, IClientSocket connection)
        {
            Data = data;
            _connection = connection;
            connection.SetHandler((short)MstMessageCodes.LobbyMemberPropertyChanged, HandleMemberPropertyChanged);
            connection.SetHandler((short)MstMessageCodes.LeftLobby, HandleLeftLobbyMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyChatMessage, HandleLobbyChatMessageMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyMemberJoined, HandleLobbyMemberJoinedMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyMemberLeft, HandleLobbyMemberLeftMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyStateChange, HandleLobbyStateChangeMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyStatusTextChange, HandleLobbyStatusTextChangeMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyMemberChangedTeam, HandlePlayerTeamChangeMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyMemberReadyStatusChange, HandleLobbyMemberReadyStatusChangeMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyMasterChange, HandleLobbyMasterChangeMsg);
            connection.SetHandler((short)MstMessageCodes.LobbyPropertyChanged, HandleLobbyPropertyChanged);

            Properties = data.LobbyProperties;
            Members = data.Players;
            Teams = data.Teams;
        }

        /// <summary>
        /// Leaves this lobby
        /// </summary>
        public void Leave()
        {
            Mst.Client.Lobbies.LeaveLobby(Id, () => { }, _connection);
        }

        /// <summary>
        /// Leaves this lobby
        /// </summary>
        /// <param name="callback"></param>
        public void Leave(Action callback)
        {
            Mst.Client.Lobbies.LeaveLobby(Id, callback, _connection);
        }

        /// <summary>
        /// Sets a lobby property to a specified value
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetLobbyProperty(string key, string value)
        {
            SetLobbyProperty(key, value, (successful, error) => { });
        }

        /// <summary>
        /// Sets a lobby property to a specified value
        /// </summary>
        public void SetLobbyProperty(string key, string value, SuccessCallback callback)
        {
            var data = new Dictionary<string, string>()
            {
                {key, value }
            };

            Mst.Client.Lobbies.SetLobbyProperties(Id, data, callback, _connection);
        }

        /// <summary>
        /// Sets a lobby properties to values, provided within a dictionary
        /// </summary>
        public void SetLobbyProperties(Dictionary<string, string> properties, SuccessCallback callback)
        {
            Mst.Client.Lobbies.SetLobbyProperties(Id, properties, callback, _connection);
        }

        /// <summary>
        /// Sets current player's properties
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetMyProperty(string key, string value)
        {
            SetMyProperty(key, value, (successful, error) => { });
        }


        /// <summary>
        /// Set's current player's properties
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="callback"></param>
        public void SetMyProperty(string key, string value, SuccessCallback callback)
        {
            var data = new Dictionary<string, string>()
            {
                {key, value }
            };

            Mst.Client.Lobbies.SetMyProperties(data, callback, _connection);
        }

        /// <summary>
        /// Set's current player's properties
        /// </summary>
        public void SetMyProperties(Dictionary<string, string> properties, SuccessCallback callback)
        {
            Mst.Client.Lobbies.SetMyProperties(properties, callback, _connection);
        }

        /// <summary>
        /// Set's current player's ready status
        /// </summary>
        /// <param name="isReady"></param>
        public void SetReadyStatus(bool isReady)
        {
            Mst.Client.Lobbies.SetReadyStatus(isReady, (successful, error) => { }, _connection);
        }

        /// <summary>
        /// Set's current player's ready status
        /// </summary>
        public void SetReadyStatus(bool isReady, SuccessCallback callback)
        {
            Mst.Client.Lobbies.SetReadyStatus(isReady, callback, _connection);
        }

        /// <summary>
        /// Set's a lobby event listener
        /// </summary>
        /// <param name="listener"></param>
        public void SetListener(ILobbyListener listener)
        {
            Listener = listener;
            Listener?.Initialize(this);
        }

        /// <summary>
        /// Send's a lobby chat message
        /// </summary>
        /// <param name="message"></param>
        public void SendChatMessage(string message)
        {
            Mst.Client.Lobbies.SendChatMessage(message, _connection);
        }

        /// <summary>
        /// Switches current user to another team (if available)
        /// </summary>
        /// <param name="teamName"></param>
        /// <param name="callback"></param>
        public void JoinTeam(string teamName, SuccessCallback callback)
        {
            Mst.Client.Lobbies.JoinTeam(Id, teamName, callback, _connection);
        }

        /// <summary>
        /// Sends a request to server to start a match
        /// </summary>
        /// <param name="callback"></param>
        public void StartGame(SuccessCallback callback)
        {
            Mst.Client.Lobbies.StartGame(callback, _connection);
        }

        /// <summary>
        /// Retrieves an access to room, which is assigned to this lobby
        /// </summary>
        /// <param name="callback"></param>
        public void GetLobbyRoomAccess(RoomAccessCallback callback)
        {
            Mst.Client.Lobbies.GetLobbyRoomAccess(new Dictionary<string, string>(), callback, _connection);
        }

        /// <summary>
        /// Retrieves an access to room, which is assigned to this lobby
        /// </summary>
        public void GetLobbyRoomAccess(Dictionary<string, string> properties, RoomAccessCallback callback)
        {
            Mst.Client.Lobbies.GetLobbyRoomAccess(properties, callback, _connection);
        }

        #region Handlers

        private void HandleMemberPropertyChanged(IIncommingMessage message)
        {
            var data = message.Deserialize(new LobbyMemberPropChangePacket());

            if (Id != data.LobbyId)
            {
                return;
            }

            Members.TryGetValue(data.Username, out LobbyMemberData member);

            if (member == null)
            {
                return;
            }

            member.Properties.Set(data.Property, data.Value);

            Listener?.OnMemberPropertyChanged(member, data.Property, data.Value);
        }

        private void HandleLeftLobbyMsg(IIncommingMessage message)
        {
            var id = message.AsInt();

            // Check the id in case there's something wrong with message order
            if (Id != id)
            {
                return;
            }

            HasLeft = true;

            Listener?.OnLobbyLeft();
        }

        private void HandleLobbyChatMessageMsg(IIncommingMessage message)
        {
            var msg = message.Deserialize(new LobbyChatPacket());

            Listener?.OnChatMessageReceived(msg);
        }

        private void HandleLobbyMemberLeftMsg(IIncommingMessage message)
        {
            var username = message.AsString();

            LobbyMemberData member;
            Members.TryGetValue(username, out member);

            if (member == null)
            {
                return;
            }

            Listener?.OnMemberLeft(member);
        }

        private void HandleLobbyMemberJoinedMsg(IIncommingMessage message)
        {
            var data = message.Deserialize(new LobbyMemberData());
            Members[data.Username] = data;
            Listener?.OnMemberJoined(data);
        }

        private void HandleLobbyMasterChangeMsg(IIncommingMessage message)
        {
            var masterUsername = message.AsString();

            Data.GameMaster = masterUsername;
            Listener?.OnMasterChanged(masterUsername);
        }

        private void HandleLobbyMemberReadyStatusChangeMsg(IIncommingMessage message)
        {
            var data = message.Deserialize(new StringPairPacket());

            LobbyMemberData member;
            Members.TryGetValue(data.A, out member);

            if (member == null)
            {
                return;
            }

            member.IsReady = bool.Parse(data.B);

            Listener?.OnMemberReadyStatusChanged(member, member.IsReady);
        }

        private void HandlePlayerTeamChangeMsg(IIncommingMessage message)
        {
            var data = message.Deserialize(new StringPairPacket());

            Members.TryGetValue(data.A, out LobbyMemberData member);

            if (member == null)
            {
                return;
            }

            Teams.TryGetValue(data.B, out LobbyTeamData newTeam);

            if (newTeam == null)
            {
                return;
            }

            member.Team = newTeam.Name;

            Listener?.OnMemberTeamChanged(member, newTeam);
        }

        private void HandleLobbyStatusTextChangeMsg(IIncommingMessage message)
        {
            var text = message.AsString();

            Data.StatusText = text;

            Listener?.OnLobbyStatusTextChanged(text);
        }

        private void HandleLobbyStateChangeMsg(IIncommingMessage message)
        {
            var newState = (LobbyState)message.AsInt();

            Data.LobbyState = newState;

            Listener?.OnLobbyStateChange(newState);
        }

        private void HandleLobbyPropertyChanged(IIncommingMessage message)
        {
            var data = message.Deserialize(new StringPairPacket());
            Properties[data.A] = data.B;

            Listener?.OnLobbyPropertyChanged(data.A, data.B);
        }

        #endregion
    }
}