using System.Text.RegularExpressions;

namespace MasterServerToolkit.Extensions
{
    public static class StringExtensions
    {
        public static ushort ToUint16Hash(this string value)
        {
            return (ushort)(value.ToUint32Hash() & 0xFFFF);
        }

        public static uint ToUint32Hash(this string value)
        {
            unchecked
            {
                uint result = 3;
                uint multiplier = 33;

                // hash += 13 + 2   [41]
                // hash += 13 + 25  [558]
                // hash += 13 + 89  [7343]
                // hash += 13 + 23  [95482]
                // etc.

                foreach (char c in value)
                    result = result * multiplier + c;

                return result;
            }
        }

        public static string SplitByUppercase(this string value)
        {
            return Regex.Replace(value, "[A-Z]", (match) =>
            {
                string v = match.ToString();
                return $" {v}";
            });
        }

        public static string ToRed(this string value)
        {
#if !UNITY_SERVER
            return $"<color=#FF0000>{value}</color>";
#else
            return value;
#endif
        }

        public static string ToGreen(this string value)
        {
#if !UNITY_SERVER
            return $"<color=#00FF00>{value}</color>";
#else
            return value;
#endif
        }

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