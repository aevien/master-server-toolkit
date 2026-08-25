using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public delegate void NotificationEvent(string message);

    public class NotificationClient : MstBaseClient
    {
        /// <summary>
        /// Invoked when new notification received from server
        /// </summary>
        public event NotificationEvent OnNotificationReceivedEvent;

        public NotificationClient(IClientSocket connection) : base(connection)
        {
            RegisterErrors(
                MstErrorCodes.NOTIFICATION_MESSAGE_REQUIRED,
                MstErrorCodes.NOTIFICATION_SEND_FORBIDDEN,
                MstErrorCodes.USER_IS_NOT_LOGGED_IN);

            connection.RegisterMessageHandler(MstOpCodes.Notification, OnNotificationMessageHandler);
        }

        /// <summary>
        /// Invoked when new notification received from server
        /// </summary>
        /// <param name="message"></param>
        private void OnNotificationMessageHandler(IIncomingMessage message)
        {
            var notification = message.AsString();

            if (!string.IsNullOrEmpty(notification))
            {
                Logs.Debug($"Notification from server: {notification}");
                Notice(notification);
            }
        }

        /// <summary>
        /// Local notice
        /// </summary>
        /// <param name="message"></param>
        public void Notice(string message)
        {
            OnNotificationReceivedEvent?.Invoke(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        public void Subscribe(SuccessCallback callback)
        {
            Subscribe(callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void Subscribe(SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                string error = Mst.Errors.Parse(ResponseStatus.NotConnected);
                Logs.Error(error);
                callback?.Invoke(false, error);
                return;
            }

            connection.SendMessage(MstOpCodes.SubscribeToNotifications, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = Mst.Errors.Parse(status, response);
                    Logs.Error($"Notification subscription request failed. Status={status}");
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        public void Unsubscribe(SuccessCallback callback)
        {
            Unsubscribe(callback, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="connection"></param>
        public void Unsubscribe(SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                string error = Mst.Errors.Parse(ResponseStatus.NotConnected);
                Logs.Error(error);
                callback?.Invoke(false, error);
                return;
            }

            connection.SendMessage(MstOpCodes.UnsubscribeFromNotifications, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = Mst.Errors.Parse(status, response);
                    Logs.Error($"Notification unsubscription request failed. Status={status}");
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }
    }
}
