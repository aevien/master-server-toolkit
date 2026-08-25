using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Logging settings wrapper
    /// </summary>
    public class MstLogController
    {
        private static readonly List<IDisposable> activeAppenders = new List<IDisposable>();

        public MstLogController(LogLevel globalLogLevel)
        {
            DisposeActiveAppenders();

            var consoleAppender = new ConsoleLogAppender(Mst.Args.ConsoleLogChannels);
            var appenders = new List<LogHandler>()
            {
                consoleAppender.HandleLog
            };

            if (FileLogAppender.TryCreateFromArgs(Mst.Args, out FileLogAppender fileAppender))
            {
                appenders.Add(fileAppender.HandleLog);
                activeAppenders.Add(fileAppender);
            }

            // Initialize the log manager
            LogManager.Initialize(appenders, globalLogLevel);

            Application.quitting -= DisposeActiveAppenders;
            Application.quitting += DisposeActiveAppenders;
        }

        public static void DisposeActiveAppenders()
        {
            foreach (IDisposable appender in activeAppenders)
            {
                appender.Dispose();
            }

            activeAppenders.Clear();
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
