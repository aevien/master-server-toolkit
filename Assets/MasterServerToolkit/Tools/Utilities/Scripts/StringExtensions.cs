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
                uint hash = 23;
                uint multiplier = 31;

                foreach (char c in value)
                    hash = hash * multiplier + c;

                return hash;
            }
        }

        public static string ToRed(this string value)
        {
            return $"<color=#FF0000>{value}</color>";
        }

        public static string ToGreen(this string value)
        {
            return $"<color=#00FF00>{value}</color>";
        }

        public static string ToBlue(this string value)
        {
            return $"<color=#0000FF>{value}</color>";
        }
    }
}