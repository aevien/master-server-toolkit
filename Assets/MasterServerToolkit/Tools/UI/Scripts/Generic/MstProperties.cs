using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstProperties
    {
        private Dictionary<string, string> properties;

        public MstProperties()
        {
            properties = new Dictionary<string, string>();
        }

        public MstProperties(Dictionary<string, string> options)
        {
            this.properties = new Dictionary<string, string>();
            Append(options);
        }

        public MstProperties(MstProperties options)
        {
            this.properties = new Dictionary<string, string>();
            Append(options);
        }

        public bool Remove(string key)
        {
            return properties.Remove(key);
        }

        public void Clear()
        {
            properties.Clear();
        }

        private void AddToOptions(string key, object value)
        {
            if (Has(key))
            {
                throw new Exception($"You have already added value with key {key}");
            }

            SetToOptions(key, value);
        }

        private void SetToOptions(string key, object value)
        {
            properties[key] = value.ToString();
        }

        public void Append(MstProperties options)
        {
            if (options != null)
                Append(options.ToDictionary());
        }

        public void Append(Dictionary<string, string> options)
        {
            if (options != null)
                foreach (var kvp in options)
                {
                    AddToOptions(kvp.Key, kvp.Value);
                }
        }

        public bool Has(string key)
        {
            return properties.ContainsKey(key);
        }

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

        public void Add(string key)
        {
            AddToOptions(key, string.Empty);
        }

        public void Add(string key, int value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, int value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, float value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, float value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, double value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, double value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, decimal value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, decimal value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, bool value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, bool value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, short value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, short value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, byte value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, byte value)
        {
            SetToOptions(key, value);
        }

        public void Add(string key, string value)
        {
            AddToOptions(key, value);
        }

        public void Set(string key, string value)
        {
            SetToOptions(key, value);
        }

        public string AsString(string key, string defValue = "")
        {
            if (!Has(key))
            {
                return defValue;
            }

            return properties[key];
        }

        public int AsInt(string key, int defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToInt32(properties[key]);
        }

        public float AsFloat(string key, float defValue = 0f)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToSingle(properties[key]);
        }

        public double AsDouble(string key, double defValue = 0d)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToDouble(properties[key]);
        }

        public decimal AsDecimal(string key, decimal defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToDecimal(properties[key]);
        }

        public bool AsBool(string key, bool defValue = false)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToBoolean(properties[key]);
        }

        public short AsShort(string key, short defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToInt16(properties[key]);
        }

        public byte AsByte(string key, byte defValue = 0)
        {
            if (!Has(key))
            {
                return defValue;
            }

            return Convert.ToByte(properties[key]);
        }

        public Dictionary<string, string> ToDictionary()
        {
            return properties;
        }

        public static MstProperties FromDictionary(IDictionary dictionary)
        {
            var properties = new MstProperties();

            foreach(var key in dictionary.Keys)
            {
                properties.Set(key.ToString(), dictionary[key].ToString());
            }

            return properties;
        }

        public byte[] ToBytes()
        {
            return properties.ToBytes();
        }

        public static MstProperties FromBytes(byte[] data)
        {
            return new MstProperties(new Dictionary<string, string>().FromBytes(data));
        }

        public string ToReadableString(string itemsSeparator = "; ", string kvpSeparator = " : ")
        {
            return ToDictionary().ToReadableString(itemsSeparator, kvpSeparator);
        }

        public override string ToString()
        {
            return ToReadableString();
        }
    }
}