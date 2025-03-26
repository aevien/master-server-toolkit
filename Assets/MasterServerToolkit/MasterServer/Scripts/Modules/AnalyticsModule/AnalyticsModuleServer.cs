using MasterServerToolkit.Networking;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModuleServer : MstBaseClient
    {
        private readonly List<string> sessionEvents = new List<string>();

        public AnalyticsModuleServer(IClientSocket connection) : base(connection) { }

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

            connection.SendMessage(MstOpCodes.SendAnalyticsData, new AnalyticsDataInfoPacket()
            {
                UserId = userId,
                Key = key,
                Data = data,
                IsSessionEvent = isSession,
                Category = category
            });

            if (isSession)
                sessionEvents.Add(key);
        }
    }
}
