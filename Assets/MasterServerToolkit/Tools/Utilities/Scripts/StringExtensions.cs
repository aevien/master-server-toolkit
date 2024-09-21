using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace MasterServerToolkit.Extensions
{
    public static class StringExtensions
    {
        private static readonly ConcurrentDictionary<uint, string> hashes = new ConcurrentDictionary<uint, string>();

        const uint FNV_offset_basis = 0x01000193;
        const uint FNV_prime = 0x811c9dc5;

        private static uint FNV_1a_hash(string value)
        {
            unchecked
            {
                uint result = FNV_offset_basis;

                foreach (char c in value)
                {
                    result ^= c;
                    result *= FNV_prime;
                }

                return result;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ushort ToUint16Hash(this string value)
        {
            ushort hash = value.ToUint16HashNoStore();

            if (!hashes.ContainsKey(hash))
            {
                hashes[hash] = value;
            }

            return hash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ushort ToUint16HashNoStore(this string value)
        {
            return (ushort)(value.ToUint32HashNoStore() & 0xFFFF);
        }

        /// <summary>
        /// https://en.wikipedia.org/wiki/Fowler–Noll–Vo_hash_function
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint ToUint32Hash(this string value)
        {
            uint hash = value.ToUint32HashNoStore();

            if (!hashes.ContainsKey(hash))
            {
                hashes[hash] = value;
            }

            return hash;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static uint ToUint32Hash(params string[] values)
        {
            return string.Join("_", values).ToUint32Hash();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint ToUint32HashNoStore(this string value)
        {
            return FNV_1a_hash(value);
        }

        /// <summary>
        /// Returns string value of hash
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public static string FromHash(uint hash)
        {
            hashes.TryGetValue(hash, out var value);

            if (string.IsNullOrEmpty(value))
            {
                return hash.ToString();
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FromCamelcase(this string value)
        {
            return Regex.Replace(value, "[A-Z]", (match) =>
            {
                string v = match.ToString();
                return $" {v}";
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToCamelcase(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            string[] words = Regex.Split(value, @"[\s\.\,\-\n\r\t]+");

            if (words.Length == 0)
            {
                return value;
            }

            StringBuilder result = new StringBuilder(words[0].ToLower());

            for (int i = 1; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    result.Append(char.ToUpper(words[i][0]));
                    result.Append(words[i].Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToSnakeCase(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var startUnderscores = Regex.Match(value, @"^[A-Z]+(?=[A-Z][a-z])|^[A-Z]");
            return startUnderscores + Regex.Replace(value, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string Escape(this string value)
        {
            return Uri.EscapeDataString(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string Unescape(this string value)
        {
            return Uri.UnescapeDataString(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] Compress(this string value)
        {
            return Encoding.UTF8.GetBytes(value).CompressDeflate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string FromBase64(this string value)
        {
            byte[] bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToBase64(this string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToRed(this string value)
        {
#if !UNITY_SERVER
            return $"<color=#FF0000>{value}</color>";
#else
            return value;
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToGreen(this string value)
        {
#if !UNITY_SERVER
            return $"<color=#00FF00>{value}</color>";
#else
            return value;
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToBlue(this string value)
        {
#if !UNITY_SERVER
            return $"<color=#0000FF>{value}</color>";
#else
            return value;
#endif
        }
    }
}