using MasterServerToolkit.Logging;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace MasterServerToolkit.Networking.Unet
{
    /// <summary>
    /// Unet low level api based peer implementation
    /// </summary>
    public class PeerUnet : BasePeer
    {
        private static readonly int _unreliableChannel = UnetSocketTopology.Unreliable;
        private static readonly int _reliableFragmentedSequencedChannel = UnetSocketTopology.ReliableFragmentedSequenced;
        private static readonly int _reliableFragmentedChannel = UnetSocketTopology.ReliableFragmented;

        private readonly int _connectionId;
        private readonly int _socketId;
        private readonly int _maxBufferSize;
        private bool _isConnected;

        public PeerUnet(int connectionId, int socketId, int maxBufferSize)
        {
            _connectionId = connectionId;
            _socketId = socketId;
            _maxBufferSize = maxBufferSize;
        }

        /// <summary>
        /// True, if connection is stil valid
        /// </summary>
        public override bool IsConnected
        {
            get { return _isConnected; }
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if (!IsConnected)
                return;

            int channelId;
            switch (deliveryMethod)
            {
                case DeliveryMethod.ReliableFragmented:
                    channelId = _reliableFragmentedChannel;
                    break;
                case DeliveryMethod.ReliableFragmentedSequenced:
                    channelId = _reliableFragmentedSequencedChannel;
                    break;
                default:
                    channelId = _unreliableChannel;
                    break;
            }

            var bytes = message.ToBytes();

            if (_maxBufferSize < bytes.Length) throw new ArgumentOutOfRangeException($"Size of message buffer [{_maxBufferSize}] is less then message length [{bytes.Length}]");

            NetworkTransport.Send(_socketId, _connectionId, channelId, bytes, bytes.Length, out byte error);

            var errorType = (NetworkError)error;

            if (errorType != NetworkError.Ok)
                Logs.Error($"{errorType}. Message size: {bytes.Length}, Buffer size: {_maxBufferSize}");
        }

        /// <summary>
        /// Force disconnect
        /// </summary>
        /// <param name="reason"></param>
        public override void Disconnect(string reason)
        {
            byte error;
            NetworkTransport.Disconnect(_socketId, _connectionId, out error);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        public void SetIsConnected(bool status)
        {
            _isConnected = status;
        }
    }
}