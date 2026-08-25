namespace MasterServerToolkit.Logging
{
    public class Logs
    {
        private const string LoggerName = "Logs";

        private static Logger GetLogger() => LogManager.GetLogger(LoggerName);

        public static void Trace(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Trace, message, channel);
        }

        public static void Trace(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Trace, message, channel);
            }
        }

        public static void Debug(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Debug, message, channel);
        }

        public static void Debug(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Debug, message, channel);
            }
        }

        public static void Info(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Info, message, channel);
        }

        public static void Info(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Info, message, channel);
            }
        }

        public static void Warn(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Warn, message, channel);
        }

        public static void Warn(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Warn, message, channel);
            }
        }

        public static void Error(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Error, message, channel);
        }

        public static void Error(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Error, message, channel);
            }
        }

        public static void Fatal(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Fatal, message, channel);
        }

        public static void Fatal(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Fatal, message, channel);
            }
        }

        public static void Log(LogLevel logLvl, object message, string channel = LogChannels.System)
        {
            GetLogger().Log(logLvl, message, channel);
        }

        public static void Log(bool condition, LogLevel logLvl, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(logLvl, message, channel);
            }
        }
    }
}
