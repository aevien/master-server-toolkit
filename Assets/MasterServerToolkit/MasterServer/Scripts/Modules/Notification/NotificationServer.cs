using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class NotificationServer : MstBaseClient
    {
        public NotificationServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void NotifyRecipient(int recipient, string message, SuccessCallback callback = null)
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
        public void NotifyRecipients(IEnumerable<int> recipients, string message, SuccessCallback callback = null)
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
            var recipientsList = recipients?.ToList() ?? new List<int>();

            if (recipientsList.Count == 0)
            {
                callback?.Invoke(true, string.Empty);
                return;
            }

            if (connection == null || !connection.IsConnected)
            {
                string error = Mst.Errors.Parse(ResponseStatus.NotConnected);
                Logs.Error("Notification request failed. Status=NotConnected");
                callback?.Invoke(false, error);
                return;
            }

            var data = new NotificationPacket()
            {
                Recipients = recipientsList,
                Message = message
            };

            connection.SendMessage(MstOpCodes.Notification, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = Mst.Errors.Parse(status, response);
                    Logs.Error($"Notification request failed. Status={status}");
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
        public void NotifyRoom(int roomId, string message, SuccessCallback callback = null)
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
        public void NotifyRoom(int roomId, IEnumerable<int> ignoreRecipients, string message, SuccessCallback callback = null)
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
            if (connection == null || !connection.IsConnected)
            {
                string error = Mst.Errors.Parse(ResponseStatus.NotConnected);
                Logs.Error("Room notification request failed. Status=NotConnected");
                callback?.Invoke(false, error);
                return;
            }

            var data = new NotificationPacket()
            {
                RoomId = roomId,
                Message = message,
                IgnoreRecipients = ignoreRecipients?.ToList() ?? new List<int>()
            };

            connection.SendMessage(MstOpCodes.Notification, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = Mst.Errors.Parse(status, response);
                    Logs.Error($"Room notification request failed. Status={status}");
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }
    }
}
