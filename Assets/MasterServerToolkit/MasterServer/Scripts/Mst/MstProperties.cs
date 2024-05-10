using MasterServerToolkit.Extensions;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class MstProperties
    {
        private readonly ConcurrentDictionary<string, string> properties;

        public int Count => properties.Count;

        public MstProperties()
        {
            properties = new ConcurrentDictionary<string, string>();
        }

        public MstProperties(Dictionary<string, string> options)
        {
            properties = new ConcurrentDictionary<string, string>();

            if (options != null)
            {
                Append(options);
            }
        }

        public MstProperties(MstProperties options)
        {
            properties = new ConcurrentDictionary<string, string>();

            if (options != null)
            {
                Append(options);
            }
        }

        public MstProperties(IEnumerable<SerializedKeyValuePair> options)
        {
            properties = new ConcurrentDictionary<string, string>();

            foreach (SerializedKeyValuePair pair in options)
            {
                properties.TryAdd(pair.key, pair.value);
            }
        }

        /// <summary>
        /// Converts all values from normal to escape
        /// </summary>
        public MstProperties EscapeValues()
        {
            foreach (KeyValuePair<string, string> pair in properties)
            {
                properties[pair.Key] = pair.Value.Escape();
            }

            return this;
        }

        /// <summary>
        /// Converts all values from escape to normal
        /// </summary>
        public MstProperties UnescapeValues()
        {
            foreach (KeyValuePair<string, string> pair in properties)
            {
                properties[pair.Key] = pair.Value.Unscape();
            }

            return this;
        }

        /// <summary>
        /// Remove item by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return properties.TryRemove(key, out _);
        }

        /// <summary>
        /// Clear all aitems
        /// </summary>
        public void Clear()
        {
            properties.Clear();
        }

        /// <summary>
        /// Add item to options
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void AddToOptions(string key, object value)
        {
            if (Has(key))
            {
                throw new Exception($"You have already added value with key {key}");
            }

            SetToOptions(key, value);
        }

        /// <summary>
        /// Set item in options
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void SetToOptions(string key, object value)
        {
            properties[key] = value != null ? value.ToString() : string.Empty;
        }

        /// <summary>
        /// Append options to this list
        /// </summary>
        /// <param name="options"></param>
        public MstProperties Append(MstProperties options)
        {
            return Append(options.ToDictionary());
        }

        /// <summary>
        /// Append dictionary to this list
        /// </summary>
        /// <param name="options"></param>
        public MstProperties Append(IDictionary options)
        {
            if (options == null)
            {
                return new MstProperties();
            }

            foreach (var key in options.Keys)
            {
                SetToOptions(key.ToString(), options[key]);
            }

            return this;
        }

        /// <summary>
        /// Adds new or updates existing options
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public bool AddOrUpdate(MstProperties options)
        {
            return AddOrUpdate(options.ToDictionary());
        }

        /// <summary>
        /// Adds new or updates existing options
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public bool AddOrUpdate(IDictionary<string, string> options)
        {
            bool differs = false;
            string[] keys = options.Keys.ToArray();

            for (int i = 0; i < keys.Length; i++)
            {
                if (!differs)
                {
                    differs = Differs(keys[i], options[keys[i]]);
                }

                Set(keys[i], options[keys[i]]);
            }

            return differs;
        }

        /// <summary>
        /// Check if options have item with key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Has(string key)
        {
            return properties.ContainsKey(key);
        }

        /// <summary>
        /// Check if option value differs from given one
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Differs(string key, string value)
        {
            return !Has(key) || AsString(key) != value;
        }

        /// <summary>
        /// Check if item value is empty
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsValueEmpty(string key)
        {
            if (!Has(key))
            {
                return true;
            }
            else
            {
                return string.IsNullOrEmpty(AsString(key).Trim());
            }
        }

        /// <summary>
        /// Add empty item
        /// </summary>
        /// <param name="key"></param>
        public void Add(string key)
        {
            AddToOptions(key, string.Empty);
        }

        /// <summary>
        /// Add float item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, object value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set float item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, object value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add integer item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, int value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set integer item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, int value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add float item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, float value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set float item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, float value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add double item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, double value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set double item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, double value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add decimal item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, decimal value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set decimal item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, decimal value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add bool item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, bool value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set bool item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, bool value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add short item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, short value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set short item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, short value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add byte item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, byte value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set byte item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, byte value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Add string item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, string value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set string item
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, string value)
        {
            SetToOptions(key, value);
        }

        /// <summary>
        /// Get item as string
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public string AsString(string key, string defValue = "")
        {
            if (!Has(key))
            {
                return defValue;
            }

            return properties[key];
        }

        /// <summary>
        /// Get item as integer
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public int AsInt(string key, int defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToInt32(properties[key]);
        }

        /// <summary>
        /// Get item as float
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public float AsFloat(string key, float defValue = 0f)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToSingle(properties[key]);
        }

        /// <summary>
        /// Get item as double
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public double AsDouble(string key, double defValue = 0d)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToDouble(properties[key]);
        }

        /// <summary>
        /// Get item as decimal
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public decimal AsDecimal(string key, decimal defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToDecimal(properties[key]);
        }

        /// <summary>
        /// Get item as bool
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public bool AsBool(string key, bool defValue = false)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToBoolean(properties[key]);
        }

        /// <summary>
        /// Get item as short
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public short AsInt16(string key, short defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToInt16(properties[key]);
        }

        /// <summary>
        /// Get item as short
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public ushort AsUInt16(string key, ushort defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToUInt16(properties[key]);
        }

        /// <summary>
        /// Get item as byte
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public byte AsByte(string key, byte defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToByte(properties[key]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyFilter"></param>
        /// <returns></returns>
        public MstProperties FindByKey(string keyFilter)
        {
            return new MstProperties(properties.Where(kvp => kvp.Key.StartsWith(keyFilter)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="valFilter"></param>
        /// <returns></returns>
        public MstProperties FindByValue(string valFilter)
        {
            return new MstProperties(properties.Where(kvp => kvp.Value.Contains(valFilter)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        /// <summary>
        /// Output options as dictionary
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> ToDictionary()
        {
            return properties.ToDictionary(p => p.Key, p => p.Value);
        }

        /// <summary>
        /// Create options from dictionary
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static MstProperties FromDictionary(IDictionary dictionary)
        {
            var properties = new MstProperties();

            foreach (var key in dictionary.Keys)
            {
                properties.Set(key.ToString(), dictionary[key].ToString());
            }

            return properties;
        }

        /// <summary>
        /// Convert options to bytes
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            return ToDictionary().ToBytes();
        }

        /// <summary>
        /// Parse options froom bytes
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static MstProperties FromBytes(byte[] data)
        {
            return new MstProperties(new Dictionary<string, string>().FromBytes(data));
        }

        /// <summary>
        /// Convert options to readable string
        /// </summary>
        /// <param name="itemsSeparator"></param>
        /// <param name="kvpSeparator"></param>
        /// <returns></returns>
        public string ToReadableString(string itemsSeparator = ";", string kvpSeparator = ":")
        {
            return ToDictionary().ToReadableString(itemsSeparator, kvpSeparator);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemsSplitter"></param>
        /// <param name="kvpSplitter"></param>
        /// <returns></returns>
        public MstProperties FromReadableString(string value, string itemsSplitter = ";", string kvpSplitter = ":")
        {
            var dic = new Dictionary<string, string>();
            dic.FromReadableString(value, itemsSplitter, kvpSplitter);
            Append(dic);
            return this;
        }

        /// <summary>
        /// Output options as string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToReadableString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var kvp in properties)
            {
                yield return kvp;
            }
        }
    }
}