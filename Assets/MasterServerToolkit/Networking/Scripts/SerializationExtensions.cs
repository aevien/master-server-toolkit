using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Contains functions to help easily serialize / deserialize some common types
    /// </summary>
    public static class SerializationExtensions
    {
        public static void WriteCount32(
            this EndianBinaryWriter writer,
            int count,
            int maxCount,
            string valueName = "Collection")
        {
            if (count < 0 || count > maxCount)
            {
                throw new InvalidDataException(
                    $"{valueName} count {count} exceeds the allowed range 0..{maxCount}");
            }

            writer.Write(count);
        }

        private static List<T> MaterializeBounded<T>(
            IEnumerable<T> values,
            int maxCount,
            string valueName)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values is ICollection<T> collection && collection.Count > maxCount)
            {
                throw new InvalidDataException(
                    $"{valueName} count {collection.Count} exceeds the allowed range 0..{maxCount}");
            }

            var result = new List<T>();

            foreach (T value in values)
            {
                if (result.Count >= maxCount)
                {
                    throw new InvalidDataException(
                        $"{valueName} count exceeds the allowed range 0..{maxCount}");
                }

                result.Add(value);
            }

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this IEnumerable<string> list)
        {
            List<string> items = MaterializeBounded(
                list,
                MstNetworkLimits.MaxCollectionEntryCount,
                "String list");
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.WriteCount32(
                        items.Count,
                        MstNetworkLimits.MaxCollectionEntryCount,
                        "String list");

                    foreach (var item in items)
                    {
                        writer.Write(item);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static List<string> FromBytes(this List<string> list, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadCount32(
                        MstNetworkLimits.MaxCollectionEntryCount,
                        "String list");

                    for (var i = 0; i < count; i++)
                    {
                        list.Add(reader.ReadString());
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this IEnumerable<ISerializablePacket> list)
        {
            List<ISerializablePacket> items = MaterializeBounded(
                list,
                MstNetworkLimits.MaxCollectionEntryCount,
                "Packet list");
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.WriteCount32(
                        items.Count,
                        MstNetworkLimits.MaxCollectionEntryCount,
                        "Packet list");

                    foreach (var item in items)
                    {
                        item.ToBinaryWriter(writer);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="list"></param>
        /// <param name="data"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public static List<ISerializablePacket> FromBytes(this List<ISerializablePacket> list, byte[] data, Func<ISerializablePacket> factory)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    list = reader.ReadList(factory);
                }
            }
            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this Dictionary<int, int> dictionary)
        {
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.WriteCount32(
                        dictionary.Count,
                        MstNetworkLimits.MaxDictionaryEntryCount,
                        "Integer dictionary");

                    foreach (var item in dictionary)
                    {
                        writer.Write(item.Key);
                        writer.Write(item.Value);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Dictionary<int, int> FromBytes(this Dictionary<int, int> dictionary, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadCount32(
                        MstNetworkLimits.MaxDictionaryEntryCount,
                        "Integer dictionary");

                    for (var i = 0; i < count; i++)
                    {
                        var key = reader.ReadInt32();
                        var value = reader.ReadInt32();

                        if (dictionary.ContainsKey(key))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                    }
                }
            }
            return dictionary;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this Dictionary<string, int> dictionary)
        {
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.WriteCount32(
                        dictionary.Count,
                        MstNetworkLimits.MaxDictionaryEntryCount,
                        "String/integer dictionary");

                    foreach (var item in dictionary)
                    {
                        writer.Write(item.Key);
                        writer.Write(item.Value);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Dictionary<string, int> FromBytes(this Dictionary<string, int> dictionary, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadCount32(
                        MstNetworkLimits.MaxDictionaryEntryCount,
                        "String/integer dictionary");

                    for (var i = 0; i < count; i++)
                    {
                        var key = reader.ReadString();
                        var value = reader.ReadInt32();

                        if (dictionary.ContainsKey(key))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                    }
                }
            }
            return dictionary;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this Dictionary<string, float> dictionary)
        {
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.WriteCount32(
                        dictionary.Count,
                        MstNetworkLimits.MaxDictionaryEntryCount,
                        "String/float dictionary");

                    foreach (var item in dictionary)
                    {
                        writer.Write(item.Key);
                        writer.Write(item.Value);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Dictionary<string, float> FromBytes(this Dictionary<string, float> dictionary, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadCount32(
                        MstNetworkLimits.MaxDictionaryEntryCount,
                        "String/float dictionary");

                    for (var i = 0; i < count; i++)
                    {
                        var key = reader.ReadString();
                        var value = reader.ReadSingle();

                        if (dictionary.ContainsKey(key))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                    }
                }
            }
            return dictionary;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this Dictionary<string, string> dictionary)
        {
            if (dictionary != null &&
                dictionary.Count > MstNetworkLimits.MaxDictionaryEntryCount)
            {
                throw new InvalidDataException(
                    $"Dictionary entry count {dictionary.Count} exceeds the allowed limit " +
                    $"{MstNetworkLimits.MaxDictionaryEntryCount}");
            }

            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    dictionary.ToWriter(writer);
                }

                b = ms.ToArray();
            }

            if (b.Length > MstNetworkLimits.MaxDictionaryPayloadByteCount)
            {
                throw new InvalidDataException(
                    $"Dictionary payload length {b.Length} exceeds the allowed limit " +
                    $"{MstNetworkLimits.MaxDictionaryPayloadByteCount}");
            }

            return b;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="writer"></param>
        public static void ToWriter(this Dictionary<string, string> dictionary, EndianBinaryWriter writer)
        {
            if (dictionary == null)
            {
                writer.Write(0);
                return;
            }

            writer.WriteCount32(
                dictionary.Count,
                MstNetworkLimits.MaxDictionaryEntryCount,
                "String dictionary");

            foreach (var item in dictionary)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static Dictionary<string, string> FromReader(this Dictionary<string, string> dictionary, EndianBinaryReader reader)
        {
            var count = reader.ReadCount32(
                MstNetworkLimits.MaxDictionaryEntryCount,
                "String dictionary");

            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadString();
                if (dictionary.ContainsKey(key))
                {
                    dictionary[key] = value;
                }
                else
                {
                    dictionary.Add(key, value);
                }
            }

            return dictionary;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Dictionary<string, string> FromBytes(this Dictionary<string, string> dictionary, byte[] data)
        {
            if (data == null)
                throw new System.ArgumentNullException(nameof(data));

            if (data.Length > MstNetworkLimits.MaxDictionaryPayloadByteCount)
            {
                throw new InvalidDataException(
                    $"Dictionary payload length {data.Length} exceeds the allowed limit " +
                    $"{MstNetworkLimits.MaxDictionaryPayloadByteCount}");
            }

            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    dictionary.FromReader(reader);
                }
            }
            return dictionary;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static byte[] ToBytes(this string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="packet"></param>
        public static void Write(this EndianBinaryWriter writer, ISerializablePacket packet)
        {
            packet.ToBinaryWriter(writer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static T ReadPacket<T>(this EndianBinaryReader reader) where T : ISerializablePacket, new()
        {
            T packet = new T();
            packet.FromBinaryReader(reader);
            return packet;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="packets"></param>
        /// <returns></returns>
        public static List<T> ReadPackets<T>(this EndianBinaryReader reader) where T : ISerializablePacket, new()
        {
            List<T> packets = new List<T>();

            int count = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "Packet list");

            for (int i = 0; i < count; i++)
            {
                T packet = new T();
                packet.FromBinaryReader(reader);
                packets.Add(packet);
            }

            return packets;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="dictionary"></param>
        public static void Write(this EndianBinaryWriter writer, Dictionary<string, string> dictionary)
        {
            WriteDictionary(writer, dictionary);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="dictionary"></param>
        public static void WriteDictionary(this EndianBinaryWriter writer, Dictionary<string, string> dictionary)
        {
            var bytes = dictionary != null ? dictionary.ToBytes() : new byte[0];
            writer.Write(bytes.Length);

            writer.Write(bytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static Dictionary<string, string> ReadDictionary(this EndianBinaryReader reader)
        {
            var length = reader.ReadLength32(
                MstNetworkLimits.MaxDictionaryPayloadByteCount,
                "Dictionary payload");

            if (length > 0)
            {
                return new Dictionary<string, string>().FromBytes(
                    reader.ReadBytesExact(length, MstNetworkLimits.MaxDictionaryPayloadByteCount));
            }

            return new Dictionary<string, string>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="reader"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static List<TValue> ReadList<TValue>(this EndianBinaryReader reader, Func<TValue> value)
        {
            var length = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "List");

            List<TValue> list = new List<TValue>();

            for (int i = 0; i < length; i++)
            {
                list.Add(value.Invoke());
            }

            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="itemsSeparator"></param>
        /// <param name="kvpSeparator"></param>
        /// <returns></returns>
        public static string ToReadableString(this Dictionary<string, string> dictionary, string itemsSeparator = ";", string kvpSeparator = ":")
        {
            var readableString = string.Empty;

            if (dictionary != null && dictionary.Count > 0)
            {
                readableString = string.Join(itemsSeparator, dictionary.Select(p => p.Key + $"{kvpSeparator}" + (!string.IsNullOrEmpty(p.Value) ? p.Value : string.Empty)).ToArray());
            }

            return readableString;
        }

        /// <summary>
        /// Parses a readable string in format "key:value;key2:value2"
        /// into the provided dictionary. The dictionary is cleared before filling.
        /// Example: "a:1;b:2" -> { ["a"] = "1", ["b"] = "2" }.
        /// </summary>
        public static Dictionary<string, string> FromReadableString(
            this Dictionary<string, string> dictionary,
            string value,
            string itemsSplitter = ";",
            string kvpSplitter = ":")
        {
            dictionary.Clear();

            if (string.IsNullOrWhiteSpace(value))
                return dictionary;

            // Split into "key:value" segments
            string[] pairs = value.Split(itemsSplitter, StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawPair in pairs)
            {
                string pair = rawPair.Trim();

                // Split only into 2 parts: key and value
                string[] kvp = pair.Split(new[] { kvpSplitter }, 2, StringSplitOptions.None);

                // Skip malformed pairs
                if (kvp.Length != 2)
                    continue;

                string key = kvp[0].Trim();
                string val = kvp[1].Trim();

                // Overwrite duplicates instead of throwing exceptions
                dictionary[key] = val;
            }

            return dictionary;
        }
    }
}
