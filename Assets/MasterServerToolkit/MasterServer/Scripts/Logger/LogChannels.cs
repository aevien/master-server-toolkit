using System;

namespace MasterServerToolkit.Logging
{
    /// <summary>
    /// Common MST log channels. Channels classify event streams; log levels still describe severity.
    /// </summary>
    public static class LogChannels
    {
        public const string System = "System";
        public const string Chat = "Chat";
        public const string Purchases = "Purchases";
        public const string Economy = "Economy";
        public const string Security = "Security";
        public const string All = "*";

        public static string Normalize(string channel)
        {
            return string.IsNullOrWhiteSpace(channel) ? System : channel.Trim();
        }

        public static bool IsAll(string channel)
        {
            return string.Equals(channel?.Trim(), All, StringComparison.Ordinal);
        }
    }
}
