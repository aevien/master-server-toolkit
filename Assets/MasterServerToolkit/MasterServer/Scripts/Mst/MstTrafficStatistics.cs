using MasterServerToolkit.Json;
using MasterServerToolkit.Extensions;
using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public enum TrafficType { Incoming, Outgoing }
    public class MstTrafficStatistics
    {
        private long totalBytesSent = 0;
        private long totalBytesReceived = 0;

        private readonly ConcurrentDictionary<ushort, long> totalBytesSentByOpCode = new();
        private readonly ConcurrentDictionary<ushort, long> totalBytesReceivedByOpCode = new();

        public long TotalReceived => totalBytesReceived;
        public long TotalSent => totalBytesSent;

        public MstJson Info()
        {
            var info = MstJson.CreateObject();
            info.AddField("totalReceived", totalBytesReceived);
            info.AddField("totalSent", totalBytesSent);
            info.AddField("totalBytesSentByOpCode", MstJson.CreateArray());
            info.AddField("totalBytesReceivedByOpCode", MstJson.CreateArray());

            foreach(var kvp  in totalBytesSentByOpCode)
            {
                var json = MstJson.CreateObject();
                json.AddField("hash", kvp.Key);
                json.AddField("opcode", StringExtensions.FromHash(kvp.Key));
                json.AddField("value", kvp.Value);
                info["totalBytesSentByOpCode"].Add(json);
            }

            foreach(var kvp  in totalBytesReceivedByOpCode)
            {
                var json = MstJson.CreateObject();
                json.AddField("hash", kvp.Key);
                json.AddField("opcode", StringExtensions.FromHash(kvp.Key));
                json.AddField("value", kvp.Value);
                info["totalBytesReceivedByOpCode"].Add(json);
            }

            return info;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataLength"></param>
        /// <param name="trafficType"></param>
        public void RegisterGenericTrafic(long dataLength, TrafficType trafficType)
        {
            if (trafficType == TrafficType.Incoming)
            {
                totalBytesReceived += dataLength;
            }
            else
            {
                totalBytesSent += dataLength;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="dataLength"></param>
        /// <param name="trafficType"></param>
        public void RegisterOpCodeTrafic(ushort opCode, long dataLength, TrafficType trafficType)
        {
            RegisterGenericTrafic(dataLength, trafficType);

            if (trafficType == TrafficType.Incoming)
            {
                if (!totalBytesReceivedByOpCode.ContainsKey(opCode)) 
                    totalBytesReceivedByOpCode[opCode] = 0;

                totalBytesReceivedByOpCode[opCode] += dataLength;
            }
            else
            {
                if (!totalBytesSentByOpCode.ContainsKey(opCode)) 
                    totalBytesSentByOpCode[opCode] = 0;

                totalBytesSentByOpCode[opCode] += dataLength;
            }
        }
    }
}