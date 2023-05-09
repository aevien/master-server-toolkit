using System.Collections.Concurrent;

namespace MasterServerToolkit.MasterServer
{
    public enum TrafficType { Incoming, Outgoing }
    public class MstTrafficStatistics
    {
        private long _totalBytesSent = 0;
        private long _totalBytesReceived = 0;

        ConcurrentDictionary<ushort, long> _totalBytesSentByOpCode = new ConcurrentDictionary<ushort, long>();
        ConcurrentDictionary<ushort, long> _totalBytesReceivedByOpCode = new ConcurrentDictionary<ushort, long>();

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
        public void RegisterOpCodeTrafic(ushort opCode, long dataLength, TrafficType trafficType)
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