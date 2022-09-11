using MasterServerToolkit.Logging;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Logging settings wrapper
    /// </summary>
    public class MstLogController
    {
        public MstLogController(LogLevel globalLogLevel)
        {
            // Add default appender
            var appenders = new List<LogHandler>()
            {
                LogAppenders.UnityConsoleAppender
            };

            // Initialize the log manager
            LogManager.Initialize(appenders, globalLogLevel);
        }

        /// <summary>
        /// Overrides log levels of all the loggers
        /// </summary>
        /// <param name="logLevel"></param>
        public void ForceLogLevel(LogLevel logLevel)
        {
            LogManager.LogLevel = logLevel;
        }

        /// <summary>
        /// 
        /// </summary>
        public LogLevel GlobalLogLevel
        {
            get { return LogManager.GlobalLogLevel; }
            set { LogManager.GlobalLogLevel = value; }
        }
    }
}