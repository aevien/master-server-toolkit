using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class AnalyticsLifecycleTests
    {
        private GameObject testObject;
        private TestAnalyticsModule analyticsModule;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(AnalyticsLifecycleTests));
            analyticsModule = testObject.AddComponent<TestAnalyticsModule>();
            analyticsModule.EnableForTest();
        }

        [TearDown]
        public void TearDown()
        {
            if (testObject != null)
                UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public async Task StopServerRunAsync_WhenInsertStarts_WaitsForRegisteredSaveTask()
        {
            var database = new ControlledAnalyticsDatabaseAccessor();
            Task stopTask = null;

            analyticsModule.DatabaseAccessor = database;
            analyticsModule.StartServerRun(CancellationToken.None);
            analyticsModule.Save(CreateEvent("before-stop"));
            database.OnInsert = () => stopTask = analyticsModule.StopServerRunAsync();

            analyticsModule.TickSave();

            Assert.That(stopTask, Is.Not.Null);
            Assert.That(stopTask.IsCompleted, Is.False);

            database.CompleteInsert();
            await AssertCompletesAsync(stopTask, "Analytics stop did not finish after the active insert completed");

            Assert.That(database.InsertedKeys, Is.EqualTo(new[] { "before-stop" }));
        }

        [Test]
        public async Task StopServerRunAsync_FlushesEventsQueuedBehindActiveSave()
        {
            var database = new ControlledAnalyticsDatabaseAccessor();

            analyticsModule.DatabaseAccessor = database;
            analyticsModule.StartServerRun(CancellationToken.None);
            analyticsModule.Save(CreateEvent("first"));
            analyticsModule.TickSave();
            analyticsModule.Save(CreateEvent("second"));

            Task stopTask = analyticsModule.StopServerRunAsync();
            Assert.That(stopTask.IsCompleted, Is.False);

            database.CompleteInsert();
            await AssertCompletesAsync(stopTask, "Analytics stop did not flush the queued event");

            Assert.That(database.InsertedKeys, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public async Task StopServerRunAsync_AfterUnityObjectDestroyed_WaitsForActiveSaveAndFlushesQueuedEvents()
        {
            var database = new ControlledAnalyticsDatabaseAccessor();

            analyticsModule.DatabaseAccessor = database;
            analyticsModule.StartServerRun(CancellationToken.None);
            analyticsModule.Save(CreateEvent("active-before-destroy"));
            analyticsModule.TickSave();
            analyticsModule.Save(CreateEvent("queued-before-destroy"));

            IServerRunModule runModule = analyticsModule;
            UnityEngine.Object.DestroyImmediate(testObject);
            testObject = null;
            analyticsModule = null;

            Task stopTask = runModule.StopServerRunAsync();
            Assert.That(stopTask.IsCompleted, Is.False);

            database.CompleteInsert();
            await AssertCompletesAsync(
                stopTask,
                "Destroyed analytics module did not complete its shutdown flush");

            Assert.That(database.InsertedKeys,
                Is.EqualTo(new[] { "active-before-destroy", "queued-before-destroy" }));
        }

        [Test]
        public async Task BackgroundSave_WhenInsertFails_RequeuesBatchForNextAttempt()
        {
            var database = new ControlledAnalyticsDatabaseAccessor(blockFirstInsert: false)
            {
                FailNextInsert = new InvalidOperationException("Expected analytics insert failure")
            };

            analyticsModule.DatabaseAccessor = database;
            analyticsModule.StartServerRun(CancellationToken.None);
            analyticsModule.Save(CreateEvent("retry-me"));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "Failed to save analytics batch"));
            analyticsModule.TickSave();
            analyticsModule.TickSave();

            await AssertCompletesAsync(
                analyticsModule.StopServerRunAsync(),
                "Analytics module did not stop after the retry completed");

            Assert.That(database.InsertedKeys, Is.EqualTo(new[] { "retry-me", "retry-me" }));
        }

        [Test]
        public void Save_OutsideActiveServerRun_IsRejected()
        {
            analyticsModule.DatabaseAccessor = new ControlledAnalyticsDatabaseAccessor();

            Assert.That(analyticsModule.Save(CreateEvent("before-run")), Is.False);

            analyticsModule.StartServerRun(CancellationToken.None);
            Assert.That(analyticsModule.Save(CreateEvent("during-run")), Is.True);
        }

        private static async Task AssertCompletesAsync(Task task, string message)
        {
            Task timeout = Task.Delay(2000);
            Assert.That(await Task.WhenAny(task, timeout), Is.SameAs(task), message);
            await task;
        }

        private static AnalyticsDataInfoPacket CreateEvent(string key)
        {
            return new AnalyticsDataInfoPacket
            {
                Key = key,
                UserId = "test-user",
                Data = new Dictionary<string, string>()
            };
        }

        private sealed class TestAnalyticsModule : AnalyticsModule
        {
            public void EnableForTest()
            {
                logger ??= Mst.Create.Logger(nameof(AnalyticsLifecycleTests));
                useAnalytics = true;
                saveDebounceTime = 0f;
            }

            public void TickSave()
            {
                UpdateThrottle();
            }

            public Task FlushForTest()
            {
                return SaveEventsAsync();
            }
        }

        private sealed class ControlledAnalyticsDatabaseAccessor : IAnalyticsDatabaseAccessor
        {
            private readonly object gate = new object();
            private readonly Queue<TaskCompletionSource<bool>> insertCompletions =
                new Queue<TaskCompletionSource<bool>>();
            private readonly List<string> insertedKeys = new List<string>();
            private bool blockNextInsert;

            public ControlledAnalyticsDatabaseAccessor(bool blockFirstInsert = true)
            {
                blockNextInsert = blockFirstInsert;
            }

            public Action OnInsert { get; set; }
            public Exception FailNextInsert { get; set; }
            public MstProperties CustomProperties { get; } = new MstProperties();
            public MasterServerToolkit.Logging.Logger Logger { get; set; }

            public IReadOnlyList<string> InsertedKeys
            {
                get
                {
                    lock (gate)
                        return insertedKeys.ToArray();
                }
            }

            public Task Insert(IEnumerable<IAnalyticsInfoData> eventsData)
            {
                TaskCompletionSource<bool> completion = null;
                Exception failure;

                lock (gate)
                {
                    insertedKeys.AddRange(eventsData.Select(item => item.Key));
                    failure = FailNextInsert;
                    FailNextInsert = null;

                    if (failure == null && blockNextInsert)
                    {
                        blockNextInsert = false;
                        completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        insertCompletions.Enqueue(completion);
                    }
                }

                OnInsert?.Invoke();
                if (failure != null)
                    return Task.FromException(failure);

                return completion?.Task ?? Task.CompletedTask;
            }

            public void CompleteInsert()
            {
                TaskCompletionSource<bool> completion;

                lock (gate)
                    completion = insertCompletions.Dequeue();

                completion.TrySetResult(true);
            }

            public Task<IEnumerable<IAnalyticsInfoData>> Get(int size, int page) =>
                Task.FromResult(Enumerable.Empty<IAnalyticsInfoData>());

            public Task<IAnalyticsInfoData> GetById(string id) =>
                Task.FromResult<IAnalyticsInfoData>(null);

            public Task<IEnumerable<IAnalyticsInfoData>> GetByKey(string eventId, int size, int page) =>
                Task.FromResult(Enumerable.Empty<IAnalyticsInfoData>());

            public Task<IEnumerable<IAnalyticsInfoData>> GetByUserId(string userId, int size, int page) =>
                Task.FromResult(Enumerable.Empty<IAnalyticsInfoData>());

            public Task<IEnumerable<IAnalyticsInfoData>> GetByTimestamp(DateTime timestamp) =>
                Task.FromResult(Enumerable.Empty<IAnalyticsInfoData>());

            public Task<IEnumerable<IAnalyticsInfoData>> GetByTimestampRange(DateTime timestampStart,
                DateTime timestampEnd, int size, int page) =>
                Task.FromResult(Enumerable.Empty<IAnalyticsInfoData>());

            public Task<IEnumerable<IAnalyticsInfoData>> GetWithQuery(string query, int size, int page) =>
                Task.FromResult(Enumerable.Empty<IAnalyticsInfoData>());

            public Task<DatabaseEntriesInfo<IAnalyticsInfoData>> Search(Dictionary<string, object> filter) =>
                Task.FromResult(new DatabaseEntriesInfo<IAnalyticsInfoData>());

            public void Dispose()
            {
            }
        }
    }
}
