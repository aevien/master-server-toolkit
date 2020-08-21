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
                Debug.Log(string.Format("[{0}] {1}", logLevel, message));
            }
            else if (logLevel <= LogLevel.Warn)
            {
                Debug.LogWarning(string.Format("[{0}] {1}", logLevel, message));
            }
            else if (logLevel <= LogLevel.Fatal)
            {
                Debug.LogError(string.Format("[{0}] {1}", logLevel, message));
            }
        }

        public static void UnityConsoleAppenderWithNames(Logger logger, LogLevel logLevel, object message)
        {
            if (logLevel <= LogLevel.Info)
            {
                Debug.Log(string.Format("[{0} | {1}] {2}", logLevel, logger.Name, message));
            }
            else if (logLevel <= LogLevel.Warn)
            {
                Debug.LogWarning(string.Format("[{0} | {1}] {2}", logLevel, logger.Name, message));
            }
            else if (logLevel <= LogLevel.Fatal)
            {
                Debug.LogError(string.Format("[{0} | {1}] {2}", logLevel, logger.Name, message));
            }
        }
    }
}