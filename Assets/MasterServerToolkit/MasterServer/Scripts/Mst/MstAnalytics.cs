using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public enum TrafficType { Incoming, Outgoing }

    public class MstAnalytics
    {
        private long _totalBytesSent = 0;
        private long _totalBytesReceived = 0;

        ConcurrentDictionary<int, long> _totalBytesSentByOpCode = new ConcurrentDictionary<int, long>();
        ConcurrentDictionary<int, long> _totalBytesReceivedByOpCode = new ConcurrentDictionary<int, long>();

        public long TotalReceived => _totalBytesReceived;
        public long TotalSent => _totalBytesSent;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataLength"></param>
        /// <param name="trafficType"></param>
        public void RegisterGenericTrafic(long dataLength, TrafficType trafficType)
        {
            if (trafficType == TrafficType.Incoming)
            {
                _totalBytesReceived += dataLength;
            }
            else
            {
                _totalBytesSent += dataLength;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="dataLength"></param>
        /// <param name="trafficType"></param>
        public void RegisterOpCodeTrafic(int opCode, long dataLength, TrafficType trafficType)
        {
            //Debug.Log($"{trafficType} Traffic, OpCode: {opCode}, Data Length: {dataLength}b");

            RegisterGenericTrafic(dataLength, trafficType);

            if (trafficType == TrafficType.Incoming)
            {
                if (!_totalBytesReceivedByOpCode.ContainsKey(opCode)) _totalBytesReceivedByOpCode[opCode] = 0;
                _totalBytesReceivedByOpCode[opCode] += dataLength;
            }
            else
            {
                if (!_totalBytesSentByOpCode.ContainsKey(opCode)) _totalBytesSentByOpCode[opCode] = 0;
                _totalBytesSentByOpCode[opCode] += dataLength;
            }
        }
    }
}