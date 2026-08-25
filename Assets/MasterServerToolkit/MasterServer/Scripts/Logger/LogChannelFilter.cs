using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Logging
{
    /// <summary>
    /// Checks whether a log appender should receive a channel.
    /// </summary>
    public sealed class LogChannelFilter
    {
        private readonly HashSet<string> channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly bool includeAll;

        public LogChannelFilter(string channelFilter)
        {
            foreach (string channel in SplitChannels(channelFilter))
            {
                if (LogChannels.IsAll(channel))
                {
                    includeAll = true;
                    channels.Clear();
                    return;
                }

                channels.Add(LogChannels.Normalize(channel));
            }

            if (channels.Count == 0)
                channels.Add(LogChannels.System);
        }

        public bool Accepts(string channel)
        {
            return includeAll || channels.Contains(LogChannels.Normalize(channel));
        }

        private static IEnumerable<string> SplitChannels(string channelFilter)
        {
            if (string.IsNullOrWhiteSpace(channelFilter))
                yield break;

            foreach (string channel in channelFilter.Split(','))
            {
                string normalized = channel.Trim();

                if (!string.IsNullOrEmpty(normalized))
                    yield return normalized;
            }
        }
    }
}
