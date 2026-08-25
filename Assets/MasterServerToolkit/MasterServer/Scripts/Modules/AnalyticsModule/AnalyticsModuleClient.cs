using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModuleClient : MstBaseClient
    {
        private readonly List<string> sessionEvents = new List<string>();

        public AnalyticsModuleClient(IClientSocket connection) : base(connection)
        {
            RegisterErrorParsers();
        }

        internal static void RegisterErrorParsers()
        {
            Mst.Errors.TryRegister(MstErrorCodes.ANALYTICS_UNAVAILABLE);
        }

        public void SendSessionEvent(string key, string category, Dictionary<string, string> data)
        {
            CreateAndSendEvent(key, category, data, true, null, Connection);
        }

        public void SendSessionEvent(string key, string category, Dictionary<string, string> data,
            SuccessCallback callback)
        {
            CreateAndSendEvent(key, category, data, true, callback, Connection);
        }

        public void SendSessionEvent(string key, string category, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(key, category, data, true, null, connection);
        }

        public void SendSessionEvent(string key, string category, Dictionary<string, string> data,
            SuccessCallback callback, IClientSocket connection)
        {
            CreateAndSendEvent(key, category, data, true, callback, connection);
        }

        public void SendEvent(string key, string category, Dictionary<string, string> data)
        {
            CreateAndSendEvent(key, category, data, false, null, Connection);
        }

        public void SendEvent(string key, string category, Dictionary<string, string> data,
            SuccessCallback callback)
        {
            CreateAndSendEvent(key, category, data, false, callback, Connection);
        }

        public void SendEvent(string key, string category, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(key, category, data, false, null, connection);
        }

        public void SendEvent(string key, string category, Dictionary<string, string> data,
            SuccessCallback callback, IClientSocket connection)
        {
            CreateAndSendEvent(key, category, data, false, callback, connection);
        }

        private void CreateAndSendEvent(string key, string category, Dictionary<string, string> data,
            bool isSession, SuccessCallback callback, IClientSocket connection)
        {
            if (sessionEvents.Contains(key))
            {
                callback?.Invoke(true, string.Empty);
                return;
            }

            if (!Mst.Client.Auth.IsSignedIn)
            {
                Logger.Error("You are not signed in");
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Unauthorized));
                return;
            }

            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (isSession)
                sessionEvents.Add(key);

            try
            {
                connection.SendMessage(MstOpCodes.SendAnalyticsData, new AnalyticsDataInfoPacket()
                {
                    UserId = Mst.Client.Auth.Account.Id,
                    Key = key,
                    Data = data,
                    IsSessionEvent = isSession,
                    Category = category
                }, (status, response) =>
                {
                    if (status == ResponseStatus.Success)
                    {
                        callback?.Invoke(true, string.Empty);
                        return;
                    }

                    if (isSession)
                        sessionEvents.Remove(key);

                    string error = Mst.Errors.Parse(status, response);
                    callback?.Invoke(false, error);
                });
            }
            catch (Exception exception)
            {
                if (isSession)
                    sessionEvents.Remove(key);

                Logger.Error($"Failed to send analytics event: {exception}");
                callback?.Invoke(false, Mst.Errors.Parse(
                    connection.IsConnected ? ResponseStatus.Error : ResponseStatus.NotConnected));
                return;
            }
        }
    }
}
