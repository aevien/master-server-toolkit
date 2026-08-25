using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public enum RoomMode { Server, Client, Host }

    public delegate void RoomAccessCallback(RoomAccessPacket access, string error);
    public delegate void RoomAccessReceivedHandler(RoomAccessPacket access);

    public class RoomsClient : MstBaseClient
    {
        /// <summary>
        /// Event, invoked when an access is received
        /// </summary>
        public event RoomAccessReceivedHandler OnAccessReceivedEvent;

        /// <summary>
        /// If set to true, game server will never be started
        /// </summary>
        public bool IsClient { get; set; } = false;

        /// <summary>
        /// An access, which was last received
        /// </summary>
        public RoomAccessPacket ReceivedAccess { get; private set; }

        /// <summary>
        /// Check if current client has access to room
        /// </summary>
        public bool HasAccess => ReceivedAccess != null;

        public RoomsClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorParsers();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Destroy()
        {
            ReceivedAccess = null;
            IsClient = false;
            OnAccessReceivedEvent = null;
        }

        /// <summary>
        /// Tries to get an access to a room with a given room id
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="callback"></param>
        public void GetAccess(int roomId, RoomAccessCallback callback)
        {
            GetAccess(roomId, string.Empty, new MstProperties(), callback, Connection);
        }

        /// <summary>
        /// Try to get an access to a room with a given room id and password
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="password"></param>
        /// <param name="callback"></param>
        public void GetAccess(int roomId, string password, RoomAccessCallback callback)
        {
            GetAccess(roomId, password, new MstProperties(), callback, Connection);
        }

        /// <summary>
        /// Tries to get an access to a room with a given room id
        /// and some other <paramref name="customOptions"/>, which will be visible to the room (game server)
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="callback"></param>
        /// <param name="customOptions"></param>
        public void GetAccess(int roomId, RoomAccessCallback callback, MstProperties customOptions)
        {
            GetAccess(roomId, "", customOptions, callback, Connection);
        }

        /// <summary>
        /// Tries to get an access to a room with a given room id, password,
        /// and some other <paramref name="customOptions"/>, which will be visible to the room (game server)
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="callback"></param>
        /// <param name="password"></param>
        /// <param name="customOptions"></param>
        public void GetAccess(int roomId, string password, MstProperties customOptions, RoomAccessCallback callback)
        {
            GetAccess(roomId, password, customOptions, callback, Connection);
        }

        /// <summary>
        /// Tries to get an access to a room with a given room id, password,
        /// and some other <paramref name="customOptions"/>, which will be visible to the room (game server)
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="password"></param>
        /// <param name="customOptions"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void GetAccess(int roomId, string password, MstProperties customOptions, RoomAccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            var roomAccessRequestPacket = new RoomAccessRequestPacket()
            {
                RoomId = roomId,
                CustomOptions = customOptions,
                Password = password
            };

            connection.SendMessage(MstOpCodes.GetRoomAccessRequest, roomAccessRequestPacket, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                var access = response.AsPacket<RoomAccessPacket>();
                ReceivedAccess = access;
                callback?.Invoke(access, null);
                OnAccessReceivedEvent?.Invoke(access);
            });
        }

        /// <summary>
        /// This method triggers the <see cref="OnAccessReceivedEvent"/> event. Call this, 
        /// if you made some custom functionality to get access to rooms
        /// </summary>
        /// <param name="access"></param>
        public void TriggerAccessReceivedEvent(RoomAccessPacket access)
        {
            ReceivedAccess = access;
            OnAccessReceivedEvent?.Invoke(access);
        }

        internal static void RegisterErrorParsers()
        {
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_REGISTRATION_DISCONNECTED);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_REGISTRATION_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_OWNER_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_REGISTRAR_REQUIRED);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_TOKEN_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_PASSWORD_INVALID);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_DENIED);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_CONTROLLER_NOT_FOUND);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_PROVIDER_TIMEOUT);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_DESTROYED);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_REQUEST_ALREADY_ACTIVE);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ALREADY_JOINED);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_FULL);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_UNAVAILABLE);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_REQUEST_INACTIVE);
            Mst.Errors.TryRegister(MstErrorCodes.ROOM_ACCESS_TOKEN_IN_USE);
            Mst.Errors.TryRegister(MstErrorCodes.WORLD_ZONE_NOT_FOUND);
        }
    }
}
