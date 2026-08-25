using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.Logging
{
    /// <summary>
    /// Writes MST logger messages into a configured file.
    /// </summary>
    public sealed class FileLogAppender : IDisposable
    {
        private const int MaxTokenLength = 64;
        private const string ChannelIdToken = "{channelId}";

        private readonly object syncRoot = new object();
        private readonly Dictionary<string, StreamWriter> writers = new Dictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);
        private readonly MstArgs args;
        private readonly string pathTemplate;
        private readonly string dateToken;
        private readonly LogLevel minLevel;
        private readonly bool append;
        private readonly bool includeUnityMessages;
        private readonly LogChannelFilter channelFilter;
        private bool isDisposed;

        public string FilePath { get; }

        private FileLogAppender(string pathTemplate, MstArgs args, LogLevel minLevel, bool append, bool includeUnityMessages, string channelFilter)
        {
            DateTime createdAt = DateTime.Now;

            this.args = args;
            this.pathTemplate = pathTemplate.Unescape();
            dateToken = createdAt.ToString("yyyyMMdd");
            FilePath = ResolveFilePath(this.pathTemplate, args, LogChannels.System, dateToken);
            this.minLevel = minLevel;
            this.append = append;
            this.includeUnityMessages = includeUnityMessages;
            this.channelFilter = new LogChannelFilter(channelFilter);

            GetWriter(LogChannels.System);

            if (includeUnityMessages)
                Application.logMessageReceivedThreaded += HandleUnityLog;
        }

        public static bool TryCreateFromArgs(MstArgs args, out FileLogAppender appender)
        {
            appender = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (args != null && !string.IsNullOrWhiteSpace(args.LogFilePath))
                UnityEngine.Debug.LogWarning("MST file log appender is disabled on WebGL because browser builds cannot write regular log files.");

            return false;
#else
            if (args == null || string.IsNullOrWhiteSpace(args.LogFilePath))
                return false;

            try
            {
                appender = new FileLogAppender(args.LogFilePath, args, args.LogFileMinLevel, args.LogFileAppend, args.LogUnityMessages, args.LogChannels);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to create MST file log appender: {e}");
                return false;
            }
#endif
        }

        public void HandleLog(Logger logger, LogLevel logLevel, string channel, object message)
        {
            channel = LogChannels.Normalize(channel);

            if (isDisposed || logLevel < minLevel || logLevel >= LogLevel.Off)
                return;

            if (!channelFilter.Accepts(channel))
                return;

            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logString = $"[{time}] [{logLevel}] [{channel}] [{logger.Name}] {message}";
            WriteLine(channel, logString);
        }

        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            LogLevel logLevel = ToLogLevel(type);

            if (isDisposed || logLevel < minLevel || logLevel >= LogLevel.Off)
                return;

            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            WriteLine(LogChannels.System, $"[{time}] [{logLevel}] [{LogChannels.System}] [Unity:{type}] {condition}");

            if (!string.IsNullOrWhiteSpace(stackTrace) && logLevel >= LogLevel.Error)
                WriteLine(LogChannels.System, stackTrace);
        }

        private void WriteLine(string channel, string message)
        {
            lock (syncRoot)
            {
                if (!isDisposed)
                {
                    StreamWriter writer = GetWriter(channel);
                    writer.WriteLine(message);
                }
            }
        }

        private StreamWriter GetWriter(string channel)
        {
            string filePath = ResolveFilePath(pathTemplate, args, channel, dateToken);

            if (writers.TryGetValue(filePath, out StreamWriter writer))
                return writer;

            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            writer = new StreamWriter(filePath, append, Encoding.UTF8)
            {
                AutoFlush = true
            };

            writers.Add(filePath, writer);
            return writer;
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (isDisposed)
                    return;

                if (includeUnityMessages)
                    Application.logMessageReceivedThreaded -= HandleUnityLog;

                isDisposed = true;

                foreach (StreamWriter writer in writers.Values)
                {
                    writer.Flush();
                    writer.Dispose();
                }

                writers.Clear();
            }
        }

        private static string ResolveFilePath(string pathTemplate, MstArgs args, string channel, string dateToken)
        {
            string path = ApplyTemplate(pathTemplate.Unescape(), args, channel, dateToken);

            if (Path.IsPathRooted(path))
                return path;

            string rootPath = Path.GetDirectoryName(Application.dataPath);

            if (string.IsNullOrEmpty(rootPath))
                rootPath = Directory.GetCurrentDirectory();

            return Path.GetFullPath(Path.Combine(rootPath, path));
        }

        private static string ApplyTemplate(string pathTemplate, MstArgs args, string channel, string dateToken)
        {
            string roomName = !string.IsNullOrWhiteSpace(args.RoomName) ? args.RoomName : args.RoomTitle;
            string spawnId = args.SpawnTaskId >= 0 ? args.SpawnTaskId.ToString() : "manual";
            string channelId = ToChannelId(channel);

            if (string.IsNullOrEmpty(channelId))
                pathTemplate = RemoveEmptyChannelToken(pathTemplate);
#if UNITY_WEBGL && !UNITY_EDITOR
            string processId = "web";
#else
            string processId = Process.GetCurrentProcess().Id.ToString();
#endif

            return pathTemplate
                .Replace(ChannelIdToken, channelId)
                .Replace("{spawnId}", spawnId)
                .Replace("{pid}", processId)
                .Replace("{roomName}", ToSafeFileNameToken(roomName, "room"))
                .Replace("{roomPort}", args.RoomPort.ToString())
                .Replace("{roomRedirectPort}", args.RoomRedirectPort.ToString())
                .Replace("{masterPort}", args.MasterPort.ToString())
                .Replace("{date}", dateToken);
        }

        private static string ToChannelId(string channel)
        {
            string normalizedChannel = LogChannels.Normalize(channel);

            if (string.Equals(normalizedChannel, LogChannels.System, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return ToSafeFileNameToken(normalizedChannel, "channel");
        }

        private static string RemoveEmptyChannelToken(string pathTemplate)
        {
            return pathTemplate
                .Replace("_" + ChannelIdToken + "_", "_")
                .Replace("-" + ChannelIdToken + "-", "-")
                .Replace("." + ChannelIdToken + ".", ".")
                .Replace("/" + ChannelIdToken + "/", "/")
                .Replace("\\" + ChannelIdToken + "\\", "\\")
                .Replace("_" + ChannelIdToken, string.Empty)
                .Replace(ChannelIdToken + "_", string.Empty)
                .Replace("-" + ChannelIdToken, string.Empty)
                .Replace(ChannelIdToken + "-", string.Empty)
                .Replace("." + ChannelIdToken, string.Empty)
                .Replace(ChannelIdToken + ".", string.Empty);
        }

        private static string ToSafeFileNameToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            string transliterated = Transliterator.CyrillicToLatin(value.Unescape());
            var builder = new StringBuilder(transliterated.Length);
            bool lastWasSeparator = false;

            foreach (char character in transliterated)
            {
                char current = char.ToLowerInvariant(character);

                if (current <= 127 && char.IsLetterOrDigit(current))
                {
                    builder.Append(current);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }

                if (builder.Length >= MaxTokenLength)
                    break;
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        private static LogLevel ToLogLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return LogLevel.Warn;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return LogLevel.Error;
                default:
                    return LogLevel.Info;
            }
        }
    }
}
