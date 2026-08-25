namespace MasterServerToolkit.Logging
{
    public delegate void LogHandler(Logger logger, LogLevel logLevel, string channel, object message);

    public class Logger
    {
        private int logLevel;

        /// <summary>
        /// Invoked when log to console
        /// </summary>
        public event LogHandler OnLogEvent;

        /// <summary>
        /// Log level of current logger
        /// </summary>
        public LogLevel LogLevel
        {
            get => (LogLevel)System.Threading.Volatile.Read(ref logLevel);
            set => System.Threading.Volatile.Write(ref logLevel, (int)value);
        }

        /// <summary>
        /// Name of current logger
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Creates new instance of logger
        /// </summary>
        /// <param name="name"></param>
        public Logger(string name)
        {
            Name = name;
            LogLevel = LogLevel.Off;
        }

        /// <summary>
        /// Returns true, if message of this level will be logged
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool IsLogging(LogLevel level)
        {
            LogLevel currentLogLevel = LogLevel;
            return currentLogLevel <= level ||
                (currentLogLevel == LogLevel.Global && level >= LogManager.GlobalLogLevel);
        }

        public void Trace(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Trace, message, channel);
        }

        public void Trace(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Trace, message, channel);
            }
        }

        public void Debug(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Debug, message, channel);
        }

        public void Debug(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Debug, message, channel);
            }
        }

        public void Info(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Info, message, channel);
        }

        public void Info(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Info, message, channel);
            }
        }

        public void Warn(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Warn, message, channel);
        }

        public void Warn(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Warn, message, channel);
            }
        }

        public void Error(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Error, message, channel);
        }

        public void Error(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Error, message, channel);
            }
        }

        public void Fatal(object message, string channel = LogChannels.System)
        {
            Log(LogLevel.Fatal, message, channel);
        }

        public void Fatal(bool condition, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(LogLevel.Fatal, message, channel);
            }
        }

        public void Log(bool condition, LogLevel logLvl, object message, string channel = LogChannels.System)
        {
            if (condition)
            {
                Log(logLvl, message, channel);
            }
        }

        public void Log(LogLevel logLvl, object message, string channel = LogChannels.System)
        {
            channel = LogChannels.Normalize(channel);
            LogLevel overrideLogLevel = LogManager.LogLevel;

            if (overrideLogLevel != LogLevel.Off && logLvl >= overrideLogLevel)
            {
                OnLogEvent?.Invoke(this, logLvl, channel, message);
                return;
            }

            // If logging level is lower than what we're logging (including global)
            LogLevel currentLogLevel = LogLevel;
            if (currentLogLevel <= logLvl ||
                (currentLogLevel == LogLevel.Global && logLvl >= LogManager.GlobalLogLevel))
            {
                OnLogEvent?.Invoke(this, logLvl, channel, message);
            }
        }
    }
}
