using System;
using System.Collections.Generic;
using System.Text;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Default implementation of Incoming message
    /// </summary>
    public class IncomingMessage : IIncomingMessage
    {
        private readonly byte[] _data;

        public IncomingMessage(ushort opCode, byte flags, byte[] data, DeliveryMethod deliveryMethod, IPeer peer)
        {
            _data = data;

            OpCode = opCode;
            Peer = peer;
            Flags = flags;
        }

        /// <summary>
        /// Message flags
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        /// Operation code (message type)
        /// </summary>
        public ushort OpCode { get; private set; }

        /// <summary>
        /// Sender
        /// </summary>
        public IPeer Peer { get; private set; }

        /// <summary>
        /// Ack id the message is responding to
        /// </summary>
        public int? AckResponseId { get; set; }

        /// <summary>
        /// We add this to a packet to so that receiver knows
        /// what he responds to
        /// </summary>
        public int? AckRequestId { get; set; }

        /// <summary>
        /// Returns true, if sender expects a response to this message
        /// </summary>
        public bool IsExpectingResponse
        {
            get { return AckResponseId.HasValue; }
        }

        /// <summary>
        /// For ordering
        /// </summary>
        public int SequenceChannel { get; set; }

        /// <summary>
        /// Message status code
        /// </summary>
        public ResponseStatus Status { get; set; }

        /// <summary>
        /// Respond with a message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="statusCode"></param>
        public void Respond(IOutgoingMessage message, ResponseStatus statusCode = ResponseStatus.Default)
        {
            message.Status = statusCode;

            if (AckResponseId.HasValue)
            {
                message.AckResponseId = AckResponseId.Value;
            }

            Peer.SendMessage(message, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Respond with data (message is created internally)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="statusCode"></param>
        public void Respond(byte[] data, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, data), statusCode);
        }

        /// <summary>
        /// Respond with data (message is created internally)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="statusCode"></param>
        public void Respond(ISerializablePacket packet, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, packet.ToBytes()), statusCode);
        }

        /// <summary>
        /// Respond with empty message and status code
        /// </summary>
        /// <param name="statusCode"></param>
        public void Respond(ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode), statusCode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="statusCode"></param>
        public void Respond(string message, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(message.ToBytes(), statusCode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="statusCode"></param>
        public void Respond(int message, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, message), statusCode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="statusCode"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Respond(bool message, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, message), statusCode);
        }

        /// <summary>
        /// Returns true if message contains any data
        /// </summary>
        public bool HasData
        {
            get { return _data.Length > 0; }
        }

        public byte[] Data => _data;

        /// <summary>
        /// Returns contents of this message. Mutable
        /// </summary>
        /// <returns></returns>
        public byte[] AsBytes()
        {
            return _data;
        }

        /// <summary>
        /// Decodes content into a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            return Encoding.UTF8.GetString(_data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool AsBool()
        {
            return EndianBitConverter.Big.ToBoolean(_data, 0);
        }

        /// <summary>
        /// Decodes content into a string. If there's no content,
        /// returns the <see cref="defaultValue"/>
        /// </summary>
        /// <returns></returns>
        public string AsString(string defaultValue)
        {
            return HasData ? AsString() : defaultValue;
        }

        /// <summary>
        /// Decodes content into an integer
        /// </summary>
        /// <returns></returns>
        public int AsInt()
        {
            return EndianBitConverter.Big.ToInt32(_data, 0);
        }

        /// <summary>
        /// Writes content of the message into a packet
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packetToBeFilled"></param>
        /// <returns></returns>
        public T AsPacket<T>() where T : ISerializablePacket, new()
        {
            return SerializablePacket.FromBytes<T>(_data);
        }

        /// <summary>
        /// Uses content of the message to regenerate list of packets
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packetCreator"></param>
        /// <returns></returns>
        public IEnumerable<T> AsPacketsList<T>() where T : ISerializablePacket, new()
        {
            return MessageHelper.DeserializeList<T>(_data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Convert.ToBase64String(_data);
        }
    }
}