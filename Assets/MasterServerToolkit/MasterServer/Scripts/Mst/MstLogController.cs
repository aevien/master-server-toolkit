using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Logging settings wrapper
    /// </summary>
    public class MstLogController
    {
        /// <summary>
        /// 
        /// </summary>
        private DateTime realtimeSinceStartup;

        public MstLogController(LogLevel globalLogLevel)
        {
            realtimeSinceStartup = DateTime.Now;

            // Add default appender
            var appenders = new List<LogHandler>()
                {
                    LogAppenders.UnityConsoleAppender
                };

            // Initialize the log manager
            LogManager.Initialize(appenders, globalLogLevel);
        }

        /// <summary>
        /// 
        /// </summary>
        public double Time
        {
            get
            {
                return (DateTime.Now - realtimeSinceStartup).TotalSeconds;
            }
        }

        /// <summary>
        /// Overrides log levels of all the loggers
        /// </summary>
        /// <param name="logLevel"></param>
        public void ForceLogging(LogLevel logLevel)
        {
            LogManager.LogLevel = logLevel;
        }

        public LogLevel GlobalLogLevel
        {
            get { return LogManager.GlobalLogLevel; }
            set { LogManager.GlobalLogLevel = value; }
        }
    }
}