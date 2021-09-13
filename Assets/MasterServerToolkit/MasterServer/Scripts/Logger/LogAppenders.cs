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
        public delegate string LogFormatter(Logger logger, LogLevel level, object message);

        /// <summary>
        /// Log lines
        /// </summary>
        private static List<string> logLines;

        static LogAppenders()
        {
            logLines = new List<string>();
            Application.logMessageReceivedThreaded += Application_logMessageReceived;
        }

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

        private static void Application_logMessageReceived(string condition, string stackTrace, LogType type)
        {
            WriteLogToFile(condition, stackTrace, type);
        }

        private static void WriteLogToFile(string condition, string stackTrace, LogType type)
        {
#if UNITY_EDITOR || UNITY_STANDALONE

            var regex = new Regex(@"\[\w+\s+[|]\s+\w+\]");

            string bracketBlock;
            string time = $"{DateTime.Now:hh:mm:ss:fff} [{Mst.Advanced.Logging.Time:F2}]";
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
                string[] parts = bracketBlock.Split('|');
                logLevel = parts[0].Trim();
                logName = parts[1].Trim();
            }

            regex = new Regex(@"\t+|\s+|\n+");

            string line = $"{time} | {logLevel.ToUpper()} | {logName} | {regex.Replace(message, " ")} | {regex.Replace(trace, " ")}";

            logLines.Add(line);

            if (logLevel.ToLower() == "error" || logLevel.ToLower() == "fatal" || logLines.Count > 100)
            {
                lock (logLines)
                {
                    StreamWriter log;

                    if (!File.Exists(Mst.Advanced.Logging.LogFile))
                    {
                        log = new StreamWriter(Mst.Advanced.Logging.LogFile);
                    }
                    else
                    {
                        log = File.AppendText(Mst.Advanced.Logging.LogFile);
                    }

                    foreach(string logLine in logLines)
                    {
                        log.WriteLine(logLine);
                    }

                    logLines.Clear();
                    log.Close();
                }
            }
#endif
        }
    }
}