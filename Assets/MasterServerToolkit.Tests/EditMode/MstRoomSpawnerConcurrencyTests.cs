using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstRoomSpawnerConcurrencyTests
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

        private static async Task AssertTaskCanceledAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (TaskCanceledException)
            {
                return;
            }

            Assert.Fail("Expected the task to be canceled");
        }

        private static async Task AssertConditionAsync(Func<bool> condition, TimeSpan timeout, string message)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.That(condition(), Is.True, message);
        }

        private static void RunConcurrently(Action first, Action second)
        {
            using var ready = new CountdownEvent(2);
            using var start = new ManualResetEventSlim(false);
            Task firstTask = Task.Run(() =>
            {
                ready.Signal();

                if (!start.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("Concurrent test start was not released");

                first();
            });
            Task secondTask = Task.Run(() =>
            {
                ready.Signal();

                if (!start.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("Concurrent test start was not released");

                second();
            });

            try
            {
                Assert.That(ready.Wait(TimeSpan.FromSeconds(2)), Is.True,
                    "Concurrent test workers were not both scheduled");
            }
            finally
            {
                start.Set();
            }

            Assert.That(Task.WaitAll(new[] { firstTask, secondTask }, TimeSpan.FromSeconds(5)), Is.True,
                "Concurrent test workers did not complete");
        }

        [Test]
        public void SpawnerReservation_WhenRequestsRace_RespectsMaxProcesses()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            SpawnTask[] tasks = Enumerable.Range(0, 64)
                .Select(id => new SpawnTask(id, spawner, new MstProperties()))
                .ToArray();
            int accepted = 0;

            Parallel.ForEach(tasks, task =>
            {
                if (spawner.TryReserveAndEnqueue(task))
                    Interlocked.Increment(ref accepted);
            });

            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(spawner.GetQueuedTasksSnapshot().Count, Is.EqualTo(1));
            Assert.That(spawner.CalculateFreeSlotsCount(), Is.Zero);
        }

        [Test]
        public void GetBeingSpawnedTasksSnapshot_WhenTaskCompletes_RemainsDetached()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);
            spawner.UpdateQueue();

            IReadOnlyList<SpawnTask> snapshot = spawner.GetBeingSpawnedTasksSnapshot();
            Assert.That(snapshot, Is.EqualTo(new[] { task }));
            Assert.That(spawner.TryMarkProcessStarted(task), Is.True);
            Assert.That(spawner.TryMarkProcessKilled(task), Is.True);

            Assert.That(snapshot, Is.EqualTo(new[] { task }));
            Assert.That(spawner.GetBeingSpawnedTasksSnapshot(), Is.Empty);
        }

        [Test]
        public void SpawnTask_WhenTerminalTransitionsRace_CompletesListenerOnce()
        {
            var task = new SpawnTask(1, null, new MstProperties());
            int completionCount = 0;
            task.WhenDone(_ => Interlocked.Increment(ref completionCount));

            Parallel.Invoke(
                () => task.TrySetStatus(SpawnStatus.Finalized),
                () => task.TrySetStatus(SpawnStatus.Killed));

            int lateCompletionCount = 0;
            task.WhenDone(_ => Interlocked.Increment(ref lateCompletionCount));

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(lateCompletionCount, Is.EqualTo(1));
            Assert.That(task.Status == SpawnStatus.Finalized || task.Status == SpawnStatus.Killed, Is.True);
        }

        [Test]
        public void SpawnTask_WhenFinalizedProcessLaterDies_PublishesKilledWithoutRepeatingDoneCallback()
        {
            var task = new SpawnTask(1, null, new MstProperties());
            var statuses = new List<SpawnStatus>();
            int completionCount = 0;
            task.OnStatusChangedEvent += statuses.Add;
            task.WhenDone(_ => completionCount++);

            Assert.That(task.TrySetStatus(SpawnStatus.Finalized), Is.True);
            Assert.That(task.TryMarkProcessKilled(), Is.True);

            Assert.That(statuses, Is.EqualTo(new[] { SpawnStatus.Finalized, SpawnStatus.Killed }));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Killed));
        }

        [Test]
        public void ProcessNotifications_WhenDuplicated_CountEachTransitionOnce()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 4);
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);

            Parallel.For(0, 32, _ => spawner.TryMarkProcessStarted(task));
            Assert.That(spawner.ProcessesRunning, Is.EqualTo(1));

            Parallel.For(0, 32, _ => spawner.TryMarkProcessKilled(task));
            Assert.That(spawner.ProcessesRunning, Is.Zero);
        }

        [Test]
        public void Abort_WhenTaskIsStillQueued_DoesNotSendSpawnRequest()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);

            task.Abort();
            spawner.UpdateQueue();

            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborted));
            Assert.That(((TestPeer)spawner.Peer).SentCount, Is.Zero);
            Assert.That(spawner.GetQueuedTasksSnapshot(), Is.Empty);
        }

        [Test]
        public void Abort_DuringStartingNotification_DoesNotSendSpawnOrKillRequest()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            TestPeer owner = (TestPeer)spawner.Peer;
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);
            task.OnStatusChangedEvent += status =>
            {
                if (status == SpawnStatus.StartingProcess)
                    task.Abort();
            };

            spawner.UpdateQueue();

            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborted));
            Assert.That(owner.SentCount, Is.Zero,
                "A locally aborted task sent a spawn or kill request");
            Assert.That(spawner.GetQueuedTasksSnapshot(), Is.Empty);
            Assert.That(spawner.CalculateFreeSlotsCount(), Is.EqualTo(1));
        }

        [Test]
        public void SpawnerClose_WhenStartingNotificationClosesOwner_DoesNotPublishSpawnRequest()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);
            task.OnStatusChangedEvent += status =>
            {
                if (status == SpawnStatus.StartingProcess)
                    spawner.CloseAndGetTasksSnapshot();
            };

            spawner.UpdateQueue();

            Assert.That(spawner.IsActive, Is.False);
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborted));
            Assert.That(((TestPeer)spawner.Peer).SentCount, Is.Zero);
        }

        [Test]
        public void SpawnAckTimeout_RequestsKillBeforeCompletingTask()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            TestPeer owner = (TestPeer)spawner.Peer;
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);
            spawner.UpdateQueue();

            LogAssert.Expect(LogType.Error,
                new Regex("Spawn request was not handled.*Timeout"));
            owner.CompleteNext(ResponseStatus.Timeout, Array.Empty<byte>());

            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborting));
            Assert.That(owner.SentCount, Is.EqualTo(1));

            owner.CompleteNext(ResponseStatus.NotFound, Array.Empty<byte>());

            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Killed));
        }

        [Test]
        public void Abort_WhenKillRequestFails_CanRetryAndWaitsForProcessKilled()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            TestPeer owner = (TestPeer)spawner.Peer;
            var task = new SpawnTask(1, spawner, new MstProperties());
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);
            spawner.UpdateQueue();
            owner.CompleteNext(ResponseStatus.Success, Array.Empty<byte>());
            Assert.That(spawner.TryMarkProcessStarted(task), Is.True);

            task.Abort();
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborting));
            Assert.That(owner.SentCount, Is.EqualTo(1));

            LogAssert.Expect(LogType.Warning,
                new Regex(@"Spawned process for task \[1\].*Status: Error"));
            owner.CompleteNext(ResponseStatus.Error, Array.Empty<byte>());
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborting));

            task.Abort();
            Assert.That(owner.SentCount, Is.EqualTo(1));
            owner.CompleteNext(ResponseStatus.Success, Array.Empty<byte>());

            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Aborting));
            Assert.That(spawner.TryMarkProcessKilled(task), Is.True);
            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Killed));
            Assert.That(spawner.ProcessesRunning, Is.Zero);
        }

        [Test]
        public async Task Abort_WhenKillAckIsNotConfirmed_WatchdogRetriesUntilNotFound()
        {
            RegisteredSpawner spawner = CreateSpawner(maxProcesses: 1);
            TestPeer owner = (TestPeer)spawner.Peer;
            var task = new SpawnTask(1, spawner, new MstProperties(), 20);
            Assert.That(spawner.TryReserveAndEnqueue(task), Is.True);
            spawner.UpdateQueue();
            owner.CompleteNext(ResponseStatus.Success, Array.Empty<byte>());
            Assert.That(spawner.TryMarkProcessStarted(task), Is.True);

            task.Abort();
            owner.CompleteNext(ResponseStatus.Success, Array.Empty<byte>());

            await AssertConditionAsync(
                () => owner.SentCount == 1,
                TimeSpan.FromSeconds(3),
                "Kill confirmation watchdog did not retry before the test deadline");
            owner.CompleteNext(ResponseStatus.NotFound, Array.Empty<byte>());

            Assert.That(task.Status, Is.EqualTo(SpawnStatus.Killed));
            Assert.That(spawner.ProcessesRunning, Is.Zero);
        }

        [Test]
        public void SpawnerController_WorkerThread_IsBackground()
        {
            var controller = new TestSpawnerController(new FakeClientSocket());
            Thread worker = controller.CreateWorkerForTest();

            Assert.That(worker.IsBackground, Is.True);

            controller.Dispose();
        }

        [Test]
        public void SpawnerController_Dispose_RemovesRegistrationWithoutRemovingServerHandlers()
        {
            var socket = new FakeClientSocket();
            var spawners = new SpawnersServer(socket);
            ISpawnerController controller = null;
            spawners.RegisterSpawner(new SpawnerOptions(), (registeredController, error) =>
            {
                Assert.That(error, Is.Null);
                controller = registeredController;
            });
            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.Spawner,
                MstPermissionLevels.Spawner);
            socket.RespondNext(MstOpCodes.RegisterSpawner, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(42));

            Assert.That(controller, Is.Not.Null);
            Assert.That(spawners.GetSpawnerController(42), Is.SameAs(controller));
            Assert.That(socket.RegisteredHandlerCount, Is.EqualTo(2));
            Assert.That(controller.KillRequestHandler(999), Is.EqualTo(ResponseStatus.NotFound));

            controller.Dispose();

            Assert.That(spawners.GetSpawnerController(42), Is.Null);
            Assert.That(spawners.GetCreatedSpawnerControllers(), Is.Empty);
            Assert.That(socket.RegisteredHandlerCount, Is.EqualTo(2));

            spawners.ClearConnection();

            Assert.That(socket.RegisteredHandlerCount, Is.Zero);
        }

        [Test]
        public void SpawnersServer_RegisterSpawner_WhenRegistrationSendThrows_CompletesCallbackOnce()
        {
            var socket = new FakeClientSocket
            {
                ThrowOnSendOpcode = MstOpCodes.RegisterSpawner
            };
            var spawnersServer = new SpawnersServer(socket);
            int callbackCount = 0;
            ISpawnerController registeredSpawner = null;
            string registrationError = null;

            spawnersServer.RegisterSpawner(new SpawnerOptions(), (controller, error) =>
            {
                callbackCount++;
                registeredSpawner = controller;
                registrationError = error;
            }, socket);

            LogAssert.Expect(
                LogType.Error,
                new Regex("Spawner registration request failed"));
            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.Spawner,
                MstPermissionLevels.Spawner);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(registeredSpawner, Is.Null);
            Assert.That(registrationError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
            socket.Close();
        }

        [Test]
        public void RoomsServer_RegisterRoom_ObtainsRoomPermissionBeforeSendingRegistration()
        {
            var socket = new FakeClientSocket();
            var rooms = new RoomsServer(socket);
            RoomController registeredRoom = null;
            string registrationError = null;

            rooms.RegisterRoom(new RoomOptions(), (controller, error) =>
            {
                registeredRoom = controller;
                registrationError = error;
            }, socket);

            Assert.That(socket.Requests.Count(request =>
                request.Message.OpCode == MstOpCodes.RegisterRoomRequest), Is.Zero);

            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.RoomServer,
                MstPermissionLevels.RoomServer);

            Assert.That(socket.Requests.Count(request =>
                request.Message.OpCode == MstOpCodes.RegisterRoomRequest), Is.EqualTo(1));

            socket.RespondNext(MstOpCodes.RegisterRoomRequest, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(91));

            Assert.That(registrationError, Is.Empty);
            Assert.That(registeredRoom, Is.Not.Null);
            Assert.That(registeredRoom.RoomId, Is.EqualTo(91));
            socket.Close();
        }

        [Test]
        public void RoomsServer_RegisterRoom_WhenRegistrationSendThrows_CompletesCallbackOnce()
        {
            var socket = new FakeClientSocket
            {
                ThrowOnSendOpcode = MstOpCodes.RegisterRoomRequest
            };
            var rooms = new RoomsServer(socket);
            int callbackCount = 0;
            RoomController registeredRoom = null;
            string registrationError = null;

            rooms.RegisterRoom(new RoomOptions(), (controller, error) =>
            {
                callbackCount++;
                registeredRoom = controller;
                registrationError = error;
            }, socket);

            LogAssert.Expect(
                LogType.Error,
                new Regex("Room registration request failed"));
            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.RoomServer,
                MstPermissionLevels.RoomServer);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(registeredRoom, Is.Null);
            Assert.That(registrationError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
            socket.Close();
        }

        [Test]
        public void RoomsServer_DestroyRoom_WhenTransportStopsAcceptingAcks_CompletesLocally()
        {
            var socket = new FakeClientSocket();
            var rooms = new RoomsServer(socket);
            RoomController registeredRoom = null;

            rooms.RegisterRoom(new RoomOptions(), (controller, _) => registeredRoom = controller,
                socket);
            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.RoomServer,
                MstPermissionLevels.RoomServer);
            socket.RespondNext(MstOpCodes.RegisterRoomRequest, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(91));

            int destroyedEventCount = 0;
            int callbackCount = 0;
            bool callbackSucceeded = false;
            rooms.OnRoomDestroyedEvent += _ => destroyedEventCount++;

            registeredRoom.Destroy((success, _) =>
            {
                callbackCount++;
                callbackSucceeded = success;
            });
            socket.RespondNext(MstOpCodes.DestroyRoomRequest, ResponseStatus.NotConnected);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(callbackSucceeded, Is.True);
            Assert.That(destroyedEventCount, Is.EqualTo(1));
            Assert.That(registeredRoom.IsActive, Is.False);
            socket.Close();
        }

        [Test]
        public void RoomsServer_AccountBlockedMessage_RaisesStableAccountId()
        {
            var socket = new FakeClientSocket();
            var rooms = new RoomsServer(socket);
            string blockedAccountId = null;
            rooms.OnAccountBlockedEvent += accountId => blockedAccountId = accountId;

            socket.Deliver(MstOpCodes.AccountBlocked, "account-42".ToBytes());

            Assert.That(blockedAccountId, Is.EqualTo("account-42"));
            socket.Close();
        }

        [Test]
        public void RoomsServer_ReleasePlayerSession_WhenRequestIsMalformed_RespondsInvalid()
        {
            var socket = new FakeClientSocket();
            var rooms = new TestRoomsServer(socket);
            TestPeer responsePeer = CreatePeer();

            LogAssert.Expect(
                LogType.Error,
                new Regex("Invalid room player session release request"));
            rooms.DeliverPlayerSessionReleaseRequest(Array.Empty<byte>(), responsePeer);

            IOutgoingMessage response = responsePeer.DequeueNextMessage();
            Assert.That(response.Status, Is.EqualTo(ResponseStatus.Invalid));
            Assert.That(responsePeer.SentCount, Is.Zero);
            socket.Close();
        }

        [Test]
        public void RoomsServer_ReleasePlayerSession_WhenHandlerIsMissing_RespondsServiceUnavailable()
        {
            var socket = new FakeClientSocket();
            var rooms = new TestRoomsServer(socket);
            TestPeer responsePeer = CreatePeer();
            var packet = new RoomPlayerSessionPacket
            {
                AccountId = "account-42",
                MasterPeerId = 73
            };

            rooms.DeliverPlayerSessionReleaseRequest(packet.ToBytes(), responsePeer);

            IOutgoingMessage response = responsePeer.DequeueNextMessage();
            Assert.That(response.Status, Is.EqualTo(ResponseStatus.ServiceUnavailable));
            Assert.That(responsePeer.SentCount, Is.Zero);
            socket.Close();
        }

        [Test]
        public void RoomsServer_ReleasePlayerSession_CompletesResponseExactlyOnce()
        {
            var socket = new FakeClientSocket();
            var rooms = new TestRoomsServer(socket);
            TestPeer responsePeer = CreatePeer();
            RoomPlayerSessionPacket receivedPacket = null;
            SuccessCallback completion = null;
            rooms.PlayerSessionReleaseHandler = (packet, callback) =>
            {
                receivedPacket = packet;
                completion = callback;
            };
            var request = new RoomPlayerSessionPacket
            {
                AccountId = "account-42",
                MasterPeerId = 73
            };

            rooms.DeliverPlayerSessionReleaseRequest(request.ToBytes(), responsePeer);

            Assert.That(responsePeer.SentCount, Is.Zero);
            Assert.That(receivedPacket, Is.Not.Null);
            Assert.That(receivedPacket.AccountId, Is.EqualTo(request.AccountId));
            Assert.That(receivedPacket.MasterPeerId, Is.EqualTo(request.MasterPeerId));
            Assert.That(completion, Is.Not.Null);

            completion(true, string.Empty);
            completion(false, "late failure");

            IOutgoingMessage response = responsePeer.DequeueNextMessage();
            Assert.That(response.Status, Is.EqualTo(ResponseStatus.Success));
            Assert.That(responsePeer.SentCount, Is.Zero);
            socket.Close();
        }

        [Test]
        public void SpawnersServer_RegisterSpawnedProcess_ObtainsRoomPermissionBeforeSendingRegistration()
        {
            var socket = new FakeClientSocket();
            var spawnersServer = new SpawnersServer(socket);
            SpawnTaskController registeredProcess = null;
            string registrationError = null;

            spawnersServer.RegisterSpawnedProcess(12, "spawn-code", (controller, error) =>
            {
                registeredProcess = controller;
                registrationError = error;
            }, socket);

            Assert.That(socket.Requests.Count(request =>
                request.Message.OpCode == MstOpCodes.RegisterSpawnedProcess), Is.Zero);

            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.RoomServer,
                MstPermissionLevels.RoomServer);

            Assert.That(socket.Requests.Count(request =>
                request.Message.OpCode == MstOpCodes.RegisterSpawnedProcess), Is.EqualTo(1));

            socket.RespondNext(MstOpCodes.RegisterSpawnedProcess, ResponseStatus.Success,
                new MstProperties().ToBytes());

            Assert.That(registrationError, Is.Null);
            Assert.That(registeredProcess, Is.Not.Null);
            socket.Close();
        }

        [Test]
        public void SpawnersServer_RegisterSpawnedProcess_WhenRegistrationSendThrows_CompletesCallbackOnce()
        {
            var socket = new FakeClientSocket
            {
                ThrowOnSendOpcode = MstOpCodes.RegisterSpawnedProcess
            };
            var spawnersServer = new SpawnersServer(socket);
            int callbackCount = 0;
            SpawnTaskController registeredProcess = null;
            string registrationError = null;

            spawnersServer.RegisterSpawnedProcess(12, "spawn-code", (controller, error) =>
            {
                callbackCount++;
                registeredProcess = controller;
                registrationError = error;
            }, socket);

            LogAssert.Expect(
                LogType.Error,
                new Regex("Spawned process registration request failed"));
            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.RoomServer,
                MstPermissionLevels.RoomServer);

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(registeredProcess, Is.Null);
            Assert.That(registrationError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
            socket.Close();
        }

        [Test]
        public void SpawnerController_CustomConnection_OwnsHandlersAndProcessNotifications()
        {
            var defaultSocket = new FakeClientSocket();
            var customSocket = new FakeClientSocket();
            var spawners = new SpawnersServer(defaultSocket);
            ISpawnerController registeredController = null;
            spawners.RegisterSpawner(new SpawnerOptions(), (controller, error) =>
            {
                Assert.That(error, Is.Null);
                registeredController = controller;
            }, customSocket);
            MstTestData.CompletePermissionHandshake(customSocket, MstPermissionKeys.Spawner,
                MstPermissionLevels.Spawner);
            customSocket.RespondNext(MstOpCodes.RegisterSpawner, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(77));

            Assert.That(registeredController, Is.Not.Null);
            Assert.That(defaultSocket.RegisteredHandlerCount, Is.EqualTo(2));
            Assert.That(customSocket.RegisteredHandlerCount, Is.EqualTo(2));

            ((SpawnerController)registeredController).NotifyProcessKilled(9);

            Assert.That(defaultSocket.SentMessages, Is.Empty);
            Assert.That(customSocket.SentMessages.Select(message => message.OpCode),
                Is.EqualTo(new[] { MstOpCodes.ProcessKilled }));

            registeredController.Dispose();

            Assert.That(customSocket.RegisteredHandlerCount, Is.Zero);
            Assert.That(defaultSocket.RegisteredHandlerCount, Is.EqualTo(2));
            spawners.ClearConnection();
        }

        [Test]
        public void SpawnerControllers_SharingCustomConnection_KeepHandlersUntilLastOwnerDisposes()
        {
            var defaultSocket = new FakeClientSocket();
            var customSocket = new FakeClientSocket();
            var spawners = new SpawnersServer(defaultSocket);
            ISpawnerController firstController = null;
            ISpawnerController secondController = null;

            spawners.RegisterSpawner(new SpawnerOptions(), (controller, error) =>
            {
                Assert.That(error, Is.Null);
                firstController = controller;
            }, customSocket);
            MstTestData.CompletePermissionHandshake(customSocket, MstPermissionKeys.Spawner,
                MstPermissionLevels.Spawner);
            customSocket.RespondNext(MstOpCodes.RegisterSpawner, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(81));

            spawners.RegisterSpawner(new SpawnerOptions(), (controller, error) =>
            {
                Assert.That(error, Is.Null);
                secondController = controller;
            }, customSocket);
            customSocket.RespondNext(MstOpCodes.RegisterSpawner, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(82));

            Assert.That(firstController, Is.Not.Null);
            Assert.That(secondController, Is.Not.Null);
            Assert.That(customSocket.RegisteredHandlerCount, Is.EqualTo(2));

            firstController.Dispose();

            Assert.That(customSocket.RegisteredHandlerCount, Is.EqualTo(2));
            Assert.That(spawners.GetSpawnerController(82), Is.SameAs(secondController));

            secondController.Dispose();

            Assert.That(customSocket.RegisteredHandlerCount, Is.Zero);
            spawners.ClearConnection();
        }

        [Test]
        public void RoomAccess_WhenCapacityRequestsRace_ReservesOneSlot()
        {
            TestPeer owner = CreatePeer();
            TestPeer firstRequester = CreatePeer();
            TestPeer secondRequester = CreatePeer();
            var room = new RegisteredRoom(1, owner, new RoomOptions { MaxPlayers = 1 });
            var errors = new ConcurrentBag<string>();

            RunConcurrently(
                () => room.GetAccess(firstRequester, (_, error) => errors.Add(error)),
                () => room.GetAccess(secondRequester, (_, error) => errors.Add(error)));

            Assert.That(owner.SentCount, Is.EqualTo(1));
            Assert.That(errors.Count(error => error == "Room is already full"), Is.EqualTo(1));

            room.Destroy();
        }

        [Test]
        public void RoomAccess_WhenAckArrivesAfterDestroy_DoesNotRestoreToken()
        {
            TestPeer owner = CreatePeer();
            TestPeer requester = CreatePeer();
            var room = new RegisteredRoom(1, owner, new RoomOptions { MaxPlayers = 1 });
            RoomAccessPacket callbackAccess = null;
            string callbackError = null;

            room.GetAccess(requester, (access, error) =>
            {
                callbackAccess = access;
                callbackError = error;
            });

            Assert.That(owner.SentCount, Is.EqualTo(1));
            room.Destroy();

            var accessPacket = new RoomAccessPacket
            {
                Token = "late-token",
                Ip = string.Empty,
                ExtraParameters = new MstProperties()
            };
            owner.CompleteNext(ResponseStatus.Success, accessPacket.ToBytes());

            Assert.That(callbackAccess, Is.Null);
            Assert.That(callbackError, Is.EqualTo(RegisteredRoom.RoomDestroyedError));
            Assert.That(room.ValidateAccess(accessPacket.Token, out _), Is.False);
        }

        [Test]
        public async Task RoomAccess_WhenCanceled_ReleasesReservedCapacityImmediately()
        {
            TestPeer owner = CreatePeer();
            TestPeer canceledRequester = CreatePeer();
            TestPeer nextRequester = CreatePeer();
            var room = new RegisteredRoom(1, owner, new RoomOptions { MaxPlayers = 1 });
            using var cancellation = new CancellationTokenSource();

            Task canceledRequest = room.GetAccessAsync(canceledRequester, new MstProperties(), null,
                cancellation.Token);

            Assert.That(owner.SentCount, Is.EqualTo(1));
            cancellation.Cancel();
            await AssertTaskCanceledAsync(canceledRequest);

            string nextError = null;
            room.GetAccess(nextRequester, (_, error) => nextError = error);

            Assert.That(owner.SentCount, Is.EqualTo(2));
            Assert.That(nextError, Is.Null);

            room.Destroy();
        }

        [Test]
        public async Task RoomAccess_WhenCancellationRacesReservation_DoesNotLeakCapacity()
        {
            for (int iteration = 0; iteration < 64; iteration++)
            {
                TestPeer owner = CreatePeer();
                TestPeer canceledRequester = CreatePeer();
                TestPeer nextRequester = CreatePeer();
                var room = new RegisteredRoom(iteration, owner, new RoomOptions { MaxPlayers = 1 });
                using var cancellation = new CancellationTokenSource();
                Task canceledRequest = null;

                RunConcurrently(
                    () => canceledRequest = room.GetAccessAsync(canceledRequester, new MstProperties(), null,
                        cancellation.Token),
                    cancellation.Cancel);

                Assert.That(canceledRequest, Is.Not.Null);
                await AssertTaskCanceledAsync(canceledRequest);
                int sentBeforeNextRequest = owner.SentCount;
                string nextError = null;

                room.GetAccess(nextRequester, (_, error) => nextError = error);

                Assert.That(owner.SentCount, Is.EqualTo(sentBeforeNextRequest + 1));
                Assert.That(nextError, Is.Null);
                room.Destroy();
            }
        }

        [Test]
        public void RegisteredRoomDestroy_RemovesRoomFromOwningModule()
        {
            var testObject = new GameObject(nameof(RegisteredRoomDestroy_RemovesRoomFromOwningModule));
            testObjects.Add(testObject);
            var module = testObject.AddComponent<TestRoomsModule>();
            module.ConfigureForTests();
            TestPeer owner = CreatePeer();
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());

            room.Destroy();

            Assert.That(room.IsActive, Is.False);
            Assert.That(module.GetRoomById(room.RoomId), Is.Null);
        }

        [Test]
        public void DestroyedLobby_WhenLateFinalizationRuns_DoesNotRebindRoom()
        {
            var lobby = new TestLobby();
            var task = new SpawnTask(1, null, new MstProperties());
            var finalizationData = new MstProperties();
            finalizationData.Set(MstParamKeys.ROOM_ID, 7);
            Assert.That(task.TryFinalize(new SpawnFinalizationPacket
            {
                FinalizationData = finalizationData
            }), Is.True);
            lobby.SetGameSpawnTask(task);
            lobby.Destroy();

            Assert.DoesNotThrow(lobby.InvokeGameServerFinalized);
            Assert.That(lobby.GameIp, Is.Empty);
            Assert.That(lobby.GamePort, Is.EqualTo(-1));
        }

        [Test]
        public void Lobby_WhenOldTaskAlreadyCapturedStatus_IgnoresItAfterTaskReplacement()
        {
            var lobby = new TestLobby();
            var oldTask = new SpawnTask(1, null, new MstProperties());
            var newTask = new SpawnTask(2, null, new MstProperties());
            using var callbackEntered = new ManualResetEventSlim();
            using var releaseCallback = new ManualResetEventSlim();
            oldTask.OnStatusChangedEvent += status =>
            {
                if (status != SpawnStatus.StartingProcess)
                    return;

                callbackEntered.Set();
                if (!releaseCallback.Wait(TimeSpan.FromSeconds(2)))
                    throw new TimeoutException("Captured lobby callback was not released by the test");
            };
            lobby.SetGameSpawnTask(oldTask);
            Task<bool> transitionTask = Task.Run(() => oldTask.TrySetStatus(SpawnStatus.StartingProcess));

            bool entered = callbackEntered.Wait(1000);

            if (!entered)
                releaseCallback.Set();

            Assert.That(entered, Is.True);
            LobbyState stateAfterReplacement;

            try
            {
                lobby.SetGameSpawnTask(newTask);
                stateAfterReplacement = lobby.State;
            }
            finally
            {
                releaseCallback.Set();
            }

            Assert.That(transitionTask.Wait(1000), Is.True);
            Assert.That(lobby.State, Is.EqualTo(stateAfterReplacement));
            lobby.Destroy();
        }

        [Test]
        public async Task WorldRoomsStop_DestroysRoomsOwnedByBaseRoomsLifecycle()
        {
            var testObject = new GameObject(nameof(WorldRoomsStop_DestroysRoomsOwnedByBaseRoomsLifecycle));
            testObjects.Add(testObject);
            var module = testObject.AddComponent<TestWorldRoomsModule>();
            module.ConfigureForTests();
            TestPeer owner = CreatePeer();
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());

            Assert.That(module.GetRoomById(room.RoomId), Is.SameAs(room));

            await module.StopServerRunAsync();

            Assert.That(room.IsActive, Is.False);
            Assert.That(module.GetRoomById(room.RoomId), Is.Null);
        }

        [Test]
        public async Task WorldRoomsStop_StopsBaseRoomLifecycleBeforePendingZoneWorkCompletes()
        {
            var testObject = new GameObject(nameof(WorldRoomsStop_StopsBaseRoomLifecycleBeforePendingZoneWorkCompletes));
            testObjects.Add(testObject);
            var module = testObject.AddComponent<TestWorldRoomsModule>();
            module.ConfigureForTests();
            TestPeer owner = CreatePeer();
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            var pendingZoneStart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            FieldInfo pendingStartsField = typeof(WorldRoomsModule).GetField(
                "pendingZoneStarts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pendingStartsField, Is.Not.Null);
            var pendingStarts = (HashSet<Task>)pendingStartsField.GetValue(module);
            pendingStarts.Add(pendingZoneStart.Task);

            Task stopTask = module.StopServerRunAsync();

            Assert.That(room.IsActive, Is.False,
                "Base room lifecycle must stop on the owner thread before background zone work is awaited");
            Assert.That(module.GetRoomById(room.RoomId), Is.Null);
            Assert.That(stopTask.IsCompleted, Is.False);

            pendingZoneStart.TrySetResult(true);
            await stopTask;
        }

        private RegisteredSpawner CreateSpawner(int maxProcesses)
        {
            var spawner = new RegisteredSpawner(
                1,
                CreatePeer(),
                new SpawnerOptions { MaxProcesses = maxProcesses },
                Mst.Create.Logger(nameof(MstRoomSpawnerConcurrencyTests)),
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

        private sealed class TestPeer : BasePeer
        {
            private readonly ConcurrentQueue<SentMessage> sentMessages =
                new ConcurrentQueue<SentMessage>();
            private bool isConnected = true;

            public int SentCount => sentMessages.Count;
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

            public void CompleteNext(ResponseStatus status, byte[] data)
            {
                Assert.That(sentMessages.TryDequeue(out SentMessage sentMessage), Is.True);
                CompleteRequest(sentMessage, status, data);
            }

            public void CompletePendingRequests()
            {
                while (sentMessages.TryDequeue(out SentMessage sentMessage))
                {
                    if (sentMessage.AckRequestId.HasValue)
                        CompleteRequest(sentMessage, ResponseStatus.Success, Array.Empty<byte>());
                }
            }

            public IOutgoingMessage DequeueNextMessage()
            {
                Assert.That(sentMessages.TryDequeue(out SentMessage sentMessage), Is.True);
                return sentMessage.Message;
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

            private void CompleteRequest(SentMessage sentMessage, ResponseStatus status, byte[] data)
            {
                Assert.That(sentMessage.AckRequestId.HasValue, Is.True);

                var response = new IncomingMessage(
                    sentMessage.Message.OpCode,
                    0,
                    data ?? Array.Empty<byte>(),
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

        private sealed class TestLobby : BaseLobby
        {
            public TestLobby()
                : base(1, new[] { new LobbyTeam("players") }, null, new LobbyConfig())
            {
            }

            public void InvokeGameServerFinalized()
            {
                OnGameServerFinalized();
            }
        }

        private sealed class TestSpawnerController : SpawnerController
        {
            public TestSpawnerController(IClientSocket connection)
                : base(1, connection, new SpawnerOptions())
            {
            }

            public Thread CreateWorkerForTest()
            {
                return CreateWorkerThread(() => { });
            }
        }

        private sealed class TestRoomsModule : RoomsModule
        {
            public void ConfigureForTests()
            {
                logger ??= Mst.Create.Logger(nameof(MstRoomSpawnerConcurrencyTests));
            }
        }

        private sealed class TestRoomsServer : RoomsServer
        {
            public TestRoomsServer(IClientSocket connection) : base(connection)
            {
            }

            public void DeliverPlayerSessionReleaseRequest(byte[] data, IPeer responsePeer)
            {
                var message = new IncomingMessage(
                    MstOpCodes.ReleaseRoomPlayerSessionRequest,
                    0,
                    data,
                    DeliveryMethod.Reliable,
                    responsePeer)
                {
                    AckResponseId = 1
                };

                handlers[MstOpCodes.ReleaseRoomPlayerSessionRequest].Handle(message);
            }
        }

        private sealed class TestWorldRoomsModule : WorldRoomsModule
        {
            public void ConfigureForTests()
            {
                logger ??= Mst.Create.Logger(nameof(MstRoomSpawnerConcurrencyTests));
            }
        }
    }
}
