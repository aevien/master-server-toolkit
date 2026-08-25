using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MasterSpawnerLifecycleRegressionTests
    {
        private readonly List<TestPeer> peers = new List<TestPeer>();
        private readonly List<RegisteredSpawner> spawners = new List<RegisteredSpawner>();
        private readonly List<GameObject> testObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (RegisteredSpawner spawner in spawners)
                CompleteSpawnerForTest(spawner);

            spawners.Clear();

            foreach (TestPeer peer in peers)
            {
                peer.CompletePendingRequests();
                peer.Dispose();
            }

            peers.Clear();

            foreach (GameObject testObject in testObjects)
                UnityEngine.Object.DestroyImmediate(testObject);

            testObjects.Clear();
        }

        private static void CompleteSpawnerForTest(RegisteredSpawner spawner)
        {
            foreach (SpawnTask task in spawner.CloseAndGetTasksSnapshot())
                spawner.TryMarkProcessKilled(task);
        }

        [Test]
        public void ClosingSpawner_RejectsAdmissionButKeepsLiveTaskUntilProcessKilled()
        {
            TestPeer owner = CreatePeer();
            RegisteredSpawner spawner = CreateRegisteredSpawner(owner);
            var liveTask = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(liveTask), Is.True);
            spawner.UpdateQueue();
            owner.CompleteNext(MstOpCodes.SpawnProcessRequest, ResponseStatus.Success);
            Assert.That(spawner.TryMarkProcessStarted(liveTask), Is.True);

            IReadOnlyList<SpawnTask> tasksToKill = spawner.CloseAndGetTasksSnapshot();
            var rejectedTask = new SpawnTask(2, spawner, new MstProperties());

            Assert.That(spawner.LifecycleState, Is.EqualTo(RegisteredSpawnerLifecycleState.Closing));
            Assert.That(tasksToKill, Is.EqualTo(new[] { liveTask }));
            Assert.That(spawner.GetAllTasksSnapshot(), Is.EqualTo(new[] { liveTask }));
            Assert.That(liveTask.Status, Is.EqualTo(SpawnStatus.Aborting));
            Assert.That(spawner.TryReserveAndEnqueue(rejectedTask), Is.False);

            Assert.That(spawner.TryMarkProcessKilled(liveTask), Is.True);
            Assert.That(spawner.LifecycleState, Is.EqualTo(RegisteredSpawnerLifecycleState.Closed));
            Assert.That(spawner.CloseCompletion.IsCompleted, Is.True);
            Assert.That(spawner.GetAllTasksSnapshot(), Is.Empty);
        }

        [Test]
        public async Task UnregisterSpawner_RequiresExactOwnerAndAcceptsNotFoundKillConfirmation()
        {
            TestSpawnersModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer intruder = CreatePeer();
            RegisteredSpawner spawner = module.CreateSpawner(owner, new SpawnerOptions { MaxProcesses = 1 });
            SpawnTask liveTask = module.Spawn(new MstProperties(), spawner);
            Assert.That(liveTask, Is.Not.Null);
            spawner.UpdateQueue();
            owner.CompleteNext(MstOpCodes.SpawnProcessRequest, ResponseStatus.Success);
            Assert.That(spawner.TryMarkProcessStarted(liveTask), Is.True);

            await module.InvokeUnregister(CreateIntRequest(MstOpCodes.UnregisterSpawner, spawner.SpawnerId, intruder));

            Assert.That(intruder.TakeNext(MstOpCodes.UnregisterSpawner).Status, Is.EqualTo(ResponseStatus.Forbidden));
            Assert.That(module.Spawners, Does.Contain(spawner));

            await module.InvokeUnregister(CreateIntRequest(MstOpCodes.UnregisterSpawner, spawner.SpawnerId, owner));

            Assert.That(module.Spawners.Contains(spawner), Is.False);
            Assert.That(spawner.LifecycleState, Is.EqualTo(RegisteredSpawnerLifecycleState.Closing));
            owner.CompleteNext(MstOpCodes.KillProcessRequest, ResponseStatus.NotFound);
            Assert.That(owner.TakeNext(MstOpCodes.UnregisterSpawner).Status, Is.EqualTo(ResponseStatus.Success));
            Assert.That(spawner.LifecycleState, Is.EqualTo(RegisteredSpawnerLifecycleState.Closed));
            Assert.That(module.Tasks.Contains(liveTask), Is.False);
            await AwaitCondition(() => module.PendingSupervisionCount == 0);
        }

        [Test]
        public async Task ProcessLifecycleHandlers_RejectForeignPeerAndAcceptOwner()
        {
            TestSpawnersModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer intruder = CreatePeer();
            RegisteredSpawner spawner = module.CreateSpawner(owner, new SpawnerOptions { MaxProcesses = 4 });
            SpawnTask task = module.Spawn(new MstProperties(), spawner);
            Assert.That(task, Is.Not.Null);
            spawner.UpdateQueue();
            owner.CompleteNext(MstOpCodes.SpawnProcessRequest, ResponseStatus.Success);

            LogAssert.Expect(LogType.Warning, new Regex("tried to report a started process"));
            await module.InvokeProcessStarted(CreateIntRequest(MstOpCodes.ProcessStarted, task.Id, intruder));
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.StartingProcess));
            Assert.That(spawner.ProcessesRunning, Is.Zero);

            await module.InvokeProcessStarted(CreateIntRequest(MstOpCodes.ProcessStarted, task.Id, owner));
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.WaitingForProcess));
            Assert.That(spawner.ProcessesRunning, Is.EqualTo(1));

            LogAssert.Expect(LogType.Warning, new Regex("tried to update process count"));
            await module.InvokeProcessesCount(CreateProcessesCountRequest(spawner.SpawnerId, 3, intruder));
            Assert.That(spawner.ProcessesRunning, Is.EqualTo(1));

            await module.InvokeProcessesCount(CreateProcessesCountRequest(spawner.SpawnerId, 2, owner));
            Assert.That(spawner.ProcessesRunning, Is.EqualTo(2));
            await module.InvokeProcessesCount(CreateProcessesCountRequest(spawner.SpawnerId, 1, owner));

            LogAssert.Expect(LogType.Warning, new Regex("tried to report a killed process"));
            await module.InvokeProcessKilled(CreateIntRequest(MstOpCodes.ProcessKilled, task.Id, intruder));
            Assert.That(module.Tasks.Contains(task), Is.True);
            Assert.That(spawner.ProcessesRunning, Is.EqualTo(1));

            await module.InvokeProcessKilled(CreateIntRequest(MstOpCodes.ProcessKilled, task.Id, owner));
            Assert.That(module.Tasks.Contains(task), Is.False);
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Killed));
            Assert.That(spawner.ProcessesRunning, Is.Zero);
        }

        [Test]
        public async Task StopServerRun_RejectsRegistrationAndSpawnAttemptedDuringDestruction()
        {
            TestSpawnersModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer lateOwner = CreatePeer();
            RegisteredSpawner spawner = module.CreateSpawner(owner, new SpawnerOptions { MaxProcesses = 1 });
            RegisteredSpawner lateSpawner = null;
            SpawnTask lateTask = null;
            module.OnSpawnerDestroyedEvent += _ =>
            {
                lateSpawner = module.CreateSpawner(lateOwner, new SpawnerOptions());
                lateTask = module.Spawn(new MstProperties(), spawner);
            };

            await AwaitCompletion(module.StopServerRunAsync());

            Assert.That(lateSpawner, Is.Null);
            Assert.That(lateTask, Is.Null);
            Assert.That(module.Spawners, Is.Empty);
            Assert.That(module.Tasks, Is.Empty);
        }

        [Test]
        public async Task RegistrationPublication_WhenStopInterleaves_PublishesDestroyedAfterRegistered()
        {
            TestSpawnersModule module = CreateModule();
            TestPeer owner = CreatePeer();
            var lifecycleEvents = new ConcurrentQueue<string>();
            using var registrationEntered = new ManualResetEventSlim();
            using var releaseRegistration = new ManualResetEventSlim();
            module.OnSpawnerRegisteredEvent += _ =>
            {
                lifecycleEvents.Enqueue("registered");
                registrationEntered.Set();
                if (!releaseRegistration.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("Registration callback was not released by the test");
            };
            module.OnSpawnerDestroyedEvent += _ => lifecycleEvents.Enqueue("destroyed");
            Task<RegisteredSpawner> registrationTask = Task.Run(() =>
                module.CreateSpawner(owner, new SpawnerOptions()));
            bool entered = registrationEntered.Wait(1000);

            if (!entered)
                releaseRegistration.Set();

            Assert.That(entered, Is.True, "Registration event did not reach the deterministic barrier");
            Task stopTask = null;

            try
            {
                stopTask = module.StopServerRunAsync();
                Assert.That(lifecycleEvents.ToArray(), Is.EqualTo(new[] { "registered" }));
            }
            finally
            {
                releaseRegistration.Set();
            }

            RegisteredSpawner spawner = await registrationTask;
            await AwaitCompletion(stopTask);

            Assert.That(spawner.LifecycleState, Is.EqualTo(RegisteredSpawnerLifecycleState.Closed));
            Assert.That(lifecycleEvents.ToArray(), Is.EqualTo(new[] { "registered", "destroyed" }));
        }

        [Test]
        public async Task UnregisterSpawner_WhenKillIsAcceptedWithoutTerminalConfirmation_CleansUpAfterTimeout()
        {
            TestSpawnersModule module = CreateModule();
            module.ShutdownTimeoutMilliseconds = 100;
            TestPeer owner = CreatePeer();
            RegisteredSpawner spawner = module.CreateSpawner(owner, new SpawnerOptions { MaxProcesses = 1 });
            SpawnTask liveTask = module.Spawn(new MstProperties(), spawner);
            Assert.That(liveTask, Is.Not.Null);
            spawner.UpdateQueue();
            owner.CompleteNext(MstOpCodes.SpawnProcessRequest, ResponseStatus.Success);
            Assert.That(spawner.TryMarkProcessStarted(liveTask), Is.True);
            LogAssert.Expect(LogType.Warning, new Regex("close confirmation timed out"));

            await module.InvokeUnregister(CreateIntRequest(MstOpCodes.UnregisterSpawner, spawner.SpawnerId, owner));
            owner.CompleteNext(MstOpCodes.KillProcessRequest, ResponseStatus.Success);
            Assert.That(owner.TakeNext(MstOpCodes.UnregisterSpawner).Status, Is.EqualTo(ResponseStatus.Success));
            await AwaitCondition(() =>
                spawner.LifecycleState == RegisteredSpawnerLifecycleState.Closed &&
                !module.Tasks.Contains(liveTask) &&
                module.PendingSupervisionCount == 0);

            Assert.That(liveTask.Status, Is.Not.EqualTo(SpawnStatus.Killed));
        }

        [Test]
        public async Task StopServerRun_WhenKillIsAccepted_AbandonsSupervisionWithoutFalseKilledStatus()
        {
            TestSpawnersModule module = CreateModule();
            TestPeer owner = CreatePeer();
            RegisteredSpawner spawner = module.CreateSpawner(owner, new SpawnerOptions { MaxProcesses = 1 });
            SpawnTask liveTask = module.Spawn(new MstProperties(), spawner);
            Assert.That(liveTask, Is.Not.Null);
            spawner.UpdateQueue();
            owner.CompleteNext(MstOpCodes.SpawnProcessRequest, ResponseStatus.Success);
            Assert.That(spawner.TryMarkProcessStarted(liveTask), Is.True);

            Task stopTask = module.StopServerRunAsync();
            Assert.That(owner.SentMessages.Any(message => message.OpCode == MstOpCodes.KillProcessRequest), Is.True);
            Assert.That(stopTask.IsCompleted, Is.False);
            owner.CompleteNext(MstOpCodes.KillProcessRequest, ResponseStatus.Success);
            await AwaitCompletion(stopTask);

            Assert.That(spawner.LifecycleState, Is.EqualTo(RegisteredSpawnerLifecycleState.Closed));
            Assert.That(liveTask.Status, Is.Not.EqualTo(SpawnStatus.Killed));
            Assert.That(module.Tasks, Is.Empty);
        }

        [Test]
        public async Task ForceCloseAfterTimeout_StopsFinalizedTaskKillWatchdog()
        {
            TestSpawnersModule module = CreateModule();
            module.ShutdownTimeoutMilliseconds = 20;
            TestPeer owner = CreatePeer();
            RegisteredSpawner spawner = module.CreateSpawner(owner, new SpawnerOptions { MaxProcesses = 1 });
            var liveTask = new SpawnTask(1, spawner, new MstProperties(), 100);
            Assert.That(spawner.TryReserveAndEnqueue(liveTask), Is.True);
            spawner.UpdateQueue();
            owner.CompleteNext(MstOpCodes.SpawnProcessRequest, ResponseStatus.Success);
            Assert.That(spawner.TryMarkProcessStarted(liveTask), Is.True);
            Assert.That(liveTask.TryFinalize(new SpawnFinalizationPacket()), Is.True);

            liveTask.KillSpawnedProcess();
            owner.CompleteNext(MstOpCodes.KillProcessRequest, ResponseStatus.Success);
            LogAssert.Expect(LogType.Warning, new Regex("close confirmation timed out"));
            module.DestroySpawner(spawner);
            owner.CompleteNext(MstOpCodes.KillProcessRequest, ResponseStatus.Success);
            await AwaitCondition(() => module.PendingSupervisionCount == 0);

            await Task.Delay(1000);

            Assert.That(owner.SentMessages.Any(message => message.OpCode == MstOpCodes.KillProcessRequest), Is.False,
                "An abandoned finalized task scheduled another kill request");
            Assert.That(liveTask.Status, Is.EqualTo(SpawnStatus.Finalized));
        }

        private TestSpawnersModule CreateModule()
        {
            var testObject = new GameObject(nameof(MasterSpawnerLifecycleRegressionTests));
            testObjects.Add(testObject);
            var module = testObject.AddComponent<TestSpawnersModule>();
            module.ConfigureForTests(spawners.Add);
            return module;
        }

        private RegisteredSpawner CreateRegisteredSpawner(TestPeer owner)
        {
            var spawner = new RegisteredSpawner(
                1,
                owner,
                new SpawnerOptions { MaxProcesses = 1 },
                Mst.Create.Logger(nameof(MasterSpawnerLifecycleRegressionTests)),
                8,
                0);

            spawners.Add(spawner);
            return spawner;
        }

        private TestPeer CreatePeer()
        {
            var peer = new TestPeer();
            peers.Add(peer);
            return peer;
        }

        private static IIncomingMessage CreateIntRequest(ushort opCode, int value, TestPeer peer)
        {
            return new IncomingMessage(
                opCode,
                0,
                MessageHelper.Create(opCode, value).Data,
                DeliveryMethod.Reliable,
                peer)
            {
                AckResponseId = 1
            };
        }

        private static IIncomingMessage CreateProcessesCountRequest(int spawnerId, int count, TestPeer peer)
        {
            var packet = new IntPairPacket
            {
                A = spawnerId,
                B = count
            };

            return new IncomingMessage(
                MstOpCodes.UpdateSpawnerProcessesCount,
                0,
                packet.ToBytes(),
                DeliveryMethod.Reliable,
                peer)
            {
                AckResponseId = 1
            };
        }

        private static async Task AwaitCompletion(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(3000));
            Assert.That(completed, Is.SameAs(task), "Spawner lifecycle operation exceeded the test deadline");
            await task;
        }

        private static async Task AwaitCondition(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);

            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.That(condition(), Is.True, "Spawner lifecycle condition was not reached before the deadline");
        }

        private sealed class TestSpawnersModule : SpawnersModule
        {
            private Action<RegisteredSpawner> onSpawnerCreated;

            public int ShutdownTimeoutMilliseconds { get; set; } = 1000;
            public int PendingSupervisionCount => PendingCloseSupervisionCount;

            protected override int ShutdownConfirmationTimeoutMs => ShutdownTimeoutMilliseconds;

            public void ConfigureForTests(Action<RegisteredSpawner> spawnerCreated)
            {
                logger ??= Mst.Create.Logger(nameof(MasterSpawnerLifecycleRegressionTests));
                onSpawnerCreated = spawnerCreated;
            }

            public override RegisteredSpawner CreateSpawner(IPeer peer, SpawnerOptions options)
            {
                RegisteredSpawner spawner = base.CreateSpawner(peer, options);

                if (spawner != null)
                    onSpawnerCreated?.Invoke(spawner);

                return spawner;
            }

            public Task InvokeUnregister(IIncomingMessage message)
            {
                return UnregisterSpawnerRequestHandler(message);
            }

            public Task InvokeProcessStarted(IIncomingMessage message)
            {
                return SetProcessStartedRequestHandler(message);
            }

            public Task InvokeProcessKilled(IIncomingMessage message)
            {
                return SetProcessKilledRequestHandler(message);
            }

            public Task InvokeProcessesCount(IIncomingMessage message)
            {
                return SetSpawnedProcessesCountRequestHandler(message);
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private readonly ConcurrentQueue<SentMessage> sentMessages =
                new ConcurrentQueue<SentMessage>();
            private bool isConnected = true;

            public IEnumerable<IOutgoingMessage> SentMessages =>
                sentMessages.ToArray().Select(sentMessage => sentMessage.Message);
            public override bool IsConnected => isConnected;

            public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
            {
                sentMessages.Enqueue(new SentMessage(message, message.AckRequestId));
            }

            protected override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod,
                Action<bool> completionCallback)
            {
                sentMessages.Enqueue(new SentMessage(message, message.AckRequestId));
                completionCallback?.Invoke(IsConnected);
            }

            public IOutgoingMessage TakeNext(ushort opCode)
            {
                return TakeNextSentMessage(opCode).Message;
            }

            public void CompleteNext(ushort opCode, ResponseStatus status)
            {
                CompleteRequest(TakeNextSentMessage(opCode), status);
            }

            public void CompletePendingRequests()
            {
                while (sentMessages.TryDequeue(out SentMessage sentMessage))
                {
                    if (sentMessage.AckRequestId.HasValue)
                        CompleteRequest(sentMessage, ResponseStatus.Success);
                }
            }

            public override void Disconnect(string reason = "")
            {
                isConnected = false;
            }

            public override void Disconnect(ushort code, string reason = "")
            {
                CloseCode = code;
                isConnected = false;
            }

            protected override void Dispose(bool disposing)
            {
                isConnected = false;
                base.Dispose(disposing);
            }

            private SentMessage TakeNextSentMessage(ushort opCode)
            {
                Assert.That(sentMessages.TryDequeue(out SentMessage sentMessage), Is.True);
                Assert.That(sentMessage.Message.OpCode, Is.EqualTo(opCode));
                return sentMessage;
            }

            private void CompleteRequest(SentMessage sentMessage, ResponseStatus status)
            {
                Assert.That(sentMessage.AckRequestId.HasValue, Is.True);
                var response = new IncomingMessage(
                    sentMessage.Message.OpCode,
                    0,
                    Array.Empty<byte>(),
                    DeliveryMethod.Reliable,
                    this)
                {
                    Status = status
                };

                TriggerAck(sentMessage.AckRequestId.Value, status, response);
            }

            private sealed class SentMessage
            {
                public SentMessage(IOutgoingMessage message, int? ackRequestId)
                {
                    Message = message;
                    AckRequestId = ackRequestId;
                }

                public IOutgoingMessage Message { get; }
                public int? AckRequestId { get; }
            }
        }
    }
}
