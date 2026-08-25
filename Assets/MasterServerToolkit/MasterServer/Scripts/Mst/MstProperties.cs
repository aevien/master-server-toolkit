using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Mutable string-based property bag used for MST packets, options and lightweight metadata.
    /// It is a convenience wrapper over string values, not an authority or validation layer.
    /// </summary>
    public class MstProperties : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly ConcurrentDictionary<string, string> properties;

        public int Count => properties.Count;

        public MstProperties()
        {
            properties = CreatePropertiesDictionary();
        }

        public MstProperties(Dictionary<string, string> options)
        {
            properties = CreatePropertiesDictionary();

            if (options != null)
            {
                Append(options);
            }
        }

        public MstProperties(MstProperties options)
        {
            properties = CreatePropertiesDictionary();

            if (options != null)
            {
                Append(options);
            }
        }

        public MstProperties(IEnumerable<SerializedKeyValuePair> options)
        {
            properties = CreatePropertiesDictionary();

            if (options == null)
            {
                return;
            }

            foreach (SerializedKeyValuePair pair in options)
            {
                SetToOptions(pair.key, pair.value);
            }
        }

        private static ConcurrentDictionary<string, string> CreatePropertiesDictionary()
        {
            return new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        }

        private static string ValueToString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is string stringValue)
            {
                return stringValue;
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
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
                properties[pair.Key] = pair.Value.Unescape();
            }

            return this;
        }

        /// <summary>
        /// Remove item by key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return properties.TryRemove(key, out _);
        }

        /// <summary>
        /// Clear all items
        /// </summary>
        public void Clear()
        {
            properties.Clear();
        }

        /// <summary>
        /// Add a new item. Throws when the key already exists.
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
        /// Set or replace an item value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void SetToOptions(string key, object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            properties[key] = ValueToString(value);
        }

        /// <summary>
        /// Append properties to this instance.
        /// </summary>
        /// <param name="options"></param>
        public MstProperties Append(MstProperties options)
        {
            if (options == null)
            {
                return this;
            }

            return Append(options.ToDictionary());
        }

        /// <summary>
        /// Append dictionary values to this instance.
        /// </summary>
        /// <param name="options"></param>
        public MstProperties Append(IDictionary options)
        {
            if (options == null)
            {
                return this;
            }

            foreach (var key in options.Keys)
            {
                SetToOptions(key.ToString(), options[key]);
            }

            return this;
        }

        /// <summary>
        /// Adds new or updates existing properties.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public bool AddOrUpdate(MstProperties options)
        {
            if (options == null)
            {
                return false;
            }

            return AddOrUpdate(options.ToDictionary());
        }

        /// <summary>
        /// Adds new or updates existing properties.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public bool AddOrUpdate(IDictionary<string, string> options)
        {
            if (options == null)
            {
                return false;
            }

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
        /// Check if a key exists.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Has(string key)
        {
            return properties.ContainsKey(key);
        }

        /// <summary>
        /// Check if stored value differs from the given value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Differs(string key, string value)
        {
            return !TryGetValue(key, out var currentValue) || currentValue != value;
        }

        /// <summary>
        /// Check if item value is missing, null, empty or whitespace.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsValueEmpty(string key)
        {
            if (!TryGetValue(key, out var value))
            {
                return true;
            }
            else
            {
                return string.IsNullOrWhiteSpace(value);
            }
        }

        /// <summary>
        /// Add an empty item.
        /// </summary>
        /// <param name="key"></param>
        public void Add(string key)
        {
            AddToOptions(key, string.Empty);
        }

        /// <summary>
        /// Add an item.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, object value)
        {
            AddToOptions(key, value);
        }

        /// <summary>
        /// Set an item.
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
        /// Try to get a raw stored value. Empty strings are valid values.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetValue(string key, out string value)
        {
            return properties.TryGetValue(key, out value);
        }

        /// <summary>
        /// Try to get a non-empty string value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetString(string key, out string value)
        {
            if (TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Get item as string. Missing, empty and whitespace values return the default value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string AsString(string key, string defaultValue = "")
        {
            return TryGetString(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse an integer value using invariant culture.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetInt(string key, out int value)
        {
            value = default;
            return TryGetString(key, out var rawValue)
                   && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Get item as integer
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public int AsInt(string key, int defaultValue = 0)
        {
            return TryGetInt(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse a float value using invariant culture, with current culture fallback for old data.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetFloat(string key, out float value)
        {
            value = default;

            if (!TryGetString(key, out var rawValue))
            {
                return false;
            }

            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || float.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        /// <summary>
        /// Get item as float
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public float AsFloat(string key, float defaultValue = 0f)
        {
            return TryGetFloat(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse a double value using invariant culture, with current culture fallback for old data.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetDouble(string key, out double value)
        {
            value = default;

            if (!TryGetString(key, out var rawValue))
            {
                return false;
            }

            return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        /// <summary>
        /// Get item as double
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public double AsDouble(string key, double defaultValue = 0d)
        {
            return TryGetDouble(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse a decimal value using invariant culture, with current culture fallback for old data.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetDecimal(string key, out decimal value)
        {
            value = default;

            if (!TryGetString(key, out var rawValue))
            {
                return false;
            }

            return decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || decimal.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        /// <summary>
        /// Get item as decimal
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public decimal AsDecimal(string key, decimal defaultValue = 0)
        {
            return TryGetDecimal(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse a bool value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            return TryGetString(key, out var rawValue) && bool.TryParse(rawValue, out value);
        }

        /// <summary>
        /// Get item as bool
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public bool AsBool(string key, bool defaultValue = false)
        {
            return TryGetBool(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Casts property to enum
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T AsEnum<T>(string key, T defaultValue = default) where T : struct, Enum
        {
            if (TryGetString(key, out var rawValue) && Enum.TryParse<T>(rawValue, out var value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Try to parse a short integer value using invariant culture.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetInt16(string key, out short value)
        {
            value = default;
            return TryGetString(key, out var rawValue)
                   && short.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Get item as short
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public short AsInt16(string key, short defaultValue = 0)
        {
            return TryGetInt16(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse an unsigned short value using invariant culture.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetUInt16(string key, out ushort value)
        {
            value = default;
            return TryGetString(key, out var rawValue)
                   && ushort.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Get item as unsigned short
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public ushort AsUInt16(string key, ushort defaultValue = 0)
        {
            return TryGetUInt16(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Try to parse a byte value using invariant culture.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetByte(string key, out byte value)
        {
            value = default;
            return TryGetString(key, out var rawValue)
                   && byte.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Get item as byte
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public byte AsByte(string key, byte defaultValue = 0)
        {
            return TryGetByte(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Find properties by key prefix using ordinal comparison.
        /// </summary>
        /// <param name="keyFilter"></param>
        /// <returns></returns>
        public MstProperties FindByKey(string keyFilter)
        {
            return new MstProperties(properties.Where(kvp => kvp.Key.StartsWith(keyFilter, StringComparison.Ordinal)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        /// <summary>
        /// Find properties by value substring using ordinal comparison.
        /// </summary>
        /// <param name="valFilter"></param>
        /// <returns></returns>
        public MstProperties FindByValue(string valFilter)
        {
            return new MstProperties(properties.Where(kvp => kvp.Value.IndexOf(valFilter, StringComparison.Ordinal) >= 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        /// <summary>
        /// Output properties as dictionary copy.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>(properties, StringComparer.Ordinal);
        }

        /// <summary>
        /// Create properties from dictionary.
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        public static MstProperties FromDictionary(IDictionary dictionary)
        {
            var properties = new MstProperties();

            if (dictionary == null)
            {
                return properties;
            }

            foreach (var key in dictionary.Keys)
            {
                properties.Set(key.ToString(), dictionary[key]);
            }

            return properties;
        }

        /// <summary>
        /// Convert properties to bytes.
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            return ToDictionary().ToBytes();
        }

        /// <summary>
        /// Parse properties from bytes.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static MstProperties FromBytes(byte[] data)
        {
            return new MstProperties(new Dictionary<string, string>().FromBytes(data));
        }

        /// <summary>
        /// Convert properties to readable string.
        /// </summary>
        /// <param name="itemsSeparator"></param>
        /// <param name="kvpSeparator"></param>
        /// <returns></returns>
        public string ToReadableString(string itemsSeparator = ";", string kvpSeparator = ":")
        {
            return ToDictionary().ToReadableString(itemsSeparator, kvpSeparator);
        }

        /// <summary>
        /// Append properties parsed from readable string.
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
        /// Output properties as string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToReadableString();
        }

        /// <summary>
        /// Convert properties to JSON object.
        /// </summary>
        /// <returns></returns>
        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();

            foreach (var property in properties)
            {
                json.AddField(property.Key, property.Value);
            }

            return json;
        }

        /// <summary>
        /// Enumerate current key/value pairs.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var kvp in properties)
            {
                yield return kvp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
