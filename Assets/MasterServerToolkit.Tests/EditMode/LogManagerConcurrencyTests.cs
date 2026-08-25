using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class LogManagerConcurrencyTests
    {
        private int originalPoolSize;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Mst.Args, Is.Not.Null);
            originalPoolSize = LogManager.InitializationPoolSize;
            LogManager.Reset();
            LogManager.LogLevel = LogLevel.All;
        }

        [TearDown]
        public void TearDown()
        {
            LogManager.InitializationPoolSize = originalPoolSize;
            LogManager.Reset();
            _ = new MstLogController(LogLevel.All);
        }

        [Test]
        public void GetLogger_WhenRequestedConcurrently_ReturnsSingleInstance()
        {
            const int workerCount = 32;
            using var startGate = new ManualResetEventSlim(false);
            Task<Logger>[] tasks = Enumerable.Range(0, workerCount)
                .Select(_ => Task.Run(() =>
                {
                    startGate.Wait();
                    return LogManager.GetLogger("shared-concurrent-logger", false);
                }))
                .ToArray();

            startGate.Set();
            Assert.That(Task.WaitAll(tasks, 10000), Is.True, "Concurrent logger creation timed out");

            Logger expected = tasks[0].Result;
            Assert.That(tasks.All(task => ReferenceEquals(expected, task.Result)), Is.True);
        }

        [Test]
        public void Initialize_DuringConcurrentLogging_DeliversEveryMessageOnce()
        {
            const int workerCount = 8;
            const int messagesPerWorker = 100;
            int expectedCount = workerCount * messagesPerWorker;
            int deliveredCount = 0;
            LogManager.InitializationPoolSize = expectedCount;

            Logger logger = LogManager.GetLogger("concurrent-pool");
            var receivedMessages = new ConcurrentDictionary<string, int>();
            using var startGate = new ManualResetEventSlim(false);
            using var initializeBarrier = new Barrier(workerCount + 1);
            Task[] tasks = Enumerable.Range(0, workerCount)
                .Select(worker => Task.Run(() =>
                {
                    startGate.Wait();

                    for (int i = 0; i < messagesPerWorker; i++)
                    {
                        if (i == messagesPerWorker / 2)
                        {
                            if (!initializeBarrier.SignalAndWait(10000))
                                throw new TimeoutException("Concurrent logging initialization barrier timed out");
                        }

                        logger.Info($"{worker}:{i}");
                    }
                }))
                .ToArray();

            startGate.Set();
            Assert.That(initializeBarrier.SignalAndWait(10000), Is.True,
                "Concurrent logging initialization barrier timed out");
            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, message) =>
                {
                    Interlocked.Increment(ref deliveredCount);
                    receivedMessages.AddOrUpdate((string)message, 1, (_, count) => count + 1);
                }
            }, LogLevel.All);

            Assert.That(Task.WaitAll(tasks, 10000), Is.True, "Concurrent logging timed out");
            Assert.That(deliveredCount, Is.EqualTo(expectedCount));
            Assert.That(receivedMessages.Count, Is.EqualTo(expectedCount));
            Assert.That(receivedMessages.Values.All(count => count == 1), Is.True);
        }

        [Test]
        public void RegistryAndLifecycle_WhenMutatedConcurrently_DoNotThrowOrDeadlock()
        {
            const int loggingWorkerCount = 8;
            var exceptions = new ConcurrentQueue<Exception>();
            using var startGate = new ManualResetEventSlim(false);
            LogHandler noOpAppender = (_, _, _, _) => { };
            var tasks = new List<Task>();

            for (int worker = 0; worker < loggingWorkerCount; worker++)
            {
                int workerId = worker;
                tasks.Add(Task.Run(() =>
                {
                    startGate.Wait();

                    try
                    {
                        for (int i = 0; i < 500; i++)
                            LogManager.GetLogger($"worker-{workerId % 4}-{i % 8}").Info(i);
                    }
                    catch (Exception exception)
                    {
                        exceptions.Enqueue(exception);
                    }
                }));
            }

            tasks.Add(Task.Run(() =>
            {
                startGate.Wait();

                try
                {
                    for (int i = 0; i < 200; i++)
                    {
                        LogManager.Initialize(new[] { noOpAppender }, LogLevel.All);
                        LogManager.AddAppender(noOpAppender);
                        LogManager.RemoveAppender(noOpAppender);

                        if (i % 4 == 0)
                            LogManager.Reset();
                    }
                }
                catch (Exception exception)
                {
                    exceptions.Enqueue(exception);
                }
            }));

            startGate.Set();
            Assert.That(Task.WaitAll(tasks.ToArray(), 10000), Is.True, "Concurrent logging lifecycle timed out");
            Assert.That(exceptions, Is.Empty);
        }

        [Test]
        public void Appender_WhenItLogsReentrantly_DoesNotDeadlock()
        {
            Logger nestedLogger = null;
            var receivedMessages = new ConcurrentQueue<string>();
            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, message) =>
                {
                    string text = (string)message;
                    receivedMessages.Enqueue(text);

                    if (text == "outer")
                    {
                        nestedLogger = LogManager.GetLogger("reentrant-appender", false);
                        nestedLogger.Info("inner");
                    }
                }
            }, LogLevel.All);

            Task logTask = Task.Run(() => LogManager.GetLogger("reentrant-source", false).Info("outer"));

            Assert.That(logTask.Wait(5000), Is.True, "Reentrant appender deadlocked");
            Assert.That(logTask.IsFaulted, Is.False, logTask.Exception?.ToString());
            Assert.That(nestedLogger, Is.Not.Null);
            CollectionAssert.AreEqual(new[] { "outer", "inner" }, receivedMessages.ToArray());
        }

        [Test]
        public void InitializationPool_WhenFull_ReplaysNewestMessagesInFifoOrder()
        {
            LogManager.InitializationPoolSize = 3;
            Logger logger = LogManager.GetLogger("bounded-pool");
            var receivedMessages = new List<int>();

            for (int i = 1; i <= 5; i++)
                logger.Info(i);

            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, message) => receivedMessages.Add((int)message)
            }, LogLevel.All);

            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, receivedMessages);
        }

        [Test]
        public void GetLogger_WhenPoolingIsDisabled_DoesNotReplayEarlyMessages()
        {
            Logger logger = LogManager.GetLogger("no-pool", false);
            var receivedMessages = new List<string>();
            logger.Info("before");

            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, message) => receivedMessages.Add((string)message)
            }, LogLevel.All);
            logger.Info("after");

            CollectionAssert.AreEqual(new[] { "after" }, receivedMessages);
        }

        [Test]
        public void Initialize_WhenAppenderEnumerationFails_PreservesCurrentConfiguration()
        {
            int invocationCount = 0;
            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, _) => invocationCount++
            }, LogLevel.All);

            Assert.Throws<InvalidOperationException>(() =>
                LogManager.Initialize(ThrowingAppenders(), LogLevel.Error));

            LogManager.GetLogger("preserved-configuration", false).Info("test");
            Assert.That(invocationCount, Is.EqualTo(1));
            Assert.That(LogManager.GlobalLogLevel, Is.EqualTo(LogLevel.All));
        }

        [Test]
        public void RemoveAppender_WhenAddedTwice_PreservesOneRegistration()
        {
            int invocationCount = 0;
            LogHandler appender = (_, _, _, _) => invocationCount++;
            LogManager.Initialize(Array.Empty<LogHandler>(), LogLevel.All);
            LogManager.AddAppender(appender);
            LogManager.AddAppender(appender);
            LogManager.RemoveAppender(appender);

            LogManager.GetLogger("duplicate-appender", false).Info("test");

            Assert.That(invocationCount, Is.EqualTo(1));
        }

        [Test]
        public void Reset_WhenLoggerBelongsToPreviousGeneration_DoesNotRouteItsMessages()
        {
            int previousGenerationCount = 0;
            int currentGenerationCount = 0;
            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, _) => previousGenerationCount++
            }, LogLevel.All);
            Logger staleLogger = LogManager.GetLogger("generation", false);
            staleLogger.Info("before-reset");

            LogManager.Reset();
            LogManager.Initialize(new LogHandler[]
            {
                (_, _, _, _) => currentGenerationCount++
            }, LogLevel.All);
            Logger currentLogger = LogManager.GetLogger("generation", false);
            staleLogger.Info("stale");
            currentLogger.Info("current");

            Assert.That(previousGenerationCount, Is.EqualTo(1));
            Assert.That(currentGenerationCount, Is.EqualTo(1));
            Assert.That(currentLogger, Is.Not.SameAs(staleLogger));
        }

        private static IEnumerable<LogHandler> ThrowingAppenders()
        {
            yield return (_, _, _, _) => { };
            throw new InvalidOperationException("Expected appender enumeration failure");
        }
    }
}
