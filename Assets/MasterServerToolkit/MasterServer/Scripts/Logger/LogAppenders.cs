using MasterServerToolkit.MasterServer;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.Logging
{
    public class LogAppenders
    {
        private static bool isListeningLog = false;

        public delegate string LogFormatter(Logger logger, LogLevel level, object message);

        public static void UnityConsoleAppender(Logger logger, LogLevel logLevel, object message)
        {

            if (!isListeningLog)
            {
                Application.logMessageReceived += Application_logMessageReceived;
                Application.logMessageReceivedThreaded += Application_logMessageReceived;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
                isListeningLog = true;
            }

            string logString = $"[{DateTime.Now:hh:mm:ss} | {logLevel} | {logger.Name}] {message}";

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

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteLogToFile(e.Exception.Message, e.Exception.StackTrace, LogType.Exception);
        }

        private static void Application_logMessageReceived(string condition, string stackTrace, LogType type)
        {
            WriteLogToFile(condition, stackTrace, type);
        }

        private static void WriteLogToFile(string condition, string stackTrace, LogType type)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            StreamWriter log;

            if (!File.Exists(Mst.Args.LogFile()))
            {
                log = new StreamWriter(Mst.Args.LogFile());
            }
            else
            {
                log = File.AppendText(Mst.Args.LogFile());
            }

            var regex = new Regex(@"\[(\d{2}:)+(\d+)(\s+[|]\s+\w+)+\]");

            string bracketBlock;
            string time = DateTime.Now.ToString("hh:mm:ss");
            string logLevel = type.ToString();
            string logName = "Unity Debug";
            string message = regex.Replace(condition, "").Trim();
            string trace = stackTrace;

            if (regex.IsMatch(condition))
            {
                // Substring
                var match = regex.Matches(condition);

                bracketBlock = match[0].Value;
                bracketBlock = bracketBlock.Replace("[", "").Replace("]", "");
                time = bracketBlock.Split('|')[0].Trim();
                logLevel = bracketBlock.Split('|')[1].Trim();
                logName = bracketBlock.Split('|')[2].Trim();
            }

            log.WriteLine($"--------\nTime:{time}\nLevel:{logLevel}\nName:{logName}\nMessage:{message}\nTrace:{trace}\n");
            log.Close();
#endif
        }
    }
}