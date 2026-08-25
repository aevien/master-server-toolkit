using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class RoomsLifecycleRegressionTests
    {
        private readonly List<TestPeer> peers = new List<TestPeer>();
        private readonly List<GameObject> testObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (TestPeer peer in peers)
                peer.Dispose();

            peers.Clear();

            foreach (GameObject testObject in testObjects)
                UnityEngine.Object.DestroyImmediate(testObject);

            testObjects.Clear();
        }

        [Test]
        public void RoomOptionsClone_IsIndependentAndNullSafeForSerialization()
        {
            var original = new RoomOptions();
            original.ExtraParameters.Set("mode", "original");

            RoomOptions clone = original.Clone();
            clone.Name = "clone";
            clone.ExtraParameters.Set("mode", "changed");

            Assert.That(original.Name, Is.EqualTo("Unnamed"));
            Assert.That(original.ExtraParameters.AsString("mode"), Is.EqualTo("original"));

            original.Name = null;
            original.RoomIp = null;
            original.Password = null;
            original.Region = null;
            original.ExtraParameters = null;

            byte[] data = null;
            Assert.DoesNotThrow(() => data = original.ToBytes());

            RoomOptions restored = SerializablePacket.FromBytes<RoomOptions>(data);
            Assert.That(restored.Name, Is.Empty);
            Assert.That(restored.RoomIp, Is.Empty);
            Assert.That(restored.Password, Is.Empty);
            Assert.That(restored.Region, Is.Empty);
            Assert.That(restored.ExtraParameters, Is.Not.Null);
            Assert.That(restored.ExtraParameters.Count, Is.Zero);
        }

        [Test]
        public void RegisteredRoom_StoresAndReturnsIndependentOptionsCopies()
        {
            TestPeer owner = CreatePeer();
            var initial = new RoomOptions { Name = "initial" };
            initial.ExtraParameters.Set("version", "one");
            var room = new RegisteredRoom(1, owner, initial);

            initial.Name = "mutated-input";
            initial.ExtraParameters.Set("version", "mutated-input");
            RoomOptions firstSnapshot = room.GetOptionsSnapshot();

            Assert.That(firstSnapshot.Name, Is.EqualTo("initial"));
            Assert.That(firstSnapshot.ExtraParameters.AsString("version"), Is.EqualTo("one"));

            firstSnapshot.Name = "mutated-output";
            firstSnapshot.ExtraParameters.Set("version", "mutated-output");

            Assert.That(room.GetOptionsSnapshot().Name, Is.EqualTo("initial"));
            Assert.That(room.GetOptionsSnapshot().ExtraParameters.AsString("version"), Is.EqualTo("one"));

            var replacement = new RoomOptions { Name = "replacement" };
            replacement.ExtraParameters.Set("version", "two");
            room.ChangeOptions(replacement);
            replacement.Name = "mutated-replacement";
            replacement.ExtraParameters.Set("version", "mutated-replacement");

            Assert.That(room.TryGetSnapshot(out RoomOptions replacementSnapshot, out _, out _), Is.True);
            Assert.That(replacementSnapshot.Name, Is.EqualTo("replacement"));
            Assert.That(replacementSnapshot.ExtraParameters.AsString("version"), Is.EqualTo("two"));

            replacementSnapshot.Name = "mutated-snapshot";
            replacementSnapshot.ExtraParameters.Set("version", "mutated-snapshot");
            Assert.That(room.TryGetSnapshot(out RoomOptions secondSnapshot, out _, out _), Is.True);
            Assert.That(secondSnapshot.Name, Is.EqualTo("replacement"));
            Assert.That(secondSnapshot.ExtraParameters.AsString("version"), Is.EqualTo("two"));

            room.Destroy();
        }

        [Test]
        public async Task RoomAccess_LateAckFromCanceledRequest_DoesNotReleaseNewSamePeerReservation()
        {
            TestPeer owner = CreatePeer();
            TestPeer requester = CreatePeer();
            var room = new RegisteredRoom(1, owner, new RoomOptions { MaxPlayers = 1 });
            using var cancellation = new CancellationTokenSource();

            Task canceledRequest = room.GetAccessAsync(requester, new MstProperties(), null,
                cancellation.Token);
            cancellation.Cancel();
            await AssertTaskCanceledAsync(canceledRequest);

            RoomAccessPacket currentAccess = null;
            string currentError = null;
            room.GetAccess(requester, (access, error) =>
            {
                currentAccess = access;
                currentError = error;
            });

            owner.CompleteNext(ResponseStatus.Success, CreateAccessPacket("stale-token").ToBytes());

            string duplicateError = null;
            room.GetAccess(requester, (_, error) => duplicateError = error);

            Assert.That(duplicateError, Is.EqualTo("You've already requested an access to this room"));
            Assert.That(owner.PendingMessageCount, Is.EqualTo(1));

            owner.CompleteNext(ResponseStatus.Success, CreateAccessPacket("current-token").ToBytes());

            Assert.That(currentError, Is.Null);
            Assert.That(currentAccess?.Token, Is.EqualTo("current-token"));
            room.Destroy();
        }

        [Test]
        public void RoomLifecycle_ReentrantDestroy_PublishesCompleteRegistrationBeforeDestruction()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            var steps = new List<string>();
            module.Steps = steps;
            module.OnRoomRegisteredEvent += room =>
            {
                steps.Add("registered-event");
                room.Destroy();
            };
            module.OnRoomDestroyedEvent += _ => steps.Add("destroyed-event");

            RegisteredRoom registeredRoom = module.RegisterRoom(owner, new RoomOptions());

            Assert.That(registeredRoom, Is.Not.Null);
            Assert.That(registeredRoom.IsActive, Is.False);
            Assert.That(steps, Is.EqualTo(new[]
            {
                "registered-event",
                "registered-hook",
                "destroyed-event",
                "destroyed-hook"
            }));
        }

        [Test]
        public void RoomDestroy_ResetsOnlyMatchingJoinedRoomBeforeCallbacks()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer matchingPlayer = CreatePeer();
            TestPeer movedPlayer = CreatePeer();
            TestUserPeerExtension matchingUser = AddUser(matchingPlayer);
            TestUserPeerExtension movedUser = AddUser(movedPlayer);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());

            GrantAccess(room, owner, matchingPlayer, "matching-token");
            GrantAccess(room, owner, movedPlayer, "moved-token");
            movedUser.JoinedRoomID = room.RoomId + 100;

            int matchingIdAtCallback = int.MinValue;
            int movedIdAtCallback = int.MinValue;
            module.OnRoomDestroyedEvent += _ =>
            {
                matchingIdAtCallback = matchingUser.JoinedRoomID;
                movedIdAtCallback = movedUser.JoinedRoomID;
            };

            room.Destroy();

            Assert.That(matchingIdAtCallback, Is.EqualTo(-1));
            Assert.That(movedIdAtCallback, Is.EqualTo(room.RoomId + 100));
            Assert.That(matchingUser.JoinedRoomID, Is.EqualTo(-1));
            Assert.That(movedUser.JoinedRoomID, Is.EqualTo(room.RoomId + 100));
        }

        [Test]
        public void RemovePlayer_DoesNotClearNewerRoomAssignment()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer player = CreatePeer();
            TestUserPeerExtension user = AddUser(player);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());

            GrantAccess(room, owner, player, "room-token");
            int newerRoomId = room.RoomId + 100;
            user.JoinedRoomID = newerRoomId;

            room.RemovePlayer(player.Id);

            Assert.That(user.JoinedRoomID, Is.EqualTo(newerRoomId));
            Assert.That(room.GetPlayersSnapshot().ContainsKey(player.Id), Is.False);
        }

        [Test]
        public void GetPlayersSnapshot_WhenRoomChanges_RemainsDetached()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer player = CreatePeer();
            AddUser(player);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            GrantAccess(room, owner, player, "snapshot-token");

            IReadOnlyDictionary<int, IPeer> snapshot = room.GetPlayersSnapshot();
            room.RemovePlayer(player.Id);

            Assert.That(snapshot.ContainsKey(player.Id), Is.True);
            Assert.That(room.GetPlayersSnapshot().ContainsKey(player.Id), Is.False);
        }

        [Test]
        public void NotifyAccountBlocked_WhenUserJoinedRoom_SendsAccountIdToRoomOwner()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer player = CreatePeer();
            TestUserPeerExtension user = AddUser(player);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            GrantAccess(room, owner, player, "block-test-token");

            bool sent = module.TryNotifyAccountBlocked(user);
            IOutgoingMessage notification = owner.DequeueNextMessage();

            Assert.That(sent, Is.True);
            Assert.That(notification.OpCode, Is.EqualTo(MstOpCodes.AccountBlocked));
            Assert.That(Encoding.UTF8.GetString(notification.Data), Is.EqualTo(user.UserId));
        }

        [Test]
        public async Task ReleasePlayerSessionAsync_WhenRoomIsMissing_ClearsOnlyRequestedMembership()
        {
            TestRoomsModule module = CreateModule();
            TestUserPeerExtension user = AddUser(CreatePeer());
            TestUserPeerExtension unrelatedUser = AddUser(CreatePeer());
            user.JoinedRoomID = 404;
            unrelatedUser.JoinedRoomID = 405;

            bool released = await module.ReleasePlayerSessionAsync(user);

            Assert.That(released, Is.True);
            Assert.That(user.JoinedRoomID, Is.EqualTo(-1));
            Assert.That(unrelatedUser.JoinedRoomID, Is.EqualTo(405));
        }

        [Test]
        public async Task ReleasePlayerSessionAsync_WhenRoomIsInactive_ClearsOnlyRequestedMembership()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestUserPeerExtension user = AddUser(CreatePeer());
            TestUserPeerExtension unrelatedUser = AddUser(CreatePeer());
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            int roomId = room.RoomId;

            Assert.That(room.TryDestroy(out _), Is.True);
            user.JoinedRoomID = roomId;
            unrelatedUser.JoinedRoomID = roomId + 1;

            bool released = await module.ReleasePlayerSessionAsync(user);

            Assert.That(released, Is.True);
            Assert.That(user.JoinedRoomID, Is.EqualTo(-1));
            Assert.That(unrelatedUser.JoinedRoomID, Is.EqualTo(roomId + 1));
            module.DestroyRoom(room);
        }

        [Test]
        public async Task ReleasePlayerSessionAsync_ActiveRoom_SendsExactSessionAndRequiresMembershipRemoval()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer player = CreatePeer();
            TestUserPeerExtension user = AddUser(player);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            GrantAccess(room, owner, player, "release-session-token");

            Task<bool> releaseTask = module.ReleasePlayerSessionAsync(user);
            IOutgoingMessage request = owner.PeekNextMessage();
            RoomPlayerSessionPacket packet =
                SerializablePacket.FromBytes<RoomPlayerSessionPacket>(request.Data);

            Assert.That(request.OpCode, Is.EqualTo(MstOpCodes.ReleaseRoomPlayerSessionRequest));
            Assert.That(packet.AccountId, Is.EqualTo(user.UserId));
            Assert.That(packet.MasterPeerId, Is.EqualTo(player.Id));

            owner.CompleteNext(ResponseStatus.Success, Array.Empty<byte>());
            bool released = await releaseTask;

            Assert.That(released, Is.False,
                "A room ACK is not sufficient until NotifyPlayerLeft has removed the membership");
            Assert.That(user.JoinedRoomID, Is.EqualTo(room.RoomId));
        }

        [Test]
        public async Task ReleasePlayerSessionAsync_WhenNotifyPlayerLeftRemovedMembership_AcceptsSuccessAck()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer player = CreatePeer();
            TestUserPeerExtension user = AddUser(player);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            GrantAccess(room, owner, player, "confirmed-release-token");

            Task<bool> releaseTask = module.ReleasePlayerSessionAsync(user);
            room.RemovePlayer(player.Id);
            owner.CompleteNext(ResponseStatus.Success, Array.Empty<byte>());

            Assert.That(await releaseTask, Is.True);
            Assert.That(user.JoinedRoomID, Is.EqualTo(-1));
        }

        [Test]
        public async Task ReleasePlayerSessionAsync_WhenRoomReturnsFailure_PreservesMembership()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            TestPeer player = CreatePeer();
            TestUserPeerExtension user = AddUser(player);
            RegisteredRoom room = module.RegisterRoom(owner, new RoomOptions());
            GrantAccess(room, owner, player, "failed-release-token");

            Task<bool> releaseTask = module.ReleasePlayerSessionAsync(user);
            owner.CompleteNext(ResponseStatus.ServiceUnavailable, Array.Empty<byte>());

            Assert.That(await releaseTask, Is.False);
            Assert.That(user.JoinedRoomID, Is.EqualTo(room.RoomId));
            Assert.That(room.GetPlayersSnapshot().ContainsKey(player.Id), Is.True);
        }

        [Test]
        public async Task StopServerRunAsync_RejectsRegistrationAttemptedDuringDestruction()
        {
            TestRoomsModule module = CreateModule();
            TestPeer initialOwner = CreatePeer();
            TestPeer lateOwner = CreatePeer();
            int registeredCount = 0;
            RegisteredRoom lateRoom = null;
            module.OnRoomRegisteredEvent += _ => registeredCount++;
            RegisteredRoom initialRoom = module.RegisterRoom(initialOwner, new RoomOptions());
            module.OnRoomDestroyedEvent += _ =>
                lateRoom = module.RegisterRoom(lateOwner, new RoomOptions { Name = "late" });

            await module.StopServerRunAsync();

            Assert.That(initialRoom.IsActive, Is.False);
            Assert.That(lateRoom, Is.Null);
            Assert.That(registeredCount, Is.EqualTo(1));
            Assert.That(module.GetAllRooms(), Is.Empty);
            Assert.That(module.RegisterRoom(lateOwner, new RoomOptions()), Is.Null);
        }

        [Test]
        public void GetPublicGames_UsesSnapshotAwarePublicPropertiesOverride()
        {
            TestRoomsModule module = CreateModule();
            TestPeer owner = CreatePeer();
            var options = new RoomOptions
            {
                IsPublic = true,
                Name = "snapshot-room",
                RoomIp = "127.0.0.1",
                RoomPort = 25000
            };
            options.ExtraParameters.Set("mode", "survival");
            module.RegisterRoom(owner, options);

            GameInfoPacket game = module.GetPublicGames(null, new MstProperties()).Single();

            Assert.That(game.Address, Is.EqualTo("127.0.0.1:25000"));
            Assert.That(game.Properties.AsString("mode"), Is.EqualTo("survival"));
            Assert.That(module.SnapshotPublicOptionsCalls, Is.EqualTo(1));
            Assert.That(module.LegacyPublicOptionsCalls, Is.Zero);
        }

        private static RoomAccessPacket CreateAccessPacket(string token)
        {
            return new RoomAccessPacket
            {
                Token = token,
                Ip = string.Empty,
                SceneName = string.Empty,
                ExtraParameters = new MstProperties()
            };
        }

        private static void GrantAccess(RegisteredRoom room, TestPeer owner, TestPeer player, string token)
        {
            RoomAccessPacket access = null;
            string error = null;
            room.GetAccess(player, (packet, callbackError) =>
            {
                access = packet;
                error = callbackError;
            });
            owner.CompleteNext(ResponseStatus.Success, CreateAccessPacket(token).ToBytes());

            Assert.That(error, Is.Null);
            Assert.That(access?.Token, Is.EqualTo(token));
            Assert.That(room.ValidateAccess(token, out IPeer validatedPeer), Is.True);
            Assert.That(validatedPeer, Is.SameAs(player));
        }

        private TestRoomsModule CreateModule()
        {
            var testObject = new GameObject(nameof(RoomsLifecycleRegressionTests));
            testObjects.Add(testObject);
            var module = testObject.AddComponent<TestRoomsModule>();
            module.ConfigureForTests();
            return module;
        }

        private TestPeer CreatePeer()
        {
            var peer = new TestPeer();
            peers.Add(peer);
            return peer;
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

        private static TestUserPeerExtension AddUser(TestPeer peer)
        {
            var user = new TestUserPeerExtension(peer);
            peer.AddExtension<IUserPeerExtension>(user);
            return user;
        }

        public sealed class TestRoomsModule : RoomsModule
        {
            public List<string> Steps { get; set; }
            public int LegacyPublicOptionsCalls { get; private set; }
            public int SnapshotPublicOptionsCalls { get; private set; }

            public void ConfigureForTests()
            {
                logger ??= Mst.Create.Logger(nameof(RoomsLifecycleRegressionTests));
            }

            protected override void OnRoomRegistered(RegisteredRoom room)
            {
                Steps?.Add("registered-hook");
            }

            protected override void OnRoomDestroyed(RegisteredRoom room)
            {
                Steps?.Add("destroyed-hook");
            }

            public override MstProperties GetPublicRoomOptions(IPeer player, RegisteredRoom room,
                MstProperties playerFilters)
            {
                LegacyPublicOptionsCalls++;
                return base.GetPublicRoomOptions(player, room, playerFilters);
            }

            public override MstProperties GetPublicRoomOptions(IPeer player, RegisteredRoom room,
                MstProperties playerFilters, RoomOptions optionsSnapshot)
            {
                SnapshotPublicOptionsCalls++;
                return base.GetPublicRoomOptions(player, room, playerFilters, optionsSnapshot);
            }
        }

        private sealed class TestUserPeerExtension : IUserPeerExtension
        {
            public TestUserPeerExtension(IPeer peer)
            {
                Peer = peer;
            }

            public IPeer Peer { get; }
            public string UserId => "test-user";
            public string Username => UserId;
            public IAccountInfoData Account { get; set; }
            public int JoinedRoomID { get; set; } = -1;

            public AccountInfoPacket CreateAccountInfoPacket()
            {
                return null;
            }

            public bool HasJoinedRoom()
            {
                return JoinedRoomID >= 0;
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private readonly ConcurrentQueue<SentMessage> sentMessages =
                new ConcurrentQueue<SentMessage>();
            private bool isConnected = true;

            public int PendingMessageCount => sentMessages.Count;
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

            public IOutgoingMessage DequeueNextMessage()
            {
                Assert.That(sentMessages.TryDequeue(out SentMessage sentMessage), Is.True);
                return sentMessage.Message;
            }

            public IOutgoingMessage PeekNextMessage()
            {
                Assert.That(sentMessages.TryPeek(out SentMessage sentMessage), Is.True);
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
