using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MasterServerToolkit.Tests.Editor
{
    [InitializeOnLoad]
    internal static class MstTestRunReporterRegistration
    {
        private static readonly TestRunnerApi TestRunner;
        private static readonly MstTestRunReporter Reporter;

        static MstTestRunReporterRegistration()
        {
            TestRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            Reporter = new MstTestRunReporter();
            TestRunner.RegisterCallbacks(Reporter);
        }
    }

    internal sealed class MstTestRunReporter : IErrorCallbacks
    {
        private static readonly string ReportsDirectory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Logs", "MstTests"));

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                Directory.CreateDirectory(ReportsDirectory);
                string timestamp = result.EndTime.ToUniversalTime().ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
                string summary = BuildSummary(result);

                WriteXml(result, Path.Combine(ReportsDirectory, "latest.xml"));
                WriteXml(result, Path.Combine(ReportsDirectory, $"{timestamp}.xml"));
                WriteText(Path.Combine(ReportsDirectory, "latest.txt"), summary);
                WriteText(Path.Combine(ReportsDirectory, $"{timestamp}.txt"), summary);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void OnError(string message)
        {
            try
            {
                Directory.CreateDirectory(ReportsDirectory);
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
                string report = $"MST / Unity Test Run Error{Environment.NewLine}" +
                    $"Time: {DateTime.UtcNow:O}{Environment.NewLine}" +
                    $"Message: {message}{Environment.NewLine}";

                WriteText(Path.Combine(ReportsDirectory, "latest.txt"), report);
                WriteText(Path.Combine(ReportsDirectory, $"{timestamp}-error.txt"), report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }

        private static void WriteXml(ITestResultAdaptor result, string path)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false)
            };

            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                result.ToXml().WriteTo(writer);
            }
        }

        private static string BuildSummary(ITestResultAdaptor result)
        {
            var summary = new StringBuilder();
            summary.AppendLine("MST / Unity Test Run");
            summary.AppendLine($"Scope: {result.FullName}");
            summary.AppendLine($"Started: {result.StartTime.ToString("O", CultureInfo.InvariantCulture)}");
            summary.AppendLine($"Finished: {result.EndTime.ToString("O", CultureInfo.InvariantCulture)}");
            summary.AppendLine($"Duration: {result.Duration.ToString("F3", CultureInfo.InvariantCulture)} s");
            summary.AppendLine($"Result: {result.ResultState}");
            summary.AppendLine($"Passed: {result.PassCount}");
            summary.AppendLine($"Failed: {result.FailCount}");
            summary.AppendLine($"Skipped: {result.SkipCount}");
            summary.AppendLine($"Inconclusive: {result.InconclusiveCount}");

            var failedTests = new List<ITestResultAdaptor>();
            CollectFailedTests(result, failedTests);

            if (failedTests.Count == 0)
                return summary.ToString();

            summary.AppendLine();
            summary.AppendLine("Failures:");

            foreach (ITestResultAdaptor failedTest in failedTests)
            {
                summary.AppendLine();
                summary.AppendLine(failedTest.FullName);
                summary.AppendLine($"State: {failedTest.ResultState}");

                if (!string.IsNullOrWhiteSpace(failedTest.Message))
                    summary.AppendLine(failedTest.Message.Trim());

                if (!string.IsNullOrWhiteSpace(failedTest.StackTrace))
                    summary.AppendLine(failedTest.StackTrace.Trim());

                if (!string.IsNullOrWhiteSpace(failedTest.Output))
                {
                    summary.AppendLine("Output:");
                    summary.AppendLine(failedTest.Output.Trim());
                }
            }

            return summary.ToString();
        }

        private static void WriteText(string path, string contents)
        {
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }

        private static void CollectFailedTests(ITestResultAdaptor result, ICollection<ITestResultAdaptor> failedTests)
        {
            if (result.HasChildren)
            {
                foreach (ITestResultAdaptor child in result.Children)
                    CollectFailedTests(child, failedTests);

                return;
            }

            if (result.TestStatus == TestStatus.Failed)
                failedTests.Add(result);
        }
    }
}
