using MasterServerToolkit.Logging;
using System;

namespace MasterServerToolkit.Networking
{
    public class MessageFactory : IMessageFactory
    {
        public IOutgoingMessage Create(ushort opCode)
        {
            return new OutgoingMessage(opCode);
        }

        public IOutgoingMessage Create(ushort opCode, byte[] data)
        {
            return new OutgoingMessage(opCode, data);
        }

        /// <summary>
        /// Used raw byte data to create an <see cref="IIncomingMessage" />
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="start"></param>
        /// <param name="peer"></param>
        /// <returns></returns>
        public IIncomingMessage FromBytes(byte[] buffer, int start, IPeer peer)
        {
            byte[] data;
            ushort opCode;
            int dataLength;

            try
            {
                var converter = EndianBitConverter.Big;
                var flags = buffer[start];

                //Debug.Log($"Flag is: {flags}");

                opCode = converter.ToUInt16(buffer, start + 1);
                var pointer = start + 3;

                //Debug.Log($"OpCode is: {opCode}");

                dataLength = converter.ToInt32(buffer, pointer);
                pointer += 4;

                //Debug.Log($"Length is: {dataLength}");

                if (dataLength > buffer.Length)
                    throw new ArgumentOutOfRangeException(nameof(dataLength));

                data = new byte[dataLength];
                Array.Copy(buffer, pointer, data, 0, dataLength);
                pointer += dataLength;

                var message = new IncomingMessage(opCode, flags, data, DeliveryMethod.Reliable, peer)
                {
                    SequenceChannel = 0
                };

                //Debug.Log($"Data is: {message.AsString()}");

                if ((flags & (byte)MessageFlag.AckRequest) > 0)
                {
                    // We received a message which requests a response
                    message.AckResponseId = converter.ToInt32(buffer, pointer);
                    pointer += 4;
                }

                if ((flags & (byte)MessageFlag.AckResponse) > 0)
                {
                    // We received a message which is a response to our ack request
                    var ackId = converter.ToInt32(buffer, pointer);
                    message.AckRequestId = ackId;
                    pointer += 4;

                    var statusCode = buffer[pointer];

                    message.Status = (ResponseStatus)statusCode; // TODO look into not exposing status code / ackRequestId
                    pointer++;
                }

                return message;
            }
            catch (Exception e)
            {
                Logs.Error($"WS Failed parsing an incoming message {e}");
            }

            return null;
        }
    }
}