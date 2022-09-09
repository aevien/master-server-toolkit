using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class MstNotificationServer : MstBaseClient
    {
        public MstNotificationServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void NotifyRecipient(int recipient, string message, SuccessCallback callback)
        {
            NotifyRecipient(recipient, message, callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void NotifyRecipient(int recipient, string message, SuccessCallback callback, IClientSocket connection)
        {
            NotifyRecipients(new int[] { recipient }, message, callback, connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void NotifyRecipients(IEnumerable<int> recipients, string message, SuccessCallback callback)
        {
            NotifyRecipients(recipients, message, callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="message"></param>
        /// <param name="connection"></param>
        public void NotifyRecipients(IEnumerable<int> recipients, string message, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                string error = "Not connected";
                Logs.Error(error);
                callback?.Invoke(false, error);
                return;
            }

            var data = new NotificationPacket()
            {
                Recipients = recipients.ToList(),
                Message = message
            };

            connection.SendMessage(MstOpCodes.Notification, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = response.AsString("Unknown error occurred while sending notification to recipients");
                    Logs.Error(error);
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void NotifyRoom(int roomId, string message, SuccessCallback callback)
        {
            NotifyRoom(roomId, message, callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void NotifyRoom(int roomId, string message, SuccessCallback callback, IClientSocket connection)
        {
            NotifyRoom(roomId, new List<int>(), message, callback, connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void NotifyRoom(int roomId, IEnumerable<int> ignoreRecipients, string message, SuccessCallback callback)
        {
            NotifyRoom(roomId, ignoreRecipients, message, callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void NotifyRoom(int roomId, IEnumerable<int> ignoreRecipients, string message, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                string error = "Not connected";
                Logs.Error(error);
                callback?.Invoke(false, error);
                return;
            }

            var data = new NotificationPacket()
            {
                RoomId = roomId,
                Message = message,
                IgnoreRecipients = ignoreRecipients.ToList()
            };

            connection.SendMessage(MstOpCodes.Notification, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = response.AsString("Unknown error occurred while sending notification to recipients");
                    Logs.Error(error);
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }
    }
}