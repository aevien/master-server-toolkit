using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class ServerHandlerShutdownTests
    {
        private const ushort TestOpCode = 65000;
        private GameObject testObject;
        private TestServerBehaviour server;
        private TestPeer peer;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject(nameof(ServerHandlerShutdownTests));
            server = testObject.AddComponent<TestServerBehaviour>();
            server.InitializeForTest();
            Assert.That(server.TryStartRunForTest(), Is.True);

            peer = new TestPeer();
            AuthenticatePeer(peer);
        }

        [TearDown]
        public void TearDown()
        {
            peer?.Dispose();
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        [Test]
        public async Task StopServerAsync_CancelsActiveHandlerAndWaitsForCleanup()
        {
            var entered = CreateCompletionSource();
            var cleanedUp = CreateCompletionSource();
            CancellationToken observedToken = default;

            server.RegisterMessageHandler(TestOpCode, async (_, cancellationToken) =>
            {
                observedToken = cancellationToken;
                entered.TrySetResult(true);

                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                finally
                {
                    cleanedUp.TrySetResult(true);
                }
            });

            server.Dispatch(CreateMessage());
            await AssertCompletesAsync(entered.Task, "The test handler did not start");

            Task stopTask = server.StopServerAsync();
            await AssertCompletesAsync(stopTask, "The asynchronous server stop did not complete");

            Assert.That(observedToken.IsCancellationRequested, Is.True);
            Assert.That(cleanedUp.Task.IsCompleted, Is.True,
                "Server stop completed before the canceled handler ran its cleanup");
            Assert.That(server.IsRunning, Is.False);
        }

        [Test]
        public async Task StopServerAsync_WhenHandlerIgnoresCancellation_DoesNotBlockCallerAndWaits()
        {
            var entered = CreateCompletionSource();
            var release = CreateCompletionSource();
            int invocationCount = 0;

            try
            {
                server.RegisterMessageHandler(TestOpCode, async _ =>
                {
                    Interlocked.Increment(ref invocationCount);
                    entered.TrySetResult(true);
                    await release.Task;
                });

                server.Dispatch(CreateMessage());
                await AssertCompletesAsync(entered.Task, "The test handler did not start");

                Task stopTask = server.StopServerAsync();

                Assert.That(stopTask.IsCompleted, Is.False,
                    "StopServerAsync blocked or completed while the tracked handler was still active");

                server.Dispatch(CreateMessage());
                await Task.Delay(50);
                Assert.That(Volatile.Read(ref invocationCount), Is.EqualTo(1),
                    "A new handler started after shutdown began");

                release.TrySetResult(true);
                await AssertCompletesAsync(stopTask, "Server stop did not finish after the handler completed");
            }
            finally
            {
                release.TrySetResult(true);
            }
        }

        [Test]
        public async Task StopServerAsync_CancellationAfterAwait_PreventsLateMutation()
        {
            var entered = CreateCompletionSource();
            var releaseProvider = CreateCompletionSource();
            int lateMutationCount = 0;

            try
            {
                server.RegisterMessageHandler(TestOpCode, async (_, cancellationToken) =>
                {
                    entered.TrySetResult(true);
                    await releaseProvider.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref lateMutationCount);
                });

                server.Dispatch(CreateMessage());
                await AssertCompletesAsync(entered.Task, "The test handler did not start");

                Task stopTask = server.StopServerAsync();
                Assert.That(stopTask.IsCompleted, Is.False,
                    "Server stop completed while the provider operation was still active");

                releaseProvider.TrySetResult(true);
                await AssertCompletesAsync(stopTask, "The canceled handler did not drain");

                Assert.That(Volatile.Read(ref lateMutationCount), Is.Zero,
                    "The handler mutated state after its server-run token was canceled");
            }
            finally
            {
                releaseProvider.TrySetResult(true);
            }
        }

        [Test]
        public async Task StopServerAsync_RepeatedCallsShareOneCompletion()
        {
            var entered = CreateCompletionSource();
            var release = CreateCompletionSource();

            try
            {
                server.RegisterMessageHandler(TestOpCode, async _ =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                });

                server.Dispatch(CreateMessage());
                await AssertCompletesAsync(entered.Task, "The test handler did not start");

                using var startCalls = new ManualResetEventSlim(false);
                Task<Task> firstCall = Task.Factory.StartNew(
                    () =>
                    {
                        startCalls.Wait();
                        return server.StopServerAsync();
                    },
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.Default);
                Task<Task> secondCall = Task.Factory.StartNew(
                    () =>
                    {
                        startCalls.Wait();
                        return server.StopServerAsync();
                    },
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.Default);

                startCalls.Set();
                Task firstStop = await firstCall;
                Task secondStop = await secondCall;

                Assert.That(secondStop, Is.SameAs(firstStop));
                release.TrySetResult(true);
                await AssertCompletesAsync(firstStop, "The shared server stop did not complete");
                Assert.That(server.StoppedCallbackCount, Is.EqualTo(1));
            }
            finally
            {
                release.TrySetResult(true);
            }
        }

        [Test]
        public async Task StopServerAsync_WaitsUntilOverlappingStartupReleasesOwnership()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial test run did not stop");

            Assert.That(server.TryBeginStartupForTest(out long generation), Is.True);
            Task stopTask = server.StopServerAsync();

            Assert.That(stopTask.IsCompleted, Is.False,
                "Server stop completed while startup still owned the run");
            server.CompleteStartupForTest(generation);

            await AssertCompletesAsync(stopTask, "Server stop did not complete after startup released ownership");
            Assert.That(server.TryStartRunForTest(), Is.True,
                "The stopped startup run still blocked the next server run");
        }

        [Test]
        public async Task StopServer_DuringStartupOnStartupThread_DoesNotWaitForItsOwnStartup()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial test run did not stop");

            Assert.That(server.TryBeginStartupForTest(out long generation), Is.True);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            server.StopServer();
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                "Synchronous stop waited for startup owned by the same thread");

            Task stopTask = server.StopServerAsync();
            Assert.That(stopTask.IsCompleted, Is.False);
            server.CompleteStartupForTest(generation);
            await AssertCompletesAsync(stopTask, "Startup-owned run did not finish stopping");
        }

        [Test]
        public async Task StopServerAsync_WhenRequestedInsideSocketListen_ClosesStartedTransport()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial test run did not stop");

            var socket = new TestServerSocket();
            Task stopTask = null;
            int ownerThreadId = Thread.CurrentThread.ManagedThreadId;
            socket.BeforeListenReturns = () => stopTask = server.StopServerAsync();
            server.SetSocketForTest(socket);

            server.StartServer("127.0.0.1", 25200);

            Assert.That(stopTask, Is.Not.Null);
            await AssertCompletesAsync(stopTask, "Stop requested during Listen did not complete");
            Assert.That(socket.ListenCount, Is.EqualTo(1));
            Assert.That(socket.StopCount, Is.EqualTo(1));
            Assert.That(socket.IsListening, Is.False);
            Assert.That(server.LastStoppedThreadId, Is.EqualTo(ownerThreadId),
                "Server stopped callbacks were not dispatched to the owner thread");
        }

        [Test]
        public async Task StartServer_WhenStartupValidationFails_DoesNotOpenSocket()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial test run did not stop");

            testObject.AddComponent<FailingStartupValidatorModule>();
            var socket = new TestServerSocket();
            server.SetSocketForTest(socket);

            Assert.Throws<InvalidOperationException>(
                () => server.StartServer("127.0.0.1", 25200));
            await AssertCompletesAsync(
                server.StopServerAsync(),
                "Rejected server startup did not finish cleanup");

            Assert.That(socket.ListenCount, Is.Zero);
            Assert.That(socket.IsListening, Is.False);
        }

        [Test]
        public async Task StopServerAsync_ReentrantSynchronousStopFromCallback_DoesNotRecurse()
        {
            server.StopAgainFromStoppedCallback = true;

            await AssertCompletesAsync(server.StopServerAsync(), "Reentrant server stop did not complete");

            Assert.That(server.StoppedCallbackCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StopServerAsync_WhenStoppedSubscriberThrows_InvokesRemainingCallbacks()
        {
            int successfulSubscriberCalls = 0;
            server.OnServerStoppedEvent += () => throw new InvalidOperationException("Expected test exception");
            server.OnServerStoppedEvent += () => successfulSubscriberCalls++;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "A server stopped event subscriber failed.*Expected test exception"));

            await AssertCompletesAsync(server.StopServerAsync(), "Server stop did not survive a failed subscriber");

            Assert.That(successfulSubscriberCalls, Is.EqualTo(1));
            Assert.That(server.StoppedCallbackCount, Is.EqualTo(1));
        }

        [Test]
        public void ModuleMutation_DuringActiveRun_IsRejectedWithoutRegisteringModule()
        {
            var module = testObject.AddComponent<TestRunModule>();

            Assert.Throws<InvalidOperationException>(() => server.AddModule(module));
            Assert.That(server.ContainsModule(module), Is.False);

            Assert.Throws<InvalidOperationException>(() => server.AddModuleAndInitialize(module));
            Assert.That(server.ContainsModule(module), Is.False);

            Assert.Throws<InvalidOperationException>(() => server.InitializeModules());
        }

        [Test]
        public async Task Startup_DoesNotAdmitMessagesUntilRunModulesAreStarted()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial test run did not stop");
            int invocationCount = 0;
            var invoked = CreateCompletionSource();
            server.RegisterMessageHandler(TestOpCode, _ =>
            {
                Interlocked.Increment(ref invocationCount);
                invoked.TrySetResult(true);
                return Task.CompletedTask;
            });

            Assert.That(server.TryBeginStartupForTest(out long generation), Is.True);
            server.Dispatch(CreateMessage());
            await Task.Delay(50);

            Assert.That(Volatile.Read(ref invocationCount), Is.Zero,
                "A message was admitted before the server run reached Running state");
            Assert.That(server.MarkStartupRunningForTest(generation), Is.True);
            server.CompleteStartupForTest(generation);

            server.Dispatch(CreateMessage());
            await AssertCompletesAsync(invoked.Task, "A running server did not admit the message");
            Assert.That(Volatile.Read(ref invocationCount), Is.EqualTo(1));
        }

        [Test]
        public async Task StopServer_WhenHandlerIsRunning_ClosesTransportAndBlocksRestartUntilHandlerEnds()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial server run did not stop");

            var entered = CreateCompletionSource();
            var release = CreateCompletionSource();
            var socket = new TestServerSocket();
            var module = testObject.AddComponent<TestRunModule>();
            server.SetSocketForTest(socket);
            server.AddModuleAndInitialize(module);
            Assert.That(server.TryStartRunForTest(), Is.True);

            try
            {
                server.RegisterMessageHandler(TestOpCode, async _ =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                });

                server.Dispatch(CreateMessage());
                await AssertCompletesAsync(entered.Task, "The test handler did not start");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                server.StopServer();
                stopwatch.Stop();

                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                    "StopServer blocked while a handler was still active");
                Assert.That(server.TryStartRunForTest(), Is.False,
                    "A new run started while a handler from the previous run was still active");
                Assert.That(module.StopCount, Is.Zero,
                    "Server-run modules began stopping before the active handler ended");
                await AssertCompletesAsync(socket.StopEntered,
                    "Immediate shutdown did not begin transport cleanup");
                Assert.That(socket.StopCount, Is.EqualTo(1),
                    "Immediate shutdown did not close the transport");

                Task finalStopCompletion = server.StopServerAsync();
                release.TrySetResult(true);
                await AssertCompletesAsync(module.StopEntered.Task,
                    "Server-run module did not begin stopping after the handler ended");
                Assert.That(socket.StopCount, Is.EqualTo(1));

                module.ReleaseStop();
                await AssertCompletesAsync(finalStopCompletion,
                    "The non-blocking server run did not complete after its handler ended");
                Assert.That(socket.StopCount, Is.EqualTo(1));

                Assert.That(server.TryStartRunForTest(), Is.True,
                    "The server did not release the completed run context");
                Task cleanupStop = server.StopServerAsync();
                await AssertCompletesAsync(module.StopEntered.Task, "Cleanup module stop did not start");
                module.ReleaseStop();
                await AssertCompletesAsync(cleanupStop, "Cleanup stop did not complete");
            }
            finally
            {
                release.TrySetResult(true);
            }
        }

        [Test]
        public async Task StopServer_WhenCancellationCallbackBlocks_ReturnsImmediately()
        {
            var entered = CreateCompletionSource();
            var cancellationCallbackCompleted = CreateCompletionSource();
            using var cancellationCallbackEntered = new ManualResetEventSlim(false);
            using var releaseCancellationCallback = new ManualResetEventSlim(false);

            try
            {
                server.RegisterMessageHandler(TestOpCode, async (_, cancellationToken) =>
                {
                    using (cancellationToken.Register(() =>
                    {
                        cancellationCallbackEntered.Set();
                        if (!releaseCancellationCallback.Wait(TimeSpan.FromSeconds(2)))
                            throw new TimeoutException("Test safety timeout while releasing cancellation callback");

                        cancellationCallbackCompleted.TrySetResult(true);
                    }))
                    {
                        entered.TrySetResult(true);
                        await cancellationCallbackCompleted.Task;
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                });

                server.Dispatch(CreateMessage());
                await AssertCompletesAsync(entered.Task, "The test handler did not start");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                server.StopServer();
                stopwatch.Stop();

                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                    "A cancellation callback blocked synchronous server shutdown");
                Assert.That(cancellationCallbackEntered.Wait(TimeSpan.FromSeconds(1)), Is.True);

                Task stopTask = server.StopServerAsync();
                Assert.That(stopTask.IsCompleted, Is.False);
                releaseCancellationCallback.Set();
                await AssertCompletesAsync(stopTask, "Server stop did not finish after cancellation callback release");
            }
            finally
            {
                releaseCancellationCallback.Set();
            }
        }

        [Test]
        public async Task StopServer_WhenTransportCleanupBlocks_RepeatedCallsReturnImmediately()
        {
            using var stopEntered = new ManualResetEventSlim(false);
            using var releaseStop = new ManualResetEventSlim(false);
            var socket = new TestServerSocket
            {
                BeforeStopReturns = () =>
                {
                    stopEntered.Set();
                    if (!releaseStop.Wait(TimeSpan.FromSeconds(2)))
                        throw new TimeoutException("Test safety timeout while releasing transport cleanup");
                }
            };
            server.SetSocketForTest(socket);

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                server.StopServer();
                server.StopServer();
                stopwatch.Stop();

                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                    "Transport cleanup blocked synchronous server shutdown");
                Assert.That(stopEntered.Wait(TimeSpan.FromSeconds(1)), Is.True);
                Assert.That(socket.StopCount, Is.EqualTo(1),
                    "Repeated shutdown started transport cleanup more than once");

                Task stopTask = server.StopServerAsync();
                Assert.That(stopTask.IsCompleted, Is.False);
                releaseStop.Set();
                await AssertCompletesAsync(stopTask, "Server stop did not finish after transport release");
            }
            finally
            {
                releaseStop.Set();
            }
        }

        [Test]
        public async Task UnityQuitAndDestroyCallbacks_ReturnImmediatelyAndCloseTransportOnce()
        {
            var socket = new TestServerSocket();
            server.SetSocketForTest(socket);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            server.InvokeApplicationQuitForTest();
            server.InvokeDestroyForTest();
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)),
                "Unity lifecycle callbacks blocked the owner thread");
            await AssertCompletesAsync(socket.StopEntered,
                "Unity lifecycle shutdown did not begin transport cleanup");
            Assert.That(socket.StopCount, Is.EqualTo(1),
                "Application quit and destroy callbacks stopped the transport more than once");
            await AssertCompletesAsync(server.StopServerAsync(),
                "Unity lifecycle shutdown did not finish asynchronously");
        }

        [Test]
        public async Task StopServer_WhenRunModuleIsDestroyedBeforeDeferredCleanup_SkipsModule()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial server run did not stop");

            var entered = CreateCompletionSource();
            var release = CreateCompletionSource();
            var module = testObject.AddComponent<TestRunModule>();
            server.AddModuleAndInitialize(module);
            Assert.That(server.TryStartRunForTest(), Is.True);

            try
            {
                server.RegisterMessageHandler(TestOpCode, async _ =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                });

                server.Dispatch(CreateMessage());
                await AssertCompletesAsync(entered.Task, "The test handler did not start");

                server.StopServer();
                UnityEngine.Object.DestroyImmediate(module);
                release.TrySetResult(true);

                await AssertCompletesAsync(server.StopServerAsync(),
                    "Shutdown did not skip the destroyed run module");
            }
            finally
            {
                release.TrySetResult(true);
            }
        }

        [Test]
        public async Task StopServerAsync_WaitsForRunModuleAndRestartsItWithNewToken()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial server run did not stop");

            var module = testObject.AddComponent<TestRunModule>();
            server.AddModuleAndInitialize(module);

            Assert.That(server.TryStartRunForTest(), Is.True);
            CancellationToken firstRunToken = module.RunToken;
            Assert.That(module.StartCount, Is.EqualTo(1));

            Task firstStop = server.StopServerAsync();
            await AssertCompletesAsync(module.StopEntered.Task, "Run module did not begin stopping");

            Assert.That(firstRunToken.IsCancellationRequested, Is.True);
            Assert.That(firstStop.IsCompleted, Is.False,
                "Server stop completed before the run module released its work");

            module.ReleaseStop();
            await AssertCompletesAsync(firstStop, "Server did not wait for the run module");
            Assert.That(module.StopCount, Is.EqualTo(1));

            Assert.That(server.TryStartRunForTest(), Is.True);
            Assert.That(module.StartCount, Is.EqualTo(2));
            Assert.That(module.RunToken, Is.Not.EqualTo(firstRunToken));
            Assert.That(module.RunToken.IsCancellationRequested, Is.False);

            Task secondStop = server.StopServerAsync();
            await AssertCompletesAsync(module.StopEntered.Task, "Run module did not stop after restart");
            module.ReleaseStop();
            await AssertCompletesAsync(secondStop, "Restarted server run did not stop");
            Assert.That(module.StopCount, Is.EqualTo(2));
        }

        [Test]
        public async Task StartServer_WhenRunModuleThrows_StopsEveryClaimedModuleOnce()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial server run did not stop");

            var firstModule = testObject.AddComponent<TestRunModule>();
            var failingModule = testObject.AddComponent<FailingStartRunModule>();
            server.AddModuleAndInitialize(firstModule);
            server.AddModuleAndInitialize(failingModule);
            server.SetSocketForTest(new TestServerSocket());

            Assert.Throws<InvalidOperationException>(() => server.StartServer("127.0.0.1", 25200));

            Task stopTask = server.StopServerAsync();
            await AssertCompletesAsync(firstModule.StopEntered.Task,
                "The module started before the failure did not begin stopping");

            Assert.That(firstModule.StopCount, Is.EqualTo(1));
            Assert.That(failingModule.StopCount, Is.EqualTo(1));

            firstModule.ReleaseStop();
            await AssertCompletesAsync(stopTask, "Partially started modules did not stop");
        }

        [Test]
        public async Task StopServerAsync_StopsRunModulesSequentiallyInReverseStartOrder()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial server run did not stop");

            var firstModule = testObject.AddComponent<TestRunModule>();
            var secondModule = testObject.AddComponent<SecondTestRunModule>();
            server.AddModuleAndInitialize(firstModule);
            server.AddModuleAndInitialize(secondModule);
            Assert.That(server.TryStartRunForTest(), Is.True);

            Task stopTask = server.StopServerAsync();
            await AssertCompletesAsync(secondModule.StopEntered.Task,
                "The last-started module did not begin stopping first");
            Assert.That(firstModule.StopCount, Is.Zero,
                "The earlier module began stopping before its dependent module finished");

            secondModule.ReleaseStop();
            await AssertCompletesAsync(firstModule.StopEntered.Task,
                "The earlier module did not begin stopping after its dependent completed");
            firstModule.ReleaseStop();

            await AssertCompletesAsync(stopTask, "Sequential module stop did not complete");
            Assert.That(firstModule.StopCount, Is.EqualTo(1));
            Assert.That(secondModule.StopCount, Is.EqualTo(1));
        }

        [Test]
        public async Task StopServerAsync_WhenRunModuleStopFails_CompletesCleanupThenFaults()
        {
            await AssertCompletesAsync(server.StopServerAsync(), "Initial server run did not stop");

            var completedModule = testObject.AddComponent<CompletedStopRunModule>();
            var failingModule = testObject.AddComponent<FailingStopRunModule>();
            var socket = new TestServerSocket();
            server.SetSocketForTest(socket);
            server.AddModuleAndInitialize(completedModule);
            server.AddModuleAndInitialize(failingModule);
            Assert.That(server.TryStartRunForTest(), Is.True);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "Server-run module FailingStopRunModule failed while stopping"));

            Exception stopFailure = await CaptureFailureAsync(
                server.StopServerAsync(),
                "Server stop did not complete after a run module failed");

            Assert.That(stopFailure, Is.TypeOf<AggregateException>());
            Assert.That(stopFailure.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(completedModule.StopCount, Is.EqualTo(1),
                "A module failure prevented the remaining modules from stopping");
            Assert.That(failingModule.StopCount, Is.EqualTo(1));
            Assert.That(socket.StopCount, Is.EqualTo(1),
                "Transport cleanup was skipped after a module stop failure");
            Assert.That(server.StoppedCallbackCount, Is.EqualTo(2),
                "Stopped callbacks were skipped after a module stop failure");
        }

        private IncomingMessage CreateMessage()
        {
            return new IncomingMessage(TestOpCode, 0, Array.Empty<byte>(), DeliveryMethod.Reliable, peer);
        }

        private static TaskCompletionSource<bool> CreateCompletionSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task AssertCompletesAsync(Task task, string message)
        {
            Task timeout = Task.Delay(2000);
            Assert.That(await Task.WhenAny(task, timeout), Is.SameAs(task), message);
            await task;
        }

        private static async Task<Exception> CaptureFailureAsync(Task task, string message)
        {
            Task timeout = Task.Delay(2000);
            Assert.That(await Task.WhenAny(task, timeout), Is.SameAs(task), message);

            try
            {
                await task;
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("Expected the task to fail");
            return null;
        }

        private static void AuthenticatePeer(TestPeer targetPeer)
        {
            SecurityInfoPeerExtension security = targetPeer.AddExtension(new SecurityInfoPeerExtension(targetPeer));
            Assert.That(security.GrantPermission(
                MstPermissionKeys.Default,
                MstPermissionLevels.Default), Is.True);
        }

        private sealed class TestServerBehaviour : ServerBehaviour
        {
            public int StoppedCallbackCount { get; private set; }
            public bool StopAgainFromStoppedCallback { get; set; }
            public int LastStoppedThreadId { get; private set; } = -1;

            public void InvokeApplicationQuitForTest()
            {
                OnApplicationQuit();
            }

            public void InvokeDestroyForTest()
            {
                OnDestroy();
            }

            protected override void OnStoppedServer()
            {
                StoppedCallbackCount++;
                LastStoppedThreadId = Thread.CurrentThread.ManagedThreadId;

                if (StopAgainFromStoppedCallback)
                    StopServer();
            }

            public void InitializeForTest()
            {
                base.Awake();
            }

            public bool TryStartRunForTest()
            {
                if (!TryBeginServerRunLifecycle(out long generation))
                    return false;

                try
                {
                    return TryStartServerRunModules(generation) && TryMarkServerRunStarted(generation);
                }
                finally
                {
                    CompleteServerStartup(generation);
                }
            }

            public bool TryBeginStartupForTest(out long generation)
            {
                return TryBeginServerRunLifecycle(out generation);
            }

            public void CompleteStartupForTest(long generation)
            {
                CompleteServerStartup(generation);
            }

            public bool MarkStartupRunningForTest(long generation)
            {
                return TryStartServerRunModules(generation) && TryMarkServerRunStarted(generation);
            }

            public void SetSocketForTest(IServerSocket serverSocket)
            {
                FieldInfo socketField = typeof(ServerBehaviour).GetField(
                    "socket",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(socketField, Is.Not.Null);
                socketField.SetValue(this, serverSocket);
            }

            public void Dispatch(IIncomingMessage message)
            {
                OnMessageReceived(message);
            }
        }

        private sealed class TestPeer : BasePeer
        {
            private bool isConnected = true;

            public override bool IsConnected => isConnected;

            public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
            {
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
        }

        private sealed class TestRunModule : BaseServerModule
        {
            private TaskCompletionSource<bool> stopEntered = CreateCompletionSource();
            private TaskCompletionSource<bool> stopRelease = CreateCompletionSource();

            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public CancellationToken RunToken { get; private set; }
            public TaskCompletionSource<bool> StopEntered => stopEntered;

            public override void Initialize(IServer server)
            {
            }

            public override void StartServerRun(CancellationToken runCancellationToken)
            {
                StartCount++;
                RunToken = runCancellationToken;
                stopEntered = CreateCompletionSource();
                stopRelease = CreateCompletionSource();
            }

            public override Task StopServerRunAsync()
            {
                StopCount++;
                stopEntered.TrySetResult(true);
                return stopRelease.Task;
            }

            public void ReleaseStop()
            {
                stopRelease.TrySetResult(true);
            }
        }

        private sealed class FailingStartRunModule : BaseServerModule
        {
            public int StopCount { get; private set; }

            public override void Initialize(IServer server)
            {
            }

            public override void StartServerRun(CancellationToken runCancellationToken)
            {
                throw new InvalidOperationException("Expected module start failure");
            }

            public override Task StopServerRunAsync()
            {
                StopCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FailingStartupValidatorModule : BaseServerModule, IServerStartupValidator
        {
            public override void Initialize(IServer server)
            {
            }

            public void ValidateServerStartup()
            {
                throw new InvalidOperationException("Expected startup validation failure");
            }
        }

        private sealed class SecondTestRunModule : BaseServerModule
        {
            private TaskCompletionSource<bool> stopEntered = CreateCompletionSource();
            private TaskCompletionSource<bool> stopRelease = CreateCompletionSource();

            public int StopCount { get; private set; }
            public TaskCompletionSource<bool> StopEntered => stopEntered;

            public override void Initialize(IServer server)
            {
            }

            public override void StartServerRun(CancellationToken runCancellationToken)
            {
                stopEntered = CreateCompletionSource();
                stopRelease = CreateCompletionSource();
            }

            public override Task StopServerRunAsync()
            {
                StopCount++;
                stopEntered.TrySetResult(true);
                return stopRelease.Task;
            }

            public void ReleaseStop()
            {
                stopRelease.TrySetResult(true);
            }
        }

        private sealed class CompletedStopRunModule : BaseServerModule
        {
            public int StopCount { get; private set; }

            public override void Initialize(IServer server)
            {
            }

            public override Task StopServerRunAsync()
            {
                StopCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FailingStopRunModule : BaseServerModule
        {
            public int StopCount { get; private set; }

            public override void Initialize(IServer server)
            {
            }

            public override Task StopServerRunAsync()
            {
                StopCount++;
                return Task.FromException(new InvalidOperationException("Expected module stop failure"));
            }
        }

        private sealed class TestServerSocket : IServerSocket
        {
            private int listenCount;
            private int stopCount;
            private int isListening;
            private readonly TaskCompletionSource<bool> stopEntered = CreateCompletionSource();

            public Action BeforeListenReturns { get; set; }
            public Action BeforeStopReturns { get; set; }
            public int ListenCount => Volatile.Read(ref listenCount);
            public int StopCount => Volatile.Read(ref stopCount);
            public Task StopEntered => stopEntered.Task;
            public bool IsListening => Volatile.Read(ref isListening) != 0;
            public bool UseSecure { get; set; }
            public string CertificatePath { get; set; } = string.Empty;
            public string CertificatePassword { get; set; } = string.Empty;
            public string Service { get; set; } = "test";
            public SslProtocols SslProtocols { get; set; }
            public MasterServerToolkit.Logging.LogLevel LogLevel { get; set; }

            public event Action<IServerSocket> OnBeforeServerStart;
            public event PeerActionHandler OnPeerConnectedEvent { add { } remove { } }
            public event PeerActionHandler OnPeerDisconnectedEvent { add { } remove { } }

            public void Listen(int port)
            {
                Listen("127.0.0.1", port);
            }

            public void Listen(string ip, int port)
            {
                Interlocked.Increment(ref listenCount);
                Volatile.Write(ref isListening, 1);
                OnBeforeServerStart?.Invoke(this);
                BeforeListenReturns?.Invoke();
            }

            public void Stop()
            {
                Interlocked.Increment(ref stopCount);
                stopEntered.TrySetResult(true);
                Volatile.Write(ref isListening, 0);
                BeforeStopReturns?.Invoke();
            }
        }
    }
}
