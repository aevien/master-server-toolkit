using MasterServerToolkit.Json;
using MasterServerToolkit.GameService;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class LeaderboardsModuleTests
    {
        private const string LeaderboardKey = "survivalTime";
        private const string SeasonId = "season_test";
        private const string UserId = "leaderboard-user";

        private readonly List<TestPeer> peers = new List<TestPeer>();
        private GameObject testObject;
        private TestAuthModule authModule;
        private TestLeaderboardsModule module;
        private InMemoryLeaderboardsAccessor accessor;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(LeaderboardsModuleTests));
            authModule = testObject.AddComponent<TestAuthModule>();
            module = testObject.AddComponent<TestLeaderboardsModule>();
            accessor = new InMemoryLeaderboardsAccessor();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (TestPeer peer in peers)
                peer.Dispose();

            accessor.Dispose();
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public void Definition_ResolvesLocalizedTitleAndAvailability()
        {
            LeaderboardDefinition definition = CreateDefinition();
            SetField(definition, "startsAt", "2026-06-01T00:00:00Z");
            SetField(definition, "endsAt", "2026-09-01T00:00:00Z");

            Assert.That(definition.TryValidate(out string error), Is.True, error);
            Assert.That(definition.GetTitle("ru"), Is.EqualTo("Время выживания"));
            Assert.That(definition.GetTitle("de"), Is.EqualTo("Survival Time"));
            Assert.That(definition.GetAvailability(
                new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc)),
                Is.EqualTo(LeaderboardAvailability.Upcoming));
            Assert.That(definition.GetAvailability(
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
                Is.EqualTo(LeaderboardAvailability.Active));
            Assert.That(definition.GetAvailability(
                new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
                Is.EqualTo(LeaderboardAvailability.Ended));
        }

        [Test]
        public void Definition_RejectsDuplicateLanguageAndInvalidPeriod()
        {
            LeaderboardDefinition definition = CreateDefinition();
            SetField(definition, "title", new List<LeaderboardLocalizedTitle>
            {
                new LeaderboardLocalizedTitle("en", "One"),
                new LeaderboardLocalizedTitle("EN", "Two")
            });

            Assert.That(definition.TryValidate(out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("duplicate language"));

            SetField(definition, "title", new List<LeaderboardLocalizedTitle>
            {
                new LeaderboardLocalizedTitle("en", "One")
            });
            SetField(definition, "startsAt", "2026-09-01T00:00:00Z");
            SetField(definition, "endsAt", "2026-06-01T00:00:00Z");

            Assert.That(definition.TryValidate(out string periodError), Is.False);
            Assert.That(periodError, Does.Contain("end after it starts"));
        }

        [Test]
        public void Definition_RejectsIdentifiersUnsupportedByPersistenceContract()
        {
            LeaderboardDefinition definition = CreateDefinition();
            SetField(definition, "key", new string('k', LeaderboardDefinition.MaxKeyLength + 1));

            Assert.That(definition.TryValidate(out string keyError), Is.False);
            Assert.That(keyError, Does.Contain("exceeds"));

            SetField(definition, "key", LeaderboardKey);
            SetField(definition, "seasonId",
                new string('s', LeaderboardDefinition.MaxSeasonIdLength + 1));

            Assert.That(definition.TryValidate(out string seasonError), Is.False);
            Assert.That(seasonError, Does.Contain("exceeds"));
        }

        [Test]
        public void LeaderboardPackets_BinaryRoundTripPreservesLongAndTitles()
        {
            LeaderboardDefinition definition = CreateDefinition();
            var source = new LeaderboardEntriesPacket
            {
                Definition = LeaderboardDefinitionPacket.FromDefinition(
                    definition, DateTime.UtcNow),
                Entries = new List<LeaderboardEntry>
                {
                    new LeaderboardEntry
                    {
                        LeaderboardKey = LeaderboardKey,
                        SeasonId = SeasonId,
                        AccountId = UserId,
                        PlayerName = "player-test",
                        PlayerAvatar = "https://cdn.example.com/avatar.png",
                        Score = (long)int.MaxValue + 123456L,
                        Rank = 1,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                },
                TotalEntries = (long)int.MaxValue + 10L,
                CurrentPlayerRank = 1,
                Offset = 0
            };

            LeaderboardEntriesPacket restored =
                SerializablePacket.FromBytes<LeaderboardEntriesPacket>(source.ToBytes());

            Assert.That(restored.Definition.Title["ru"], Is.EqualTo("Время выживания"));
            Assert.That(restored.Entries.Single().Score,
                Is.EqualTo((long)int.MaxValue + 123456L));
            Assert.That(restored.Entries.Single().PlayerAvatar,
                Is.EqualTo("https://cdn.example.com/avatar.png"));
            Assert.That(restored.TotalEntries, Is.EqualTo((long)int.MaxValue + 10L));
        }

        [Test]
        public void PlatformMirror_UnsupportedModuleCompletesWithFailure()
        {
            BaseLeaderboardsModule platformModule =
                testObject.AddComponent<BaseLeaderboardsModule>();
            int callbackCount = 0;
            bool succeeded = true;

            platformModule.SetScore(
                LeaderboardKey,
                long.MaxValue,
                MstJson.CreateObject(),
                (success, error) =>
                {
                    callbackCount++;
                    succeeded = success;
                });

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(succeeded, Is.False);
        }

        [Test]
        public void PlatformMirror_EditorModuleAcceptsFullLongScore()
        {
            EditorLeaderboardsModule platformModule =
                testObject.AddComponent<EditorLeaderboardsModule>();
            int callbackCount = 0;
            bool succeeded = false;

            platformModule.SetScore(
                LeaderboardKey,
                long.MaxValue,
                MstJson.CreateObject(),
                (success, error) =>
                {
                    callbackCount++;
                    succeeded = success;
                });

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(succeeded, Is.True);
        }

        [Test]
        public void PlatformLeaderboardEntry_PreservesFullLongScore()
        {
            long score = (long)int.MaxValue + 123456L;
            var entry = new LeaderboardPlayerInfo
            {
                Score = score
            };

            Assert.That(entry.Score, Is.EqualTo(score));
        }

        [Test]
        public void ClientEntriesCache_CoalescesPendingRequestAndReusesResponse()
        {
            var socket = new FakeClientSocket();
            var client = new LeaderboardsModuleClient(socket);
            int firstCallbackCount = 0;
            int secondCallbackCount = 0;
            int cachedCallbackCount = 0;
            LeaderboardEntriesPacket firstResult = null;
            LeaderboardEntriesPacket secondResult = null;

            try
            {
                client.GetEntries(LeaderboardKey, (result, error) =>
                {
                    firstCallbackCount++;
                    firstResult = result;
                }, 0, 20, SeasonId);
                client.GetEntries(LeaderboardKey, (result, error) =>
                {
                    secondCallbackCount++;
                    secondResult = result;
                }, 0, 20, SeasonId);

                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntries), Is.EqualTo(1));

                LeaderboardEntriesPacket response = CreateEntriesResponse();
                socket.RespondNext(MstOpCodes.ClientGetLeaderboardEntries,
                    ResponseStatus.Success, response.ToBytes());

                Assert.That(firstCallbackCount, Is.EqualTo(1));
                Assert.That(secondCallbackCount, Is.EqualTo(1));
                Assert.That(firstResult, Is.Not.Null);
                Assert.That(secondResult, Is.SameAs(firstResult));

                client.GetEntries(LeaderboardKey, (result, error) =>
                {
                    cachedCallbackCount++;
                    Assert.That(result, Is.SameAs(firstResult));
                    Assert.That(error, Is.Empty);
                }, 0, 20, SeasonId);

                Assert.That(cachedCallbackCount, Is.EqualTo(1));
                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntries), Is.EqualTo(1));
            }
            finally
            {
                client.ClearConnection();
                socket.Close();
            }
        }

        [Test]
        public void ClientEntriesCache_SeparatesLeaderboardsSeasonsAndPages()
        {
            var socket = new FakeClientSocket();
            var client = new LeaderboardsModuleClient(socket);

            try
            {
                client.GetEntries(LeaderboardKey, null, 0, 20, SeasonId);
                client.GetEntries(LeaderboardKey, null, 20, 20, SeasonId);
                client.GetEntries(LeaderboardKey, null, 0, 20,
                    "another_season");
                client.GetEntries("zombiesKilled", null, 0, 20, SeasonId);

                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntries), Is.EqualTo(4));
            }
            finally
            {
                client.ClearConnection();
                socket.Close();
            }
        }

        [Test]
        public void ClientEntriesCache_ReconnectRemovesCachedResponse()
        {
            var socket = new FakeClientSocket();
            var client = new LeaderboardsModuleClient(socket);

            try
            {
                client.GetEntries(LeaderboardKey, null, 0, 20, SeasonId);
                socket.RespondNext(MstOpCodes.ClientGetLeaderboardEntries,
                    ResponseStatus.Success, CreateEntriesResponse().ToBytes());
                client.GetEntries(LeaderboardKey, null, 0, 20, SeasonId);

                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntries), Is.EqualTo(1));

                socket.Reconnect();
                client.GetEntries(LeaderboardKey, null, 0, 20, SeasonId);

                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntries), Is.EqualTo(2));
            }
            finally
            {
                client.ClearConnection();
                socket.Close();
            }
        }

        [Test]
        public void ClientEntryCache_ReusesAuthenticatedPlayerEntry()
        {
            var socket = new FakeClientSocket();
            var client = new LeaderboardsModuleClient(socket);
            LeaderboardEntry cachedEntry = null;

            try
            {
                client.GetMyEntry(LeaderboardKey, null, SeasonId);
                socket.RespondNext(MstOpCodes.ClientGetLeaderboardEntry,
                    ResponseStatus.Success,
                    CreateEntriesResponse().Entries.Single().ToBytes());
                client.GetMyEntry(LeaderboardKey, (entry, error) =>
                {
                    cachedEntry = entry;
                    Assert.That(error, Is.Empty);
                }, SeasonId);

                Assert.That(cachedEntry, Is.Not.Null);
                Assert.That(cachedEntry.AccountId, Is.EqualTo(UserId));
                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntry), Is.EqualTo(1));
            }
            finally
            {
                client.ClearConnection();
                socket.Close();
            }
        }

        [Test]
        public void ClientEntriesCache_SuccessfulSubmissionInvalidatesLeaderboard()
        {
            var socket = new FakeClientSocket();
            var client = new LeaderboardsModuleClient(socket);

            try
            {
                client.GetEntries(LeaderboardKey, null, 0, 20, SeasonId);
                socket.RespondNext(MstOpCodes.ClientGetLeaderboardEntries,
                    ResponseStatus.Success, CreateEntriesResponse().ToBytes());

                client.SubmitScore(LeaderboardKey, 200, null, SeasonId);
                socket.RespondNext(MstOpCodes.ClientSubmitLeaderboardScore,
                    ResponseStatus.Success,
                    new LeaderboardSubmitResultPacket
                    {
                        Entry = CreateEntriesResponse().Entries.Single(),
                        ScoreChanged = true
                    }.ToBytes());

                client.GetEntries(LeaderboardKey, null, 0, 20, SeasonId);

                Assert.That(socket.Requests.Count(request =>
                    request.Message.OpCode ==
                    MstOpCodes.ClientGetLeaderboardEntries), Is.EqualTo(2));
            }
            finally
            {
                client.ClearConnection();
                socket.Close();
            }
        }

        [Test]
        public async Task ClientSubmission_WhenServerOnly_ReturnsForbidden()
        {
            ConfigureModule(CreateDefinition(serverOnly: true));
            TestPeer peer = CreateAuthenticatedPeer();

            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 100));

            AssertResponse(peer, ResponseStatus.Forbidden);
            Assert.That(accessor.EntryCount, Is.Zero);
        }

        [Test]
        public async Task ClientSubmission_UsesAuthenticatedAccountAndIgnoresSpoofedIdentity()
        {
            ConfigureModule(CreateDefinition(serverOnly: false));
            TestPeer peer = CreateAuthenticatedPeer();
            await accessor.SubmitScoreAsync(CreateSubmission(
                UserId,
                50,
                "https://cdn.example.com/existing.png"));
            var packet = new LeaderboardSubmitScorePacket
            {
                Key = LeaderboardKey,
                SeasonId = SeasonId,
                AccountId = "another-account",
                PlayerName = "spoofed-name",
                PlayerAvatar = "https://attacker.example/avatar.png",
                Score = 100
            };

            await module.InvokeClientSubmit(CreateRequest(
                MstOpCodes.ClientSubmitLeaderboardScore, peer, packet.ToBytes()));

            LeaderboardSubmitResultPacket result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                peer, ResponseStatus.Success);
            Assert.That(result.Entry.AccountId, Is.EqualTo(UserId));
            Assert.That(result.Entry.PlayerName, Is.EqualTo("player-test"));
            Assert.That(result.Entry.PlayerAvatar,
                Is.EqualTo("https://cdn.example.com/existing.png"));
            Assert.That(accessor.GetStoredEntry(LeaderboardKey, SeasonId, "another-account"), Is.Null);
        }

        [Test]
        public async Task ClientSubmission_GuestRejectedWhenGuestsAreDisabled()
        {
            ConfigureModule(CreateDefinition(serverOnly: false, allowGuests: false));
            TestPeer peer = CreateAuthenticatedPeer(isGuest: true);

            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 100));

            AssertResponse(peer, ResponseStatus.Forbidden);
            Assert.That(accessor.EntryCount, Is.Zero);
        }

        [Test]
        public async Task ServerSubmission_RequiresExactRoomServerPermission()
        {
            ConfigureModule(CreateDefinition());
            CreateAuthenticatedPeer();
            TestPeer serverPeer = CreatePeer();
            var security = new SecurityInfoPeerExtension(serverPeer);
            security.GrantPermission("same_level_wrong_key", MstPermissionLevels.RoomServer);
            security.SetAccountPermissionLevel(MstPermissionLevels.Admin);
            serverPeer.AddExtension(security);

            await module.InvokeServerSubmit(CreateServerSubmitRequest(serverPeer, 100));

            AssertResponse(serverPeer, ResponseStatus.Forbidden);
            Assert.That(accessor.EntryCount, Is.Zero);
        }

        [Test]
        public async Task ServerSubmission_WithRoomPermissionUsesTrustedDisplayName()
        {
            ConfigureModule(CreateDefinition());
            CreateAuthenticatedPeer();
            TestPeer serverPeer = CreatePeer();
            GrantRoomPermission(serverPeer);

            await module.InvokeServerSubmit(CreateServerSubmitRequest(
                serverPeer,
                100,
                "Public Survivor",
                "https://cdn.example.com/public-survivor.png"));

            LeaderboardSubmitResultPacket result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                serverPeer, ResponseStatus.Success);
            Assert.That(result.Entry.AccountId, Is.EqualTo(UserId));
            Assert.That(result.Entry.PlayerName, Is.EqualTo("Public Survivor"));
            Assert.That(result.Entry.PlayerAvatar,
                Is.EqualTo("https://cdn.example.com/public-survivor.png"));
            Assert.That(result.ScoreChanged, Is.True);
        }

        [Test]
        public async Task ServerSubmission_WorseKeepBestScoreRefreshesPlayerMetadata()
        {
            ConfigureModule(CreateDefinition(keepBest: true));
            CreateAuthenticatedPeer();
            TestPeer serverPeer = CreatePeer();
            GrantRoomPermission(serverPeer);

            await module.InvokeServerSubmit(CreateServerSubmitRequest(
                serverPeer,
                200,
                "Old Name",
                "https://cdn.example.com/old.png"));
            serverPeer.SentMessages.Clear();

            await module.InvokeServerSubmit(CreateServerSubmitRequest(
                serverPeer,
                100,
                "Current Name",
                "https://cdn.example.com/current.png"));

            LeaderboardSubmitResultPacket result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                serverPeer, ResponseStatus.Success);
            Assert.That(result.ScoreChanged, Is.False);
            Assert.That(result.Entry.Score, Is.EqualTo(200));
            Assert.That(result.Entry.PlayerName, Is.EqualTo("Current Name"));
            Assert.That(result.Entry.PlayerAvatar,
                Is.EqualTo("https://cdn.example.com/current.png"));

            serverPeer.SentMessages.Clear();
            await module.InvokeServerSubmit(CreateServerSubmitRequest(
                serverPeer,
                100,
                "Current Name",
                string.Empty));

            result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                serverPeer, ResponseStatus.Success);
            Assert.That(result.ScoreChanged, Is.False);
            Assert.That(result.Entry.Score, Is.EqualTo(200));
            Assert.That(result.Entry.PlayerAvatar, Is.Empty);
        }

        [Test]
        public void LeaderboardEntry_NormalizesUnsupportedAvatarUris()
        {
            const string avatarPrefix = "https://cdn.example.com/";
            var entry = new LeaderboardEntry
            {
                PlayerAvatar = "file:///C:/private/avatar.png"
            };

            entry.Normalize();

            Assert.That(entry.PlayerAvatar, Is.Empty);
            string maximumLengthAvatar = avatarPrefix + new string(
                'a',
                LeaderboardEntry.MaxPlayerAvatarLength - avatarPrefix.Length);
            Assert.That(
                LeaderboardEntry.NormalizePlayerAvatar(maximumLengthAvatar),
                Is.EqualTo(maximumLengthAvatar));
            Assert.That(
                LeaderboardEntry.NormalizePlayerAvatar(maximumLengthAvatar + "a"),
                Is.Empty);
        }

        [Test]
        public async Task KeepBest_PreservesBetterScoreAndReturnsCanonicalEntry()
        {
            ConfigureModule(CreateDefinition(serverOnly: false, keepBest: true));
            TestPeer peer = CreateAuthenticatedPeer();

            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 200));
            peer.SentMessages.Clear();
            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 100));

            LeaderboardSubmitResultPacket result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                peer, ResponseStatus.Success);
            Assert.That(result.ScoreChanged, Is.False);
            Assert.That(result.Entry.Score, Is.EqualTo(200));
        }

        [Test]
        public async Task KeepBest_AscendingLeaderboardPreservesLowerScore()
        {
            ConfigureModule(CreateDefinition(
                serverOnly: false,
                keepBest: true,
                sortOrder: LeaderboardSortOrder.Ascending));
            TestPeer peer = CreateAuthenticatedPeer();

            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 100));
            peer.SentMessages.Clear();
            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 200));

            LeaderboardSubmitResultPacket result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                peer, ResponseStatus.Success);
            Assert.That(result.ScoreChanged, Is.False);
            Assert.That(result.Entry.Score, Is.EqualTo(100));
        }

        [Test]
        public async Task KeepBestDisabled_ReplacesPreviousScore()
        {
            ConfigureModule(CreateDefinition(serverOnly: false, keepBest: false));
            TestPeer peer = CreateAuthenticatedPeer();

            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 200));
            peer.SentMessages.Clear();
            await module.InvokeClientSubmit(CreateSubmitRequest(peer, 100));

            LeaderboardSubmitResultPacket result = AssertPacketResponse<LeaderboardSubmitResultPacket>(
                peer, ResponseStatus.Success);
            Assert.That(result.ScoreChanged, Is.True);
            Assert.That(result.Entry.Score, Is.EqualTo(100));
        }

        [Test]
        public async Task Entries_UseAccountIdAsStableTieBreaker()
        {
            ConfigureModule(CreateDefinition(serverOnly: false));
            await accessor.SubmitScoreAsync(CreateSubmission("account-c", 100));
            await accessor.SubmitScoreAsync(CreateSubmission("account-a", 100));
            await accessor.SubmitScoreAsync(CreateSubmission("account-b", 200));
            TestPeer peer = CreateAuthenticatedPeer();
            var request = new LeaderboardEntriesRequestPacket
            {
                Key = LeaderboardKey,
                SeasonId = SeasonId,
                Limit = 10
            };

            await module.InvokeGetEntries(CreateRequest(
                MstOpCodes.ClientGetLeaderboardEntries, peer, request.ToBytes()));

            LeaderboardEntriesPacket result = AssertPacketResponse<LeaderboardEntriesPacket>(
                peer, ResponseStatus.Success);
            Assert.That(result.Entries.Select(entry => entry.AccountId), Is.EqualTo(new[]
            {
                "account-b",
                "account-a",
                "account-c"
            }));
            Assert.That(result.Entries.Select(entry => entry.Rank), Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        private void ConfigureModule(LeaderboardDefinition definition)
        {
            module.Configure(authModule, accessor, definition);
        }

        private TestPeer CreateAuthenticatedPeer(bool isGuest = false)
        {
            TestPeer peer = CreatePeer();
            var account = new TestAccountInfo
            {
                Id = UserId,
                Username = "player-test",
                IsGuest = isGuest
            };
            var user = new UserPeerExtension(peer) { Account = account };
            peer.AddExtension(user);
            authModule.AddLoggedInUser(user);
            return peer;
        }

        private TestPeer CreatePeer()
        {
            var peer = new TestPeer();
            peers.Add(peer);
            return peer;
        }

        private static void GrantRoomPermission(TestPeer peer)
        {
            var security = new SecurityInfoPeerExtension(peer);
            security.GrantPermission(MstPermissionKeys.RoomServer, MstPermissionLevels.RoomServer);
            peer.AddExtension(security);
        }

        private static LeaderboardDefinition CreateDefinition(
            bool serverOnly = true,
            bool allowGuests = true,
            bool keepBest = true,
            LeaderboardSortOrder sortOrder = LeaderboardSortOrder.Descending)
        {
            var definition = new LeaderboardDefinition();
            SetField(definition, "key", LeaderboardKey);
            SetField(definition, "seasonId", SeasonId);
            SetField(definition, "serverOnly", serverOnly);
            SetField(definition, "allowGuests", allowGuests);
            SetField(definition, "keepBest", keepBest);
            SetField(definition, "sortOrder", sortOrder);
            SetField(definition, "title", new List<LeaderboardLocalizedTitle>
            {
                new LeaderboardLocalizedTitle("en", "Survival Time"),
                new LeaderboardLocalizedTitle("ru", "Время выживания")
            });
            return definition;
        }

        private static LeaderboardEntriesPacket CreateEntriesResponse()
        {
            return new LeaderboardEntriesPacket
            {
                Entries = new List<LeaderboardEntry>
                {
                    new LeaderboardEntry
                    {
                        LeaderboardKey = LeaderboardKey,
                        SeasonId = SeasonId,
                        AccountId = UserId,
                        PlayerName = "player-test",
                        Score = 100,
                        Rank = 1,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                },
                TotalEntries = 1,
                CurrentPlayerRank = 1,
                Offset = 0
            };
        }

        private static LeaderboardScoreSubmission CreateSubmission(
            string accountId,
            long score,
            string playerAvatar = null)
        {
            return new LeaderboardScoreSubmission
            {
                LeaderboardKey = LeaderboardKey,
                SeasonId = SeasonId,
                AccountId = accountId,
                PlayerName = accountId,
                PlayerAvatar = playerAvatar,
                Score = score,
                SortOrder = LeaderboardSortOrder.Descending,
                KeepBest = true,
                SubmittedAtUtc = DateTime.UtcNow
            };
        }

        private static IIncomingMessage CreateSubmitRequest(TestPeer peer, long score)
        {
            var packet = new LeaderboardSubmitScorePacket
            {
                Key = LeaderboardKey,
                SeasonId = SeasonId,
                Score = score
            };
            return CreateRequest(MstOpCodes.ClientSubmitLeaderboardScore, peer, packet.ToBytes());
        }

        private static IIncomingMessage CreateServerSubmitRequest(
            TestPeer peer,
            long score,
            string playerName = "",
            string playerAvatar = "")
        {
            var packet = new LeaderboardSubmitScorePacket
            {
                Key = LeaderboardKey,
                SeasonId = SeasonId,
                AccountId = UserId,
                PlayerName = playerName,
                PlayerAvatar = playerAvatar,
                Score = score
            };
            return CreateRequest(MstOpCodes.ServerSubmitLeaderboardScore, peer, packet.ToBytes());
        }

        private static IIncomingMessage CreateRequest(
            ushort opCode, TestPeer peer, byte[] data)
        {
            return new IncomingMessage(opCode, 0, data, DeliveryMethod.Reliable, peer)
            {
                AckResponseId = 1
            };
        }

        private static IOutgoingMessage AssertResponse(
            TestPeer peer, ResponseStatus expectedStatus)
        {
            Assert.That(peer.SentMessages, Is.Not.Empty);
            IOutgoingMessage response = peer.SentMessages.Last();
            Assert.That(response.Status, Is.EqualTo(expectedStatus));
            return response;
        }

        private static T AssertPacketResponse<T>(
            TestPeer peer, ResponseStatus expectedStatus)
            where T : SerializablePacket, new()
        {
            IOutgoingMessage response = AssertResponse(peer, expectedStatus);
            Assert.That(response.HasData, Is.True);
            return SerializablePacket.FromBytes<T>(response.Data);
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found");
            field.SetValue(target, value);
        }

        private sealed class TestLeaderboardsModule : LeaderboardsModule
        {
            public void Configure(
                AuthModule auth,
                ILeaderboardsDatabaseAccessor accessor,
                params LeaderboardDefinition[] definitions)
            {
                authModule = auth;
                databaseAccessor = accessor;
                leaderboards = definitions.ToList();
                CacheDefinitions();
            }

            public Task InvokeGetEntries(IIncomingMessage message)
            {
                return GetEntriesRequestHandler(message, CancellationToken.None);
            }

            public Task InvokeClientSubmit(IIncomingMessage message)
            {
                return ClientSubmitScoreRequestHandler(message, CancellationToken.None);
            }

            public Task InvokeServerSubmit(IIncomingMessage message)
            {
                return ServerSubmitScoreRequestHandler(message, CancellationToken.None);
            }
        }

        private sealed class TestAuthModule : AuthModule
        {
            public void AddLoggedInUser(IUserPeerExtension user)
            {
                loggedInUsers[user.UserId] = user;
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private bool isConnected = true;

            public List<IOutgoingMessage> SentMessages { get; } = new List<IOutgoingMessage>();
            public override bool IsConnected => isConnected;

            public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
            {
                SentMessages.Add(message);
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

        private sealed class TestAccountInfo : IAccountInfoData
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
            public Dictionary<string, string> ExtraProperties { get; set; } =
                new Dictionary<string, string>();

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

        private sealed class InMemoryLeaderboardsAccessor : ILeaderboardsDatabaseAccessor
        {
            private readonly object sync = new object();
            private readonly Dictionary<string, LeaderboardEntry> entries =
                new Dictionary<string, LeaderboardEntry>(StringComparer.Ordinal);

            public MstProperties CustomProperties { get; } = new MstProperties();
            public MasterServerToolkit.Logging.Logger Logger { get; set; }
            public int EntryCount
            {
                get
                {
                    lock (sync)
                        return entries.Count;
                }
            }

            public Task<LeaderboardScoreUpdateResult> SubmitScoreAsync(
                LeaderboardScoreSubmission submission,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (sync)
                {
                    string identity = CreateIdentity(
                        submission.LeaderboardKey, submission.SeasonId, submission.AccountId);
                    bool scoreChanged = false;

                    if (!entries.TryGetValue(identity, out LeaderboardEntry entry))
                    {
                        entry = new LeaderboardEntry
                        {
                            LeaderboardKey = submission.LeaderboardKey,
                            SeasonId = submission.SeasonId,
                            AccountId = submission.AccountId,
                            PlayerName = submission.PlayerName,
                            PlayerAvatar = submission.PlayerAvatar ?? string.Empty,
                            Score = submission.Score,
                            CreatedAtUtc = submission.SubmittedAtUtc,
                            UpdatedAtUtc = submission.SubmittedAtUtc
                        };
                        entry.Normalize();
                        entries.Add(identity, entry);
                        scoreChanged = true;
                    }
                    else
                    {
                        bool isBetter = submission.SortOrder == LeaderboardSortOrder.Descending
                            ? submission.Score > entry.Score
                            : submission.Score < entry.Score;

                        if ((!submission.KeepBest || isBetter) &&
                            entry.Score != submission.Score)
                        {
                            entry.Score = submission.Score;
                            scoreChanged = true;
                        }

                        entry.PlayerName = submission.PlayerName;
                        if (submission.PlayerAvatar != null)
                            entry.PlayerAvatar = submission.PlayerAvatar;
                        entry.UpdatedAtUtc = submission.SubmittedAtUtc;
                        entry.Normalize();
                    }

                    return Task.FromResult(new LeaderboardScoreUpdateResult
                    {
                        Entry = entry.Clone(),
                        ScoreChanged = scoreChanged
                    });
                }
            }

            public Task<LeaderboardEntry> GetEntryAsync(
                string leaderboardKey,
                string seasonId,
                string accountId,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(GetStoredEntry(leaderboardKey, seasonId, accountId));
            }

            public LeaderboardEntry GetStoredEntry(
                string leaderboardKey, string seasonId, string accountId)
            {
                lock (sync)
                {
                    return entries.TryGetValue(CreateIdentity(
                            leaderboardKey, seasonId, accountId), out LeaderboardEntry entry)
                        ? entry.Clone()
                        : null;
                }
            }

            public Task<IReadOnlyList<LeaderboardEntry>> GetEntriesAsync(
                string leaderboardKey,
                string seasonId,
                LeaderboardSortOrder sortOrder,
                int offset,
                int limit,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (sync)
                {
                    IEnumerable<LeaderboardEntry> query = entries.Values.Where(entry =>
                        entry.LeaderboardKey == leaderboardKey && entry.SeasonId == seasonId);
                    query = sortOrder == LeaderboardSortOrder.Descending
                        ? query.OrderByDescending(entry => entry.Score)
                            .ThenBy(entry => entry.AccountId, StringComparer.Ordinal)
                        : query.OrderBy(entry => entry.Score)
                            .ThenBy(entry => entry.AccountId, StringComparer.Ordinal);

                    IReadOnlyList<LeaderboardEntry> result = query
                        .Skip(offset)
                        .Take(limit)
                        .Select(entry => entry.Clone())
                        .ToList();
                    return Task.FromResult(result);
                }
            }

            public Task<long> CountEntriesAsync(
                string leaderboardKey,
                string seasonId,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (sync)
                {
                    long count = entries.Values.LongCount(entry =>
                        entry.LeaderboardKey == leaderboardKey && entry.SeasonId == seasonId);
                    return Task.FromResult(count);
                }
            }

            public Task<long> CountBetterEntriesAsync(
                string leaderboardKey,
                string seasonId,
                LeaderboardSortOrder sortOrder,
                long score,
                string accountId,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (sync)
                {
                    long count = entries.Values.LongCount(entry =>
                        entry.LeaderboardKey == leaderboardKey &&
                        entry.SeasonId == seasonId &&
                        (sortOrder == LeaderboardSortOrder.Descending
                            ? entry.Score > score || entry.Score == score &&
                                string.CompareOrdinal(entry.AccountId, accountId) < 0
                            : entry.Score < score || entry.Score == score &&
                                string.CompareOrdinal(entry.AccountId, accountId) < 0));
                    return Task.FromResult(count);
                }
            }

            public void Dispose()
            {
                lock (sync)
                    entries.Clear();

                CustomProperties.Clear();
            }

            private static string CreateIdentity(
                string leaderboardKey, string seasonId, string accountId)
            {
                return $"{leaderboardKey}\n{seasonId}\n{accountId}";
            }
        }
    }
}
