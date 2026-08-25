using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    public delegate void RoomCreationCallback(RoomController controller, string error = "");
    public delegate void RoomAccessValidateCallback(UsernameAndPeerIdPacket usernameAndPeerId, string error = "");

    /// <summary>
    /// Persists and releases one exact player session owned by the current room process.
    /// </summary>
    /// <param name="session">Stable account and master-peer identity of the session.</param>
    /// <param name="callback">Completion invoked after persistence and master membership removal.</param>
    public delegate void RoomPlayerSessionReleaseHandler(RoomPlayerSessionPacket session, SuccessCallback callback);

    public class RoomsServer : MstBaseClient
    {
        private readonly Dictionary<int, RoomController> localCreatedRooms = new();

        /// <summary>
        /// Maximum time the master server can wait for a response from game server
        /// to see if it can give access to a peer
        /// </summary>
        public float AccessProviderTimeout { get; set; } = 30;

        /// <summary>
        /// Event, invoked when a room is registered
        /// </summary>
        public event Action<RoomController> OnRoomRegisteredEvent;

        /// <summary>
        /// Event, invoked when a room is destroyed
        /// </summary>
        public event Action<RoomController> OnRoomDestroyedEvent;

        /// <summary>
        /// Invoked when the master blocks an account that is currently connected to this room.
        /// The argument is the stable account identifier used by <see cref="RoomPlayer.UserId"/>.
        /// </summary>
        public event Action<string> OnAccountBlockedEvent;

        /// <summary>
        /// Handles a master request to persist and disconnect one exact room player session.
        /// The room integration must invoke the callback only after the profile and room leave
        /// have both been confirmed by the master.
        /// </summary>
        public RoomPlayerSessionReleaseHandler PlayerSessionReleaseHandler { get; set; }

        public RoomsServer(IClientSocket connection) : base(connection)
        {
            RoomsClient.RegisterErrorParsers();
            RegisterMessageHandler(MstOpCodes.AccountBlocked, AccountBlockedMessageHandler);
            RegisterMessageHandler(
                MstOpCodes.ReleaseRoomPlayerSessionRequest,
                ReleaseRoomPlayerSessionRequestHandler);
        }

        private void ReleaseRoomPlayerSessionRequestHandler(IIncomingMessage message)
        {
            RoomPlayerSessionPacket session;

            try
            {
                session = message.AsPacket<RoomPlayerSessionPacket>();
            }
            catch (Exception exception)
            {
                Logging.Logs.Error(
                    $"Invalid room player session release request: {exception}",
                    Logging.LogChannels.System);
                message.Respond(ResponseStatus.Invalid);
                return;
            }

            if (session == null ||
                string.IsNullOrWhiteSpace(session.AccountId) ||
                session.MasterPeerId < 0)
            {
                message.Respond(ResponseStatus.Invalid);
                return;
            }

            RoomPlayerSessionReleaseHandler handler = PlayerSessionReleaseHandler;

            if (handler == null)
            {
                message.Respond(ResponseStatus.ServiceUnavailable);
                return;
            }

            int completionState = 0;

            void Complete(bool isSuccessful, string error)
            {
                if (Interlocked.Exchange(ref completionState, 1) != 0)
                    return;

                message.Respond(
                    isSuccessful
                        ? ResponseStatus.Success
                        : ResponseStatus.ServiceUnavailable);
            }

            try
            {
                handler(session, Complete);
            }
            catch (Exception exception)
            {
                Logging.Logs.Error(
                    $"Room player session release handler failed. " +
                    $"AccountId={session.AccountId}, MasterPeerId={session.MasterPeerId}, error={exception}",
                    Logging.LogChannels.System);
                Complete(false, exception.Message);
            }
        }

        private void AccountBlockedMessageHandler(IIncomingMessage message)
        {
            string accountId = message.AsString();

            if (string.IsNullOrWhiteSpace(accountId))
            {
                Logging.Logs.Warn("Master sent an account block notification without an account id",
                    Logging.LogChannels.System);
                return;
            }

            Action<string> handlers = OnAccountBlockedEvent;

            if (handlers == null)
                return;

            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(accountId);
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error(
                        $"Room account block subscriber failed. AccountId={accountId}, error={exception}",
                        Logging.LogChannels.System);
                }
            }
        }

        /// <summary>
        /// Sends a request to register a room to the master server,
        /// uses default room options <see cref="RoomOptions"/>
        /// </summary>
        /// <param name="callback"></param>
        public void RegisterRoom(RoomCreationCallback callback = null)
        {
            RegisterRoom(new RoomOptions(), callback);
        }

        /// <summary>
        /// Sends a request to register a room to master server
        /// </summary>
        /// <param name="options"></param>
        /// <param name="callback"></param>
        public void RegisterRoom(RoomOptions options, RoomCreationCallback callback = null)
        {
            RegisterRoom(options, callback, Connection);
        }

        /// <summary>
        /// Sends a request to register a room to master server
        /// </summary>
        public void RegisterRoom(RoomOptions options, RoomCreationCallback callback, IClientSocket connection)
        {
            int completionState = 0;

            void Complete(RoomController controller, string error)
            {
                if (Interlocked.Exchange(ref completionState, 1) != 0)
                    return;

                try
                {
                    callback?.Invoke(controller, error);
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error($"Room registration callback failed: {exception}",
                        Logging.LogChannels.System);
                }
            }

            if (connection == null)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!connection.IsConnected)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            Mst.Security.RequestPermission(MstPermissionKeys.RoomServer, (isSuccessful, error) =>
            {
                if (!isSuccessful)
                {
                    Logging.Logs.Error(
                        $"Failed to obtain '{MstPermissionKeys.RoomServer}' permission",
                        Logging.LogChannels.System);
                    Complete(null, Mst.Errors.Parse(ResponseStatus.Unauthorized));
                    return;
                }

                if (!connection.IsConnected)
                {
                    Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                    return;
                }

                try
                {
                    connection.SendMessage(MstOpCodes.RegisterRoomRequest, options, (status, response) =>
                    {
                        try
                        {
                            if (status != ResponseStatus.Success)
                            {
                                Complete(null, Mst.Errors.Parse(status, response));
                                return;
                            }

                            if (response == null)
                            {
                                Complete(null, Mst.Errors.Parse(ResponseStatus.Invalid));
                                return;
                            }

                            var roomId = response.AsInt();
                            var controller = new RoomController(roomId, connection, options);

                            // Save the reference
                            localCreatedRooms[roomId] = controller;

                            Complete(controller, string.Empty);

                            // Invoke event
                            OnRoomRegisteredEvent?.Invoke(controller);
                        }
                        catch (Exception exception)
                        {
                            if (Volatile.Read(ref completionState) != 0)
                            {
                                Logging.Logs.Error($"Room registered event subscriber failed: {exception}",
                                    Logging.LogChannels.System);
                            }
                            else
                            {
                                Logging.Logs.Error($"Room registration response processing failed: {exception}",
                                    Logging.LogChannels.System);
                                Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                            }
                        }
                    });
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error($"Room registration request failed: {exception}",
                        Logging.LogChannels.System);
                    Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                }
            }, connection);
        }

        /// <summary>
        /// Sends a request to destroy a room of a given room id
        /// </summary>
        public void DestroyRoom(int roomId, SuccessCallback callback = null)
        {
            DestroyRoom(roomId, callback, Connection);
        }

        /// <summary>
        /// Sends a request to destroy a room of a given room id
        /// </summary>
        public void DestroyRoom(int roomId, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                ForgetLocalRoom(roomId, out _);
                callback?.Invoke(true, null);
                return;
            }

            connection.SendMessage(MstOpCodes.DestroyRoomRequest, roomId, (status, response) =>
            {
                if (status == ResponseStatus.NotConnected)
                {
                    CompleteLocalRoomDestruction(roomId, callback);
                    return;
                }

                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                CompleteLocalRoomDestruction(roomId, callback);
            });
        }

        private void CompleteLocalRoomDestruction(int roomId, SuccessCallback callback)
        {
            ForgetLocalRoom(roomId, out RoomController destroyedRoom);

            callback?.Invoke(true, null);

            if (destroyedRoom != null)
                OnRoomDestroyedEvent?.Invoke(destroyedRoom);
        }

        private bool ForgetLocalRoom(int roomId, out RoomController destroyedRoom)
        {
            if (localCreatedRooms.TryGetValue(roomId, out destroyedRoom))
            {
                localCreatedRooms.Remove(roomId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a request to master server, to see if a given token is valid
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="token"></param>
        /// <param name="callback"></param>
        public void ValidateAccess(int roomId, string token, RoomAccessValidateCallback callback = null)
        {
            ValidateAccess(roomId, token, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to see if a given token is valid
        /// </summary>
        public void ValidateAccess(int roomId, string token, RoomAccessValidateCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            // Create access validation data packet
            var accessValidationData = new RoomAccessValidatePacket()
            {
                RoomId = roomId,
                Token = token
            };

            connection.SendMessage(MstOpCodes.ValidateRoomAccessRequest, accessValidationData, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(response.AsPacket<UsernameAndPeerIdPacket>(), null);
            });
        }

        /// <summary>
        /// Updates the options of the registered room
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="options"></param>
        /// <param name="callback"></param>
        public void SaveOptions(int roomId, RoomOptions options, SuccessCallback callback = null)
        {
            SaveOptions(roomId, options, callback, Connection);
        }

        /// <summary>
        /// Updates the options of the registered room
        /// </summary>
        public void SaveOptions(int roomId, RoomOptions options, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            var changePacket = new SaveRoomOptionsPacket()
            {
                Options = options,
                RoomId = roomId
            };

            connection.SendMessage(MstOpCodes.SaveRoomOptionsRequest, changePacket, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Notifies master server that a user with a given peer id has left the room
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void NotifyPlayerLeft(int roomId, int peerId, SuccessCallback callback = null)
        {
            NotifyPlayerLeft(roomId, peerId, callback, Connection);
        }

        /// <summary>
        /// Notifies master server that a user with a given peer id has left the room
        /// </summary>
        public void NotifyPlayerLeft(int roomId, int peerId, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            var packet = new PlayerLeftRoomPacket()
            {
                PeerId = peerId,
                RoomId = roomId
            };

            connection.SendMessage(MstOpCodes.PlayerLeftRoomRequest, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                }
                else
                {
                    callback?.Invoke(true, string.Empty);
                }
            });
        }

        /// <summary>
        /// Get's a room controller (of a registered room, which was registered in current process)
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        public RoomController GetRoomController(int roomId)
        {
            localCreatedRooms.TryGetValue(roomId, out RoomController controller);
            return controller;
        }

        /// <summary>
        /// Retrieves all of the locally created rooms (their controllers)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RoomController> GetLocallyCreatedRooms()
        {
            return localCreatedRooms.Values;
        }
    }
}
