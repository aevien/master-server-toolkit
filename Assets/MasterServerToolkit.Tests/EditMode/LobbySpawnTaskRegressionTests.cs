using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class LobbySpawnTaskRegressionTests
    {
        [Test]
        public void SubscribeStatusChanged_WhenTaskAlreadyFinalized_DeliversTerminalStatus()
        {
            var task = new SpawnTask(1, null, new MstProperties());
            Assert.That(task.TryFinalize(new SpawnFinalizationPacket()), Is.True);
            var statuses = new List<SpawnStatus>();

            using (task.SubscribeStatusChanged(statuses.Add))
            {
                Assert.That(statuses, Is.EqualTo(new[] { SpawnStatus.Finalized }));
            }
        }

        [Test]
        public void SubscribeStatusChanged_WhenTaskFinalizesDuringCurrentStatusDelivery_PreservesOrder()
        {
            var task = new SpawnTask(1, null, new MstProperties());
            var statuses = new ConcurrentQueue<SpawnStatus>();
            using var currentStatusEntered = new ManualResetEventSlim();
            using var releaseCurrentStatus = new ManualResetEventSlim();

            Task<IDisposable> subscribeTask = Task.Run(() => task.SubscribeStatusChanged(status =>
            {
                statuses.Enqueue(status);

                if (status == SpawnStatus.None)
                {
                    currentStatusEntered.Set();
                    if (!releaseCurrentStatus.Wait(TimeSpan.FromSeconds(2)))
                        throw new TimeoutException("Current status callback was not released by the test");
                }
            }));

            bool entered = currentStatusEntered.Wait(1000);

            try
            {
                Assert.That(entered, Is.True, "Current status callback did not start");
                Assert.That(task.TryFinalize(new SpawnFinalizationPacket()), Is.True);
            }
            finally
            {
                releaseCurrentStatus.Set();
            }

            Assert.That(subscribeTask.Wait(2000), Is.True, "Status subscription did not complete");

            using (subscribeTask.Result)
            {
                Assert.That(statuses.ToArray(), Is.EqualTo(new[]
                {
                    SpawnStatus.None,
                    SpawnStatus.Finalized
                }));
            }
        }

        [Test]
        public void SubscribeStatusChanged_WhenSameCallbackSubscribedTwice_DisposesExactRegistration()
        {
            var task = new SpawnTask(1, null, new MstProperties());
            int callbackCount = 0;
            Action<SpawnStatus> callback = _ => Interlocked.Increment(ref callbackCount);
            IDisposable firstSubscription = task.SubscribeStatusChanged(callback);
            using IDisposable secondSubscription = task.SubscribeStatusChanged(callback);

            Assert.That(callbackCount, Is.EqualTo(2));
            firstSubscription.Dispose();
            Assert.That(task.TrySetStatus(SpawnStatus.InQueue), Is.True);

            Assert.That(callbackCount, Is.EqualTo(3));
        }

        [Test]
        public void Lobby_WhenOldTaskCallbackWasCaptured_IgnoresItAfterTaskReplacement()
        {
            var lobby = new TestLobby();
            var oldTask = new SpawnTask(1, null, new MstProperties());
            var replacementTask = new SpawnTask(2, null, new MstProperties());
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
            Task<bool> transitionTask = Task.Run(
                () => oldTask.TrySetStatus(SpawnStatus.StartingProcess));
            bool entered = callbackEntered.Wait(1000);

            try
            {
                Assert.That(entered, Is.True, "Old task callback was not captured");
                lobby.SetGameSpawnTask(replacementTask);
                LobbyState stateAfterReplacement = lobby.State;

                releaseCallback.Set();
                Assert.That(transitionTask.Wait(2000), Is.True, "Old task transition did not complete");
                Assert.That(lobby.State, Is.EqualTo(stateAfterReplacement));
            }
            finally
            {
                releaseCallback.Set();
                lobby.Destroy();
            }
        }

        [Test]
        public void StatusText_WhenLobbyStateChanges_StoresCurrentValue()
        {
            var lobby = new TestLobby();

            lobby.SetState(LobbyState.StartingGameServer);
            Assert.That(lobby.StatusText, Is.EqualTo("Starting game server"));

            lobby.SetState(LobbyState.GameInProgress);
            Assert.That(lobby.StatusText, Is.EqualTo("Game in progress"));
        }

        [Test]
        public void StatusText_WhenLobbyReturnsToPreparations_DoesNotReportFailure()
        {
            var lobby = new TestLobby();

            lobby.SetState(LobbyState.FailedToStart);
            Assert.That(lobby.StatusText, Is.EqualTo("Failed to start server"));

            lobby.SetState(LobbyState.Preparations);
            Assert.That(lobby.StatusText, Is.EqualTo("Preparing for game"));
        }

        [Test]
        public void GetMembersSnapshot_WhenLobbyChanges_RemainsDetached()
        {
            var lobby = new TestLobby();
            var member = new LobbyMember("player", null);
            lobby.AddMemberForTest(7, member);

            List<LobbyMember> snapshot = lobby.GetMembersSnapshot();
            lobby.RemoveMemberForTest(7);

            Assert.That(snapshot, Is.EqualTo(new[] { member }));
            Assert.That(lobby.GetMembersSnapshot(), Is.Empty);
        }

        private sealed class TestLobby : BaseLobby
        {
            public TestLobby()
                : base(1, new[] { new LobbyTeam("players") }, null, new LobbyConfig())
            {
            }

            public void SetState(LobbyState state)
            {
                State = state;
            }

            public void AddMemberForTest(int peerId, LobbyMember member)
            {
                membersByPeerIdList.Add(peerId, member);
            }

            public void RemoveMemberForTest(int peerId)
            {
                membersByPeerIdList.Remove(peerId);
            }
        }
    }
}
