using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class MstAllocationConcurrencyTests
    {
        private const int AllocationCount = 8192;

        private GameObject moduleObject;
        private LobbiesModule lobbiesModule;
        private RoomsModule roomsModule;
        private SpawnersModule spawnersModule;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Mst.Create, Is.Not.Null);
            moduleObject = new GameObject(nameof(MstAllocationConcurrencyTests));
            lobbiesModule = moduleObject.AddComponent<LobbiesModule>();
            roomsModule = moduleObject.AddComponent<RoomsModule>();
            spawnersModule = moduleObject.AddComponent<SpawnersModule>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(moduleObject);
        }

        [Test]
        public void RoomIds_WhenAllocatedConcurrently_AreUniqueAndPreserveSequence()
        {
            AssertConcurrentSequence(roomsModule.AllocateRoomId);
        }

        [Test]
        public void LobbyIds_WhenAllocatedConcurrently_AreUniqueAndPreserveSequence()
        {
            AssertConcurrentSequence(lobbiesModule.NextLobbyId);
        }

        [Test]
        public void SpawnerIds_WhenAllocatedConcurrently_AreUniqueAndPreserveSequence()
        {
            AssertConcurrentSequence(spawnersModule.GenerateSpawnerId);
        }

        [Test]
        public void SpawnTaskIds_WhenAllocatedConcurrently_AreUniqueAndPreserveSequence()
        {
            AssertConcurrentSequence(spawnersModule.GenerateSpawnTaskId);
        }

        [Test]
        public void RoomPorts_WhenAllocatedConcurrently_AreUniqueAndStartAtConfiguredPort()
        {
            const int firstPort = 20000;
            var spawnersServer = CreateSpawnersServer(firstPort, 40000);

            int[] ports = AllocateConcurrently(spawnersServer.GetAvailablePort);

            AssertSequence(ports, firstPort);
        }

        [Test]
        public void RedirectPorts_WhenAllocatedConcurrently_AreUniqueAndStartAtConfiguredPort()
        {
            const int firstPort = 40000;
            var spawnersServer = CreateSpawnersServer(20000, firstPort);

            int[] ports = AllocateConcurrently(spawnersServer.GetAvailableRedirectPort);

            AssertSequence(ports, firstPort);
        }

        [Test]
        public void ReleasedPorts_WhenReleasedConcurrently_AreReusedOnceBeforeNewPorts()
        {
            const int firstPort = 20000;
            var spawnersServer = CreateSpawnersServer(firstPort, 40000);
            int[] allocatedPorts = AllocateConcurrently(spawnersServer.GetAvailablePort);

            Parallel.ForEach(allocatedPorts, spawnersServer.ReleasePort);
            int[] reusedPorts = AllocateConcurrently(spawnersServer.GetAvailablePort);

            AssertSameValues(allocatedPorts, reusedPorts);
            Assert.That(spawnersServer.GetAvailablePort(), Is.EqualTo(firstPort + AllocationCount));
        }

        [Test]
        public void ReleasedRedirectPorts_WhenReleasedConcurrently_AreReusedOnceBeforeNewPorts()
        {
            const int firstPort = 40000;
            var spawnersServer = CreateSpawnersServer(20000, firstPort);
            int[] allocatedPorts = AllocateConcurrently(spawnersServer.GetAvailableRedirectPort);

            Parallel.ForEach(allocatedPorts, spawnersServer.ReleaseRedirectPort);
            int[] reusedPorts = AllocateConcurrently(spawnersServer.GetAvailableRedirectPort);

            AssertSameValues(allocatedPorts, reusedPorts);
            Assert.That(spawnersServer.GetAvailableRedirectPort(), Is.EqualTo(firstPort + AllocationCount));
        }

        private static SpawnersServer CreateSpawnersServer(int defaultPort, int defaultRedirectPort)
        {
            return new SpawnersServer(new FakeClientSocket())
            {
                DefaultPort = defaultPort,
                DefaultRedirectPort = defaultRedirectPort
            };
        }

        private static void AssertConcurrentSequence(Func<int> allocate)
        {
            AssertSequence(AllocateConcurrently(allocate), 0);
        }

        private static int[] AllocateConcurrently(Func<int> allocate)
        {
            var values = new int[AllocationCount];
            Parallel.For(0, values.Length, index => values[index] = allocate());
            return values;
        }

        private static void AssertSequence(int[] values, int firstValue)
        {
            int[] orderedValues = values.OrderBy(value => value).ToArray();
            int[] expectedValues = Enumerable.Range(firstValue, values.Length).ToArray();
            CollectionAssert.AreEqual(expectedValues, orderedValues);
        }

        private static void AssertSameValues(int[] expectedValues, int[] actualValues)
        {
            Array.Sort(expectedValues);
            Array.Sort(actualValues);
            CollectionAssert.AreEqual(expectedValues, actualValues);
        }
    }
}
