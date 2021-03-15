using System;
using UnityEngine;

namespace MasterServerToolkit.Logging
{
    public class LogAppenders
    {
        public delegate string LogFormatter(Logger logger, LogLevel level, object message);

        public static void UnityConsoleAppender(Logger logger, LogLevel logLevel, object message)
        {
            if (logLevel <= LogLevel.Info)
            {
                Debug.Log($"[{DateTime.Now:hh:mm:ss} | {logLevel}] {message}");
            }
            else if (logLevel <= LogLevel.Warn)
            {
                Debug.LogWarning($"[{DateTime.Now:hh:mm:ss} | {logLevel}] {message}");
            }
            else if (logLevel <= LogLevel.Fatal)
            {
                Debug.LogError($"[{DateTime.Now:hh:mm:ss} | {logLevel}] {message}");
            }
        }

        public static void UnityConsoleAppenderWithNames(Logger logger, LogLevel logLevel, object message)
        {
            if (logLevel <= LogLevel.Info)
            {
                Debug.Log($"[{DateTime.Now:hh:mm:ss} | {logLevel} | {logger.Name}] {message}");
            }
            else if (logLevel <= LogLevel.Warn)
            {
                Debug.LogWarning($"[{DateTime.Now:hh:mm:ss} | {logLevel} | {logger.Name}] {message}");
            }
            else if (logLevel <= LogLevel.Fatal)
            {
                Debug.LogError($"[{DateTime.Now:hh:mm:ss} | {logLevel} | {logger.Name}] {message}");
            }
        }
    }
}