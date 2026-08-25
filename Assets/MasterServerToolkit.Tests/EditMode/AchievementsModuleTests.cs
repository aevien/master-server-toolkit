using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections;
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
    public class AchievementsModuleTests
    {
        private const string AchievementKey = "survive_test";
        private const string UserId = "achievement-user";
        private const int RequiredProgress = 5;

        private readonly List<TestPeer> peers = new List<TestPeer>();
        private readonly List<ObservableServerProfile> profiles = new List<ObservableServerProfile>();
        private GameObject testObject;
        private TestAuthModule authModule;
        private TestRoomsModule roomsModule;
        private TestAchievementsModule achievementsModule;
        private TestAchievementsDatabase achievementsDatabase;
        private AchievementData achievementData;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(AchievementsModuleTests));
            authModule = testObject.AddComponent<TestAuthModule>();
            roomsModule = testObject.AddComponent<TestRoomsModule>();
            achievementsModule = testObject.AddComponent<TestAchievementsModule>();
            achievementsModule.InitializeForTests();
            achievementsDatabase = ScriptableObject.CreateInstance<TestAchievementsDatabase>();
            achievementData = ScriptableObject.CreateInstance<AchievementData>();
            achievementData.key = AchievementKey;
            achievementData.requiredProgress = RequiredProgress;
            achievementData.resultCommands = Array.Empty<AchievementData.AchievementExtraData>();
            achievementsDatabase.SetItems(achievementData);
            achievementsModule.Configure(authModule, roomsModule, achievementsDatabase);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ObservableServerProfile profile in profiles)
                profile.Dispose();

            foreach (TestPeer peer in peers)
                peer.Dispose();

            UnityEngine.Object.DestroyImmediate(testObject);
            UnityEngine.Object.DestroyImmediate(achievementsDatabase);
            UnityEngine.Object.DestroyImmediate(achievementData);
        }

        [Test]
        public void TryToUnlock_PublishesDistinctOldAndNewValues()
        {
            ObservableAchievements property = CreateAchievementsProperty();
            AchievementProgressInfo oldValue = null;
            AchievementProgressInfo newValue = null;
            property.OnSetEvent += (oldItem, newItem) =>
            {
                oldValue = oldItem;
                newValue = newItem;
            };

            bool unlocked = property.TryToUnlock(AchievementKey, RequiredProgress);

            Assert.That(unlocked, Is.True);
            Assert.That(oldValue, Is.Not.Null);
            Assert.That(newValue, Is.Not.Null);
            Assert.That(newValue, Is.Not.SameAs(oldValue));
            Assert.That(oldValue.IsUnlocked, Is.False);
            Assert.That(newValue.IsUnlocked, Is.True);
            Assert.That(newValue.unlockedAt, Is.GreaterThan(0));
            Assert.That(newValue.rewardApplied, Is.False);
        }

        [Test]
        public void PendingReward_RemainsPendingAfterJsonRoundTrip()
        {
            var source = new AchievementProgressInfo(achievementData)
            {
                progress = RequiredProgress,
                unlockedAt = 1785214830123,
                rewardApplied = false
            };
            var restored = new AchievementProgressInfo();

            restored.FromJson(source.ToJson());

            Assert.That(restored.IsUnlocked, Is.True);
            Assert.That(restored.unlockedAt, Is.EqualTo(source.unlockedAt));
            Assert.That(restored.rewardApplied, Is.False);
            Assert.That(restored.IsRewardApplied, Is.False);
        }

        [Test]
        public void AchievementWithRequiredProgressAndZeroUnlockTime_RemainsLocked()
        {
            var achievement = new AchievementProgressInfo(achievementData)
            {
                progress = RequiredProgress,
                unlockedAt = 0,
                rewardApplied = false
            };

            Assert.That(achievement.IsUnlocked, Is.False);
            Assert.That(achievement.IsRewardApplied, Is.False);
        }

        [TestCase("2026-07-28T10:20:30.0000000Z")]
        [TestCase("28.07.2026 10:20:30")]
        [TestCase("7/28/2026 10:20:30 AM")]
        public void LegacyUnlockedDate_LoadsAsAppliedUnixState(string legacyUnlockTime)
        {
            MstJson json = CreateLegacyAchievementJson(RequiredProgress, legacyUnlockTime);
            var restored = new AchievementProgressInfo();
            long expectedUnlockTime = new DateTimeOffset(
                2026, 7, 28, 10, 20, 30, TimeSpan.Zero).ToUnixTimeMilliseconds();

            restored.FromJson(json);

            Assert.That(restored.unlockedAt, Is.EqualTo(expectedUnlockTime));
            Assert.That(restored.IsUnlocked, Is.True);
            Assert.That(restored.rewardApplied, Is.True);
            Assert.That(restored.IsRewardApplied, Is.True);
        }

        [Test]
        public void LegacyPendingDate_LoadsAsUnlockedPendingState()
        {
            MstJson json = CreateLegacyAchievementJson(RequiredProgress, "31.12.9999 23:59:59");
            var restored = new AchievementProgressInfo();

            restored.FromJson(json);

            Assert.That(restored.unlockedAt, Is.GreaterThan(0));
            Assert.That(restored.rewardApplied, Is.False);
            Assert.That(restored.IsRewardApplied, Is.False);
        }

        [Test]
        public void LegacyLockedDate_LoadsWithZeroUnlockTime()
        {
            MstJson json = CreateLegacyAchievementJson(0, "31.12.9999 23:59:59");
            var restored = new AchievementProgressInfo();

            restored.FromJson(json);

            Assert.That(restored.IsUnlocked, Is.False);
            Assert.That(restored.unlockedAt, Is.Zero);
            Assert.That(restored.rewardApplied, Is.False);
        }

        [Test]
        public void AchievementProgress_BinaryRoundTripPreservesRewardState()
        {
            var source = new AchievementProgressInfo(achievementData)
            {
                progress = RequiredProgress,
                unlockedAt = 1785214830123,
                rewardApplied = true
            };

            AchievementProgressInfo restored =
                SerializablePacket.FromBytes<AchievementProgressInfo>(source.ToBytes());

            Assert.That(restored.key, Is.EqualTo(source.key));
            Assert.That(restored.progress, Is.EqualTo(source.progress));
            Assert.That(restored.required, Is.EqualTo(source.required));
            Assert.That(restored.unlockedAt, Is.EqualTo(source.unlockedAt));
            Assert.That(restored.rewardApplied, Is.True);
            Assert.That(restored.IsRewardApplied, Is.True);
        }

        [Test]
        public void ObservableAchievements_BinaryRoundTripPreservesUnlockState()
        {
            ObservableAchievements source = CreateAchievementsProperty();
            source.TrySetProgress(AchievementKey, RequiredProgress);
            source.MarkRewardApplied(AchievementKey);
            var restored = new ObservableAchievements(ProfilePropertyOpCodes.achievements);

            restored.FromBytes(source.ToBytes());

            AchievementProgressInfo restoredAchievement = restored.Get(AchievementKey);
            Assert.That(restoredAchievement, Is.Not.Null);
            Assert.That(restoredAchievement.unlockedAt,
                Is.EqualTo(source.Get(AchievementKey).unlockedAt));
            Assert.That(restoredAchievement.IsRewardApplied, Is.True);
        }

        [Test]
        public void ProfileTransition_ReplayedUnlockExecutesRewardAndPushOnce()
        {
            TestPeer userPeer = CreatePeer();
            IUserPeerExtension user = AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);

            bool firstUnlock = property.TrySetProgress(AchievementKey, RequiredProgress);
            long unlockedAt = property.Get(AchievementKey).unlockedAt;
            bool replayedUnlock = property.TrySetProgress(AchievementKey, RequiredProgress);

            Assert.That(firstUnlock, Is.True);
            Assert.That(replayedUnlock, Is.False);
            Assert.That(property.Get(AchievementKey).unlockedAt, Is.EqualTo(unlockedAt));
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(user.UserId, Is.EqualTo(UserId));
            Assert.That(userPeer.SentMessages.Count(message =>
                message.OpCode == MstOpCodes.ClientAchievementUnlocked), Is.EqualTo(1));
        }

        [Test]
        public void RoomProfileReplay_CannotReopenAppliedRewardMarker()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);
            property.TrySetProgress(AchievementKey, RequiredProgress);
            AchievementProgressInfo applied = property.Get(AchievementKey);
            long unlockedAt = applied.unlockedAt;
            var staleRoomValue = new AchievementProgressInfo(applied)
            {
                rewardApplied = false
            };

            property[0] = staleRoomValue;

            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(property.Get(AchievementKey).IsRewardApplied, Is.True);
            Assert.That(property.Get(AchievementKey).unlockedAt, Is.EqualTo(unlockedAt));
            Assert.That(userPeer.SentMessages.Count(message =>
                message.OpCode == MstOpCodes.ClientAchievementUnlocked), Is.EqualTo(1));
        }

        [Test]
        public void RoomProfileReplay_CannotRelockAppliedReward()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);
            property.TrySetProgress(AchievementKey, RequiredProgress);
            AchievementProgressInfo applied = property.Get(AchievementKey);
            var staleLockedValue = new AchievementProgressInfo(applied)
            {
                progress = 0,
                rewardApplied = false
            };

            property[0] = staleLockedValue;
            bool replayedUnlock = property.TrySetProgress(AchievementKey, RequiredProgress);

            AchievementProgressInfo restored = property.Get(AchievementKey);
            Assert.That(restored.IsUnlocked, Is.True);
            Assert.That(restored.IsRewardApplied, Is.True);
            Assert.That(replayedUnlock, Is.False);
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(userPeer.SentMessages.Count(message =>
                message.OpCode == MstOpCodes.ClientAchievementUnlocked), Is.EqualTo(1));
        }

        [Test]
        public void RoomProfileReplay_CannotErasePendingUnlock()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);
            achievementsModule.FailNextReward = true;
            LogAssert.Expect(LogType.Error,
                new Regex("Achievement .* completion failed .*Expected reward failure"));
            property.TrySetProgress(AchievementKey, RequiredProgress);
            AchievementProgressInfo pending = property.Get(AchievementKey);
            long unlockedAt = pending.unlockedAt;
            var staleLockedValue = new AchievementProgressInfo(pending)
            {
                progress = 0,
                unlockedAt = 0,
                rewardApplied = false
            };
            achievementsModule.FailNextReward = true;
            LogAssert.Expect(LogType.Error,
                new Regex("Achievement .* completion failed .*Expected reward failure"));

            property[0] = staleLockedValue;

            AchievementProgressInfo restored = property.Get(AchievementKey);
            Assert.That(restored.IsUnlocked, Is.True);
            Assert.That(restored.unlockedAt, Is.EqualTo(unlockedAt));
            Assert.That(restored.IsRewardApplied, Is.False);
            Assert.That(achievementsModule.RewardAttemptCount, Is.EqualTo(2));
            Assert.That(achievementsModule.RewardCount, Is.Zero);

            achievementsModule.TrackProfile(profile);

            Assert.That(property.Get(AchievementKey).unlockedAt, Is.EqualTo(unlockedAt));
            Assert.That(property.Get(AchievementKey).IsRewardApplied, Is.True);
            Assert.That(achievementsModule.RewardAttemptCount, Is.EqualTo(3));
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
        }

        [Test]
        public void ProfileLoad_WhenAchievementIsAlreadyUnlocked_DoesNotRepeatReward()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer, RequiredProgress);

            achievementsModule.TrackProfile(profile);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            bool replayedUnlock = property.TrySetProgress(AchievementKey, RequiredProgress);

            Assert.That(replayedUnlock, Is.False);
            Assert.That(achievementsModule.RewardCount, Is.Zero);
            Assert.That(userPeer.SentMessages, Is.Empty);
        }

        [Test]
        public void RewardFailure_RemainsPendingAndRetriesWhenProfileIsTrackedAgain()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);
            achievementsModule.FailNextReward = true;
            LogAssert.Expect(LogType.Error,
                new Regex("Achievement .* completion failed .*Expected reward failure"));

            bool unlocked = property.TrySetProgress(AchievementKey, RequiredProgress);
            long unlockedAt = property.Get(AchievementKey).unlockedAt;

            Assert.That(unlocked, Is.True);
            Assert.That(unlockedAt, Is.GreaterThan(0));
            Assert.That(property.Get(AchievementKey).IsRewardApplied, Is.False);
            Assert.That(achievementsModule.RewardAttemptCount, Is.EqualTo(1));
            Assert.That(achievementsModule.RewardCount, Is.Zero);

            achievementsModule.TrackProfile(profile);

            Assert.That(property.Get(AchievementKey).IsRewardApplied, Is.True);
            Assert.That(property.Get(AchievementKey).unlockedAt, Is.EqualTo(unlockedAt));
            Assert.That(achievementsModule.RewardAttemptCount, Is.EqualTo(2));
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(userPeer.SentMessages.Count(message =>
                message.OpCode == MstOpCodes.ClientAchievementUnlocked), Is.EqualTo(1));
        }

        [Test]
        public void DisposedProfile_NoLongerCompletesAchievementUnlock()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);

            profile.Dispose();
            property.TrySetProgress(AchievementKey, RequiredProgress);

            Assert.That(achievementsModule.RewardCount, Is.Zero);
            Assert.That(userPeer.SentMessages, Is.Empty);
        }

        [Test]
        public void ProfileTransition_AfterLogoutStillExecutesRewardWithoutPush()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            ObservableAchievements property = profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements);
            achievementsModule.TrackProfile(profile);
            LogOutUser(userPeer);

            bool unlocked = property.TrySetProgress(AchievementKey, RequiredProgress);

            Assert.That(unlocked, Is.True);
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(userPeer.SentMessages, Is.Empty);
        }

        [Test]
        public async Task ServerUpdate_AfterLogoutUsesRetainedProfile()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            achievementsModule.TrackProfile(profile);
            LogOutUser(userPeer);
            TestPeer roomPeer = CreatePeer();
            GrantRoomPermission(roomPeer);

            await achievementsModule.InvokeServerUpdate(CreateServerUpdateRequest(roomPeer, true));

            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(userPeer.SentMessages, Is.Empty);
            AssertResponse(roomPeer, ResponseStatus.Success);
        }

        [UnityTest]
        public IEnumerator ServerUpdate_FromWorkerAppliesRewardOnUnityMainThread()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            achievementsModule.TrackProfile(profile);
            TestPeer roomPeer = CreatePeer();
            GrantRoomPermission(roomPeer);
            IIncomingMessage request = CreateServerUpdateRequest(roomPeer, true);

            Task worker = Task.Run(() => achievementsModule.InvokeServerUpdate(request));
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);

            while (!worker.IsCompleted && DateTime.UtcNow < deadline)
                yield return null;

            Assert.That(worker.IsCompleted, Is.True, "Worker handler did not complete");

            if (worker.IsFaulted)
                throw worker.Exception;

            Assert.That(achievementsModule.LastRewardThreadId, Is.EqualTo(mainThreadId));
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            AssertResponse(roomPeer, ResponseStatus.Success);
        }

        [Test]
        public async Task ServerUpdate_WithoutExactRoomPermission_ReturnsForbidden()
        {
            TestPeer requestPeer = CreatePeer();
            var security = new SecurityInfoPeerExtension(requestPeer);
            security.GrantPermission("same_level_but_wrong_key", MstPermissionLevels.RoomServer);
            security.SetAccountPermissionLevel(MstPermissionLevels.Admin);
            requestPeer.AddExtension(security);
            IIncomingMessage request = CreateServerUpdateRequest(requestPeer, true);

            await achievementsModule.InvokeServerUpdate(request);

            AssertResponse(requestPeer, ResponseStatus.Forbidden);
            Assert.That(achievementsModule.RewardCount, Is.Zero);
        }

        [Test]
        public async Task ServerUpdate_WhenServerRunIsCancelled_DoesNotMutateOrRespond()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            achievementsModule.TrackProfile(profile);
            TestPeer roomPeer = CreatePeer();
            GrantRoomPermission(roomPeer);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await achievementsModule.InvokeServerUpdate(
                CreateServerUpdateRequest(roomPeer, true), cancellation.Token);

            Assert.That(achievementsModule.RewardCount, Is.Zero);
            Assert.That(roomPeer.SentMessages, Is.Empty);
        }

        [Test]
        public async Task ServerUpdate_WithRoomPermission_AppliesUnlockOnlyOnce()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            achievementsModule.TrackProfile(profile);
            TestPeer roomPeer = CreatePeer();
            GrantRoomPermission(roomPeer);

            await achievementsModule.InvokeServerUpdate(CreateServerUpdateRequest(roomPeer, true));
            await achievementsModule.InvokeServerUpdate(CreateServerUpdateRequest(roomPeer, true));

            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(userPeer.SentMessages.Count(message =>
                message.OpCode == MstOpCodes.ClientAchievementUnlocked), Is.EqualTo(1));
            Assert.That(roomPeer.SentMessages.Count(message =>
                message.Status == ResponseStatus.Success), Is.EqualTo(2));
        }

        [Test]
        public async Task ServerUpdate_WhenProfileIsMissing_DoesNotAcknowledgeBatch()
        {
            TestPeer roomPeer = CreatePeer();
            GrantRoomPermission(roomPeer);

            await achievementsModule.InvokeServerUpdate(
                CreateServerUpdateRequest(roomPeer, true));

            AssertResponse(roomPeer, ResponseStatus.NotFound);
            Assert.That(achievementsModule.RewardCount, Is.Zero);
        }

        [Test]
        public async Task ServerUpdate_FireAndForgetDoesNotCreateResponseLoop()
        {
            TestPeer userPeer = CreatePeer();
            AddLoggedInUser(userPeer);
            ObservableServerProfile profile = CreateProfile(userPeer);
            achievementsModule.TrackProfile(profile);
            TestPeer roomPeer = CreatePeer();
            GrantRoomPermission(roomPeer);

            await achievementsModule.InvokeServerUpdate(CreateServerUpdateRequest(roomPeer, false));

            Assert.That(roomPeer.SentMessages, Is.Empty);
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ClientUpdate_OutsideRoomDistinguishesProgressFromUnlock()
        {
            TestPeer clientPeer = CreatePeer();
            AddLoggedInUser(clientPeer);
            ObservableServerProfile profile = CreateProfile(clientPeer);
            achievementsModule.TrackProfile(profile);
            achievementsModule.ClientCanUpdateProgress = true;

            await achievementsModule.InvokeClientUpdate(CreateRequest(
                MstOpCodes.ClientUpdateAchievementProgress,
                clientPeer,
                new UpdateAchievementProgressPacket
                {
                    key = AchievementKey,
                    progress = 1
                }.ToBytes(),
                true));

            IOutgoingMessage progressResponse = AssertResponse(clientPeer, ResponseStatus.Success);
            Assert.That(EndianBitConverter.Big.ToBoolean(progressResponse.Data, 0), Is.False);
            Assert.That(achievementsModule.RewardCount, Is.Zero);

            await achievementsModule.InvokeClientUpdate(CreateRequest(
                MstOpCodes.ClientUpdateAchievementProgress,
                clientPeer,
                new UpdateAchievementProgressPacket
                {
                    key = AchievementKey,
                    progress = RequiredProgress - 1
                }.ToBytes(),
                true));

            IOutgoingMessage unlockResponse = AssertResponse(clientPeer, ResponseStatus.Success);
            Assert.That(EndianBitConverter.Big.ToBoolean(unlockResponse.Data, 0), Is.True);
            Assert.That(achievementsModule.RewardCount, Is.EqualTo(1));
            Assert.That(clientPeer.SentMessages.Count(message =>
                message.OpCode == MstOpCodes.ClientAchievementUnlocked), Is.EqualTo(1));
        }

        [Test]
        public async Task ClientUpdate_InRoomForwardsAuthenticatedUserAndReturnsRoomResult()
        {
            TestPeer clientPeer = CreatePeer();
            IUserPeerExtension user = AddLoggedInUser(clientPeer);
            user.JoinedRoomID = 77;
            TestPeer roomPeer = CreatePeer();
            roomsModule.AddRoom(new RegisteredRoom(user.JoinedRoomID, roomPeer, new RoomOptions()));
            achievementsModule.ClientCanUpdateProgress = true;
            var clientPacket = new UpdateAchievementProgressPacket
            {
                key = AchievementKey,
                progress = RequiredProgress,
                userId = "forged-user"
            };
            IIncomingMessage request = CreateRequest(
                MstOpCodes.ClientUpdateAchievementProgress,
                clientPeer,
                clientPacket.ToBytes(),
                true);

            await achievementsModule.InvokeClientUpdate(request);

            UpdateAchievementProgressPacket forwarded =
                SerializablePacket.FromBytes<UpdateAchievementProgressPacket>(
                    roomPeer.SentMessages.Single().Data);
            Assert.That(forwarded.userId, Is.EqualTo(UserId));
            Assert.That(clientPeer.SentMessages, Is.Empty);

            roomPeer.CompleteLastRequest(ResponseStatus.Success,
                MessageHelper.Create(MstOpCodes.ServerUpdateAchievementProgress, true).Data);

            IOutgoingMessage response = AssertResponse(clientPeer, ResponseStatus.Success);
            Assert.That(EndianBitConverter.Big.ToBoolean(response.Data, 0), Is.True);
        }

        [Test]
        public async Task ClientUpdate_InRoomForwardsTerminalRoomFailure()
        {
            TestPeer clientPeer = CreatePeer();
            IUserPeerExtension user = AddLoggedInUser(clientPeer);
            user.JoinedRoomID = 91;
            TestPeer roomPeer = CreatePeer();
            roomsModule.AddRoom(new RegisteredRoom(user.JoinedRoomID, roomPeer, new RoomOptions()));
            achievementsModule.ClientCanUpdateProgress = true;
            var packet = new UpdateAchievementProgressPacket
            {
                key = AchievementKey,
                progress = 1
            };
            IIncomingMessage request = CreateRequest(
                MstOpCodes.ClientUpdateAchievementProgress,
                clientPeer,
                packet.ToBytes(),
                true);

            await achievementsModule.InvokeClientUpdate(request);
            byte[] structuredError = CreateStructuredError(MstErrorCodes.ACHIEVEMENT_NOT_FOUND);
            roomPeer.CompleteLastRequest(ResponseStatus.NotFound, structuredError);

            IOutgoingMessage response = AssertResponse(clientPeer, ResponseStatus.NotFound);
            Assert.That(response.Data, Is.EqualTo(structuredError));
        }

        [Test]
        public async Task ClientUpdate_InRoomForwardsRoomDisconnect()
        {
            TestPeer clientPeer = CreatePeer();
            IUserPeerExtension user = AddLoggedInUser(clientPeer);
            user.JoinedRoomID = 92;
            TestPeer roomPeer = CreatePeer();
            roomsModule.AddRoom(new RegisteredRoom(user.JoinedRoomID, roomPeer, new RoomOptions()));
            achievementsModule.ClientCanUpdateProgress = true;
            IIncomingMessage request = CreateRequest(
                MstOpCodes.ClientUpdateAchievementProgress,
                clientPeer,
                new UpdateAchievementProgressPacket
                {
                    key = AchievementKey,
                    progress = 1
                }.ToBytes(),
                true);

            await achievementsModule.InvokeClientUpdate(request);
            roomPeer.CompleteLastRequest(ResponseStatus.NotConnected, Array.Empty<byte>());

            IOutgoingMessage response = AssertResponse(clientPeer, ResponseStatus.NotConnected);
            MstProperties error = MstProperties.FromBytes(response.Data);
            Assert.That(error.AsString(MstErrorPropertyKeys.CODE),
                Is.EqualTo(MstErrorCodes.ROOM_ACHIEVEMENT_FORWARD_FAILED));
        }

        [Test]
        public async Task ClientUpdate_WhenJoinedRoomIsMissing_DoesNotMutateMasterProfile()
        {
            TestPeer clientPeer = CreatePeer();
            IUserPeerExtension user = AddLoggedInUser(clientPeer);
            user.JoinedRoomID = 404;
            ObservableServerProfile profile = CreateProfile(clientPeer);
            achievementsModule.TrackProfile(profile);
            achievementsModule.ClientCanUpdateProgress = true;

            await achievementsModule.InvokeClientUpdate(CreateRequest(
                MstOpCodes.ClientUpdateAchievementProgress,
                clientPeer,
                new UpdateAchievementProgressPacket
                {
                    key = AchievementKey,
                    progress = RequiredProgress
                }.ToBytes(),
                true));

            AssertResponse(clientPeer, ResponseStatus.DependencyError);
            Assert.That(profile.Get<ObservableAchievements>(
                ProfilePropertyOpCodes.achievements).Get(AchievementKey).progress, Is.Zero);
            Assert.That(achievementsModule.RewardCount, Is.Zero);
        }

        [Test]
        public void ClientFacade_AckReportsResultWithoutRaisingUnlockEvent()
        {
            var socket = new FakeClientSocket();
            var client = CreateAchievementsClient(socket);
            int unlockEventCount = 0;
            bool callbackSuccessful = false;
            bool callbackUnlocked = false;
            client.OnAchievementUnlocked += _ => unlockEventCount++;

            client.UpdateProgressWithResult(AchievementKey, RequiredProgress,
                (isSuccessful, unlockedNow, error) =>
                {
                    callbackSuccessful = isSuccessful;
                    callbackUnlocked = unlockedNow;
                    Assert.That(error, Is.Empty);
                });
            socket.RespondNext(
                MstOpCodes.ClientUpdateAchievementProgress,
                ResponseStatus.Success,
                MessageHelper.Create(MstOpCodes.ClientUpdateAchievementProgress, true).Data);

            Assert.That(callbackSuccessful, Is.True);
            Assert.That(callbackUnlocked, Is.True);
            Assert.That(unlockEventCount, Is.Zero);

            socket.Deliver(MstOpCodes.ClientAchievementUnlocked, AchievementKey.ToBytes());

            Assert.That(unlockEventCount, Is.EqualTo(1));
            client.ClearConnection();
        }

        [Test]
        public void ClientFacade_InvalidKeyCompletesOnceWithoutSending()
        {
            var socket = new FakeClientSocket();
            var client = CreateAchievementsClient(socket);
            int callbackCount = 0;

            client.UpdateProgressWithResult(null, RequiredProgress,
                (isSuccessful, unlockedNow, error) =>
                {
                    callbackCount++;
                    Assert.That(isSuccessful, Is.False);
                    Assert.That(unlockedNow, Is.False);
                    Assert.That(error, Is.EqualTo(Mst.Errors.Localize("ui.error.achievements.key_required.message")));
                });

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(socket.Requests, Is.Empty);
            client.ClearConnection();
        }

        [Test]
        public void ClientFacade_SendFailureCompletesOnce()
        {
            var socket = new FakeClientSocket
            {
                ThrowOnSendOpcode = MstOpCodes.ClientUpdateAchievementProgress
            };
            var client = CreateAchievementsClient(socket);
            int callbackCount = 0;

            LogAssert.Expect(
                LogType.Error,
                new Regex("Failed to send achievement progress"));

            client.UpdateProgressWithResult(AchievementKey, RequiredProgress,
                (isSuccessful, unlockedNow, error) =>
                {
                    callbackCount++;
                    Assert.That(isSuccessful, Is.False);
                    Assert.That(unlockedNow, Is.False);
                    Assert.That(error, Is.EqualTo(Mst.Errors.Localize("ui.error.response.internal.message")));
                });

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(socket.Requests, Is.Empty);
            client.ClearConnection();
        }

        [Test]
        public void RoomServerQueue_RetriesUntilMasterAcknowledgesUpdate()
        {
            TestPeer userPeer = CreatePeer();
            ObservableServerProfile profile = CreateProfile(userPeer);
            var socket = new FakeClientSocket();
            var roomAchievements = new TestAchievementsModuleServer(socket, profile)
            {
                UpdatesInterval = 0f
            };

            roomAchievements.AddProgress(AchievementKey, RequiredProgress, UserId);
            roomAchievements.SendPendingUpdatesForTest();
            Assert.That(socket.Requests.Count, Is.EqualTo(1));
            LogAssert.Expect(
                LogType.Error,
                new Regex("Master rejected achievement update batch.*status=Timeout"));
            socket.RespondNext(MstOpCodes.ServerUpdateAchievementProgress,
                ResponseStatus.Timeout);
            roomAchievements.SendPendingUpdatesForTest();
            Assert.That(socket.Requests.Count, Is.EqualTo(2));
            socket.RespondNext(MstOpCodes.ServerUpdateAchievementProgress,
                ResponseStatus.Success);
            roomAchievements.SendPendingUpdatesForTest();
            Assert.That(socket.Requests.Count, Is.EqualTo(2));
            roomAchievements.ClearConnection();
        }

        private TestPeer CreatePeer()
        {
            var peer = new TestPeer();
            peers.Add(peer);
            return peer;
        }

        private IUserPeerExtension AddLoggedInUser(TestPeer peer)
        {
            var account = new TestAccountInfoData
            {
                Id = UserId,
                Username = "achievement-user",
                ExtraProperties = new Dictionary<string, string>()
            };
            var user = new UserPeerExtension(peer)
            {
                Account = account
            };
            peer.AddExtension<IUserPeerExtension>(user);
            authModule.AddLoggedInUser(user);
            return user;
        }

        private void LogOutUser(TestPeer peer)
        {
            authModule.RemoveLoggedInUser(UserId);
            peer.ClearExtension<IUserPeerExtension>();
            peer.ClearExtension<ProfilePeerExtension>();
        }

        private ObservableServerProfile CreateProfile(TestPeer userPeer, int progress = 0)
        {
            var profile = new ObservableServerProfile(UserId, userPeer);
            var property = CreateAchievementsProperty(progress);
            profile.Add(property);
            profile.ClearUpdates();
            userPeer.AddExtension(new ProfilePeerExtension(profile, userPeer));
            profiles.Add(profile);
            return profile;
        }

        private ObservableAchievements CreateAchievementsProperty(int progress = 0)
        {
            var property = new ObservableAchievements(ProfilePropertyOpCodes.achievements);
            var achievement = new AchievementProgressInfo(achievementData)
            {
                progress = progress,
                unlockedAt = progress >= RequiredProgress
                    ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    : 0,
                rewardApplied = progress >= RequiredProgress
            };
            property.Add(achievement);
            property.ClearUpdates();
            return property;
        }

        private static MstJson CreateLegacyAchievementJson(int progress, string unlockTime)
        {
            var json = MstJson.CreateObject();
            json.AddField("key", AchievementKey);
            json.AddField("progress", progress);
            json.AddField("required", RequiredProgress);
            json.AddField("unlock_time", unlockTime);
            return json;
        }

        private static void GrantRoomPermission(TestPeer peer)
        {
            var security = new SecurityInfoPeerExtension(peer);
            security.GrantPermission(MstPermissionKeys.RoomServer, MstPermissionLevels.RoomServer);
            peer.AddExtension(security);
        }

        private static IIncomingMessage CreateServerUpdateRequest(TestPeer peer, bool expectsResponse)
        {
            var packet = new UpdateAchievementProgressPacket
            {
                key = AchievementKey,
                userId = UserId,
                progress = RequiredProgress
            };

            return CreateRequest(
                MstOpCodes.ServerUpdateAchievementProgress,
                peer,
                new List<UpdateAchievementProgressPacket> { packet }.ToBytes(),
                expectsResponse);
        }

        private static IIncomingMessage CreateRequest(ushort opCode, TestPeer peer, byte[] data,
            bool expectsResponse)
        {
            return new IncomingMessage(opCode, 0, data, DeliveryMethod.Reliable, peer)
            {
                AckResponseId = expectsResponse ? 1 : (int?)null
            };
        }

        private static AchievementsModuleClient CreateAchievementsClient(IClientSocket socket)
        {
            return new AchievementsModuleClient(socket);
        }

        private static byte[] CreateStructuredError(string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);
            return properties.ToBytes();
        }

        private static IOutgoingMessage AssertResponse(TestPeer peer, ResponseStatus expectedStatus)
        {
            Assert.That(peer.SentMessages, Is.Not.Empty);
            IOutgoingMessage response = peer.SentMessages.Last();
            Assert.That(response.Status, Is.EqualTo(expectedStatus));
            return response;
        }

        private sealed class TestAchievementsModule : AchievementsModule
        {
            public int RewardCount { get; private set; }
            public int RewardAttemptCount { get; private set; }
            public int LastRewardThreadId { get; private set; } = -1;
            public bool FailNextReward { get; set; }
            public bool ClientCanUpdateProgress
            {
                set => clientCanUpdateProgress = value;
            }

            public void InitializeForTests()
            {
                base.Awake();
            }

            public void Configure(AuthModule auth, RoomsModule rooms, AchievementsDatabase database)
            {
                authModule = auth;
                roomsModule = rooms;
                achievementsDatabase = database;
                CacheAchievementDefinitions();
            }

            public void TrackProfile(ObservableServerProfile profile)
            {
                ProfilesModule_OnProfileLoaded(profile);
            }

            public Task InvokeClientUpdate(IIncomingMessage message)
            {
                return ClientUpdateAchievementProgressRequestHandler(
                    message, CancellationToken.None);
            }

            public Task InvokeServerUpdate(IIncomingMessage message)
            {
                return ServerUpdateAchievementProgressRequestHandler(
                    message, CancellationToken.None);
            }

            public Task InvokeServerUpdate(IIncomingMessage message,
                CancellationToken cancellationToken)
            {
                return ServerUpdateAchievementProgressRequestHandler(
                    message, cancellationToken);
            }

            protected override void OnAchievementResultCommand(ObservableServerProfile profile,
                AchievementData achievement)
            {
                RewardAttemptCount++;
                LastRewardThreadId = Thread.CurrentThread.ManagedThreadId;

                if (FailNextReward)
                {
                    FailNextReward = false;
                    throw new InvalidOperationException("Expected reward failure");
                }

                RewardCount++;
            }
        }

        private sealed class TestAchievementsModuleServer : AchievementsModuleServer
        {
            private readonly ObservableServerProfile profile;

            public TestAchievementsModuleServer(IClientSocket connection,
                ObservableServerProfile profile) : base(connection)
            {
                this.profile = profile;
            }

            protected override bool TryGetProfile(string userId,
                out ObservableServerProfile foundProfile)
            {
                foundProfile = userId == profile.UserId ? profile : null;
                return foundProfile != null;
            }

            protected override void StartSendingUpdates() { }

            public void SendPendingUpdatesForTest()
            {
                SendPendingUpdates();
            }
        }

        private sealed class TestAuthModule : AuthModule
        {
            public void AddLoggedInUser(IUserPeerExtension user)
            {
                loggedInUsers[user.UserId] = user;
            }

            public void RemoveLoggedInUser(string userId)
            {
                loggedInUsers.TryRemove(userId, out _);
            }
        }

        private sealed class TestRoomsModule : RoomsModule
        {
            public void AddRoom(RegisteredRoom room)
            {
                roomsList[room.RoomId] = room;
            }
        }

        private sealed class TestAchievementsDatabase : AchievementsDatabase
        {
            public void SetItems(params AchievementData[] items)
            {
                objects = new List<AchievementData>(items);
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private bool isConnected = true;
            private int lastRequestAckId = -1;
            private ushort lastRequestOpCode;

            public List<IOutgoingMessage> SentMessages { get; } = new List<IOutgoingMessage>();
            public override bool IsConnected => isConnected;

            public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
            {
                if (message.AckRequestId.HasValue)
                {
                    lastRequestAckId = message.AckRequestId.Value;
                    lastRequestOpCode = message.OpCode;
                }

                SentMessages.Add(message);
            }

            public void CompleteLastRequest(ResponseStatus status, byte[] data)
            {
                Assert.That(lastRequestAckId, Is.GreaterThan(0));
                var response = new IncomingMessage(
                    lastRequestOpCode,
                    0,
                    data ?? Array.Empty<byte>(),
                    DeliveryMethod.Reliable,
                    this)
                {
                    Status = status
                };

                TriggerAck(lastRequestAckId, status, response);
                lastRequestAckId = -1;
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
        }

        private sealed class TestAccountInfoData : IAccountInfoData
        {
            public string Id { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Token { get; set; }
            public DateTime LastLoginAt { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsGuest { get; set; }
            public bool IsEmailConfirmed { get; set; }
            public Dictionary<string, string> ExtraProperties { get; set; }

            public event Action<IAccountInfoData> OnChangedEvent;

            public void MarkAsDirty()
            {
                OnChangedEvent?.Invoke(this);
            }

            public MstJson ToJson()
            {
                return MstJson.CreateObject();
            }
        }
    }
}
