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
            ushort hash = (ushort)(FNV_1a_hash(value) & 0xFFFF);

            if (!hashes.ContainsKey(hash))
            {
                hashes[hash] = value;
            }

            return hash;
        }

        /// <summary>
        /// https://en.wikipedia.org/wiki/Fowler–Noll–Vo_hash_function
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint ToUint32Hash(this string value)
        {
            uint hash = FNV_1a_hash(value);

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
        public static string ToSpaceByUppercase(this string value)
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
        public static byte[] Compress(this string value)
        {
            return Encoding.UTF8.GetBytes(value).CompressDeflate();
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