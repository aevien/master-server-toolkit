using MasterServerToolkit.Json;
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
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static T FromBytes<T>(byte[] data) where T : ISerializablePacket, new()
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var packet = new T();
                    packet.FromBinaryReader(reader);
                    return packet;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static T FromBase64String<T>(string value) where T : ISerializablePacket, new()
        {
            return FromBytes<T>(Convert.FromBase64String(value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual MstJson ToJson()
        {
            return new MstJson();
        }

        public void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }

        public virtual void FromJson(MstJson json) { }

        public override string ToString()
        {
            return ToJson().ToString();
        }
    }
}