using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public delegate void NotificationEvent(string message);

    public class MstNotificationClient : MstBaseClient
    {
        /// <summary>
        /// Invoked when new notification received from server
        /// </summary>
        public event NotificationEvent OnNotificationReceivedEvent;

        public MstNotificationClient(IClientSocket connection) : base(connection)
        {
            connection.RegisterMessageHandler(MstOpCodes.Notification, OnNotificationMessageHandler);
        }

        /// <summary>
        /// Invoked when new notification received from server
        /// </summary>
        /// <param name="message"></param>
        private void OnNotificationMessageHandler(IIncomingMessage message)
        {
            var notification = message.AsString();

            if (notification != null)
            {
                Logs.Debug($"Notification from server: {notification}");
                OnNotificationReceivedEvent?.Invoke(notification);
            }
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
                string error = "Not connected";
                Logs.Error(error);
                callback?.Invoke(false, error);
                return;
            }

            connection.SendMessage(MstOpCodes.SubscribeToNotifications, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = response.AsString("Unknown error occurred while requesting a subscription to notifications");
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
                string error = "Not connected";
                Logs.Error(error);
                callback?.Invoke(false, error);
                return;
            }

            connection.SendMessage(MstOpCodes.UnsubscribeFromNotifications, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = response.AsString("Unknown error occurred while requesting an unsubscription from notifications");
                    Logs.Error(error);
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }
    }
}