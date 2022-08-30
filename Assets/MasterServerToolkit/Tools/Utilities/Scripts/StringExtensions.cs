using System.Text.RegularExpressions;

namespace MasterServerToolkit.Extensions
{
    public static class StringExtensions
    {
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
            return (ushort)(value.ToUint32Hash() & 0xFFFF);
        }

        /// <summary>
        /// https://en.wikipedia.org/wiki/Fowler–Noll–Vo_hash_function
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static uint ToUint32Hash(this string value)
        {
            return FNV_1a_hash(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public static uint ToUint32Hash(params string[] values)
        {
            return string.Join('_', values).ToUint32Hash();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string SplitByUppercase(this string value)
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