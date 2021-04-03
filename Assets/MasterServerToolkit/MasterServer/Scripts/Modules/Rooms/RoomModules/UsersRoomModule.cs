using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class UsersRoomModule : BaseServerModule
    {
        /// <summary>
        /// List of players filtered by master peer id
        /// </summary>
        private Dictionary<int, IRoomPlayerPeerExtension> roomPlayersByMsfPeerId;

        /// <summary>
        /// List of players filtered by room peer id
        /// </summary>
        private Dictionary<int, IRoomPlayerPeerExtension> roomPlayersByRoomPeerId;

        /// <summary>
        /// List of players filtered by username
        /// </summary>
        private Dictionary<string, IRoomPlayerPeerExtension> roomPlayersByUsername;

        /// <summary>
        /// Fires when new player joined room
        /// </summary>
        public event Action<IRoomPlayerPeerExtension> OnPlayerJoinedEvent;

        /// <summary>
        /// Fires when new player left room
        /// </summary>
        public event Action<IRoomPlayerPeerExtension> OnPlayerLeftEvent;

        private void OnDestroy()
        {
            if (Server != null)
                Server.OnPeerDisconnectedEvent -= Server_OnPeerDisconnectedEvent;
        }

        public override void Initialize(IServer server)
        {
            // Initialize lists
            roomPlayersByMsfPeerId = new Dictionary<int, IRoomPlayerPeerExtension>();
            roomPlayersByRoomPeerId = new Dictionary<int, IRoomPlayerPeerExtension>();
            roomPlayersByUsername = new Dictionary<string, IRoomPlayerPeerExtension>();

            Server.OnPeerDisconnectedEvent += Server_OnPeerDisconnectedEvent;

            // Register handler to handle validate access request
            server.RegisterMessageHandler((short)MstMessageCodes.ValidateRoomAccessRequest, ValidateRoomAccessRequestHandler);
        }

        /// <summary>
        /// Fires when room player is disconnected
        /// </summary>
        /// <param name="peer"></param>
        private void Server_OnPeerDisconnectedEvent(IPeer peer)
        {
            var roomUserExtension = peer.GetExtension<RoomPlayerPeerExtension>();

            if (roomUserExtension != null)
            {
                roomPlayersByMsfPeerId.Remove(roomUserExtension.MasterPeerId);
                roomPlayersByRoomPeerId.Remove(peer.Id);
                roomPlayersByUsername.Remove(roomUserExtension.Username);

                var roomServer = Server as RoomServer;
                //roomServer.CurrentRoomController.NotifyPlayerLeft(roomUserExtension.MasterPeerId);

                OnPlayerLeftEvent?.Invoke(roomUserExtension);
            }
        }

        /// <summary>
        /// Fired when player requested access to this room
        /// </summary>
        /// <param name="message"></param>
        private void ValidateRoomAccessRequestHandler(IIncomingMessage message)
        {
            var token = message.AsString();
            var roomServer = Server as RoomServer;

            logger.Debug($"Client {message.Peer.Id} requested access validation");

            // Trying to validate room client connection access
            //Mst.Server.Rooms.ValidateAccess(roomServer.CurrentRoomController.RoomId, token, (usernameAndPeerId, error) =>
            //{
            //    if (usernameAndPeerId == null)
            //    {
            //        logger.Error(error);
            //        message.Peer.Disconnect("Invalid room access token");
            //        return;
            //    }

            //    // Let's get account info of the connected peer from master server
            //    Mst.Server.Auth.GetPeerAccountInfo(usernameAndPeerId.PeerId, (info, infoError) =>
            //    {
            //        if (info == null)
            //        {
            //            logger.Error(infoError);
            //            return;
            //        }

            //        logger.Debug($"Room got peer account info. {info}");

            //        // Create new room player info
            //        var roomUserExtension = new RoomUserPeerExtension(info.PeerId, info.Username, message.Peer, info.CustomOptions);

            //        // Add extension to peer
            //        message.Peer.AddExtension(roomUserExtension);

            //        // Inform all listeners
            //        OnPlayerJoinedEvent?.Invoke(roomUserExtension);
            //    });

            //    message.Respond(ResponseStatus.Success);
            //});
        }
    }
}
