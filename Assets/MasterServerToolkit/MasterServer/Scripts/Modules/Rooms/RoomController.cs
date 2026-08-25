using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Room access provider callback
    /// </summary>
    /// <param name="access"></param>
    /// <param name="error"></param>
    public delegate void RoomAccessProviderCallbackDelegate(RoomAccessPacket access, string error);

    /// <summary>
    /// Room access provider factory
    /// </summary>
    /// <param name="accessCheckOptions">Options you may use to dive access</param>
    /// <param name="giveAccess"></param>
    public delegate void RoomAccessProviderDelegate(RoomAccessProviderCheck accessCheckOptions, RoomAccessProviderCallbackDelegate giveAccess);

    /// <summary>
    /// Instance of this class will be created when room registration is completed.
    /// It acts as a helpful way to manage a registered room.
    /// </summary>
    public class RoomController
    {
        /// <summary>
        /// Access provider
        /// </summary>
        private RoomAccessProviderDelegate accessProvider;

        /// <summary>
        /// Connection of current room controller
        /// </summary>
        public IClientSocket Connection { get; private set; }

        /// <summary>
        /// Room Id
        /// </summary>
        public int RoomId { get; private set; } = -1;

        /// <summary>
        /// Options of current room controller
        /// </summary>
        public RoomOptions Options { get; private set; }

        /// <summary>
        /// Logger of all room controllers
        /// </summary>
        public static Logger Logger { get; private set; }

        /// <summary>
        /// Check if room is active
        /// </summary>
        public bool IsActive => Connection != null && Connection.IsConnected && RoomId > -1;

        /// <summary>
        /// Access provider of current room controller
        /// </summary>
        public RoomAccessProviderDelegate AccessProvider
        {
            get
            {
                return accessProvider ?? DefaultAccessProvider;
            }
            set
            {
                accessProvider = value;
            }
        }

        public RoomController(int roomId, IClientSocket connection, RoomOptions options)
        {
            Logger = Mst.Create.Logger(typeof(RoomController).Name, LogLevel.Warn);

            Connection = connection;
            RoomId = roomId;
            Options = options;

            // Add handlers
            connection.RegisterMessageHandler(MstOpCodes.ProvideRoomAccessCheck, ProvideRoomAccessCheckHandler);
        }

        /// <summary>
        /// Destroys and unregisters the room
        /// </summary>
        public void Destroy()
        {
            Destroy(null);
        }

        /// <summary>
        /// Destroys and unregisters the room
        /// </summary>
        public void Destroy(SuccessCallback callback)
        {
            if (RoomId < 0)
            {
                callback?.Invoke(true);
                return;
            }

            if (Connection == null || !Connection.IsConnected)
            {
                Mst.Server.Rooms.DestroyRoom(RoomId, null, Connection);
                MarkAsDestroyed();
                callback?.Invoke(true);
                return;
            }

            var destroyedRoomId = RoomId;

            Mst.Server.Rooms.DestroyRoom(RoomId, (isSuccess, error) =>
            {
                if (!isSuccess)
                {
                    if (Connection == null || !Connection.IsConnected)
                    {
                        MarkAsDestroyed();
                        callback?.Invoke(true);
                        return;
                    }

                    callback?.Invoke(false, error);
                    Logger.Error($"Failed to unregister room {RoomId}");
                    return;
                }

                MarkAsDestroyed();

                Logger.Debug($"Room {destroyedRoomId} was successfully unregistered");

                callback?.Invoke(true);

            }, Connection);
        }

        private void MarkAsDestroyed()
        {
            Options = null;
            Connection = null;
            RoomId = -1;
        }

        /// <summary>
        /// Send's current options to master server
        /// </summary>
        public void SaveOptions()
        {
            SaveOptions(Options);
        }

        /// <summary>
        /// Send's new options to master server
        /// </summary>
        public void SaveOptions(RoomOptions options)
        {
            SaveOptions(options, null);
        }

        /// <summary>
        /// Send's new options to master server
        /// </summary>
        public void SaveOptions(RoomOptions options, SuccessCallback callback)
        {
            Mst.Server.Rooms.SaveOptions(RoomId, options, (isSuccessful, error) =>
            {
                if (!isSuccessful)
                {
                    Logger.Error($"Failed to save options for room {RoomId}");
                }
                else
                {
                    Options = options;
                }

                callback?.Invoke(isSuccessful, error);
            }, Connection);
        }

        /// <summary>
        /// Sends the token to "master" server to see if it's valid. If it is -
        /// callback will be invoked with peer id of the user, whos access was confirmed.
        /// This peer id can be used to retrieve users data from master server
        /// </summary>
        /// <param name="token"></param>
        /// <param name="callback"></param>
        public void ValidateAccess(string token, RoomAccessValidateCallback callback)
        {
            Mst.Server.Rooms.ValidateAccess(RoomId, token, callback, Connection);
        }

        /// <summary>
        /// Call this method when one of the players left current room
        /// </summary>
        /// <param name="peerId"></param>
        public void NotifyPlayerLeft(int peerId)
        {
            NotifyPlayerLeft(peerId, null);
        }

        /// <summary>
        /// Notifies the master that a player left and reports when the room membership
        /// has been updated by the master.
        /// </summary>
        /// <param name="peerId">Master-server peer identifier stored for the room player.</param>
        /// <param name="callback">Receives the confirmed master update result.</param>
        public void NotifyPlayerLeft(int peerId, SuccessCallback callback)
        {
            Mst.Server.Rooms.NotifyPlayerLeft(RoomId, peerId, (successful, error) =>
            {
                if (!successful)
                {
                    Logger.Error($"Failed to notify master that player {peerId} left room {RoomId}");
                    callback?.Invoke(false, error);
                    return;
                }

                Logger.Info($"Player {peerId} left room");
                callback?.Invoke(true, string.Empty);
            }, Connection);
        }

        /// <summary>
        /// Default access provider, which always confirms access requests
        /// </summary>
        /// <param name="accessCheckOptions"></param>
        /// <param name="callback"></param>
        public void DefaultAccessProvider(RoomAccessProviderCheck accessCheckOptions, RoomAccessProviderCallbackDelegate callback)
        {
            callback.Invoke(new RoomAccessPacket()
            {
                Id = RoomId,
                Ip = Options.RoomIp,
                Port = Options.RoomPort,
                MaxPlayers = Options.MaxPlayers,
                ExtraParameters = Options.ExtraParameters,
                Token = Mst.Helper.CreateGuidString(),
                SceneName = SceneManager.GetActiveScene().name
            }, null);
        }

        #region MESSAGE HANDLERS

        private void ProvideRoomAccessCheckHandler(IIncomingMessage message)
        {
            var provideRoomAccessCheckPacket = message.AsPacket<ProvideRoomAccessCheckPacket>();
            RoomController roomController = Mst.Server.Rooms.GetRoomController(provideRoomAccessCheckPacket.RoomId);

            if (roomController == null)
            {
                Logger.Warn($"Room controller {provideRoomAccessCheckPacket.RoomId} not found");
                message.RespondError(ResponseStatus.NotFound, MstErrorCodes.ROOM_CONTROLLER_NOT_FOUND,
                    CreateAccessErrorProperties(provideRoomAccessCheckPacket));
                return;
            }

            var isProviderDone = false;

            // Create access provider check options
            var roomAccessProviderCheck = new RoomAccessProviderCheck()
            {
                PeerId = provideRoomAccessCheckPacket.PeerId,
                Username = provideRoomAccessCheckPacket.Username,
                CustomOptions = provideRoomAccessCheckPacket.CustomOptions
            };

            // Invoke the access provider
            roomController.AccessProvider.Invoke(roomAccessProviderCheck, (access, error) =>
            {
                // In case provider timed out or done successfully
                if (isProviderDone)
                {
                    return;
                }

                isProviderDone = true;

                // If access is not provided
                if (access == null)
                {
                    Logger.Warn($"Access for {provideRoomAccessCheckPacket.Username} was denied");
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.ROOM_ACCESS_DENIED,
                        CreateAccessErrorProperties(provideRoomAccessCheckPacket));
                    return;
                }

                Logger.Info("Room controller gave address to peer " + provideRoomAccessCheckPacket.Username + ":" + access);
                message.Respond(access, ResponseStatus.Success);
            });

            // Timeout the access provider
            MstTimer.WaitForRealtimeSeconds(Mst.Server.Rooms.AccessProviderTimeout, () =>
            {
                if (!isProviderDone)
                {
                    isProviderDone = true;
                    var properties = CreateAccessErrorProperties(provideRoomAccessCheckPacket);
                    properties.Set(MstErrorPropertyKeys.TIMEOUT_SECONDS, Mst.Server.Rooms.AccessProviderTimeout);
                    message.RespondError(ResponseStatus.Timeout, MstErrorCodes.ROOM_ACCESS_PROVIDER_TIMEOUT,
                        properties);
                    Logger.Warn($"Access provider took longer than {Mst.Server.Rooms.AccessProviderTimeout} seconds to provide access. " +
                               $"If it's intended, increase the threshold at {nameof(Mst.Server.Rooms.AccessProviderTimeout)}");
                }
            });
        }

        #endregion

        private static MstProperties CreateAccessErrorProperties(ProvideRoomAccessCheckPacket packet)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.ROOM_ID, packet.RoomId);
            properties.Set(MstErrorPropertyKeys.USERNAME, packet.Username ?? string.Empty);
            return properties;
        }
    }
}
