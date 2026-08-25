using System;
using System.Collections.Generic;
using System.Threading;

namespace MasterServerToolkit.Logging
{
    public class LogManager
    {
        private static readonly object syncRoot = new();
        private static readonly Dictionary<string, Logger> loggers = new();
        private static readonly HashSet<Logger> pooledLoggers = new();
        private static readonly Queue<PooledLog> pooledLogs = new();

        private static LogHandler appenders;
        private static int globalLogLevel;
        private static int logLevel;
        private static int isInitialized;

        /// <summary>
        /// Overrides logging set
        /// </summary>
        public static LogLevel GlobalLogLevel
        {
            get => (LogLevel)Volatile.Read(ref globalLogLevel);
            set => Volatile.Write(ref globalLogLevel, (int)value);
        }

        /// <summary>
        /// This overrides all logging settings
        /// </summary>
        public static LogLevel LogLevel
        {
            get => (LogLevel)Volatile.Read(ref logLevel);
            set => Volatile.Write(ref logLevel, (int)value);
        }

        /// <summary>
        /// 
        /// </summary>
        public static bool IsInitialized => Volatile.Read(ref isInitialized) != 0;

        /// <summary>
        /// 
        /// </summary>
        public static int InitializationPoolSize = 100;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newAppenders"></param>
        /// <param name="newGlobalLogLevel"></param>
        public static void Initialize(IEnumerable<LogHandler> newAppenders, LogLevel newGlobalLogLevel)
        {
            LogHandler nextAppenders = null;

            foreach (LogHandler appender in newAppenders)
                nextAppenders += appender;

            PooledLog[] logsToReplay;

            lock (syncRoot)
            {
                appenders = nextAppenders;
                GlobalLogLevel = newGlobalLogLevel;
                logsToReplay = pooledLogs.ToArray();
                pooledLogs.Clear();
                pooledLoggers.Clear();
                Volatile.Write(ref isInitialized, 1);
            }

            foreach (PooledLog log in logsToReplay)
                log.Logger.Log(log.LogLevel, log.Message, log.Channel);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appender"></param>
        public static void AddAppender(LogHandler appender)
        {
            lock (syncRoot)
            {
                appenders += appender;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appender"></param>
        public static void RemoveAppender(LogHandler appender)
        {
            lock (syncRoot)
            {
                appenders -= appender;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Logger GetLogger(string name)
        {
            return GetLogger(name, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="poolUntilInitialized"></param>
        /// <returns></returns>
        public static Logger GetLogger(string name, bool poolUntilInitialized)
        {
            lock (syncRoot)
            {
                if (!loggers.TryGetValue(name, out Logger logger))
                {
                    logger = CreateLogger(name);
                    loggers.Add(name, logger);
                }

                if (!IsInitialized && poolUntilInitialized)
                    pooledLoggers.Add(logger);

                return logger;
            }
        }

        public static void Reset()
        {
            lock (syncRoot)
            {
                foreach (Logger logger in loggers.Values)
                    logger.OnLogEvent -= RouteLog;

                loggers.Clear();
                pooledLoggers.Clear();
                pooledLogs.Clear();
                appenders = null;
                Volatile.Write(ref isInitialized, 0);
            }
        }

        private static Logger CreateLogger(string name)
        {
            var logger = new Logger(name)
            {
                LogLevel = GlobalLogLevel
            };

            logger.OnLogEvent += RouteLog;
            return logger;
        }

        private static void RouteLog(Logger logger, LogLevel level, string channel, object message)
        {
            LogHandler currentAppenders;

            lock (syncRoot)
            {
                if (!loggers.TryGetValue(logger.Name, out Logger currentLogger) ||
                    !ReferenceEquals(currentLogger, logger))
                {
                    return;
                }

                if (!IsInitialized && pooledLoggers.Contains(logger))
                    AddPooledLog(logger, level, channel, message);

                currentAppenders = appenders;
            }

            currentAppenders?.Invoke(logger, level, channel, message);
        }

        private static void AddPooledLog(Logger logger, LogLevel level, string channel, object message)
        {
            int poolSize = InitializationPoolSize;

            if (poolSize <= 0)
                return;

            PooledLog log = pooledLogs.Count >= poolSize ? pooledLogs.Dequeue() : new PooledLog();
            log.LogLevel = level;
            log.Logger = logger;
            log.Channel = channel;
            log.Message = message;
            log.Date = DateTime.Now;
            pooledLogs.Enqueue(log);
        }

        private sealed class PooledLog
        {
            public DateTime Date;
            public LogLevel LogLevel;
            public Logger Logger;
            public string Channel;
            public object Message;
        }
    }
}
