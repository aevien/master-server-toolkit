using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.IO;

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
        private string logFileId;

        /// <summary>
        /// 
        /// </summary>
        private DateTime realtimeSinceStartup;

        public MstLogController(LogLevel globalLogLevel)
        {
            logFileId = Mst.Helper.CreateRandomAlphanumericString(6);
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
        /// <returns></returns>
        public string LogFile
        {
            get
            {
                var dt = DateTime.Now;

                if (!Directory.Exists(Mst.Args.LogFileDir))
                    Directory.CreateDirectory(Mst.Args.LogFileDir);

                string filePrefix = Mst.Args.AsString("-processLogFilePrefix", $"mst_{dt:MM_dd_yyyy_hh}") + $"_{logFileId}";
                string logFile = $"{filePrefix}.log";

                return Path.Combine(Mst.Args.LogFileDir, logFile);
            }
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
            LogManager.ForceLogLevel = logLevel;
        }

        public LogLevel GlobalLogLevel
        {
            get { return LogManager.GlobalLogLevel; }
            set { LogManager.GlobalLogLevel = value; }
        }
    }
}