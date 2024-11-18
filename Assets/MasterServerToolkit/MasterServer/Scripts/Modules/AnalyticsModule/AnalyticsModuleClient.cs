using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsModuleClient : MstBaseClient
    {
        private readonly List<string> sessionEvents = new List<string>();

        public AnalyticsModuleClient(IClientSocket connection) : base(connection) { }

        public void SendSessionEvent(string eventId, Dictionary<string, string> data)
        {
            if (!Mst.Client.Auth.IsSignedIn)
            {
                Logger.Error("You are not signed in");
                return;
            }

            CreateAndSendEvent(Mst.Client.Auth.AccountInfo.Id, eventId, data, true, Connection);
        }

        public void SendSessionEvent(string eventId, Dictionary<string, string> data, IClientSocket connection)
        {
            if (!Mst.Client.Auth.IsSignedIn)
            {
                Logger.Error("You are not signed in");
                return;
            }

            CreateAndSendEvent(Mst.Client.Auth.AccountInfo.Id, eventId, data, true, connection);
        }

        public void SendSessionEvent(string userId, string eventId, Dictionary<string, string> data)
        {
            CreateAndSendEvent(userId, eventId, data, true, Connection);
        }

        public void SendSessionEvent(string userId, string eventId, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(userId, eventId, data, true, connection);
        }

        public void SendEvent(string eventId, Dictionary<string, string> data)
        {
            if (!Mst.Client.Auth.IsSignedIn)
            {
                Logger.Error("You are not signed in");
                return;
            }

            CreateAndSendEvent(Mst.Client.Auth.AccountInfo.Id, eventId, data, false, Connection);
        }

        public void SendEvent(string eventId, Dictionary<string, string> data, IClientSocket connection)
        {
            if (!Mst.Client.Auth.IsSignedIn)
            {
                Logger.Error("You are not signed in");
                return;
            }

            CreateAndSendEvent(Mst.Client.Auth.AccountInfo.Id, eventId, data, false, connection);
        }

        public void SendEvent(string userId, string eventId, Dictionary<string, string> data)
        {
            CreateAndSendEvent(userId, eventId, data, false, Connection);
        }

        public void SendEvent(string userId, string eventId, Dictionary<string, string> data, IClientSocket connection)
        {
            CreateAndSendEvent(userId, eventId, data, false, connection);
        }

        private void CreateAndSendEvent(string userId, string eventId, Dictionary<string, string> data, bool isSession, IClientSocket connection)
        {
            if (sessionEvents.Contains(eventId))
                return;

            connection.SendMessage(MstOpCodes.SendAnalyticsData, new AnalyticsDataInfoPacket()
            {
                UserId = userId,
                EventId = eventId,
                Data = data,
                IsSessionEvent = isSession
            });

            if (isSession)
                sessionEvents.Add(eventId);
        }
    }
}
