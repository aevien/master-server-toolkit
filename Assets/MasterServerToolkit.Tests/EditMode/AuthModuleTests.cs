using MasterServerToolkit.Bridges.LiteDB;
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class AuthModuleTests
    {
        private const string UserId = "auth-module-user";
        private const string LegacyPasswordHash =
            "1000:AAECAwQFBgcICQoLDA0ODxAREhMUFRYX:MrEeqN8TgWVtj2QCI2aYlwg0I79t3QYX";
        private const string TestTokenSecret =
            "auth-module-tests-token-secret-with-more-than-32-bytes";
        private const string TestTokenIssuer = "mst-auth-module-tests";
        private const string TestTokenAudience = "mst-auth-module-test-users";

        private readonly List<TestPeer> peers = new List<TestPeer>();
        private GameObject testObject;
        private TestAuthModule authModule;
        private TestMailer mailer;
        private AccountsDatabaseAccessor accountsAccessor;
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            MstTestData.InitializeServerSecurity(Mst.Security);
            testObject = new GameObject(nameof(AuthModuleTests));
            authModule = testObject.AddComponent<TestAuthModule>();
            authModule.InitializeForTests();
            mailer = testObject.AddComponent<TestMailer>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (TestPeer peer in peers)
                peer.Dispose();

            peers.Clear();
            accountsAccessor?.Dispose();
            accountsAccessor = null;
            UnityEngine.Object.DestroyImmediate(testObject);

            if (!string.IsNullOrEmpty(testDirectory) && Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, true);
        }

        [Test]
        public void SignOut_CurrentSession_RemovesMappingPublishesEventAndCleansPeer()
        {
            TestSession currentSession = CreateSession();
            int logoutEventCount = 0;
            IUserPeerExtension loggedOutUser = null;

            authModule.TrackSession(currentSession.User);
            authModule.OnUserLoggedOutEvent += user =>
            {
                logoutEventCount++;
                loggedOutUser = user;
            };

            authModule.SignOut(currentSession.User);

            Assert.That(authModule.GetLoggedInUserById(UserId), Is.Null);
            Assert.That(logoutEventCount, Is.EqualTo(1));
            Assert.That(loggedOutUser, Is.SameAs(currentSession.User));
            AssertPeerWasCleaned(currentSession);

            currentSession.Peer.TriggerConnectionClose();

            Assert.That(authModule.DisconnectListenerCallCount, Is.Zero);
            Assert.That(logoutEventCount, Is.EqualTo(1));
        }

        [Test]
        public void SignOut_StaleOldSession_PreservesCurrentMappingSuppressesEventAndCleansOldPeer()
        {
            TestSession oldSession = CreateSession();
            TestSession currentSession = CreateSession();
            int logoutEventCount = 0;

            authModule.TrackSession(oldSession.User);
            authModule.TrackSession(currentSession.User);
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;

            authModule.SignOut(oldSession.User);

            Assert.That(authModule.GetLoggedInUserById(UserId), Is.SameAs(currentSession.User));
            Assert.That(logoutEventCount, Is.Zero);
            AssertPeerWasCleaned(oldSession);
            Assert.That(currentSession.Security.AccountPermissionLevel, Is.EqualTo(MstPermissionLevels.Admin));
            Assert.That(currentSession.Peer.GetExtension<IUserPeerExtension>(), Is.SameAs(currentSession.User));

            oldSession.Peer.TriggerConnectionClose();

            Assert.That(authModule.DisconnectListenerCallCount, Is.Zero);
            Assert.That(authModule.GetLoggedInUserById(UserId), Is.SameAs(currentSession.User));
            Assert.That(logoutEventCount, Is.Zero);
        }

        [Test]
        public async Task FinalizeSignIn_WhilePreparationIsPending_DoesNotPublishSession()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "pending-session@example.com",
                "password-hash");
            TestPeer peer = CreatePeer();
            authModule.BlockAccountSessionPreparation();

            Task signInTask = authModule.InvokeFinalizeSignIn(
                account,
                CreateMessage(peer));
            IUserPeerExtension sessionDuringPreparation;
            IUserPeerExtension peerExtensionDuringPreparation;
            int responseCountDuringPreparation;

            try
            {
                await WaitForAccountSessionPreparation(authModule);
                sessionDuringPreparation = authModule.GetLoggedInUserById(account.Id);
                peerExtensionDuringPreparation =
                    peer.GetExtension<IUserPeerExtension>();
                responseCountDuringPreparation = peer.SentMessages.Count;
            }
            finally
            {
                authModule.ReleaseAccountSessionPreparation();
            }

            await signInTask;

            Assert.That(sessionDuringPreparation, Is.Null);
            Assert.That(peerExtensionDuringPreparation, Is.Null);
            Assert.That(responseCountDuringPreparation, Is.Zero);
            AssertResponseStatus(peer, ResponseStatus.Success);
            IUserPeerExtension publishedSession =
                peer.GetExtension<IUserPeerExtension>();
            Assert.That(publishedSession, Is.Not.Null);
            Assert.That(
                authModule.GetLoggedInUserById(account.Id),
                Is.SameAs(publishedSession));
        }

        [Test]
        public async Task FinalizeSignIn_WhenPeerDisconnectsDuringPreparation_LeavesNoSession()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "disconnected-pending-session@example.com",
                "password-hash");
            TestPeer peer = CreatePeer();
            int loginEventCount = 0;
            authModule.OnUserLoggedInEvent += (_, __) =>
            {
                loginEventCount++;
                return Task.CompletedTask;
            };
            authModule.BlockAccountSessionPreparation();

            Task signInTask = authModule.InvokeFinalizeSignIn(
                account,
                CreateMessage(peer));

            try
            {
                await WaitForAccountSessionPreparation(authModule);
                peer.TriggerConnectionClose();
            }
            finally
            {
                authModule.ReleaseAccountSessionPreparation();
            }

            await signInTask;

            AssertResponseStatus(peer, ResponseStatus.NotConnected);
            Assert.That(authModule.GetLoggedInUserById(account.Id), Is.Null);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
            Assert.That(authModule.DisconnectListenerCallCount, Is.Zero);
            Assert.That(loginEventCount, Is.Zero);
        }

        [Test]
        public void SignOut_StaleSamePeerSession_PreservesReplacementAndDisconnectListener()
        {
            TestSession oldSession = CreateSession();
            var replacement = new UserPeerExtension(oldSession.Peer)
            {
                Account = oldSession.User.Account
            };
            int logoutEventCount = 0;
            authModule.TrackSession(oldSession.User);
            oldSession.Peer.AddExtension<IUserPeerExtension>(replacement);
            authModule.TrackSession(replacement);
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;

            authModule.SignOut(oldSession.User);

            Assert.That(
                authModule.GetLoggedInUserById(UserId),
                Is.SameAs(replacement));
            Assert.That(
                oldSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(replacement));
            Assert.That(
                oldSession.Security.AccountPermissionLevel,
                Is.EqualTo(MstPermissionLevels.Admin));
            Assert.That(logoutEventCount, Is.Zero);

            oldSession.Peer.TriggerConnectionClose();

            Assert.That(authModule.GetLoggedInUserById(UserId), Is.Null);
            Assert.That(oldSession.Peer.GetExtension<IUserPeerExtension>(), Is.Null);
            Assert.That(authModule.DisconnectListenerCallCount, Is.EqualTo(1));
            Assert.That(logoutEventCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SessionReplacement_WhenTargetDisconnectsDuringPreparation_NewPeerTakesOwnership()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "takeover-target-disconnect@example.com",
                "password-hash");
            TestSession targetSession = CreateSession(account);
            TestPeer replacementPeer = CreatePeer();
            int logoutEventCount = 0;
            authModule.TrackSession(targetSession.User);
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;
            authModule.BlockAccountSessionPreparation();

            Task takeoverTask = authModule.InvokeSessionReplacement(
                account,
                CreateMessage(replacementPeer));

            try
            {
                await WaitForAccountSessionPreparation(authModule);
                targetSession.Peer.TriggerConnectionClose();
            }
            finally
            {
                authModule.ReleaseAccountSessionPreparation();
            }

            await takeoverTask;

            AssertResponseStatus(replacementPeer, ResponseStatus.Success);
            IUserPeerExtension replacement =
                replacementPeer.GetExtension<IUserPeerExtension>();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.UserId, Is.EqualTo(account.Id));
            Assert.That(
                authModule.GetLoggedInUserById(account.Id),
                Is.SameAs(replacement));
            Assert.That(targetSession.Peer.GetExtension<IUserPeerExtension>(), Is.Null);
            Assert.That(authModule.DisconnectListenerCallCount, Is.EqualTo(1));
            Assert.That(logoutEventCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SessionReplacement_StaleTargetDisconnectAfterTakeover_PreservesNewOwner()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "takeover-stale-disconnect@example.com",
                "password-hash");
            TestSession targetSession = CreateSession(account);
            TestPeer replacementPeer = CreatePeer();
            int logoutEventCount = 0;
            authModule.TrackSession(targetSession.User);
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;

            await authModule.InvokeSessionReplacement(
                account,
                CreateMessage(replacementPeer));

            AssertResponseStatus(replacementPeer, ResponseStatus.Success);
            IUserPeerExtension replacement =
                replacementPeer.GetExtension<IUserPeerExtension>();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(
                authModule.GetLoggedInUserById(account.Id),
                Is.SameAs(replacement));
            Assert.That(targetSession.Peer.GetExtension<IUserPeerExtension>(), Is.Null);
            Assert.That(logoutEventCount, Is.EqualTo(1));

            targetSession.Peer.TriggerConnectionClose();

            Assert.That(
                authModule.GetLoggedInUserById(account.Id),
                Is.SameAs(replacement));
            Assert.That(
                replacementPeer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(replacement));
            Assert.That(authModule.DisconnectListenerCallCount, Is.Zero);
            Assert.That(logoutEventCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SessionReplacement_WithEmptyPrecreatedExtension_CompletesInitialSignIn()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "bridge-initial@example.com",
                "password-hash");
            TestPeer peer = CreatePeer();
            peer.AddExtension<IUserPeerExtension>(new UserPeerExtension(peer));
            int loginEventCount = 0;
            int logoutEventCount = 0;
            authModule.OnUserLoggedInEvent += (_, __) =>
            {
                loginEventCount++;
                return Task.CompletedTask;
            };
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;

            await authModule.InvokeSessionReplacement(
                account,
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            IUserPeerExtension signedInUser = peer.GetExtension<IUserPeerExtension>();
            Assert.That(signedInUser, Is.Not.Null);
            Assert.That(signedInUser.Account.Id, Is.EqualTo(account.Id));
            Assert.That(authModule.GetLoggedInUserById(account.Id), Is.SameAs(signedInUser));
            Assert.That(loginEventCount, Is.EqualTo(1));
            Assert.That(logoutEventCount, Is.Zero);
        }

        [Test]
        public async Task SessionReplacement_WithoutCurrentAccount_TakesOverActiveTargetSession()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData targetAccount = await InsertAccount(
                "bridge-initial-takeover@example.com",
                "password-hash");
            TestSession targetSession = CreateSession(targetAccount);
            TestPeer replacementPeer = CreatePeer();
            int loginEventCount = 0;
            int logoutEventCount = 0;
            authModule.TrackSession(targetSession.User);
            authModule.OnUserLoggedInEvent += (_, __) =>
            {
                loginEventCount++;
                return Task.CompletedTask;
            };
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;

            await authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(replacementPeer));

            AssertResponseStatus(replacementPeer, ResponseStatus.Success);
            IUserPeerExtension replacement =
                replacementPeer.GetExtension<IUserPeerExtension>();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.UserId, Is.EqualTo(targetAccount.Id));
            Assert.That(
                authModule.GetLoggedInUserById(targetAccount.Id),
                Is.SameAs(replacement));
            Assert.That(targetSession.Peer.IsConnected, Is.False);
            Assert.That(
                targetSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.Null);
            Assert.That(loginEventCount, Is.EqualTo(1));
            Assert.That(logoutEventCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SessionReplacement_WhenTargetTakeoverIsDisabled_PreservesBothSessions()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData targetAccount = await InsertAccount(
                "password-target-active@example.com",
                "password-hash");
            var guestAccount = new TestAccountInfoData
            {
                Id = "password-current-guest",
                Username = "password-current-guest",
                IsGuest = true,
                ExtraProperties = new Dictionary<string, string>()
            };
            TestSession guestSession = CreateSession(guestAccount);
            TestSession targetSession = CreateSession(targetAccount);
            authModule.TrackSession(guestSession.User);
            authModule.TrackSession(targetSession.User);

            await authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(guestSession.Peer),
                allowTargetSessionTakeover: false);

            AssertResponseStatus(guestSession.Peer, ResponseStatus.DuplicateLogin);
            Assert.That(
                guestSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(guestSession.User));
            Assert.That(
                targetSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(targetSession.User));
            Assert.That(
                authModule.GetLoggedInUserById(guestAccount.Id),
                Is.SameAs(guestSession.User));
            Assert.That(
                authModule.GetLoggedInUserById(targetAccount.Id),
                Is.SameAs(targetSession.User));
            Assert.That(targetSession.Peer.IsConnected, Is.True);
        }

        [Test]
        public async Task SessionReplacement_WithSameAccount_RefreshesExistingSessionWithoutLifecycleEvents()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "bridge-refresh@example.com",
                "password-hash");
            TestSession session = CreateSession(account);
            int loginEventCount = 0;
            int logoutEventCount = 0;
            authModule.TrackSession(session.User);
            authModule.OnUserLoggedInEvent += (_, __) =>
            {
                loginEventCount++;
                return Task.CompletedTask;
            };
            authModule.OnUserLoggedOutEvent += _ => logoutEventCount++;

            await authModule.InvokeSessionReplacement(
                account,
                CreateMessage(session.Peer));

            AssertResponseStatus(session.Peer, ResponseStatus.Success);
            Assert.That(
                session.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(session.User));
            Assert.That(
                authModule.GetLoggedInUserById(account.Id),
                Is.SameAs(session.User));
            Assert.That(session.Peer.IsConnected, Is.True);
            Assert.That(loginEventCount, Is.Zero);
            Assert.That(logoutEventCount, Is.Zero);
        }

        [Test]
        public async Task SessionReplacement_WithDifferentAccount_TransitionsLifecycleInOrder()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData currentAccount = await InsertAccount(
                "bridge-current@example.com",
                "password-hash");
            IAccountInfoData targetAccount = await InsertAccount(
                "bridge-target@example.com",
                "password-hash");
            TestSession session = CreateSession(currentAccount);
            var lifecycle = new List<string>();
            authModule.TrackSession(session.User);
            authModule.OnUserLoggedOutEvent += user =>
                lifecycle.Add($"logout:{user.UserId}");
            authModule.OnUserLoggedInEvent += (user, _) =>
            {
                lifecycle.Add($"login:{user.UserId}");
                return Task.CompletedTask;
            };

            await authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(session.Peer));

            AssertResponseStatus(session.Peer, ResponseStatus.Success);
            IUserPeerExtension replacement =
                session.Peer.GetExtension<IUserPeerExtension>();
            Assert.That(replacement, Is.Not.Null.And.Not.SameAs(session.User));
            Assert.That(replacement.UserId, Is.EqualTo(targetAccount.Id));
            Assert.That(authModule.GetLoggedInUserById(currentAccount.Id), Is.Null);
            Assert.That(
                authModule.GetLoggedInUserById(targetAccount.Id),
                Is.SameAs(replacement));
            Assert.That(
                session.Security.AccountPermissionLevel,
                Is.EqualTo(MstPermissionLevels.Default));
            CollectionAssert.AreEqual(
                new[]
                {
                    $"logout:{currentAccount.Id}",
                    $"login:{targetAccount.Id}"
                },
                lifecycle);
        }

        [Test]
        public async Task SessionReplacement_WhileJoinedToRoom_ReturnsConflictAndPreservesSession()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData currentAccount = await InsertAccount(
                "bridge-in-room@example.com",
                "password-hash");
            IAccountInfoData targetAccount = await InsertAccount(
                "bridge-in-room-target@example.com",
                "password-hash");
            TestSession session = CreateSession(currentAccount);
            session.User.JoinedRoomID = 42;
            authModule.TrackSession(session.User);

            await authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(session.Peer));

            AssertResponseStatus(session.Peer, ResponseStatus.Conflict);
            Assert.That(
                session.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(session.User));
            Assert.That(
                authModule.GetLoggedInUserById(currentAccount.Id),
                Is.SameAs(session.User));
            Assert.That(authModule.GetLoggedInUserById(targetAccount.Id), Is.Null);
            Assert.That(session.Peer.IsConnected, Is.True);
        }

        [Test]
        public async Task SessionReplacement_WhenTargetIsJoinedToRoom_ReturnsConflictAndPreservesTarget()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData targetAccount = await InsertAccount(
                "bridge-target-in-room@example.com",
                "password-hash");
            TestSession targetSession = CreateSession(targetAccount);
            targetSession.User.JoinedRoomID = 73;
            TestPeer replacementPeer = CreatePeer();
            authModule.TrackSession(targetSession.User);

            await authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(replacementPeer));

            AssertResponseStatus(replacementPeer, ResponseStatus.Conflict);
            Assert.That(
                authModule.GetLoggedInUserById(targetAccount.Id),
                Is.SameAs(targetSession.User));
            Assert.That(
                targetSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(targetSession.User));
            Assert.That(targetSession.Peer.IsConnected, Is.True);
            Assert.That(
                replacementPeer.GetExtension<IUserPeerExtension>(),
                Is.Null);
        }

        [Test]
        public async Task SessionReplacement_WhenTargetHasAnotherSession_MovesOwnershipAndDisconnectsOldPeer()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData currentAccount = await InsertAccount(
                "bridge-takeover-current@example.com",
                "password-hash");
            IAccountInfoData targetAccount = await InsertAccount(
                "bridge-takeover-target@example.com",
                "password-hash");
            TestSession currentSession = CreateSession(currentAccount);
            TestSession targetSession = CreateSession(targetAccount);
            var lifecycle = new List<string>();
            authModule.TrackSession(currentSession.User);
            authModule.TrackSession(targetSession.User);
            authModule.OnUserLoggedOutEvent += user =>
                lifecycle.Add($"logout:{user.UserId}");
            authModule.OnUserLoggedInEvent += (user, _) =>
            {
                lifecycle.Add($"login:{user.UserId}");
                return Task.CompletedTask;
            };

            await authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(currentSession.Peer));

            AssertResponseStatus(currentSession.Peer, ResponseStatus.Success);
            IUserPeerExtension replacement =
                currentSession.Peer.GetExtension<IUserPeerExtension>();
            Assert.That(replacement.UserId, Is.EqualTo(targetAccount.Id));
            Assert.That(
                authModule.GetLoggedInUserById(targetAccount.Id),
                Is.SameAs(replacement));
            Assert.That(authModule.GetLoggedInUserById(currentAccount.Id), Is.Null);
            Assert.That(targetSession.Peer.IsConnected, Is.False);
            Assert.That(
                targetSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.Null);
            Assert.That(
                targetSession.Security.AccountPermissionLevel,
                Is.EqualTo(MstPermissionLevels.Default));
            CollectionAssert.AreEqual(
                new[]
                {
                    $"logout:{currentAccount.Id}",
                    $"logout:{targetAccount.Id}",
                    $"login:{targetAccount.Id}"
                },
                lifecycle);
        }

        [Test]
        public async Task SessionReplacement_ConcurrentTakeovers_SerializeLifecycleAndLeaveOneOwner()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData firstAccount = await InsertAccount(
                "bridge-race-first@example.com",
                "password-hash");
            IAccountInfoData secondAccount = await InsertAccount(
                "bridge-race-second@example.com",
                "password-hash");
            IAccountInfoData targetAccount = await InsertAccount(
                "bridge-race-target@example.com",
                "password-hash");
            TestSession firstSession = CreateSession(firstAccount);
            TestSession secondSession = CreateSession(secondAccount);
            var firstLoginEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstLogin =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int loginEventCount = 0;
            authModule.TrackSession(firstSession.User);
            authModule.TrackSession(secondSession.User);
            authModule.OnUserLoggedInEvent += async (_, __) =>
            {
                if (Interlocked.Increment(ref loginEventCount) != 1)
                    return;

                firstLoginEntered.TrySetResult(true);
                await releaseFirstLogin.Task;
            };

            Task firstTakeover = authModule.InvokeSessionReplacement(
                targetAccount,
                CreateMessage(firstSession.Peer));
            Task secondTakeover = null;

            try
            {
                Task firstLoginSignal = await Task.WhenAny(
                    firstLoginEntered.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));

                Assert.That(
                    firstLoginSignal,
                    Is.SameAs(firstLoginEntered.Task),
                    "First takeover did not reach the login lifecycle");

                secondTakeover = authModule.InvokeSessionReplacement(
                    targetAccount,
                    CreateMessage(secondSession.Peer));
                await Task.Yield();
                Assert.That(secondTakeover.IsCompleted, Is.False);
            }
            finally
            {
                releaseFirstLogin.TrySetResult(true);
            }

            Assert.That(secondTakeover, Is.Not.Null);
            await Task.WhenAll(firstTakeover, secondTakeover);

            AssertResponseStatus(firstSession.Peer, ResponseStatus.Success);
            AssertResponseStatus(secondSession.Peer, ResponseStatus.Success);
            Assert.That(firstSession.Peer.IsConnected, Is.False);
            Assert.That(secondSession.Peer.IsConnected, Is.True);
            Assert.That(
                authModule.GetLoggedInUserById(targetAccount.Id).Peer,
                Is.SameAs(secondSession.Peer));
            Assert.That(authModule.GetLoggedInUserById(firstAccount.Id), Is.Null);
            Assert.That(authModule.GetLoggedInUserById(secondAccount.Id), Is.Null);
            Assert.That(loginEventCount, Is.EqualTo(2));
        }

        [Test]
        public void LogoutLifecycle_WhenSubscriberThrows_ContinuesWithRemainingSubscribers()
        {
            TestSession session = CreateSession();
            int successfulSubscriberCalls = 0;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            authModule.OnUserLoggedOutEvent += _ =>
                throw new InvalidOperationException("Expected logout subscriber failure");
            authModule.OnUserLoggedOutEvent += _ => successfulSubscriberCalls++;

            try
            {
                authModule.NotifyOnUserLoggedOutEvent(session.User);

                Assert.That(successfulSubscriberCalls, Is.EqualTo(1));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void ProductionTokenConfiguration_WithKnownDefaultSecret_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => authModule.ValidateProductionTokenConfiguration("t0k9n-$ecr9t"));
        }

        [Test]
        public void ProductionTokenConfiguration_WithStrongSecret_IsAccepted()
        {
            Assert.DoesNotThrow(
                () => authModule.ValidateProductionTokenConfiguration(
                    "production-token-secret-with-more-than-32-bytes"));
        }

        [Test]
        public async Task TokenSignIn_WithCurrentToken_RotatesStoredAndReturnedToken()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "current-token@example.com",
                "password-hash");
            string currentToken = await authModule.IssueToken(account);
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(currentToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            AccountInfoPacket responseAccount =
                SerializablePacket.FromBytes<AccountInfoPacket>(peer.SentMessages[0].Data);
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            Assert.That(responseAccount.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(responseAccount.Token, Does.StartWith("v2."));
            Assert.That(responseAccount.Token, Is.Not.EqualTo(currentToken));
            Assert.That(storedAccount.Token, Is.EqualTo(responseAccount.Token));
        }

        [Test]
        public async Task TokenSignIn_WithValidLegacyToken_RotatesToVersionTwo()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "legacy-token@example.com",
                "password-hash");
            string legacyToken = CreateLegacySignedToken(
                account.Id,
                account.Username,
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(legacyToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            AccountInfoPacket responseAccount =
                SerializablePacket.FromBytes<AccountInfoPacket>(peer.SentMessages[0].Data);
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            Assert.That(responseAccount.Token, Does.StartWith("v2."));
            Assert.That(responseAccount.Token, Is.Not.EqualTo(legacyToken));
            Assert.That(storedAccount.Token, Is.EqualTo(responseAccount.Token));
        }

        [Test]
        public async Task TokenSignIn_WithPreviouslyReplacedToken_ResolvesSignedAccountAndRotatesAgain()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "previous-token@example.com",
                "password-hash");
            string previousToken = await authModule.IssueToken(account);
            string replacementToken = await authModule.IssueToken(account);
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(previousToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            AccountInfoPacket responseAccount =
                SerializablePacket.FromBytes<AccountInfoPacket>(peer.SentMessages[0].Data);
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            Assert.That(responseAccount.Token, Is.Not.EqualTo(previousToken));
            Assert.That(responseAccount.Token, Is.Not.EqualTo(replacementToken));
            Assert.That(storedAccount.Token, Is.EqualTo(responseAccount.Token));
        }

        [Test]
        public async Task TokenSignIn_AfterRevisionIncrement_ReturnsTokenExpired()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "revoked-token@example.com",
                "password-hash");
            string token = await authModule.IssueToken(account);
            await accountsAccessor.IncrementAuthTokenRevisionAsync(account.Id);
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(token),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.TokenExpired);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task ChangePassword_RevokesIssuedTokensAndClearsStoredToken()
        {
            ConfigureTokenAuthentication();
            const string email = "password-change@example.com";
            const string resetCode = "123456";
            const string newPassword = "new-secure-password";
            IAccountInfoData account = await InsertAccount(email, "old-password-hash");
            string token = await authModule.IssueToken(account);
            await accountsAccessor.SavePasswordResetCodeAsync(
                email,
                resetCode,
                DateTime.UtcNow.AddMinutes(10),
                5);
            var changeData = new MstProperties();
            changeData.Add(MstParamKeys.RESET_PASSWORD_EMAIL, email);
            changeData.Add(MstParamKeys.RESET_PASSWORD_CODE, resetCode);
            changeData.Add(MstParamKeys.RESET_PASSWORD, newPassword);
            TestPeer changePeer = CreatePeer();

            await authModule.InvokeChangePassword(
                CreateMessage(
                    changePeer,
                    MstOpCodes.ChangePassword,
                    changeData.ToBytes()));

            IAccountInfoData storedAccount = await accountsAccessor.GetAccountByIdAsync(account.Id);
            AssertResponseStatus(changePeer, ResponseStatus.Success);
            Assert.That(string.IsNullOrEmpty(storedAccount.Token), Is.True);
            Assert.That(
                Mst.Security.ValidatePassword(newPassword, storedAccount.Password),
                Is.True);
            Assert.That(
                await accountsAccessor.GetAuthTokenRevisionAsync(account.Id),
                Is.EqualTo(1));

            TestPeer signInPeer = CreatePeer();
            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(token),
                CreateMessage(signInPeer));

            AssertResponseStatus(signInPeer, ResponseStatus.TokenExpired);
        }

        [Test]
        public async Task TokenSignIn_WithExpiredToken_ReturnsTokenExpired()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "expired-token@example.com",
                "password-hash");
            string expiredToken = CreateLegacySignedToken(
                account.Id,
                account.Username,
                DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds());
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(expiredToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.TokenExpired);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task TokenSignIn_WithForgedSignature_ReturnsTokenExpired()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "forged-token@example.com",
                "password-hash");
            string validToken = await authModule.IssueToken(account);
            int signatureIndex = validToken.IndexOf('.') + 1;
            char replacement = validToken[signatureIndex] == 'A' ? 'B' : 'A';
            string forgedToken = validToken.Substring(0, signatureIndex) +
                replacement +
                validToken.Substring(signatureIndex + 1);
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(forgedToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.TokenExpired);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task TokenSignIn_WhenSignedUsernameDoesNotMatchAccount_ReturnsInvalid()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "identity-token@example.com",
                "password-hash");
            string mismatchedToken = CreateLegacySignedToken(
                account.Id,
                "different-username",
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
            await accountsAccessor.InsertOrUpdateTokenAsync(
                account,
                mismatchedToken);
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(mismatchedToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Invalid);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task TokenSignIn_WhenSignedAccountDoesNotExist_ReturnsInvalid()
        {
            ConfigureTokenAuthentication();
            string token = CreateLegacySignedToken(
                "missing-account",
                "missing-user",
                DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(token),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Invalid);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task TokenSignIn_WhenAccountIsBlocked_DoesNotRotateToken()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "blocked-token@example.com",
                "password-hash");
            string currentToken = await authModule.IssueToken(account);
            IAccountBlockData block = accountsAccessor.CreateAccountBlockInstance();
            block.AccountId = account.Id;
            block.BlockReason = "Token sign-in test";
            block.BlockedUntil = DateTime.UtcNow.AddDays(1);
            await accountsAccessor.InsertAccountBlockAsync(block);
            TestPeer peer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(currentToken),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Banned);
            MstProperties error = MstProperties.FromBytes(peer.SentMessages[0].Data);
            Assert.That(error.AsString(MstErrorPropertyKeys.CODE),
                Is.EqualTo(MstErrorCodes.ACCOUNT_BLOCKED));
            Assert.That(error.AsString(MstErrorPropertyKeys.REASON),
                Is.EqualTo(block.BlockReason));
            Assert.That(error.AsString(MstErrorPropertyKeys.EXPIRES_AT), Is.Not.Empty);
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            Assert.That(storedAccount.Token, Is.EqualTo(currentToken));
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task TokenSignIn_WhenAccountIsAlreadySignedIn_ReturnsDuplicateWithoutSecondRotation()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData account = await InsertAccount(
                "duplicate-token@example.com",
                "password-hash");
            string originalToken = await authModule.IssueToken(account);
            TestPeer firstPeer = CreatePeer();
            TestPeer secondPeer = CreatePeer();

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(originalToken),
                CreateMessage(firstPeer));
            IAccountInfoData accountAfterFirstSignIn =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            string rotatedToken = accountAfterFirstSignIn.Token;

            await authModule.InvokeTokenSignIn(
                CreateTokenCredentials(originalToken),
                CreateMessage(secondPeer));

            AssertResponseStatus(firstPeer, ResponseStatus.Success);
            AssertResponseStatus(secondPeer, ResponseStatus.DuplicateLogin);
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            Assert.That(rotatedToken, Is.Not.EqualTo(originalToken));
            Assert.That(storedAccount.Token, Is.EqualTo(rotatedToken));
            Assert.That(secondPeer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task SignUp_WhenPeerAuthenticationIsAlreadyActive_ReturnsDuplicateLogin()
        {
            TestPeer peer = CreatePeer();

            using (IDisposable reservation = authModule.AcquirePeerAuthOperationForTest(peer.Id))
            {
                Assert.That(reservation, Is.Not.Null);
                await authModule.InvokeSignUp(CreateMessage(peer));
            }

            AssertResponseStatus(peer, ResponseStatus.DuplicateLogin);
        }

        [Test]
        public async Task SignUp_FromGuestSession_UpgradesSameAccountAndReturnsAuthenticatedPacket()
        {
            ConfigureTokenAuthentication();
            IAccountInfoData guestAccount = accountsAccessor.CreateAccountInstance();
            guestAccount.Username = "guest-before-registration";
            guestAccount.Email = "guest-before-registration@example.com";
            guestAccount.IsGuest = true;
            guestAccount.ExtraProperties = new Dictionary<string, string>();
            await accountsAccessor.InsertAccountAsync(guestAccount);
            TestSession guestSession = CreateSession(guestAccount);
            authModule.TrackSession(guestSession.User);

            await authModule.InvokeSignUp(
                CreateSignUpMessage(
                    guestSession.Peer,
                    "new_player",
                    "registered-player@example.com"));

            AssertResponseStatus(guestSession.Peer, ResponseStatus.Success);
            AccountInfoPacket responseAccount =
                SerializablePacket.FromBytes<AccountInfoPacket>(
                    guestSession.Peer.SentMessages[0].Data);
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(guestAccount.Id);
            IUserPeerExtension currentUser =
                guestSession.Peer.GetExtension<IUserPeerExtension>();

            Assert.That(responseAccount.Id, Is.EqualTo(guestAccount.Id));
            Assert.That(responseAccount.IsGuest, Is.False);
            Assert.That(responseAccount.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(currentUser, Is.SameAs(guestSession.User));
            Assert.That(currentUser.Account.Id, Is.EqualTo(guestAccount.Id));
            Assert.That(storedAccount.Username, Is.EqualTo("new_player"));
            Assert.That(storedAccount.IsGuest, Is.False);
            Assert.That(storedAccount.Token, Is.EqualTo(responseAccount.Token));
        }

        [Test]
        public async Task SignUp_FromUnauthenticatedPeer_ReturnsAccountAndAttachesSession()
        {
            ConfigureTokenAuthentication();
            TestPeer peer = CreatePeer();

            await authModule.InvokeSignUp(
                CreateSignUpMessage(
                    peer,
                    "new_player",
                    "new-player@example.com"));

            AssertResponseStatus(peer, ResponseStatus.Success);
            AccountInfoPacket responseAccount =
                SerializablePacket.FromBytes<AccountInfoPacket>(
                    peer.SentMessages[0].Data);
            IUserPeerExtension currentUser = peer.GetExtension<IUserPeerExtension>();
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(responseAccount.Id);

            Assert.That(responseAccount.IsGuest, Is.False);
            Assert.That(responseAccount.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(currentUser, Is.Not.Null);
            Assert.That(currentUser.Account.Id, Is.EqualTo(responseAccount.Id));
            Assert.That(storedAccount.Token, Is.EqualTo(responseAccount.Token));
        }

        [Test]
        public async Task PasswordSignIn_WhenPasswordExceedsConfiguredLimit_RejectsBeforeDatabaseWork()
        {
            TestPeer peer = CreatePeer();
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_NAME, "user");
            credentials.Add(MstParamKeys.USER_PASSWORD, new string('x', 1025));

            await authModule.InvokePasswordSignIn(credentials, CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Invalid);
        }

        [Test]
        public async Task PasswordSignIn_FromGuestSession_ReplacesGuestAndIssuesRememberMeToken()
        {
            ConfigureTokenAuthentication();
            const string password = "guest-replacement-password";
            IAccountInfoData account = await InsertAccount(
                "guest-replacement@example.com",
                Mst.Security.CreateHash(password),
                "guest-user");
            var guestAccount = new TestAccountInfoData
            {
                Id = "password-guest-session",
                Username = "password-guest-session",
                IsGuest = true,
                ExtraProperties = new Dictionary<string, string>()
            };
            TestSession guestSession = CreateSession(guestAccount);
            authModule.TrackSession(guestSession.User);
            var credentials = new MstProperties
            {
                { MstParamKeys.USER_NAME, account.Username },
                { MstParamKeys.USER_PASSWORD, password },
                { MstParamKeys.USER_REMEMBER_ME, true }
            };

            await authModule.InvokePasswordSignIn(
                credentials,
                CreateMessage(guestSession.Peer));

            AssertResponseStatus(guestSession.Peer, ResponseStatus.Success);
            IUserPeerExtension signedInUser =
                guestSession.Peer.GetExtension<IUserPeerExtension>();
            IAccountInfoData storedAccount =
                await accountsAccessor.GetAccountByIdAsync(account.Id);
            Assert.That(signedInUser, Is.Not.Null);
            Assert.That(signedInUser.Account.Id, Is.EqualTo(account.Id));
            Assert.That(authModule.GetLoggedInUserById(guestAccount.Id), Is.Null);
            Assert.That(authModule.GetLoggedInUserById(account.Id), Is.SameAs(signedInUser));
            Assert.That(storedAccount.Token, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task PasswordSignIn_FromNonGuestSession_RemainsRejected()
        {
            ConfigureTokenAuthentication();
            const string password = "ordinary-account-password";
            IAccountInfoData targetAccount = await InsertAccount(
                "ordinary-target@example.com",
                Mst.Security.CreateHash(password),
                "ordinary-target-user");
            TestSession currentSession = CreateSession(new TestAccountInfoData
            {
                Id = "ordinary-current-session",
                Username = "ordinary-current-session",
                IsGuest = false,
                ExtraProperties = new Dictionary<string, string>()
            });
            authModule.TrackSession(currentSession.User);
            var credentials = new MstProperties
            {
                { MstParamKeys.USER_NAME, targetAccount.Username },
                { MstParamKeys.USER_PASSWORD, password },
                { MstParamKeys.USER_REMEMBER_ME, true }
            };

            await authModule.InvokePasswordSignIn(
                credentials,
                CreateMessage(currentSession.Peer));

            AssertResponseStatus(currentSession.Peer, ResponseStatus.DuplicateLogin);
            Assert.That(
                currentSession.Peer.GetExtension<IUserPeerExtension>(),
                Is.SameAs(currentSession.User));
            Assert.That(authModule.GetLoggedInUserById(targetAccount.Id), Is.Null);
        }

        [Test]
        public async Task EmailSignInRequest_ExistingAccount_DoesNotChangePassword()
        {
            ConfigureEmailSignIn();
            const string email = "existing@example.com";
            const string passwordHash = "existing-password-hash";
            IAccountInfoData account = await InsertAccount(email, passwordHash);
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));

            IAccountInfoData storedAccount = await accountsAccessor.GetAccountByIdAsync(account.Id);
            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(storedAccount.Password, Is.EqualTo(passwordHash));
            Assert.That(mailer.LastRecipient, Is.EqualTo(email));
            Assert.That(mailer.LastCode, Has.Length.EqualTo(6));
        }

        [Test]
        public async Task ConfirmEmailSignIn_ExistingAccount_SignsInWithoutChangingPassword()
        {
            ConfigureEmailSignIn();
            const string email = "confirmed@example.com";
            const string passwordHash = "preserved-password-hash";
            IAccountInfoData account = await InsertAccount(email, passwordHash);
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string code = mailer.LastCode;
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, code),
                CreateMessage(peer));

            IAccountInfoData storedAccount = await accountsAccessor.GetAccountByIdAsync(account.Id);
            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(storedAccount.Password, Is.EqualTo(passwordHash));
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Not.Null);
            Assert.That(peer.GetExtension<IUserPeerExtension>().UserId, Is.EqualTo(account.Id));
        }

        [Test]
        public async Task ConfirmEmailSignIn_UnknownEmail_CreatesAccountOnlyAfterValidCode()
        {
            ConfigureEmailSignIn();
            const string email = "new-player@example.com";
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string code = mailer.LastCode;

            Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Null);
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, code),
                CreateMessage(peer));

            IAccountInfoData account = await accountsAccessor.GetAccountByEmailAsync(email);
            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(account, Is.Not.Null);
            Assert.That(account.Username, Is.EqualTo(email));
            Assert.That(account.IsEmailConfirmed, Is.True);
            Assert.That(account.IsGuest, Is.False);
        }

        [Test]
        public async Task ConfirmEmailSignIn_WhenAttemptLimitIsReached_ConsumesChallenge()
        {
            ConfigureEmailSignIn(maxAttempts: 2);
            const string email = "attempts@example.com";
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string validCode = mailer.LastCode;
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, "000000"),
                CreateMessage(peer));
            AssertResponseStatus(peer, ResponseStatus.Invalid);
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, "111111"),
                CreateMessage(peer));
            AssertResponseStatus(peer, ResponseStatus.Invalid);
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, validCode),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Invalid);
            Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Null);
        }

        [Test]
        public async Task EmailSignInRequest_WhenChallengeIsReplaced_OldCodeCannotSignIn()
        {
            ConfigureEmailSignIn();
            authModule.SetEmailOperationCooldown(0);
            const string email = "replacement@example.com";
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string oldCode = mailer.LastCode;
            string currentCode = oldCode;

            for (int attempt = 0; attempt < 5 && currentCode == oldCode; attempt++)
            {
                peer.SentMessages.Clear();
                await authModule.RequestEmailSignIn(
                    CreateEmailCredentials(email),
                    CreateMessage(peer));
                currentCode = mailer.LastCode;
            }

            Assert.That(currentCode, Is.Not.EqualTo(oldCode));
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, oldCode),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Invalid);
            Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Null);
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, currentCode),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Not.Null);
        }

        [Test]
        public async Task EmailSignInRequest_WhenMailerFails_AllowsImmediateRetry()
        {
            ConfigureEmailSignIn();
            const string email = "retry@example.com";
            TestPeer peer = CreatePeer();
            mailer.SendResult = false;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                await authModule.RequestEmailSignIn(
                    CreateEmailCredentials(email),
                    CreateMessage(peer));

                AssertResponseStatus(peer, ResponseStatus.Error);
                Assert.That(mailer.SendCount, Is.EqualTo(1));
                peer.SentMessages.Clear();
                mailer.SendResult = true;

                await authModule.RequestEmailSignIn(
                    CreateEmailCredentials(email),
                    CreateMessage(peer));

                AssertResponseStatus(peer, ResponseStatus.Success);
                Assert.That(mailer.SendCount, Is.EqualTo(2));
                Assert.That(mailer.LastCode, Has.Length.EqualTo(6));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public async Task EmailSignInRequest_WhenMailerThrows_AllowsImmediateRetry()
        {
            ConfigureEmailSignIn();
            const string email = "mailer-exception@example.com";
            TestPeer peer = CreatePeer();
            mailer.ExceptionToThrow = new InvalidOperationException("Expected mailer failure");
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                await authModule.RequestEmailSignIn(
                    CreateEmailCredentials(email),
                    CreateMessage(peer));

                AssertResponseStatus(peer, ResponseStatus.Error);
                Assert.That(mailer.SendCount, Is.EqualTo(1));
                peer.SentMessages.Clear();
                mailer.ExceptionToThrow = null;

                await authModule.RequestEmailSignIn(
                    CreateEmailCredentials(email),
                    CreateMessage(peer));

                AssertResponseStatus(peer, ResponseStatus.Success);
                Assert.That(mailer.SendCount, Is.EqualTo(2));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public async Task EmailSignInRequest_WhenReplacementMailFails_PreservesActiveCode()
        {
            ConfigureEmailSignIn();
            authModule.SetEmailOperationCooldown(0);
            const string email = "preserved-code@example.com";
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string activeCode = mailer.LastCode;
            peer.SentMessages.Clear();
            mailer.SendResult = false;
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                await authModule.RequestEmailSignIn(
                    CreateEmailCredentials(email),
                    CreateMessage(peer));

                AssertResponseStatus(peer, ResponseStatus.Error);
                peer.SentMessages.Clear();
                mailer.SendResult = true;

                await authModule.InvokeConfirmEmailSignIn(
                    CreateEmailCredentials(email, activeCode),
                    CreateMessage(peer));

                AssertResponseStatus(peer, ResponseStatus.Success);
                Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Not.Null);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public async Task ConfirmEmailSignIn_WhenEmailBecomesConfirmed_PublishesEventOnce()
        {
            ConfigureEmailSignIn();
            const string email = "event@example.com";
            TestPeer peer = CreatePeer();
            int confirmationEventCount = 0;
            IAccountInfoData confirmedAccount = null;
            authModule.OnUserEmailConfirmedEvent += account =>
            {
                confirmationEventCount++;
                confirmedAccount = account;
            };

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string code = mailer.LastCode;
            peer.SentMessages.Clear();

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, code),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(confirmationEventCount, Is.EqualTo(1));
            Assert.That(confirmedAccount, Is.Not.Null);
            Assert.That(confirmedAccount.Email, Is.EqualTo(email));
        }

        [Test]
        public async Task PasswordSignIn_WhenTwoPeersRace_OnlyOneOwnsAccount()
        {
            ConfigureEmailSignIn();
            const string email = "race@example.com";
            const string username = "raceuser";
            const string password = "race-password";
            await InsertAccount(email, Mst.Security.CreateHash(password), username);
            TestPeer firstPeer = CreatePeer();
            TestPeer secondPeer = CreatePeer();
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_NAME, username);
            credentials.Add(MstParamKeys.USER_PASSWORD, password);
            credentials.Add(MstParamKeys.USER_REMEMBER_ME, true);
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                await Task.WhenAll(
                    authModule.InvokePasswordSignIn(credentials, CreateMessage(firstPeer)),
                    authModule.InvokePasswordSignIn(credentials, CreateMessage(secondPeer)));

                ResponseStatus firstStatus = firstPeer.SentMessages[0].Status;
                ResponseStatus secondStatus = secondPeer.SentMessages[0].Status;
                int successCount =
                    (firstStatus == ResponseStatus.Success ? 1 : 0) +
                    (secondStatus == ResponseStatus.Success ? 1 : 0);
                int duplicateCount =
                    (firstStatus == ResponseStatus.DuplicateLogin ? 1 : 0) +
                    (secondStatus == ResponseStatus.DuplicateLogin ? 1 : 0);
                int attachedExtensionCount =
                    (firstPeer.GetExtension<IUserPeerExtension>() != null ? 1 : 0) +
                    (secondPeer.GetExtension<IUserPeerExtension>() != null ? 1 : 0);

                Assert.That(successCount, Is.EqualTo(1));
                Assert.That(duplicateCount, Is.EqualTo(1));
                Assert.That(attachedExtensionCount, Is.EqualTo(1));

                TestPeer successfulPeer =
                    firstStatus == ResponseStatus.Success ? firstPeer : secondPeer;
                AccountInfoPacket responseAccount =
                    SerializablePacket.FromBytes<AccountInfoPacket>(
                        successfulPeer.SentMessages[0].Data);
                IAccountInfoData storedAccount =
                    await accountsAccessor.GetAccountByEmailAsync(email);

                Assert.That(responseAccount.Token, Is.Not.Null.And.Not.Empty);
                Assert.That(storedAccount.Token, Is.EqualTo(responseAccount.Token));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public async Task SignUp_WhenSameUsernameRacesAcrossEmails_CreatesOnlyOneAccount()
        {
            ConfigureEmailSignIn();
            const string username = "sameuser";
            TestPeer firstPeer = CreatePeer();
            TestPeer secondPeer = CreatePeer();
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                await Task.WhenAll(
                    authModule.InvokeSignUp(
                        CreateSignUpMessage(
                            firstPeer,
                            username,
                            "first@example.com")),
                    authModule.InvokeSignUp(
                        CreateSignUpMessage(
                            secondPeer,
                            username,
                            "second@example.com")));

                ResponseStatus firstStatus = firstPeer.SentMessages[0].Status;
                ResponseStatus secondStatus = secondPeer.SentMessages[0].Status;
                int successCount =
                    (firstStatus == ResponseStatus.Success ? 1 : 0) +
                    (secondStatus == ResponseStatus.Success ? 1 : 0);
                int alreadyExistsCount =
                    (firstStatus == ResponseStatus.AlreadyExists ? 1 : 0) +
                    (secondStatus == ResponseStatus.AlreadyExists ? 1 : 0);

                Assert.That(successCount, Is.EqualTo(1));
                Assert.That(alreadyExistsCount, Is.EqualTo(1));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public async Task PasswordSignIn_WithLegacyHash_UpgradesHashAfterValidation()
        {
            ConfigureEmailSignIn();
            const string email = "legacy@example.com";
            const string username = "legacyuser";
            IAccountInfoData account = await InsertAccount(
                email,
                LegacyPasswordHash,
                username);
            TestPeer peer = CreatePeer();
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_NAME, username);
            credentials.Add(MstParamKeys.USER_PASSWORD, "legacy-password");

            await authModule.InvokePasswordSignIn(credentials, CreateMessage(peer));

            IAccountInfoData storedAccount = await accountsAccessor.GetAccountByIdAsync(account.Id);
            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(storedAccount.Password, Does.StartWith("v2$"));
            Assert.That(Mst.Security.ValidatePassword("legacy-password", storedAccount.Password), Is.True);
        }

        [Test]
        public void PasswordHashOperations_WhenCapacityIsReached_RejectsAdditionalWork()
        {
            IDisposable firstOperation = null;
            IDisposable secondOperation = null;

            try
            {
                firstOperation = authModule.AcquirePasswordHashOperationForTest();
                secondOperation = authModule.AcquirePasswordHashOperationForTest();

                Assert.That(firstOperation, Is.Not.Null);
                Assert.That(secondOperation, Is.Not.Null);
                Assert.That(authModule.AcquirePasswordHashOperationForTest(), Is.Null);
            }
            finally
            {
                secondOperation?.Dispose();
                firstOperation?.Dispose();
            }

            using IDisposable recoveredOperation = authModule.AcquirePasswordHashOperationForTest();
            Assert.That(recoveredOperation, Is.Not.Null);
        }

        [Test]
        public async Task PasswordSignIn_WhenHashCapacityIsUnavailable_ReturnsServiceUnavailable()
        {
            ConfigureEmailSignIn();
            const string email = "busy-login@example.com";
            const string username = "busyuser";
            const string password = "busy-password";
            await InsertAccount(email, Mst.Security.CreateHash(password), username);
            TestPeer peer = CreatePeer();
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_NAME, username);
            credentials.Add(MstParamKeys.USER_PASSWORD, password);
            authModule.RejectPasswordHashOperations = true;

            await authModule.InvokePasswordSignIn(credentials, CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.ServiceUnavailable);
            Assert.That(peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        [Test]
        public async Task ConfirmEmailSignIn_WhenHashCapacityIsUnavailable_PreservesChallenge()
        {
            ConfigureEmailSignIn();
            const string email = "busy-email-confirm@example.com";
            TestPeer peer = CreatePeer();

            await authModule.RequestEmailSignIn(
                CreateEmailCredentials(email),
                CreateMessage(peer));
            string code = mailer.LastCode;
            peer.SentMessages.Clear();
            authModule.RejectPasswordHashOperations = true;

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, code),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.ServiceUnavailable);
            Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Null);
            peer.SentMessages.Clear();
            authModule.RejectPasswordHashOperations = false;

            await authModule.InvokeConfirmEmailSignIn(
                CreateEmailCredentials(email, code),
                CreateMessage(peer));

            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(await accountsAccessor.GetAccountByEmailAsync(email), Is.Not.Null);
        }

        private void ConfigureEmailSignIn(int maxAttempts = 5)
        {
            testDirectory = Path.Combine(
                Path.GetTempPath(),
                "mst_auth_tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            accountsAccessor = new AccountsDatabaseAccessor(Path.Combine(testDirectory, "accounts"));
            authModule.ConfigureEmailSignIn(accountsAccessor, mailer, maxAttempts);
        }

        private void ConfigureTokenAuthentication()
        {
            ConfigureEmailSignIn();
            authModule.ConfigureTokenAuthentication(
                accountsAccessor,
                TestTokenSecret,
                TestTokenIssuer,
                TestTokenAudience);
        }

        private async Task<IAccountInfoData> InsertAccount(
            string email,
            string passwordHash,
            string username = null)
        {
            IAccountInfoData account = accountsAccessor.CreateAccountInstance();
            account.Username = username ?? email;
            account.Email = email;
            account.Password = passwordHash;
            account.IsGuest = false;
            account.IsEmailConfirmed = true;
            await accountsAccessor.InsertAccountAsync(account);
            return account;
        }

        private static MstProperties CreateTokenCredentials(string token)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_AUTH_TOKEN, token);
            return credentials;
        }

        private static string CreateLegacySignedToken(
            string accountId,
            string username,
            long expiresAtUnixTimeSeconds)
        {
            MstJson tokenJson = MstJson.CreateArray();
            tokenJson.Add(accountId);
            tokenJson.Add(username);
            tokenJson.Add(expiresAtUnixTimeSeconds);
            tokenJson.Add(TestTokenIssuer);
            tokenJson.Add(TestTokenAudience);

            string encryptedToken =
                MstLegacyTokenCrypto.Encrypt(tokenJson.ToString(), TestTokenSecret);
            string signature;

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestTokenSecret)))
            {
                signature = Convert.ToBase64String(
                    hmac.ComputeHash(Encoding.UTF8.GetBytes(encryptedToken)));
            }

            return $"{encryptedToken}.{signature}";
        }

        private TestPeer CreatePeer()
        {
            var peer = new TestPeer();
            peers.Add(peer);
            return peer;
        }

        private static MstProperties CreateEmailCredentials(string email, string code = null)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_EMAIL, email);

            if (code != null)
                credentials.Add(MstParamKeys.USER_EMAIL_SIGN_IN_CODE, code);

            return credentials;
        }

        private static IIncomingMessage CreateMessage(TestPeer peer)
        {
            return CreateMessage(peer, MstOpCodes.SignIn, Array.Empty<byte>());
        }

        private static IIncomingMessage CreateMessage(
            TestPeer peer,
            ushort opCode,
            byte[] data)
        {
            return new IncomingMessage(
                opCode,
                0,
                data,
                DeliveryMethod.Reliable,
                peer)
            {
                AckResponseId = 1
            };
        }

        private static IIncomingMessage CreateSignUpMessage(
            TestPeer peer,
            string username,
            string email)
        {
            var credentials = new MstProperties();
            credentials.Add(MstParamKeys.USER_NAME, username);
            credentials.Add(MstParamKeys.USER_PASSWORD, "test-password");
            credentials.Add(MstParamKeys.USER_EMAIL, email);

            return new IncomingMessage(
                MstOpCodes.SignUp,
                0,
                MstTestData.CreateSealedPayload(
                    Mst.Security,
                    peer,
                    credentials.ToBytes(),
                    MstSecurityPurposes.AuthSignUp,
                    MstOpCodes.SignUp),
                DeliveryMethod.Reliable,
                peer)
            {
                AckResponseId = 1
            };
        }

        private static void AssertResponseStatus(TestPeer peer, ResponseStatus expectedStatus)
        {
            Assert.That(peer.SentMessages.Count, Is.EqualTo(1));
            Assert.That(peer.SentMessages[0].Status, Is.EqualTo(expectedStatus));
        }

        private static async Task WaitForAccountSessionPreparation(TestAuthModule module)
        {
            Task preparationEntered = module.AccountSessionPreparationEntered;
            Task completedTask = await Task.WhenAny(
                preparationEntered,
                Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.That(
                completedTask,
                Is.SameAs(preparationEntered),
                "Authentication did not reach account session preparation");
        }

        private TestSession CreateSession()
        {
            return CreateSession(
                new TestAccountInfoData
                {
                    Id = UserId,
                    Username = UserId,
                    ExtraProperties = new Dictionary<string, string>()
                });
        }

        private TestSession CreateSession(IAccountInfoData account)
        {
            TestPeer peer = CreatePeer();
            var security = new SecurityInfoPeerExtension(peer);
            var user = new UserPeerExtension(peer)
            {
                Account = account
            };

            security.GrantPermission(MstPermissionKeys.Default, MstPermissionLevels.Default);
            security.GrantPermission(MstPermissionKeys.RoomServer, MstPermissionLevels.RoomServer);
            security.SetAccountPermissionLevel(MstPermissionLevels.Admin);
            peer.AddExtension(security);
            peer.AddExtension<IUserPeerExtension>(user);

            return new TestSession(peer, user, security);
        }

        private static void AssertPeerWasCleaned(TestSession session)
        {
            Assert.That(session.Security.AccountPermissionLevel, Is.EqualTo(MstPermissionLevels.Default));
            Assert.That(session.Security.HasPermission(MstPermissionKeys.Default), Is.True);
            Assert.That(session.Security.HasPermission(MstPermissionKeys.RoomServer), Is.True);
            Assert.That(session.Peer.GetExtension<IUserPeerExtension>(), Is.Null);
        }

        private sealed class TestSession
        {
            public TestSession(TestPeer peer, IUserPeerExtension user, SecurityInfoPeerExtension security)
            {
                Peer = peer;
                User = user;
                Security = security;
            }

            public TestPeer Peer { get; }
            public IUserPeerExtension User { get; }
            public SecurityInfoPeerExtension Security { get; }
        }

        public sealed class TestAuthModule : AuthModule
        {
            private TaskCompletionSource<bool> accountSessionPreparationEntered;
            private TaskCompletionSource<bool> releaseAccountSessionPreparation;

            public int DisconnectListenerCallCount { get; private set; }
            public bool RejectPasswordHashOperations { get; set; }
            public Task AccountSessionPreparationEntered =>
                accountSessionPreparationEntered?.Task ?? Task.CompletedTask;

            public void InitializeForTests()
            {
                logger = Mst.Create.Logger(nameof(TestAuthModule));
            }

            public void ConfigureEmailSignIn(
                IAccountsDatabaseAccessor accessor,
                Mailer emailMailer,
                int maxAttempts)
            {
                databaseAccessor = accessor;
                mailer = emailMailer;
                emailSignInMaxAttempts = maxAttempts;
                emailSignInCodeLifetimeMinutes = 10;
                emailOperationCooldownSeconds = 60;
                verificationCodeLifetimeMinutes = 10;
                verificationCodeMaxAttempts = 5;
            }

            public void ValidateProductionTokenConfiguration(string secret)
            {
                tokenSecret = secret;
                tokenExpiresInDays = 7;
                tokenIssuer = "mst-test";
                tokenAudience = "mst-test-users";
                ValidateTokenConfiguration(false);
            }

            public void ConfigureTokenAuthentication(
                IAccountsDatabaseAccessor accessor,
                string secret,
                string issuer,
                string audience)
            {
                databaseAccessor = accessor;
                tokenSecret = secret;
                tokenExpiresInDays = 7;
                tokenIssuer = issuer;
                tokenAudience = audience;
            }

            public Task<string> IssueToken(
                IAccountInfoData account,
                CancellationToken cancellationToken = default)
            {
                return CreateAccountToken(account, cancellationToken);
            }

            public Task InvokeTokenSignIn(
                MstProperties credentials,
                IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return SignInWithToken(credentials, message, cancellationToken);
            }

            public Task InvokeSessionReplacement(
                IAccountInfoData account,
                IIncomingMessage message,
                CancellationToken cancellationToken = default,
                bool allowTargetSessionTakeover = true)
            {
                return FinalizeSingInWithSessionReplacement(
                    account,
                    message,
                    cancellationToken,
                    allowTargetSessionTakeover: allowTargetSessionTakeover);
            }

            public Task InvokeFinalizeSignIn(
                IAccountInfoData account,
                IIncomingMessage message,
                bool createToken = false,
                CancellationToken cancellationToken = default)
            {
                return FinalizeSingIn(
                    account,
                    message,
                    createToken,
                    cancellationToken);
            }

            public void BlockAccountSessionPreparation()
            {
                accountSessionPreparationEntered =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                releaseAccountSessionPreparation =
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public void ReleaseAccountSessionPreparation()
            {
                releaseAccountSessionPreparation?.TrySetResult(true);
            }

            public Task RequestEmailSignIn(
                MstProperties credentials,
                IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return SignInWithEmail(credentials, message, cancellationToken);
            }

            public Task InvokeConfirmEmailSignIn(
                MstProperties credentials,
                IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return base.ConfirmEmailSignIn(credentials, message, cancellationToken);
            }

            public Task InvokePasswordSignIn(
                MstProperties credentials,
                IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return SignInWithLoginAndPassword(credentials, message, cancellationToken);
            }

            public Task InvokeChangePassword(
                IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return ChangePasswordMessageHandler(message, cancellationToken);
            }

            public Task InvokeSignUp(
                IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return SignUpMessageHandler(message, cancellationToken);
            }

            public void SetEmailOperationCooldown(int seconds)
            {
                emailOperationCooldownSeconds = seconds;
            }

            public IDisposable AcquirePasswordHashOperationForTest()
            {
                return base.TryAcquirePasswordHashOperation();
            }

            public IDisposable AcquirePeerAuthOperationForTest(int peerId)
            {
                return base.TryAcquirePeerAuthOperation(peerId);
            }

            public void TrackSession(IUserPeerExtension user)
            {
                user.Peer.OnConnectionCloseEvent -= OnUserDisconnectedEventListener;
                user.Peer.OnConnectionCloseEvent += OnUserDisconnectedEventListener;
                loggedInUsers[user.UserId] = user;
            }

            protected override void OnUserDisconnectedEventListener(IPeer peer)
            {
                DisconnectListenerCallCount++;
                base.OnUserDisconnectedEventListener(peer);
            }

            protected override async Task PrepareAccountSessionAsync(
                IAccountInfoData account,
                bool createToken,
                CancellationToken cancellationToken)
            {
                TaskCompletionSource<bool> preparationEntered =
                    accountSessionPreparationEntered;
                TaskCompletionSource<bool> preparationRelease =
                    releaseAccountSessionPreparation;

                if (preparationEntered != null && preparationRelease != null)
                {
                    preparationEntered.TrySetResult(true);
                    await preparationRelease.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await base.PrepareAccountSessionAsync(
                    account,
                    createToken,
                    cancellationToken);
            }

            protected override IDisposable TryAcquirePasswordHashOperation()
            {
                return RejectPasswordHashOperations
                    ? null
                    : base.TryAcquirePasswordHashOperation();
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private bool isConnected = true;

            public List<IOutgoingMessage> SentMessages { get; } = new List<IOutgoingMessage>();
            public override bool IsConnected => isConnected;

            public void TriggerConnectionClose()
            {
                isConnected = false;
                NotifyConnectionCloseEvent(0);
            }

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

        public sealed class TestMailer : Mailer
        {
            public string LastRecipient { get; private set; }
            public string LastCode { get; private set; }
            public bool SendResult { get; set; } = true;
            public int SendCount { get; private set; }
            public Exception ExceptionToThrow { get; set; }

            public override Task<bool> SendMailAsync(
                string to,
                string subject,
                string body,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SendCount++;

                if (ExceptionToThrow != null)
                    throw ExceptionToThrow;

                LastRecipient = to;
                LastCode = ExtractCode(body);
                return Task.FromResult(SendResult);
            }

            private static string ExtractCode(string body)
            {
                const string startTag = "<h1>";
                const string endTag = "</h1>";
                int startIndex = body.IndexOf(startTag, StringComparison.Ordinal);

                if (startIndex < 0)
                    return string.Empty;

                startIndex += startTag.Length;
                int endIndex = body.IndexOf(endTag, startIndex, StringComparison.Ordinal);
                return endIndex < 0
                    ? string.Empty
                    : body.Substring(startIndex, endIndex - startIndex);
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
