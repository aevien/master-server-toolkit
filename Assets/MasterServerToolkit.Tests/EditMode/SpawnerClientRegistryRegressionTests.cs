using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    public class SpawnerClientRegistryRegressionTests
    {
        private readonly List<GameObject> testObjects = new List<GameObject>();

        [Test]
        public void SpawnRequestPacket_RoundTrip_PreservesPermissionCredentialsAndRoomOptions()
        {
            const string credentials =
                "{\"default\":\"client-secret\",\"room_server\":\"room-secret\"}";
            var packet = new SpawnRequestPacket
            {
                SpawnerId = 7,
                SpawnTaskId = 11,
                SpawnTaskUniqueCode = "spawn-code"
            };
            packet.Options.Set(Mst.Args.Names.PermissionCredentials, credentials);
            packet.Options.Set(Mst.Args.Names.RoomName, "Test Room");

            SpawnRequestPacket restored =
                SerializablePacket.FromBytes<SpawnRequestPacket>(packet.ToBytes());

            Assert.That(restored.Options.TryGetValue(Mst.Args.Names.PermissionCredentials,
                out string restoredCredentials), Is.True);
            Assert.That(restoredCredentials, Is.EqualTo(credentials));
            Assert.That(restored.Options.TryGetValue(Mst.Args.Names.RoomName,
                out string restoredRoomName), Is.True);
            Assert.That(restoredRoomName, Is.EqualTo("Test Room"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject testObject in testObjects)
            {
                if (testObject != null)
                    UnityEngine.Object.DestroyImmediate(testObject);
            }

            testObjects.Clear();
        }

        [Test]
        public void Controllers_WithSameIdOnDifferentConnections_CoexistAndCleanupExactly()
        {
            var defaultSocket = new FakeClientSocket();
            var firstSocket = new FakeClientSocket();
            var secondSocket = new FakeClientSocket();
            var spawners = new SpawnersServer(defaultSocket);
            ISpawnerController first = RegisterController(spawners, firstSocket, 41);
            ISpawnerController second = RegisterController(spawners, secondSocket, 41);

            Assert.That(spawners.GetCreatedSpawnerControllers().Count(), Is.EqualTo(2));
            Assert.That(spawners.GetSpawnerController(41), Is.Null,
                "A connection-less lookup must not choose an arbitrary controller for an ambiguous id");
            Assert.That(spawners.GetSpawnerController(41, firstSocket), Is.SameAs(first));
            Assert.That(spawners.GetSpawnerController(41, secondSocket), Is.SameAs(second));
            Assert.That(firstSocket.RegisteredHandlerCount, Is.EqualTo(2));
            Assert.That(secondSocket.RegisteredHandlerCount, Is.EqualTo(2));

            first.Dispose();

            Assert.That(spawners.GetSpawnerController(41, firstSocket), Is.Null);
            Assert.That(spawners.GetSpawnerController(41, secondSocket), Is.SameAs(second));
            Assert.That(firstSocket.RegisteredHandlerCount, Is.Zero);
            Assert.That(secondSocket.RegisteredHandlerCount, Is.EqualTo(2));

            second.Dispose();
            spawners.ClearConnection();

            Assert.That(spawners.GetCreatedSpawnerControllers(), Is.Empty);
            Assert.That(secondSocket.RegisteredHandlerCount, Is.Zero);
        }

        [Test]
        public void ConnectionlessLookup_WithDefaultAndCustomMatch_ReturnsNull()
        {
            var defaultSocket = new FakeClientSocket();
            var customSocket = new FakeClientSocket();
            var spawners = new SpawnersServer(defaultSocket);
            ISpawnerController defaultController = RegisterController(spawners, defaultSocket, 42);
            ISpawnerController customController = RegisterController(spawners, customSocket, 42);

            Assert.That(spawners.GetSpawnerController(42), Is.Null);
            Assert.That(spawners.GetSpawnerController(42, defaultSocket), Is.SameAs(defaultController));
            Assert.That(spawners.GetSpawnerController(42, customSocket), Is.SameAs(customController));

            defaultController.Dispose();
            customController.Dispose();
            spawners.ClearConnection();
        }

        [Test]
        public void CustomSocketHandlers_RouteSameSpawnerIdBySourceConnection()
        {
            var defaultSocket = new FakeClientSocket();
            var firstSocket = new FakeClientSocket();
            var secondSocket = new FakeClientSocket();
            var spawners = new SpawnersServer(defaultSocket);
            var first = new DeferredUnregisterController(firstSocket, 45);
            var second = new DeferredUnregisterController(secondSocket, 45);
            MethodInfo register = typeof(SpawnersServer).GetMethod("RegisterSpawnerController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo unregister = typeof(SpawnersServer).GetMethod("UnregisterSpawnerController",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(register, Is.Not.Null);
            Assert.That(unregister, Is.Not.Null);
            register.Invoke(spawners, new object[] { first });
            register.Invoke(spawners, new object[] { second });

            var request = new SpawnRequestPacket
            {
                SpawnerId = 45,
                SpawnTaskId = 1
            };
            firstSocket.Deliver(MstOpCodes.SpawnProcessRequest, request.ToBytes());

            Assert.That(first.SpawnRequestCount, Is.EqualTo(1));
            Assert.That(second.SpawnRequestCount, Is.Zero);

            request.SpawnTaskId = 2;
            secondSocket.Deliver(MstOpCodes.SpawnProcessRequest, request.ToBytes());

            Assert.That(first.SpawnRequestCount, Is.EqualTo(1));
            Assert.That(second.SpawnRequestCount, Is.EqualTo(1));

            unregister.Invoke(spawners, new object[] { first });
            unregister.Invoke(spawners, new object[] { second });
            spawners.ClearConnection();

            Assert.That(firstSocket.RegisteredHandlerCount, Is.Zero);
            Assert.That(secondSocket.RegisteredHandlerCount, Is.Zero);
        }

        [Test]
        public void DuplicateControllerKey_DoesNotRemoveExistingControllerOrItsHandlers()
        {
            var defaultSocket = new FakeClientSocket();
            var customSocket = new FakeClientSocket();
            var spawners = new SpawnersServer(defaultSocket);
            ISpawnerController first = RegisterController(spawners, customSocket, 52);
            ISpawnerController duplicate = null;
            string duplicateError = null;

            spawners.RegisterSpawner(new SpawnerOptions(), (controller, error) =>
            {
                duplicate = controller;
                duplicateError = error;
            }, customSocket);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Failed to register local spawner controller.*already registered"));
            customSocket.RespondNext(MstOpCodes.RegisterSpawner, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(52));

            Assert.That(duplicate, Is.Null);
            Assert.That(duplicateError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
            Assert.That(spawners.GetSpawnerController(52, customSocket), Is.SameAs(first));
            Assert.That(customSocket.RegisteredHandlerCount, Is.EqualTo(2));

            first.Dispose();
            spawners.ClearConnection();

            Assert.That(customSocket.RegisteredHandlerCount, Is.Zero);
        }

        [Test]
        public void RequestUnregister_ReportsMasterSuccessAndFailureSeparately()
        {
            var socket = new FakeClientSocket();
            var spawners = new SpawnersServer(socket);
            ISpawnerController controller = RegisterController(spawners, socket, 63);
            bool? succeeded = null;
            string callbackError = null;

            controller.RequestUnregister((isSuccessful, error) =>
            {
                succeeded = isSuccessful;
                callbackError = error;
            });
            socket.RespondNext(MstOpCodes.UnregisterSpawner, ResponseStatus.Forbidden,
                Encoding.UTF8.GetBytes("not owner"));

            Assert.That(succeeded, Is.False);
            Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.forbidden.message")));

            succeeded = null;
            callbackError = "not cleared";
            controller.RequestUnregister((isSuccessful, error) =>
            {
                succeeded = isSuccessful;
                callbackError = error;
            });
            socket.RespondNext(MstOpCodes.UnregisterSpawner, ResponseStatus.Success);

            Assert.That(succeeded, Is.True);
            Assert.That(callbackError, Is.Null);

            controller.Dispose();
            spawners.ClearConnection();
        }

        [Test]
        public void RequestUnregister_WhenSendThrows_ReportsFailureSynchronously()
        {
            var socket = new FakeClientSocket();
            var spawners = new SpawnersServer(socket);
            ISpawnerController controller = RegisterController(spawners, socket, 64);
            bool? succeeded = null;
            string callbackError = null;
            socket.ThrowOnSendOpcode = MstOpCodes.UnregisterSpawner;

            LogAssert.Expect(
                LogType.Error,
                new Regex("Spawner unregister request failed"));
            controller.RequestUnregister((isSuccessful, error) =>
            {
                succeeded = isSuccessful;
                callbackError = error;
            });

            Assert.That(succeeded, Is.False);
            Assert.That(callbackError, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));

            controller.Dispose();
            spawners.ClearConnection();
        }

        [Test]
        public void StopSpawner_LateUnregisterCallback_DoesNotClearReplacementController()
        {
            var testObject = new GameObject(nameof(StopSpawner_LateUnregisterCallback_DoesNotClearReplacementController));
            testObjects.Add(testObject);
            var behaviour = testObject.AddComponent<TestSpawnerBehaviour>();
            behaviour.ConfigureForTests();
            var first = new DeferredUnregisterController(new FakeClientSocket(), 71);
            var replacement = new DeferredUnregisterController(new FakeClientSocket(), 72);

            behaviour.Attach(first);
            behaviour.StopSpawner();
            behaviour.Attach(replacement);
            first.CompleteUnregister(true, null);

            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(replacement.DisposeCount, Is.Zero);
            Assert.That(behaviour.CurrentController, Is.SameAs(replacement));
            Assert.That(behaviour.IsSpawnerRegistered, Is.True);

            replacement.Dispose();
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public void StopSpawner_WhenDisconnectedOrSendThrows_DisposesDeterministically()
        {
            var testObject = new GameObject(nameof(StopSpawner_WhenDisconnectedOrSendThrows_DisposesDeterministically));
            testObjects.Add(testObject);
            var behaviour = testObject.AddComponent<TestSpawnerBehaviour>();
            behaviour.ConfigureForTests();
            var disconnected = new DeferredUnregisterController(new FakeClientSocket(false), 81);

            behaviour.Attach(disconnected);
            behaviour.StopSpawner();

            Assert.That(disconnected.UnregisterRequestCount, Is.Zero);
            Assert.That(disconnected.DisposeCount, Is.EqualTo(1));

            var throwing = new DeferredUnregisterController(new FakeClientSocket(), 82)
            {
                ThrowOnUnregister = true
            };
            LogAssert.Expect(LogType.Error, new Regex("Failed to request unregister for spawner \\[82\\]"));
            LogAssert.Expect(LogType.Error, new Regex("send failed"));
            behaviour.Attach(throwing);
            behaviour.StopSpawner();

            Assert.That(throwing.UnregisterRequestCount, Is.EqualTo(1));
            Assert.That(throwing.DisposeCount, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(testObject);
        }

        private static ISpawnerController RegisterController(SpawnersServer spawners, FakeClientSocket socket,
            int spawnerId)
        {
            ISpawnerController controller = null;
            string registrationError = null;

            spawners.RegisterSpawner(new SpawnerOptions(), (registeredController, error) =>
            {
                controller = registeredController;
                registrationError = error;
            }, socket);
            MstTestData.CompletePermissionHandshake(socket, MstPermissionKeys.Spawner,
                MstPermissionLevels.Spawner);
            socket.RespondNext(MstOpCodes.RegisterSpawner, ResponseStatus.Success,
                EndianBitConverter.Big.GetBytes(spawnerId));

            Assert.That(registrationError, Is.Null);
            Assert.That(controller, Is.Not.Null);
            return controller;
        }

        private sealed class TestSpawnerBehaviour : SpawnerBehaviour
        {
            public ISpawnerController CurrentController => spawnerController;

            protected override void Awake()
            {
                logger = Mst.Create.Logger(nameof(TestSpawnerBehaviour));
                killProcessesWhenStop = false;
            }

            protected override void OnDestroy()
            {
            }

            public void ConfigureForTests()
            {
                logger ??= Mst.Create.Logger(nameof(TestSpawnerBehaviour));
                killProcessesWhenStop = false;
            }

            public void Attach(ISpawnerController controller)
            {
                spawnerController = controller;
                IsSpawnerStarted = true;
            }
        }

        private sealed class DeferredUnregisterController : ISpawnerController
        {
            private SuccessCallback unregisterCallback;

            public DeferredUnregisterController(IClientSocket connection, int spawnerId)
            {
                Connection = connection;
                SpawnerId = spawnerId;
                Logger = Mst.Create.Logger(nameof(DeferredUnregisterController));
                SpawnSettings = new SpawnerConfig();
            }

            public event Action OnProcessStartedEvent;
            public event Action OnProcessKilledEvent;
            public SpawnerConfig SpawnSettings { get; }
            public MasterServerToolkit.Logging.Logger Logger { get; }
            public IClientSocket Connection { get; }
            public int SpawnerId { get; }
            public bool ThrowOnUnregister { get; set; }
            public int UnregisterRequestCount { get; private set; }
            public int DisposeCount { get; private set; }
            public int SpawnRequestCount { get; private set; }

            public void RequestUnregister(SuccessCallback callback = null)
            {
                UnregisterRequestCount++;

                if (ThrowOnUnregister)
                    throw new InvalidOperationException("send failed");

                unregisterCallback = callback;
            }

            public void CompleteUnregister(bool isSuccessful, string error)
            {
                SuccessCallback callback = unregisterCallback;
                unregisterCallback = null;
                callback?.Invoke(isSuccessful, error);
            }

            public void SpawnRequestHandler(SpawnRequestPacket data, SuccessCallback callback)
            {
                SpawnRequestCount++;
            }

            public ResponseStatus KillRequestHandler(int spawnId)
            {
                return ResponseStatus.NotFound;
            }

            public void KillProcesses()
            {
            }

            public int ProcessesCount()
            {
                return 0;
            }

            public void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
