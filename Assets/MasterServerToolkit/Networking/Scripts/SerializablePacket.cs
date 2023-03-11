using System;
using System.IO;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Base class for serializable packets
    /// </summary>
    public abstract class SerializablePacket : ISerializablePacket
    {
        /// <summary>
        /// Writes data of this packet to binary writer
        /// </summary>
        /// <param name="writer"></param>
        public abstract void ToBinaryWriter(EndianBinaryWriter writer);

        /// <summary>
        /// Reads all data from binary reader
        /// </summary>
        /// <param name="reader"></param>
        public abstract void FromBinaryReader(EndianBinaryReader reader);

        /// <summary>
        /// Convert packet to byte array
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    ToBinaryWriter(writer);
                }

                b = ms.ToArray();
            }
            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ToBase64String()
        {
            return Convert.ToBase64String(ToBytes());
        }

        /// <summary>
        /// Parses packet from bytes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static T FromBytes<T>(byte[] data, T packet) where T : ISerializablePacket
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    packet.FromBinaryReader(reader);
                    return packet;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static T FromBytes<T>(byte[] data) where T : ISerializablePacket, new()
        {
            return FromBytes<T>(data, new T());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static T FromBase64String<T>(string value, T packet) where T : ISerializablePacket
        {
            return FromBytes(Convert.FromBase64String(value), packet);
        }

        /// <summary>
        /// Write an array which length is lower than byte value
        /// </summary>
        /// <param name="data"></param>
        /// <param name="writer"></param>
        public void WriteSmallArray(float[] data, EndianBinaryWriter writer)
        {
            writer.Write((byte)(data != null ? data.Length : 0));

            if (data != null)
            {
                foreach (var val in data)
                {
                    writer.Write(val);
                }
            }
        }

        /// <summary>
        /// Read an array whichs length is lower than byte value
        /// </summary>
        /// <param name="reader"></param>
        public float[] ReadSmallArray(EndianBinaryReader reader)
        {
            var length = reader.ReadByte();

            // If we have no data
            if (length == 0)
            {
                return null;
            }

            var result = new float[length];
            for (var i = 0; i < length; i++)
            {
                result[i] = reader.ReadSingle();
            }

            return result;
        }
    }
}