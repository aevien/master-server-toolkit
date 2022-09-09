using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public delegate void MemberPropertyChangedHandler(LobbyMemberData member, string propertyKey, string propertyValue);
    public delegate void LobbyPropertyChangeHandler(string property, string key);
    public delegate void ChatMessageReceivedHandler(LobbyChatPacket chatMessage);
    public delegate void MemberLeftHandler(LobbyMemberData member);
    public delegate void MemberJoinedHandler(LobbyMemberData member);
    public delegate void MemberTeamChangedHandler(LobbyMemberData member, LobbyTeamData team);
    public delegate void MemberReadyStatusChangedHandler(LobbyMemberData member, bool isReady);
    public delegate void GameMasterChangedHandler(string username);
    public delegate void LobbyStateChangeHandler(LobbyState state);
    public delegate void LobbyStatusTextChangeHandler(string text);

    /// <summary>
    /// Represents a joined lobby. When player joins a lobby,
    /// an instance of this class is created. It acts as a convenient way
    /// to manage lobby state from player perspective
    /// </summary>
    public class JoinedLobby
    {
        /// <summary>
        /// Connection of this lobby
        /// </summary>
        private readonly IClientSocket _connection;

        /// <summary>
        /// Data of this lobby
        /// </summary>
        public LobbyDataPacket Data { get; private set; }
        /// <summary>
        /// List of the properties of this lobby
        /// </summary>
        public Dictionary<string, string> Properties { get; private set; }
        /// <summary>
        /// List of all members joined this lobby
        /// </summary>
        public Dictionary<string, LobbyMemberData> Members { get; private set; }
        /// <summary>
        /// List of teams joined this lobby
        /// </summary>
        public Dictionary<string, LobbyTeamData> Teams { get; private set; }
        /// <summary>
        /// Id of the lobby
        /// </summary>
        public int Id { get { return Data.LobbyId; } }
        /// <summary>
        /// Name of the lobby
        /// </summary>
        public string Name { get { return Data.LobbyName; } }
        /// <summary>
        /// Current state of the lobby
        /// </summary>
        public LobbyState State { get { return Data.LobbyState; } }
        /// <summary>
        /// Check if player left lobby or not
        /// </summary>
        public bool HasLeft { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public event Action OnLobbyLeftEvent;
        /// <summary>
        /// 
        /// </summary>
        public event MemberPropertyChangedHandler OnMemberPropertyChangedEvent;
        /// <summary>
        /// 
        /// </summary>
        public event ChatMessageReceivedHandler OnChatMessageReceivedEvent;
        /// <summary>
        /// 
        /// </summary>
        public event MemberLeftHandler OnMemberLeftEvent;
        /// <summary>
        /// 
        /// </summary>
        public event MemberJoinedHandler OnMemberJoinedEvent;
        /// <summary>
        /// 
        /// </summary>
        public event MemberTeamChangedHandler OnMemberTeamChangedEvent;
        /// <summary>
        /// 
        /// </summary>
        public event MemberReadyStatusChangedHandler OnMemberReadyStatusChangedEvent;
        /// <summary>
        /// 
        /// </summary>
        public event GameMasterChangedHandler OnGameMasterChangedEvent;
        /// <summary>
        /// 
        /// </summary>
        public event LobbyStateChangeHandler OnLobbyStateChangeEvent;
        /// <summary>
        /// 
        /// </summary>
        public event LobbyStatusTextChangeHandler OnLobbyStatusTextChangeEvent;
        /// <summary>
        /// 
        /// </summary>
        public event LobbyPropertyChangeHandler OnLobbyPropertyChangeEvent;

        public JoinedLobby(LobbyDataPacket data, IClientSocket connection)
        {
            _connection = connection;
            Data = data;

            connection.RegisterMessageHandler(MstOpCodes.LobbyMemberPropertyChanged, HandleMemberPropertyChanged);
            connection.RegisterMessageHandler(MstOpCodes.LeftLobby, HandleLeftLobbyMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyChatMessage, HandleLobbyChatMessageMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyMemberJoined, HandleLobbyMemberJoinedMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyMemberLeft, HandleLobbyMemberLeftMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyStateChange, HandleLobbyStateChangeMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyStatusTextChange, HandleLobbyStatusTextChangeMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyMemberChangedTeam, HandlePlayerTeamChangeMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyMemberReadyStatusChange, HandleLobbyMemberReadyStatusChangeMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyMasterChange, HandleGameMasterChangeMsg);
            connection.RegisterMessageHandler(MstOpCodes.LobbyPropertyChanged, HandleLobbyPropertyChanged);

            Properties = data.LobbyProperties;
            Members = data.Members;
            Teams = data.Teams;
        }

        /// <summary>
        /// Check if given user is master
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public bool IsMasterUser(string username)
        {
            return Data.GameMaster == username;
        }

        /// <summary>
        /// Leaves this lobby
        /// </summary>
        public void Leave()
        {
            Leave(null);
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
            var data = new MstProperties();
            data.Set(key, value);

            Mst.Client.Lobbies.SetLobbyProperties(Id, data, callback, _connection);
        }

        /// <summary>
        /// Sets a lobby properties to values, provided within a dictionary
        /// </summary>
        public void SetLobbyProperties(MstProperties properties, SuccessCallback callback)
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
            var data = new MstProperties();
            data.Set(key, value);

            Mst.Client.Lobbies.SetMyProperties(data, callback, _connection);
        }

        /// <summary>
        /// Set's current player's properties
        /// </summary>
        public void SetMyProperties(MstProperties properties, SuccessCallback callback)
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
            Mst.Client.Lobbies.GetLobbyRoomAccess(new MstProperties(), callback, _connection);
        }

        /// <summary>
        /// Retrieves an access to room, which is assigned to this lobby
        /// </summary>
        public void GetLobbyRoomAccess(MstProperties properties, RoomAccessCallback callback)
        {
            Mst.Client.Lobbies.GetLobbyRoomAccess(properties, callback, _connection);
        }

        #region MASSAGE HANDLERS

        private void HandleMemberPropertyChanged(IIncomingMessage message)
        {
            var data = message.AsPacket(new LobbyMemberPropChangePacket());

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

            OnMemberPropertyChangedEvent?.Invoke(member, data.Property, data.Value);
        }

        private void HandleLeftLobbyMsg(IIncomingMessage message)
        {
            var id = message.AsInt();

            // Check the id in case there's something wrong with message order
            if (Id != id)
            {
                return;
            }

            HasLeft = true;
            OnLobbyLeftEvent?.Invoke();
        }

        private void HandleLobbyChatMessageMsg(IIncomingMessage message)
        {
            var msg = message.AsPacket(new LobbyChatPacket());
            OnChatMessageReceivedEvent?.Invoke(msg);
        }

        private void HandleLobbyMemberLeftMsg(IIncomingMessage message)
        {
            var username = message.AsString();

            Members.TryGetValue(username, out LobbyMemberData member);

            if (member == null)
            {
                return;
            }

            Members.Remove(username);
            OnMemberLeftEvent?.Invoke(member);
        }

        private void HandleLobbyMemberJoinedMsg(IIncomingMessage message)
        {
            var member = message.AsPacket(new LobbyMemberData());
            Members[member.Username] = member;
            OnMemberJoinedEvent?.Invoke(member);
        }

        private void HandleGameMasterChangeMsg(IIncomingMessage message)
        {
            var masterUsername = message.AsString();
            Data.GameMaster = masterUsername;
            OnGameMasterChangedEvent?.Invoke(masterUsername);
        }

        private void HandleLobbyMemberReadyStatusChangeMsg(IIncomingMessage message)
        {
            var data = message.AsPacket(new StringPairPacket());
            Members.TryGetValue(data.A, out LobbyMemberData member);

            if (member == null)
            {
                return;
            }

            member.IsReady = bool.Parse(data.B);
            OnMemberReadyStatusChangedEvent?.Invoke(member, member.IsReady);
        }

        private void HandlePlayerTeamChangeMsg(IIncomingMessage message)
        {
            var data = message.AsPacket(new StringPairPacket());
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
            OnMemberTeamChangedEvent?.Invoke(member, newTeam);
        }

        private void HandleLobbyStateChangeMsg(IIncomingMessage message)
        {
            var newState = (LobbyState)message.AsInt();
            Data.LobbyState = newState;
            OnLobbyStateChangeEvent?.Invoke(newState);
        }

        private void HandleLobbyPropertyChanged(IIncomingMessage message)
        {
            var data = message.AsPacket(new StringPairPacket());
            Properties[data.A] = data.B;
            OnLobbyPropertyChangeEvent?.Invoke(data.A, data.B);
        }

        private void HandleLobbyStatusTextChangeMsg(IIncomingMessage message)
        {
            string msg = message.AsString();
            OnLobbyStatusTextChangeEvent?.Invoke(msg);
        }

        #endregion
    }
}