using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MasterServerToolkit.Logging
{
    public class LogAppenders
    {
        public static void UnityConsoleAppender(Logger logger, LogLevel logLevel, object message)
        {
            string logString = $"[{logLevel} | {logger.Name}] {message}";

            if (logLevel <= LogLevel.Info)
            {
                Debug.Log(logString);
            }
            else if (logLevel <= LogLevel.Warn)
            {
                Debug.LogWarning(logString);
            }
            else if (logLevel <= LogLevel.Fatal)
            {
                Debug.LogError(logString);
            }
        }
    }
}