using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public delegate void RoomAccessCallback(RoomAccessPacket access, string error);
    public delegate void RoomAccessReceivedHandler(RoomAccessPacket access);

    public class MstRoomsClient : MstBaseClient
    {
        /// <summary>
        /// Event, invoked when an access is received
        /// </summary>
        public event RoomAccessReceivedHandler OnAccessReceivedEvent;

        /// <summary>
        /// If set to true, game server will never be started
        /// </summary>
        public bool IsClientMode { get; set; } = false;

        public MstRoomsClient(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// An access, which was last received
        /// </summary>
        public RoomAccessPacket ReceivedAccess { get; private set; }

        /// <summary>
        /// Check if current client has access to room
        /// </summary>
        public bool HasAccess => ReceivedAccess != null;

        /// <summary>
        /// Tries to get an access to a room with a given room id
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="callback"></param>
        public void GetAccess(int roomId, RoomAccessCallback callback)
        {
            GetAccess(roomId, "", new MstProperties(), callback, Connection);
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
                callback.Invoke(null, "Not connected");
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
                    callback.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                var access = response.AsPacket(new RoomAccessPacket());
                ReceivedAccess = access;
                callback.Invoke(access, null);
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
    }
}