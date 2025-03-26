using MasterServerToolkit.Networking;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModuleClient : MstBaseClient
    {
        private readonly List<string> sessionEvents = new List<string>();

        public AnalyticsModuleClient(IClientSocket connection) : base(connection) { }

        public void SendSessionEvent(string key, string category, Dictionary<string, string> data)
        {
            CreateAndSendEvent(key, category, data, true, Connection);
        }

        public void SendSessionEvent(string key, string category, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(key, category, data, true, connection);
        }

        public void SendEvent(string key, string category, Dictionary<string, string> data)
        {
            CreateAndSendEvent(key, category, data, false, Connection);
        }

        public void SendEvent(string key, string category, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(key, category, data, false, connection);
        }

        private void CreateAndSendEvent(string key, string category, Dictionary<string, string> data, bool isSession, IClientSocket connection)
        {
            if (sessionEvents.Contains(key))
                return;

            if (!Mst.Client.Auth.IsSignedIn)
            {
                Logger.Error("You are not signed in");
                return;
            }

            connection.SendMessage(MstOpCodes.SendAnalyticsData, new AnalyticsDataInfoPacket()
            {
                UserId = Mst.Client.Auth.AccountInfo.Id,
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
