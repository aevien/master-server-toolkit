using System;
using UnityEngine;

namespace MasterServerToolkit.Logging
{
    public class LogAppenders
    {
        public static void UnityConsoleAppender(Logger logger, LogLevel logLevel, string channel, object message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string channelPart = LogChannels.Normalize(channel) == LogChannels.System ? string.Empty : $" | {channel}";
            string logString = $"[{time} | {logLevel}{channelPart} | {logger.Name}] {message}";

            if (Application.isEditor)
            {
                logString = $"[{logLevel}{channelPart} | {logger.Name}] {message}";
            }

            if (logLevel <= LogLevel.Info)
            {
#if !UNITY_EDITOR && !UNITY_WEBGL
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(logString);
                Console.ResetColor();
#else
                Debug.Log(logString);
#endif
            }
            else if (logLevel <= LogLevel.Warn)
            {
#if !UNITY_EDITOR && !UNITY_WEBGL
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(logString);
                Console.ResetColor();
#else
                Debug.LogWarning(logString);
#endif
            }
            else if (logLevel <= LogLevel.Fatal)
            {
#if !UNITY_EDITOR && !UNITY_WEBGL
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(logString);
                Console.ResetColor();
#else
                Debug.LogError(logString);
#endif
            }
        }
    }

    public sealed class ConsoleLogAppender
    {
        private readonly LogChannelFilter channelFilter;

        public ConsoleLogAppender(string channels)
        {
            channelFilter = new LogChannelFilter(channels);
        }

        public void HandleLog(Logger logger, LogLevel logLevel, string channel, object message)
        {
            if (channelFilter.Accepts(channel))
                LogAppenders.UnityConsoleAppender(logger, logLevel, channel, message);
        }
    }
}
