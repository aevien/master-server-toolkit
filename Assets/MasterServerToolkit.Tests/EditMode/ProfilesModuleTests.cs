using MasterServerToolkit.DebounceThrottle;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ProfilesModuleTests
    {
        private const ushort PropertyKey = 701;
        private const int InitialValue = 10;
        private const int RestoredValue = 42;
        private const string UserId = "profile-user";

        private readonly List<TestPeer> peers = new List<TestPeer>();
        private GameObject testObject;
        private TestAuthModule authModule;
        private TestProfilesModule profilesModule;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(ProfilesModuleTests));
            authModule = testObject.AddComponent<TestAuthModule>();
            profilesModule = testObject.AddComponent<TestProfilesModule>();
            profilesModule.InitializeForTests();
            profilesModule.Configure(authModule, 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (profilesModule != null)
            {
                foreach (ObservableServerProfile profile in profilesModule.Profiles)
                    profile.Dispose();
            }

            foreach (TestPeer peer in peers)
                peer.Dispose();

            if (testObject != null)
                UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public async Task Login_RestoresBeforeLoadedNotificationsAndProfileExtensionPublication()
        {
            var steps = new List<string>();
            var database = new ControlledProfilesDatabaseAccessor(steps);
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            int loadedEventCount = 0;
            int valueAtLoadedEvent = 0;
            bool extensionPublishedAtLoadedEvent = true;
            var loadedEvent = new TaskCompletionSource<ObservableServerProfile>();

            authModule.MarkLoggedIn(user);
            profilesModule.DatabaseAccessor = database;
            profilesModule.LifecycleSteps = steps;
            profilesModule.OnProfileLoaded += profile =>
            {
                steps.Add("event");
                loadedEventCount++;
                valueAtLoadedEvent = profile.Get<ObservableInt>(PropertyKey).Value;
                extensionPublishedAtLoadedEvent = peer.HasExtension<ProfilePeerExtension>();
                loadedEvent.TrySetResult(profile);
            };

            profilesModule.TriggerUserLoggedIn(user);
            await AwaitCompletion(database.RestoreStarted.Task);

            Assert.That(database.RestoreCallCount, Is.EqualTo(1));
            Assert.That(profilesModule.ProfileLoadedCallCount, Is.Zero);
            Assert.That(loadedEventCount, Is.Zero);
            Assert.That(peer.HasExtension<ProfilePeerExtension>(), Is.False);

            database.CompleteRestore();
            await AwaitCompletion(loadedEvent.Task);
            await AwaitCondition(() => peer.HasExtension<ProfilePeerExtension>());

            CollectionAssert.AreEqual(new[] { "restore", "hook", "event" }, steps);
            Assert.That(profilesModule.ProfileLoadedCallCount, Is.EqualTo(1));
            Assert.That(profilesModule.ValueAtProfileLoaded, Is.EqualTo(RestoredValue));
            Assert.That(profilesModule.ExtensionPublishedAtProfileLoaded, Is.False);
            Assert.That(loadedEventCount, Is.EqualTo(1));
            Assert.That(valueAtLoadedEvent, Is.EqualTo(RestoredValue));
            Assert.That(extensionPublishedAtLoadedEvent, Is.False);
            Assert.That(peer.GetExtension<ProfilePeerExtension>().Profile, Is.SameAs(loadedEvent.Task.Result));
        }

        [Test]
        public async Task Login_WhileRestoreIsPending_DoesNotExposeProfileOrAnswerServerFill()
        {
            var database = new ControlledProfilesDatabaseAccessor(new List<string>());
            TestPeer clientPeer = CreatePeer();
            IUserPeerExtension user = AddUser(clientPeer);
            TestPeer serverPeer = CreatePeer();
            GrantServerPermission(serverPeer);
            var loadedEvent = new TaskCompletionSource<ObservableServerProfile>();

            authModule.MarkLoggedIn(user);
            profilesModule.DatabaseAccessor = database;
            profilesModule.OnProfileLoaded += profile => loadedEvent.TrySetResult(profile);
            Task login = profilesModule.InvokeUserLoggedIn(user);
            await AwaitCompletion(database.RestoreStarted.Task);

            var visibleProfilesBeforeReady = new List<ObservableServerProfile>(profilesModule.Profiles);
            ObservableServerProfile profileByIdBeforeReady = profilesModule.GetProfileByUserId(UserId);
            IIncomingMessage message = CreateMessage(MstOpCodes.ServerFillInProfileValues, serverPeer, UserId);
            Task request = profilesModule.InvokeServerFill(message);
            bool requestCompletedBeforeReady = request.IsCompleted;
            int responseCountBeforeReady = serverPeer.SentMessages.Count;

            database.CompleteRestore();
            await AwaitCompletion(login);
            await AwaitCompletion(loadedEvent.Task);
            await AwaitCompletion(request);

            Assert.That(visibleProfilesBeforeReady, Is.Empty);
            Assert.That(profileByIdBeforeReady, Is.Null);
            Assert.That(requestCompletedBeforeReady, Is.False);
            Assert.That(responseCountBeforeReady, Is.Zero);
            AssertResponseStatus(serverPeer, ResponseStatus.Success);
        }

        [Test]
        public async Task ConcurrentLogins_ForSameUser_ShareRestoreAndPublishOnlyCurrentPeerWhenReady()
        {
            var database = new ControlledProfilesDatabaseAccessor(new List<string>());
            TestPeer firstPeer = CreatePeer();
            IUserPeerExtension firstUser = AddUser(firstPeer);
            TestPeer currentPeer = CreatePeer();
            IUserPeerExtension currentUser = AddUser(currentPeer);
            var loadedEvent = new TaskCompletionSource<ObservableServerProfile>();

            profilesModule.DatabaseAccessor = database;
            profilesModule.OnProfileLoaded += profile => loadedEvent.TrySetResult(profile);
            authModule.MarkLoggedIn(firstUser);
            Task firstLogin = profilesModule.InvokeUserLoggedIn(firstUser);
            await AwaitCompletion(database.RestoreStarted.Task);

            authModule.MarkLoggedIn(currentUser);
            Task currentLogin = profilesModule.InvokeUserLoggedIn(currentUser);
            int restoreCallsBeforeReady = database.RestoreCallCount;
            bool firstExtensionPublishedBeforeReady = firstPeer.HasExtension<ProfilePeerExtension>();
            bool currentExtensionPublishedBeforeReady = currentPeer.HasExtension<ProfilePeerExtension>();

            database.CompleteRestore();
            await AwaitCompletion(Task.WhenAll(firstLogin, currentLogin));
            await AwaitCompletion(loadedEvent.Task);

            ObservableServerProfile profile = currentPeer.GetExtension<ProfilePeerExtension>().Profile;
            Assert.That(restoreCallsBeforeReady, Is.EqualTo(1));
            Assert.That(database.RestoreCallCount, Is.EqualTo(1));
            Assert.That(firstExtensionPublishedBeforeReady, Is.False);
            Assert.That(currentExtensionPublishedBeforeReady, Is.False);
            Assert.That(firstPeer.HasExtension<ProfilePeerExtension>(), Is.False);
            Assert.That(profile.ClientPeer, Is.SameAs(currentPeer));
            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.SameAs(profile));
        }

        [Test]
        public async Task Logout_DuringRestore_DoesNotPublishProfileExtensionOrLoadedEvent()
        {
            var database = new ControlledProfilesDatabaseAccessor(new List<string>());
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            int loadedEventCount = 0;

            authModule.MarkLoggedIn(user);
            profilesModule.DatabaseAccessor = database;
            profilesModule.OnProfileLoaded += _ => loadedEventCount++;
            Task login = profilesModule.InvokeUserLoggedIn(user);
            await AwaitCompletion(database.RestoreStarted.Task);

            authModule.MarkLoggedOut(UserId);
            profilesModule.TriggerUserLoggedOut(user);
            database.CompleteRestore();
            await AwaitCompletion(login);

            Assert.That(profilesModule.IsProfileLoadInProgress(UserId), Is.False);
            Assert.That(profilesModule.Profiles, Is.Empty);
            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.Null);
            Assert.That(peer.HasExtension<ProfilePeerExtension>(), Is.False);
            Assert.That(profilesModule.ProfileLoadedCallCount, Is.Zero);
            Assert.That(loadedEventCount, Is.Zero);
        }

        [Test]
        public async Task Login_AfterThreeRestoreFailures_ClearsStateAndAllowsSubsequentRestore()
        {
            var database = new ControlledProfilesDatabaseAccessor(new List<string>(), 3);
            TestPeer failedPeer = CreatePeer();
            IUserPeerExtension failedUser = AddUser(failedPeer);
            int loadedEventCount = 0;

            authModule.MarkLoggedIn(failedUser);
            profilesModule.DatabaseAccessor = database;
            profilesModule.OnProfileLoaded += _ => loadedEventCount++;
            ExpectErrorLog("Attempt 1/3 failed");
            ExpectErrorLog("Attempt 2/3 failed");
            ExpectErrorLog("Attempt 3/3 failed");
            ExpectErrorLog($"Failed to load profile for user {UserId} after 3 attempts");

            Task failedLogin = profilesModule.InvokeUserLoggedIn(failedUser);
            await AwaitCompletion(database.PlannedFailuresCompleted);
            await AwaitCompletion(failedLogin);

            Assert.That(database.RestoreCallCount, Is.EqualTo(3));
            Assert.That(profilesModule.IsProfileLoadInProgress(UserId), Is.False);
            Assert.That(profilesModule.Profiles, Is.Empty);
            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.Null);
            Assert.That(failedPeer.HasExtension<ProfilePeerExtension>(), Is.False);
            Assert.That(loadedEventCount, Is.Zero);

            TestPeer successfulPeer = CreatePeer();
            IUserPeerExtension successfulUser = AddUser(successfulPeer);
            var loadedEvent = new TaskCompletionSource<ObservableServerProfile>();
            profilesModule.OnProfileLoaded += profile => loadedEvent.TrySetResult(profile);
            authModule.MarkLoggedIn(successfulUser);
            database.CompleteRestore();

            Task successfulLogin = profilesModule.InvokeUserLoggedIn(successfulUser);
            await AwaitCompletion(successfulLogin);
            await AwaitCompletion(loadedEvent.Task);

            ObservableServerProfile profile = successfulPeer.GetExtension<ProfilePeerExtension>().Profile;
            Assert.That(database.RestoreCallCount, Is.EqualTo(4));
            Assert.That(profile.Get<ObservableInt>(PropertyKey).Value, Is.EqualTo(RestoredValue));
            Assert.That(profile.ClientPeer, Is.SameAs(successfulPeer));
            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.SameAs(profile));
            Assert.That(loadedEventCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SavePendingProfiles_WhenNewerSaveIsQueuedInFlight_RetainsItForSecondBatch()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false, false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(profile);

            Task firstSave = profilesModule.InvokeSavePendingProfiles();
            IReadOnlyList<ObservableServerProfile> firstBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(0));

            Assert.That(firstSave.IsCompleted, Is.False);
            Assert.That(firstBatch, Has.Count.EqualTo(1));
            Assert.That(firstBatch[0], Is.SameAs(profile));

            profilesModule.QueueProfileSave(profile);
            database.CompleteUpdate(0);
            await AwaitCompletion(firstSave);

            Task secondSave = profilesModule.InvokeSavePendingProfiles();
            IReadOnlyList<ObservableServerProfile> secondBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(1));

            Assert.That(secondBatch, Has.Count.EqualTo(1));
            Assert.That(secondBatch[0], Is.SameAs(profile));

            database.CompleteUpdate(1);
            await AwaitCompletion(secondSave);
            await AwaitCompletion(profilesModule.InvokeSavePendingProfiles());

            Assert.That(database.UpdateCallCount, Is.EqualTo(2));
            profile.Dispose();
        }

        [Test]
        public async Task SavePendingProfiles_WhenSecondBatchStartsInParallel_DoesNotPersistSameSnapshotTwice()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);

            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(profile);

            Task firstSave = profilesModule.InvokeSavePendingProfiles();
            await AwaitCompletion(database.GetUpdateStartedTask(0));
            Task parallelSave = profilesModule.InvokeSavePendingProfiles();

            Assert.That(parallelSave.IsCompleted, Is.False);

            database.CompleteUpdate(0);
            await AwaitCompletion(Task.WhenAll(firstSave, parallelSave));

            Assert.That(database.UpdateCallCount, Is.EqualTo(1));
            Assert.That(profilesModule.PendingSaveCount, Is.Zero);
            profile.Dispose();
        }

        [Test]
        public async Task SavePendingProfiles_WhenSaveFails_RetainsProfileForRetry()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(true, false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(profile);
            ExpectErrorLog("Error saving profiles.");

            Task failedSave = profilesModule.InvokeSavePendingProfiles();
            IReadOnlyList<ObservableServerProfile> failedBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(0));

            Assert.That(failedBatch, Has.Count.EqualTo(1));
            Assert.That(failedBatch[0], Is.SameAs(profile));

            database.CompleteUpdate(0);
            await AwaitCompletion(failedSave);

            Task retrySave = profilesModule.InvokeSavePendingProfiles();
            IReadOnlyList<ObservableServerProfile> retryBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(1));

            Assert.That(retryBatch, Has.Count.EqualTo(1));
            Assert.That(retryBatch[0], Is.SameAs(profile));

            database.CompleteUpdate(1);
            await AwaitCompletion(retrySave);
            await AwaitCompletion(profilesModule.InvokeSavePendingProfiles());

            Assert.That(database.UpdateCallCount, Is.EqualTo(2));
            profile.Dispose();
        }

        [Test]
        public async Task SavePendingProfiles_WhenNewerProfileInstanceIsQueued_DoesNotRemoveItOnStaleCompletion()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false, false);
            ObservableServerProfile staleProfile = CreateProfile(UserId, InitialValue);
            ObservableServerProfile newerProfile = CreateProfile(UserId, RestoredValue);
            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(staleProfile);

            Task staleSave = profilesModule.InvokeSavePendingProfiles();
            IReadOnlyList<ObservableServerProfile> staleBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(0));

            Assert.That(staleSave.IsCompleted, Is.False);
            Assert.That(staleBatch, Has.Count.EqualTo(1));
            Assert.That(staleBatch[0], Is.SameAs(staleProfile));

            profilesModule.QueueProfileSave(newerProfile);
            database.CompleteUpdate(0);
            await AwaitCompletion(staleSave);

            Task newerSave = profilesModule.InvokeSavePendingProfiles();
            IReadOnlyList<ObservableServerProfile> newerBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(1));

            Assert.That(newerBatch, Has.Count.EqualTo(1));
            Assert.That(newerBatch[0], Is.SameAs(newerProfile));

            database.CompleteUpdate(1);
            await AwaitCompletion(newerSave);
            await AwaitCompletion(profilesModule.InvokeSavePendingProfiles());

            Assert.That(database.UpdateCallCount, Is.EqualTo(2));
            staleProfile.Dispose();
            newerProfile.Dispose();
        }

        [Test]
        public async Task UnloadProfile_RemovesProfileOnlyAfterSuccessfulSave()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false);
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            profile.UnloadDebounceDispatcher = new DebounceDispatcher(0);
            int unloadedEventCount = 0;

            profilesModule.DatabaseAccessor = database;
            profilesModule.AddLoadedProfile(profile);
            profilesModule.OnProfileUnloaded += _ => unloadedEventCount++;
            authModule.MarkLoggedIn(user);
            authModule.MarkLoggedOut(UserId);

            profilesModule.TriggerUserLoggedOut(user);
            await AwaitCondition(() => profilesModule.PendingSaveCount == 1);

            Task save = profilesModule.InvokeSavePendingProfiles();
            await AwaitCompletion(database.GetUpdateStartedTask(0));

            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.SameAs(profile));
            Assert.That(unloadedEventCount, Is.Zero);

            database.CompleteUpdate(0);
            await AwaitCompletion(save);

            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.Null);
            Assert.That(unloadedEventCount, Is.EqualTo(1));
            profile.Dispose();
        }

        [Test]
        public async Task UnloadProfile_WhenNewerSaveIsQueuedInFlight_WaitsForLatestSave()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false, false);
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            profile.UnloadDebounceDispatcher = new DebounceDispatcher(0);
            int unloadedEventCount = 0;

            profilesModule.DatabaseAccessor = database;
            profilesModule.AddLoadedProfile(profile);
            profilesModule.OnProfileUnloaded += _ => unloadedEventCount++;
            authModule.MarkLoggedIn(user);
            authModule.MarkLoggedOut(UserId);

            profilesModule.TriggerUserLoggedOut(user);
            await AwaitCondition(() => profilesModule.PendingSaveCount == 1);

            Task firstSave = profilesModule.InvokeSavePendingProfiles();
            await AwaitCompletion(database.GetUpdateStartedTask(0));

            profilesModule.QueueProfileSave(profile);
            database.CompleteUpdate(0);
            await AwaitCompletion(firstSave);

            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.SameAs(profile));
            Assert.That(profilesModule.PendingSaveCount, Is.EqualTo(1));
            Assert.That(unloadedEventCount, Is.Zero);

            Task latestSave = profilesModule.InvokeSavePendingProfiles();
            await AwaitCompletion(database.GetUpdateStartedTask(1));
            database.CompleteUpdate(1);
            await AwaitCompletion(latestSave);

            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.Null);
            Assert.That(profilesModule.PendingSaveCount, Is.Zero);
            Assert.That(unloadedEventCount, Is.EqualTo(1));
            profile.Dispose();
        }

        [Test]
        public async Task LogoutUnload_FromStoppedRun_DoesNotQueueSaveAfterRestart()
        {
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            profile.UnloadDebounceDispatcher = new DebounceDispatcher(100);

            profilesModule.AddLoadedProfile(profile);
            authModule.MarkLoggedIn(user);
            authModule.MarkLoggedOut(UserId);

            profilesModule.TriggerUserLoggedOut(user);
            await profilesModule.StopServerRunAsync();

            profilesModule.TriggerUserLoggedOut(user);
            profilesModule.StartServerRun(CancellationToken.None);
            await Task.Delay(200);

            Assert.That(profilesModule.PendingSaveCount, Is.Zero);
            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.SameAs(profile));
        }

        [Test]
        public async Task UnloadProfile_WhenUserReconnectsDuringSave_KeepsAndAttachesCurrentProfile()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false);
            TestPeer oldPeer = CreatePeer();
            IUserPeerExtension oldUser = AddUser(oldPeer);
            TestPeer currentPeer = CreatePeer();
            IUserPeerExtension currentUser = AddUser(currentPeer);
            ObservableServerProfile profile = CreateProfile(UserId, RestoredValue);
            profile.UnloadDebounceDispatcher = new DebounceDispatcher(0);
            int unloadedEventCount = 0;

            profilesModule.DatabaseAccessor = database;
            profilesModule.AddLoadedProfile(profile);
            profilesModule.OnProfileUnloaded += _ => unloadedEventCount++;
            authModule.MarkLoggedIn(oldUser);
            authModule.MarkLoggedOut(UserId);

            profilesModule.TriggerUserLoggedOut(oldUser);
            await AwaitCondition(() => profilesModule.PendingSaveCount == 1);

            Task save = profilesModule.InvokeSavePendingProfiles();
            await AwaitCompletion(database.GetUpdateStartedTask(0));

            authModule.MarkLoggedIn(currentUser);
            await AwaitCompletion(profilesModule.InvokeUserLoggedIn(currentUser));

            Assert.That(currentPeer.GetExtension<ProfilePeerExtension>().Profile, Is.SameAs(profile));

            database.CompleteUpdate(0);
            await AwaitCompletion(save);

            Assert.That(profilesModule.GetProfileByUserId(UserId), Is.SameAs(profile));
            Assert.That(profile.ClientPeer, Is.SameAs(currentPeer));
            Assert.That(unloadedEventCount, Is.Zero);
        }

        [Test]
        public async Task ClientFill_WhenUserExtensionIsMissing_ReturnsUnauthorized()
        {
            TestPeer peer = CreatePeer();
            IIncomingMessage message = CreateMessage(MstOpCodes.ClientFillInProfileValues, peer);
            ExpectErrorLog("User is not logged in");

            await AwaitCompletion(profilesModule.InvokeClientFill(message));

            AssertResponseStatus(peer, ResponseStatus.Unauthorized);
        }

        [Test]
        public async Task ClientFill_WhenUserLogsOutWhileWaiting_ReturnsUnauthorized()
        {
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ClientFillInProfileValues, peer);
            ExpectErrorLog("User logged out during profile fetch");

            Task request = profilesModule.InvokeClientFill(message);
            Assert.That(request.IsCompleted, Is.False);
            authModule.MarkLoggedOut(UserId);
            await AwaitCompletion(request);

            AssertResponseStatus(peer, ResponseStatus.Unauthorized);
        }

        [Test]
        public async Task ClientFill_WhenProfileWaitExpires_ReturnsTimeout()
        {
            profilesModule.SetProfileLoadTimeout(0);
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ClientFillInProfileValues, peer);
            ExpectErrorLog($"Profile fetch timeout for user {UserId}");

            await AwaitCompletion(profilesModule.InvokeClientFill(message));

            AssertResponseStatus(peer, ResponseStatus.Timeout);
        }

        [Test]
        public async Task ClientFill_WhenServerRunIsCancelled_DoesNotRespondOrAttachProfile()
        {
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ClientFillInProfileValues, peer);
            ObservableServerProfile profile = CreateProfile(UserId, RestoredValue);
            using var cancellation = new CancellationTokenSource();

            Task request = profilesModule.InvokeClientFill(message, cancellation.Token);
            Assert.That(request.IsCompleted, Is.False);

            cancellation.Cancel();
            await AssertOperationCanceled(request);
            peer.AddExtension(new ProfilePeerExtension(profile, peer));

            Assert.That(peer.SentMessages, Is.Empty);
            Assert.That(profile.ClientPeer, Is.Null);
            profile.Dispose();
        }

        [Test]
        public async Task SaveProfileAsync_WhenPendingSaveIsRunning_WaitsForItToFinish()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false, false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(profile);

            Task pendingSave = profilesModule.InvokeSavePendingProfiles();
            await AwaitCompletion(database.GetUpdateStartedTask(0));

            Task confirmedSave = profilesModule.SaveProfileAsync(profile);

            Assert.That(database.UpdateCallCount, Is.EqualTo(1));
            Assert.That(confirmedSave.IsCompleted, Is.False);

            database.CompleteUpdate(0);
            await AwaitCompletion(pendingSave);
            await AwaitCompletion(database.GetUpdateStartedTask(1));

            Assert.That(database.UpdateCallCount, Is.EqualTo(2));

            database.CompleteUpdate(1);
            await AwaitCompletion(confirmedSave);
            profile.Dispose();
        }

        [Test]
        public async Task StopServerRunAsync_WhenConfirmedSaveIsActive_WaitsForItBeforeCompleting()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);

            profilesModule.DatabaseAccessor = database;
            Task confirmedSave = profilesModule.SaveProfileAsync(profile);
            await AwaitCompletion(database.GetUpdateStartedTask(0));

            Task stopTask = profilesModule.StopServerRunAsync();
            Assert.That(stopTask.IsCompleted, Is.False);

            database.CompleteUpdate(0);
            await AwaitCompletion(Task.WhenAll(confirmedSave, stopTask));

            Assert.That(database.UpdateCallCount, Is.EqualTo(1));
            Assert.That(profilesModule.PendingSaveCount, Is.Zero);
            profile.Dispose();
        }

        [Test]
        public async Task StopServerRunAsync_AfterUnityObjectDestroyed_WaitsForActiveSaveAndFlushesLatestQueue()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false, false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);

            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(profile);
            profilesModule.StartServerRun(CancellationToken.None);
            profilesModule.InvokeUpdateThrottle();
            await AwaitCompletion(database.GetUpdateStartedTask(0));

            profilesModule.QueueProfileSave(profile);

            IServerRunModule runModule = profilesModule;
            UnityEngine.Object.DestroyImmediate(testObject);
            testObject = null;
            authModule = null;
            profilesModule = null;

            Task stopTask = runModule.StopServerRunAsync();
            Assert.That(stopTask.IsCompleted, Is.False);

            database.CompleteUpdate(0);
            IReadOnlyList<ObservableServerProfile> finalBatch =
                await AwaitCompletion(database.GetUpdateStartedTask(1));

            Assert.That(finalBatch, Has.Count.EqualTo(1));
            Assert.That(finalBatch[0], Is.SameAs(profile));
            Assert.That(stopTask.IsCompleted, Is.False);

            database.CompleteUpdate(1);
            await AwaitCompletion(stopTask);

            Assert.That(database.UpdateCallCount, Is.EqualTo(2));
            profile.Dispose();
        }

        [Test]
        public async Task StopServerRunAsync_WhenFinalSaveFails_LeavesProfilePendingAndFailsStop()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(true);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);

            profilesModule.DatabaseAccessor = database;
            profilesModule.QueueProfileSave(profile);
            ExpectErrorLog("Error saving profiles.");

            Task stopTask = profilesModule.StopServerRunAsync();
            await AwaitCompletion(database.GetUpdateStartedTask(0));
            database.CompleteUpdate(0);

            Exception stopFailure = null;

            try
            {
                await stopTask;
            }
            catch (Exception exception)
            {
                stopFailure = exception;
            }

            Assert.That(stopFailure, Is.TypeOf<InvalidOperationException>());
            Assert.That(profilesModule.PendingSaveCount, Is.EqualTo(1));
            Assert.That(database.UpdateCallCount, Is.EqualTo(1));
            profile.Dispose();
        }

        [Test]
        public async Task ServerUpdateConfirmed_WaitsForDatabaseBeforeResponding()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(false);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            TestPeer peer = CreatePeer();
            GrantServerPermission(peer);
            profilesModule.DatabaseAccessor = database;
            profilesModule.AddLoadedProfile(profile);
            profilesModule.QueueProfileSave(profile);

            IIncomingMessage message = CreateBinaryMessage(
                MstOpCodes.ServerUpdateProfileValues, peer,
                CreateProfileUpdatePayload(UserId, 5));
            Task updateTask = profilesModule.InvokeServerUpdate(message);

            await AwaitCompletion(database.GetUpdateStartedTask(0));

            Assert.That(profile.Get<ObservableInt>(PropertyKey).Value,
                Is.EqualTo(InitialValue + 5));
            Assert.That(peer.SentMessages, Is.Empty);
            Assert.That(updateTask.IsCompleted, Is.False);

            database.CompleteUpdate(0);
            await AwaitCompletion(updateTask);

            AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(profilesModule.PendingSaveCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ServerUpdateConfirmed_WhenDatabaseFails_ReturnsServiceUnavailable()
        {
            var database = new ControlledProfileSaveDatabaseAccessor(true);
            ObservableServerProfile profile = CreateProfile(UserId, InitialValue);
            TestPeer peer = CreatePeer();
            int profileUpdateFailureLogged = 0;
            LogHandler captureAppender = (_, level, channel, loggedMessage) =>
            {
                if (level == LogLevel.Error &&
                    channel == LogChannels.Economy &&
                    loggedMessage?.ToString().Contains("Profile update failed") == true)
                {
                    Interlocked.Exchange(ref profileUpdateFailureLogged, 1);
                }
            };

            GrantServerPermission(peer);
            profilesModule.DatabaseAccessor = database;
            profilesModule.AddLoadedProfile(profile);
            profilesModule.QueueProfileSave(profile);

            IIncomingMessage message = CreateBinaryMessage(
                MstOpCodes.ServerUpdateProfileValues, peer,
                CreateProfileUpdatePayload(UserId, 5));
            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            bool respondedBeforeDatabaseCompleted = false;

            LogManager.AddAppender(captureAppender);
            LogAssert.ignoreFailingMessages = true;

            try
            {
                Task updateTask = profilesModule.InvokeServerUpdate(message);
                await AwaitCompletion(database.GetUpdateStartedTask(0));
                respondedBeforeDatabaseCompleted = peer.SentMessages.Count > 0;

                database.CompleteUpdate(0);
                await AwaitCompletion(updateTask);
            }
            finally
            {
                database.CompleteUpdate(0);
                LogManager.RemoveAppender(captureAppender);
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(respondedBeforeDatabaseCompleted, Is.False);
            IOutgoingMessage response = AssertResponseStatus(peer, ResponseStatus.ServiceUnavailable);
            MstProperties error = MstProperties.FromBytes(response.Data);
            Assert.That(error.AsString(MstErrorPropertyKeys.CODE),
                Is.EqualTo(MstErrorCodes.PROFILE_SAVE_UNAVAILABLE));
            Assert.That(profilesModule.PendingSaveCount, Is.EqualTo(1));
            Assert.That(Volatile.Read(ref profileUpdateFailureLogged), Is.EqualTo(1));
        }

        [Test]
        public async Task ServerUpdateWithoutResponse_AppliesExistingMultiProfileBatch()
        {
            const string secondUserId = "profile-user-two";
            ObservableServerProfile firstProfile = CreateProfile(UserId, InitialValue);
            ObservableServerProfile secondProfile = CreateProfile(secondUserId, InitialValue);
            TestPeer peer = CreatePeer();
            GrantServerPermission(peer);
            profilesModule.AddLoadedProfile(firstProfile);
            profilesModule.AddLoadedProfile(secondProfile);

            IIncomingMessage message = CreateBinaryMessage(
                MstOpCodes.ServerUpdateProfileValues, peer,
                CreateProfileUpdatePayload(
                    new KeyValuePair<string, int>(UserId, 5),
                    new KeyValuePair<string, int>(secondUserId, 7)), false);

            await profilesModule.InvokeServerUpdate(message);

            Assert.That(firstProfile.Get<ObservableInt>(PropertyKey).Value,
                Is.EqualTo(InitialValue + 5));
            Assert.That(secondProfile.Get<ObservableInt>(PropertyKey).Value,
                Is.EqualTo(InitialValue + 7));
            Assert.That(peer.SentMessages, Is.Empty);
        }

        [Test]
        public async Task UpdateThrottle_WhenOnlyClientUpdateIsPending_SendsIt()
        {
            TestPeer peer = CreatePeer();
            var profile = new ObservableServerProfile(UserId, peer);
            var property = new ObservableInt(PropertyKey, InitialValue);
            profile.Add(property);
            property.Value = RestoredValue;
            profilesModule.QueueProfileSend(profile);

            profilesModule.InvokeUpdateThrottle();
            await AwaitCondition(() => !profile.HasDirtyProperties);

            Assert.That(peer.SentMessages.Count, Is.EqualTo(1));
            Assert.That(peer.SentMessages[0].OpCode, Is.EqualTo(MstOpCodes.UpdateClientProfile));
            profile.Dispose();
        }

        [Test]
        public void ObservableDictionaryString_ApplyUpdates_PreservesUpdateForNextHop()
        {
            const ushort dictionaryKey = 702;

            using (var source = new ObservableServerProfile(UserId))
            using (var master = new ObservableServerProfile(UserId))
            using (var client = new ObservableProfile())
            {
                var sourceProperty = new ObservableDictionaryString(dictionaryKey);
                var masterProperty = new ObservableDictionaryString(dictionaryKey);
                var clientProperty = new ObservableDictionaryString(dictionaryKey);

                source.Add(sourceProperty);
                master.Add(masterProperty);
                client.Add(clientProperty);

                sourceProperty.Add("style", "blobs");
                source.ClearUpdates();
                sourceProperty["style"] = "clay";

                master.ApplyUpdates(source.GetUpdates());

                Assert.That(masterProperty["style"], Is.EqualTo("clay"));
                Assert.That(master.HasDirtyProperties, Is.True);

                client.ApplyUpdates(master.GetUpdates());

                Assert.That(clientProperty["style"], Is.EqualTo("clay"));
            }
        }

        [Test]
        public async Task TryUpdateOfflineProfile_WhenUserLogsInDuringRestore_DoesNotPersist()
        {
            var database = new ControlledProfilesDatabaseAccessor(null);
            profilesModule.DatabaseAccessor = database;
            bool updateInvoked = false;

            Task<ObservableServerProfile> update = profilesModule.TryUpdateOfflineProfileAsync(
                UserId, profile =>
                {
                    updateInvoked = true;
                    return true;
                });

            await AwaitCompletion(database.RestoreStarted.Task);
            TestPeer peer = CreatePeer();
            authModule.MarkLoggedIn(AddUser(peer));
            database.CompleteRestore();

            ObservableServerProfile result = await AwaitCompletion(update);

            Assert.That(result, Is.Null);
            Assert.That(updateInvoked, Is.False);
            Assert.That(database.UpdateCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task TryUpdateOfflineProfile_WhenLoggedOutProfileAwaitsUnload_DoesNotPersist()
        {
            var database = new ControlledProfileSaveDatabaseAccessor();
            ObservableServerProfile activeProfile = CreateProfile(UserId, InitialValue);
            profilesModule.DatabaseAccessor = database;
            profilesModule.AddLoadedProfile(activeProfile);
            bool updateInvoked = false;

            ObservableServerProfile result = await profilesModule.TryUpdateOfflineProfileAsync(
                UserId, profile =>
                {
                    updateInvoked = true;
                    return true;
                });

            Assert.That(result, Is.Null);
            Assert.That(updateInvoked, Is.False);
            Assert.That(database.UpdateCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ClientFill_WhenProfileExtensionIsPublished_ReturnsSuccess()
        {
            TestPeer peer = CreatePeer();
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ClientFillInProfileValues, peer);
            var profile = CreateProfile(UserId, RestoredValue);

            Task request = profilesModule.InvokeClientFill(message);
            Assert.That(request.IsCompleted, Is.False);
            peer.AddExtension(new ProfilePeerExtension(profile, peer));
            await AwaitCompletion(request);

            IOutgoingMessage response = AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(response.Data, Is.EqualTo(profile.ToBytes()));
            Assert.That(profile.ClientPeer, Is.SameAs(peer));
            profile.Dispose();
        }

        [Test]
        public async Task ServerFill_WhenPeerLacksPermission_ReturnsForbidden()
        {
            TestPeer peer = CreatePeer();
            IIncomingMessage message = CreateMessage(MstOpCodes.ServerFillInProfileValues, peer, UserId);
            ExpectErrorLog("did not have sufficient permissions");

            await AwaitCompletion(profilesModule.InvokeServerFill(message));

            AssertResponseStatus(peer, ResponseStatus.Forbidden);
        }

        [Test]
        public async Task ServerFill_WhenUserLogsOutWhileWaiting_ReturnsUnauthorized()
        {
            TestPeer peer = CreatePeer();
            GrantServerPermission(peer);
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ServerFillInProfileValues, peer, UserId);
            ExpectErrorLog("User logged out during profile fetch");

            Task request = profilesModule.InvokeServerFill(message);
            Assert.That(request.IsCompleted, Is.False);
            authModule.MarkLoggedOut(UserId);
            await AwaitCompletion(request);

            AssertResponseStatus(peer, ResponseStatus.Unauthorized);
        }

        [Test]
        public async Task ServerFill_WhenProfileWaitExpires_ReturnsTimeout()
        {
            profilesModule.SetProfileLoadTimeout(0);
            TestPeer peer = CreatePeer();
            GrantServerPermission(peer);
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ServerFillInProfileValues, peer, UserId);
            ExpectErrorLog($"Profile fetch timeout for user {UserId}");

            await AwaitCompletion(profilesModule.InvokeServerFill(message));

            AssertResponseStatus(peer, ResponseStatus.Timeout);
        }

        [Test]
        public async Task ServerFill_WhenServerRunIsCancelled_DoesNotRespondAfterProfilePublication()
        {
            TestPeer peer = CreatePeer();
            GrantServerPermission(peer);
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ServerFillInProfileValues, peer, UserId);
            ObservableServerProfile profile = CreateProfile(UserId, RestoredValue);
            using var cancellation = new CancellationTokenSource();

            Task request = profilesModule.InvokeServerFill(message, cancellation.Token);
            Assert.That(request.IsCompleted, Is.False);

            cancellation.Cancel();
            await AssertOperationCanceled(request);
            profilesModule.AddLoadedProfile(profile);

            Assert.That(peer.SentMessages, Is.Empty);
        }

        [Test]
        public async Task ServerFill_WhenProfileBecomesAvailable_ReturnsSuccess()
        {
            TestPeer peer = CreatePeer();
            GrantServerPermission(peer);
            IUserPeerExtension user = AddUser(peer);
            authModule.MarkLoggedIn(user);
            IIncomingMessage message = CreateMessage(MstOpCodes.ServerFillInProfileValues, peer, UserId);
            ObservableServerProfile profile = CreateProfile(UserId, RestoredValue);

            Task request = profilesModule.InvokeServerFill(message);
            Assert.That(request.IsCompleted, Is.False);
            profilesModule.AddLoadedProfile(profile);
            await AwaitCompletion(request);

            IOutgoingMessage response = AssertResponseStatus(peer, ResponseStatus.Success);
            Assert.That(response.Data, Is.EqualTo(profile.ToBytes()));
        }

        private TestPeer CreatePeer()
        {
            var peer = new TestPeer();
            peers.Add(peer);
            return peer;
        }

        private static IUserPeerExtension AddUser(TestPeer peer)
        {
            var user = new TestUserPeerExtension(UserId, peer);
            peer.AddExtension<IUserPeerExtension>(user);
            return user;
        }

        private static void GrantServerPermission(TestPeer peer)
        {
            var security = new SecurityInfoPeerExtension(peer);
            security.SetAccountPermissionLevel(MstPermissionLevels.Admin);
            peer.AddExtension(security);
        }

        private static ObservableServerProfile CreateProfile(string userId, int value)
        {
            var profile = new ObservableServerProfile(userId);
            profile.Add(new ObservableInt(PropertyKey, value));
            return profile;
        }

        private static IIncomingMessage CreateMessage(ushort opCode, TestPeer peer, string payload = null)
        {
            byte[] data = payload == null ? Array.Empty<byte>() : System.Text.Encoding.UTF8.GetBytes(payload);
            return new IncomingMessage(opCode, 0, data, DeliveryMethod.Reliable, peer)
            {
                AckResponseId = 1
            };
        }

        private static IIncomingMessage CreateBinaryMessage(ushort opCode,
            TestPeer peer, byte[] payload, bool expectsResponse = true)
        {
            return new IncomingMessage(opCode, 0, payload,
                DeliveryMethod.Reliable, peer)
            {
                AckResponseId = expectsResponse ? 1 : (int?)null
            };
        }

        private static byte[] CreateProfileUpdatePayload(string userId,
            int valueDelta)
        {
            return CreateProfileUpdatePayload(
                new KeyValuePair<string, int>(userId, valueDelta));
        }

        private static byte[] CreateProfileUpdatePayload(
            params KeyValuePair<string, int>[] profileUpdates)
        {
            using (var stream = new MemoryStream())
            using (var writer = new EndianBinaryWriter(
                EndianBitConverter.Big, stream))
            {
                writer.WriteCount32(profileUpdates.Length,
                    MstNetworkLimits.MaxProfilePropertyCount,
                    "Profile update batch");

                foreach (KeyValuePair<string, int> profileUpdate in profileUpdates)
                {
                    using (var profile = CreateProfile(profileUpdate.Key,
                        InitialValue))
                    {
                        profile.Get<ObservableInt>(PropertyKey).Add(
                            profileUpdate.Value);
                        byte[] updates = profile.GetUpdates();
                        writer.Write(profileUpdate.Key);
                        writer.Write(updates.Length);
                        writer.Write(updates);
                    }
                }

                return stream.ToArray();
            }
        }

        private static IOutgoingMessage AssertResponseStatus(TestPeer peer, ResponseStatus expectedStatus)
        {
            Assert.That(peer.SentMessages.Count, Is.EqualTo(1));
            IOutgoingMessage response = peer.SentMessages[0];
            Assert.That(response.Status, Is.EqualTo(expectedStatus));
            return response;
        }

        private static void ExpectErrorLog(string messageFragment)
        {
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(messageFragment)));
        }

        private static async Task AwaitCompletion(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(3000));
            Assert.That(completed, Is.SameAs(task), "The profile request did not complete within the test deadline");
            await task;
        }

        private static async Task AssertOperationCanceled(Task task)
        {
            try
            {
                await AwaitCompletion(task);
                Assert.Fail("The profile request should have been cancelled by the server lifecycle token");
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static async Task<T> AwaitCompletion<T>(Task<T> task)
        {
            await AwaitCompletion((Task)task);
            return await task;
        }

        private static async Task AwaitCondition(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);

            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.That(condition(), Is.True, "The expected profile lifecycle state was not reached");
        }

        public sealed class TestProfilesModule : ProfilesModule
        {
            public List<string> LifecycleSteps { get; set; }
            public int ProfileLoadedCallCount { get; private set; }
            public int ValueAtProfileLoaded { get; private set; }
            public bool ExtensionPublishedAtProfileLoaded { get; private set; }
            public int PendingSaveCount => PendingProfileSaveCount;

            public void InitializeForTests()
            {
                base.Awake();
            }

            public void Configure(AuthModule auth, int timeoutSeconds)
            {
                authModule = auth;
                profileLoadTimeoutSeconds = timeoutSeconds;
            }

            public void SetProfileLoadTimeout(int timeoutSeconds)
            {
                profileLoadTimeoutSeconds = timeoutSeconds;
            }

            public Task InvokeClientFill(IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return ClientFillInProfileValuesRequestHandler(message, cancellationToken);
            }

            public Task InvokeServerFill(IIncomingMessage message,
                CancellationToken cancellationToken = default)
            {
                return ServerFillInProfileValuesRequestHandler(message, cancellationToken);
            }

            public Task InvokeServerUpdate(IIncomingMessage message)
            {
                return ServerUpdateProfileValuesHandler(message);
            }

            public void TriggerUserLoggedIn(IUserPeerExtension user)
            {
                OnUserLoggedInEventHandler(user);
            }

            public Task InvokeUserLoggedIn(IUserPeerExtension user)
            {
                return HandleUserLoggedInAsync(user);
            }

            public void TriggerUserLoggedOut(IUserPeerExtension user)
            {
                OnUserLoggedOutEvent(user);
            }

            public bool IsProfileLoadInProgress(string userId)
            {
                return profilesInitialLoadInProgress.ContainsKey(userId);
            }

            public void AddLoadedProfile(ObservableServerProfile profile)
            {
                profilesList[profile.UserId] = profile;
            }

            public void QueueProfileSave(ObservableServerProfile profile)
            {
                QueueProfileToSave(profile);
            }

            public Task InvokeSavePendingProfiles()
            {
                return SavePendingProfilesAsync();
            }

            public void QueueProfileSend(ObservableServerProfile profile)
            {
                SendUpdatesToClient(profile);
            }

            public void InvokeUpdateThrottle()
            {
                UpdateThrottle();
            }

            public override ObservableServerProfile CreateProfile(string userId, IPeer clientPeer = null)
            {
                var profile = new ObservableServerProfile(userId, clientPeer);
                profile.Add(new ObservableInt(PropertyKey, InitialValue));
                return profile;
            }

            protected override void ProfileLoaded(ObservableServerProfile profile)
            {
                LifecycleSteps?.Add("hook");
                ProfileLoadedCallCount++;
                ValueAtProfileLoaded = profile.Get<ObservableInt>(PropertyKey).Value;
                ExtensionPublishedAtProfileLoaded = profile.ClientPeer.HasExtension<ProfilePeerExtension>();
            }

            protected override Task DelayProfileLoadRetryAsync(int retryCount, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public sealed class TestAuthModule : AuthModule
        {
            public void MarkLoggedIn(IUserPeerExtension user)
            {
                loggedInUsers[user.UserId] = user;
            }

            public void MarkLoggedOut(string userId)
            {
                loggedInUsers.TryRemove(userId, out _);
            }
        }

        private sealed class TestUserPeerExtension : IUserPeerExtension
        {
            public TestUserPeerExtension(string userId, IPeer peer)
            {
                UserId = userId;
                Peer = peer;
            }

            public IPeer Peer { get; }
            public string UserId { get; }
            public string Username => UserId;
            public IAccountInfoData Account { get; set; }
            public int JoinedRoomID { get; set; } = -1;

            public AccountInfoPacket CreateAccountInfoPacket()
            {
                return null;
            }

            public bool HasJoinedRoom()
            {
                return false;
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

        private sealed class ControlledProfilesDatabaseAccessor : IProfilesDatabaseAccessor
        {
            private readonly List<string> lifecycleSteps;
            private readonly int failuresBeforeSuccess;
            private readonly TaskCompletionSource<bool> plannedFailuresCompleted = new TaskCompletionSource<bool>();
            private readonly TaskCompletionSource<bool> restoreGate = new TaskCompletionSource<bool>();
            private int restoreCallCount;
            private int updateCallCount;

            public ControlledProfilesDatabaseAccessor(List<string> lifecycleSteps, int failuresBeforeSuccess = 0)
            {
                this.lifecycleSteps = lifecycleSteps;
                this.failuresBeforeSuccess = failuresBeforeSuccess;
            }

            public int RestoreCallCount => Volatile.Read(ref restoreCallCount);
            public int UpdateCallCount => Volatile.Read(ref updateCallCount);
            public Task PlannedFailuresCompleted => plannedFailuresCompleted.Task;
            public TaskCompletionSource<ObservableServerProfile> RestoreStarted { get; } =
                new TaskCompletionSource<ObservableServerProfile>();
            public MstProperties CustomProperties { get; } = new MstProperties();
            public MasterServerToolkit.Logging.Logger Logger { get; set; }

            public async Task RestoreProfileAsync(ObservableServerProfile profile,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int attempt = Interlocked.Increment(ref restoreCallCount);
                RestoreStarted.TrySetResult(profile);

                if (attempt <= failuresBeforeSuccess)
                {
                    if (attempt == failuresBeforeSuccess)
                        plannedFailuresCompleted.TrySetResult(true);

                    throw new InvalidOperationException($"Restore attempt {attempt} failed");
                }

                await restoreGate.Task;
                cancellationToken.ThrowIfCancellationRequested();
                profile.Get<ObservableInt>(PropertyKey).Value = RestoredValue;
                lifecycleSteps?.Add("restore");
            }

            public Task RestoreProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task<DatabaseEntriesInfo<IProfilePropertyData>> Search(Dictionary<string, object> filter,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<DatabaseEntriesInfo<IProfilePropertyData>>(null);
            }

            public Task UpdateProfileAsync(ObservableServerProfile profile,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref updateCallCount);
                return Task.CompletedTask;
            }

            public Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public void CompleteRestore()
            {
                restoreGate.TrySetResult(true);
            }

            public void Dispose()
            {
            }
        }

        private sealed class ControlledProfileSaveDatabaseAccessor : IProfilesDatabaseAccessor
        {
            private readonly bool[] updateFailures;
            private readonly TaskCompletionSource<IReadOnlyList<ObservableServerProfile>>[] updateStarted;
            private readonly TaskCompletionSource<bool>[] updateGates;
            private int updateCallCount;

            public ControlledProfileSaveDatabaseAccessor(params bool[] updateFailures)
            {
                this.updateFailures = updateFailures;
                updateStarted = new TaskCompletionSource<IReadOnlyList<ObservableServerProfile>>[updateFailures.Length];
                updateGates = new TaskCompletionSource<bool>[updateFailures.Length];

                for (int i = 0; i < updateFailures.Length; i++)
                {
                    updateStarted[i] = new TaskCompletionSource<IReadOnlyList<ObservableServerProfile>>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    updateGates[i] = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            public int UpdateCallCount => Volatile.Read(ref updateCallCount);
            public MstProperties CustomProperties { get; } = new MstProperties();
            public MasterServerToolkit.Logging.Logger Logger { get; set; }

            public Task<IReadOnlyList<ObservableServerProfile>> GetUpdateStartedTask(int attemptIndex)
            {
                return updateStarted[attemptIndex].Task;
            }

            public void CompleteUpdate(int attemptIndex)
            {
                updateGates[attemptIndex].TrySetResult(true);
            }

            public Task RestoreProfileAsync(ObservableServerProfile profile,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task RestoreProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public Task<DatabaseEntriesInfo<IProfilePropertyData>> Search(Dictionary<string, object> filter,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<DatabaseEntriesInfo<IProfilePropertyData>>(null);
            }

            public Task UpdateProfileAsync(ObservableServerProfile profile,
                CancellationToken cancellationToken = default)
            {
                return UpdateProfilesAsync(new[] { profile }, cancellationToken);
            }

            public async Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int attemptIndex = Interlocked.Increment(ref updateCallCount) - 1;

                if (attemptIndex >= updateFailures.Length)
                    throw new InvalidOperationException($"Unexpected profile update attempt {attemptIndex + 1}");

                var batch = new List<ObservableServerProfile>(profiles);
                updateStarted[attemptIndex].TrySetResult(batch);
                await updateGates[attemptIndex].Task;
                cancellationToken.ThrowIfCancellationRequested();

                if (updateFailures[attemptIndex])
                    throw new InvalidOperationException($"Profile update attempt {attemptIndex + 1} failed");
            }

            public void Dispose()
            {
            }
        }
    }
}
