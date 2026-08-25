using MasterServerToolkit.Networking;
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModuleServer : MstBaseClient
    {
        private readonly List<string> sessionEvents = new List<string>();

        public AnalyticsModuleServer(IClientSocket connection)
            : base(connection)
        {
            AnalyticsModuleClient.RegisterErrorParsers();
        }

        public void SendSessionEvent(string userId, string key, string category, Dictionary<string, string> data)
        {
            CreateAndSendEvent(userId, key, category, data, true, Connection);
        }

        public void SendSessionEvent(string userId, string key, string category, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(userId, key, category, data, true, connection);
        }

        public void SendEvent(string userId, string key, string category, Dictionary<string, string> data)
        {
            CreateAndSendEvent(userId, key, category, data, false, Connection);
        }

        public void SendEvent(string userId, string key, string category, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(userId, key, category, data, false, connection);
        }

        private void CreateAndSendEvent(string userId, string key, string category, Dictionary<string, string> data, bool isSession, IClientSocket connection)
        {
            if (sessionEvents.Contains(key))
                return;

            if (connection == null || !connection.IsConnected)
                return;

            if (isSession)
                sessionEvents.Add(key);

            try
            {
                connection.SendMessage(MstOpCodes.SendAnalyticsData, new AnalyticsDataInfoPacket()
                {
                    UserId = userId,
                    Key = key,
                    Data = data,
                    IsSessionEvent = isSession,
                    Category = category
                }, (status, response) =>
                {
                    if (status == ResponseStatus.Success)
                        return;

                    if (isSession)
                        sessionEvents.Remove(key);

                    if (status == ResponseStatus.NotConnected && !IsMasterResponse(response))
                        return;

                    Logs.Error($"Master rejected analytics event. status={status}, " +
                        $"error={ReadServerError(status, response)}");
                });
            }
            catch (Exception exception)
            {
                if (isSession)
                    sessionEvents.Remove(key);

                Logs.Error($"Failed to send analytics event to master: {exception}");
            }
        }

        private static string ReadServerError(ResponseStatus status, IIncomingMessage response)
        {
            if (!IsMasterResponse(response) || !response.HasData)
                return status.ToString();

            try
            {
                MstProperties properties = MstProperties.FromBytes(response.AsBytes());
                string code = properties.AsString(MstErrorPropertyKeys.CODE);
                return string.IsNullOrWhiteSpace(code) ? status.ToString() : code;
            }
            catch (Exception exception)
            {
                Logs.Error($"Failed to decode structured analytics error from master: {exception}");
                return status.ToString();
            }
        }

        private static bool IsMasterResponse(IIncomingMessage response)
        {
            return response != null && response.OpCode == MstOpCodes.SendAnalyticsData;
        }
    }
}
