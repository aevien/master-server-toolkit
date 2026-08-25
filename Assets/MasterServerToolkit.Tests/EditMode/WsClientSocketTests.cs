using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using NUnit.Framework;
using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class WsClientSocketTests
    {
        private const ushort ClientUpdateFailureCloseCode = 4000;

        private static readonly MethodInfo runnerUpdateMethod = typeof(MstUpdateRunner).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo runnerInstanceField = typeof(SingletonBehaviour<MstUpdateRunner>).GetField(
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo runnerWasCreatedField = typeof(SingletonBehaviour<MstUpdateRunner>).GetField(
            "_wasCreated",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo runnerCreationHasPendingConfigField =
            typeof(SingletonBehaviour<MstUpdateRunner>).GetField(
                "_creationHasPendingConfig",
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo socketTransportField = typeof(WsClientSocket).GetField(
            "webSocket",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo socketPeerField = typeof(WsClientSocket).GetField(
            "_peer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo socketWasTransportConnectedField = typeof(WsClientSocket).GetField(
            "wasTransportConnected",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo pendingConnectionWaitsField = typeof(WsClientSocket).GetField(
            "pendingConnectionWaits",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Type pendingConnectionWaitType = typeof(WsClientSocket).GetNestedType(
            "PendingConnectionWait",
            BindingFlags.NonPublic);

        private static readonly PropertyInfo transportIsConnectedProperty = typeof(WebSocket).GetProperty(
            "IsConnected",
            BindingFlags.Instance | BindingFlags.Public);

        private static readonly MethodInfo transportResetReceivedMessagesMethod =
            typeof(WebSocket).GetMethod(
                "ResetReceivedMessages",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo transportTryEnqueueReceivedMessageMethod =
            typeof(WebSocket).GetMethod(
                "TryEnqueueReceivedMessage",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private WsClientSocket activeSocket;
        private MstUpdateRunner runner;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Mst.Create, Is.Not.Null);
            MessageHelper.SetFactory(new MessageFactory());
            DestroyRunner();
        }

        [TearDown]
        public void TearDown()
        {
            MessageHelper.SetFactory(new MessageFactory());
            activeSocket?.Close(false);
            activeSocket = null;
            DestroyRunner();
        }

        [Test]
        public void UpdateFailure_ClosesSocketAndCompletesPendingOperationsOnce()
        {
            DeferredSendPeer peer = CreateConnectedSocket();
            int closeCallbackCount = 0;
            int waitCallbackCount = 0;
            int responseCallbackCount = 0;
            int failingResponseCallbackCount = 0;
            ResponseStatus responseStatus = ResponseStatus.Success;
            IIncomingMessage terminalResponse = null;

            activeSocket.AddConnectionCloseListener(_ => closeCallbackCount++, false);
            AddPendingConnectionWait(activeSocket, _ => waitCallbackCount++);

            int failingAckId = peer.SendMessage(MessageHelper.Create(1), (_, _) =>
            {
                failingResponseCallbackCount++;
                throw new InvalidOperationException("Expected response callback failure");
            });

            int ackId = peer.SendMessage(MessageHelper.Create(1), (status, response) =>
            {
                responseCallbackCount++;
                responseStatus = status;
                terminalResponse = response;
            });

            Assert.That(failingAckId, Is.GreaterThan(0));
            Assert.That(ackId, Is.GreaterThan(0));
            MessageHelper.SetFactory(new ThrowingMessageFactory());
            RegisterSocket();
            LogHandler throwingAppender = (_, _, _, _) =>
                throw new InvalidOperationException("Expected appender failure");
            LogManager.AddAppender(throwingAppender);

            try
            {
                LogAssert.Expect(LogType.Error, new Regex("Expected message factory failure"));
                LogAssert.Expect(LogType.Error, new Regex("WebSocket client update failed.*Expected appender failure"));
                LogAssert.Expect(LogType.Error, new Regex("Response callback failed.*Expected response callback failure"));
                Assert.DoesNotThrow(activeSocket.DoUpdate);
            }
            finally
            {
                LogManager.RemoveAppender(throwingAppender);
            }

            Assert.That(activeSocket.Status, Is.EqualTo(ConnectionStatus.Disconnected));
            Assert.That(activeSocket.IsConnected, Is.False);
            Assert.That(activeSocket.CloseCode, Is.EqualTo(ClientUpdateFailureCloseCode));
            Assert.That(MstUpdateRunner.Contains(activeSocket), Is.False);
            Assert.That(socketTransportField.GetValue(activeSocket), Is.Null);
            Assert.That(socketPeerField.GetValue(activeSocket), Is.Null);
            Assert.That(closeCallbackCount, Is.EqualTo(1));
            Assert.That(waitCallbackCount, Is.EqualTo(1));
            Assert.That(failingResponseCallbackCount, Is.EqualTo(1));
            Assert.That(responseCallbackCount, Is.EqualTo(1));
            Assert.That(responseStatus, Is.EqualTo(ResponseStatus.NotConnected));
            Assert.That(terminalResponse, Is.Not.Null);

            Assert.DoesNotThrow(activeSocket.DoUpdate);
            Assert.That(closeCallbackCount, Is.EqualTo(1));
            Assert.That(waitCallbackCount, Is.EqualTo(1));
            Assert.That(failingResponseCallbackCount, Is.EqualTo(1));
            Assert.That(responseCallbackCount, Is.EqualTo(1));
        }

        [TestCase(true, ConnectionStatus.Connected)]
        [TestCase(false, ConnectionStatus.Connecting)]
        public void UpdateFailure_WhileTransportStops_ReportsFailureCloseCodeOnce(
            bool transportWasConnected,
            ConnectionStatus status)
        {
            CreateConnectedSocket();
            var transport = (WebSocket)socketTransportField.GetValue(activeSocket);
            SetPrivateProperty(transportIsConnectedProperty, transport, false);
            SetPrivateProperty(typeof(WsClientSocket).GetProperty(nameof(WsClientSocket.IsConnected)), activeSocket, false);
            socketWasTransportConnectedField.SetValue(activeSocket, transportWasConnected);
            activeSocket.Status = status;
            MessageHelper.SetFactory(new ThrowingMessageFactory());
            int closeCallbackCount = 0;
            ushort callbackCloseCode = 0;

            activeSocket.AddConnectionCloseListener(socket =>
            {
                closeCallbackCount++;
                callbackCloseCode = socket.CloseCode;
            }, false);

            ExpectUpdateFailureLogs();
            Assert.DoesNotThrow(activeSocket.DoUpdate);

            Assert.That(activeSocket.Status, Is.EqualTo(ConnectionStatus.Disconnected));
            Assert.That(activeSocket.CloseCode, Is.EqualTo(ClientUpdateFailureCloseCode));
            Assert.That(callbackCloseCode, Is.EqualTo(ClientUpdateFailureCloseCode));
            Assert.That(closeCallbackCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateFailure_WhenLoggingAppenderThrows_StillClosesSocket()
        {
            CreateConnectedSocket();
            MessageHelper.SetFactory(new ThrowingMessageFactory());
            LogHandler throwingAppender = (_, _, _, _) =>
                throw new InvalidOperationException("Expected appender failure");
            LogManager.AddAppender(throwingAppender);

            try
            {
                LogAssert.Expect(LogType.Error, new Regex("Expected message factory failure"));
                LogAssert.Expect(LogType.Error, new Regex("WebSocket client update failed.*Expected appender failure"));
                Assert.DoesNotThrow(activeSocket.DoUpdate);
            }
            finally
            {
                LogManager.RemoveAppender(throwingAppender);
            }

            Assert.That(activeSocket.Status, Is.EqualTo(ConnectionStatus.Disconnected));
            Assert.That(activeSocket.CloseCode, Is.EqualTo(ClientUpdateFailureCloseCode));
            Assert.That(socketTransportField.GetValue(activeSocket), Is.Null);
            Assert.That(socketPeerField.GetValue(activeSocket), Is.Null);
        }

        [Test]
        public void UpdateFailure_FromReplacedConnection_DoesNotCloseReplacement()
        {
            CreateConnectedSocket();
            WebSocket replacementTransport = null;
            DeferredSendPeer replacementPeer = null;
            MessageHelper.SetFactory(new ThrowingMessageFactory(() =>
            {
                activeSocket.Close(false);
                replacementPeer = InstallConnectedTransport(false, out replacementTransport);
            }));

            ExpectUpdateFailureLogs();
            Assert.DoesNotThrow(activeSocket.DoUpdate);

            Assert.That(replacementTransport, Is.Not.Null);
            Assert.That(replacementPeer, Is.Not.Null);
            Assert.That(activeSocket.Status, Is.EqualTo(ConnectionStatus.Connected));
            Assert.That(activeSocket.IsConnected, Is.True);
            Assert.That(socketTransportField.GetValue(activeSocket), Is.SameAs(replacementTransport));
            Assert.That(socketPeerField.GetValue(activeSocket), Is.SameAs(replacementPeer));
        }

        [Test]
        public void UpdateFailure_WhenCloseCallbackReRegistersSocket_PreservesRegistration()
        {
            CreateConnectedSocket();
            MessageHelper.SetFactory(new ThrowingMessageFactory());
            activeSocket.AddConnectionCloseListener(_ => MstUpdateRunner.Add(activeSocket), false);
            RegisterSocket();

            ExpectUpdateFailureLogs();
            Assert.DoesNotThrow(RunUpdate);

            Assert.That(activeSocket.Status, Is.EqualTo(ConnectionStatus.Disconnected));
            Assert.That(MstUpdateRunner.Contains(activeSocket), Is.True);
            Assert.That(runner.Count, Is.EqualTo(1));
        }

        private DeferredSendPeer CreateConnectedSocket()
        {
            AssertReflectionContract();
            activeSocket = new WsClientSocket();
            return InstallConnectedTransport(true, out _);
        }

        private DeferredSendPeer InstallConnectedTransport(bool enqueueFailureMessage, out WebSocket transport)
        {
            transport = new WebSocket(new Uri("ws://localhost:1/mst"));
            SetPrivateProperty(transportIsConnectedProperty, transport, true);
            transportResetReceivedMessagesMethod.Invoke(transport, new object[] { true });

            if (enqueueFailureMessage)
            {
                object[] enqueueArguments = { new byte[] { 1 }, null };
                bool enqueued = (bool)transportTryEnqueueReceivedMessageMethod.Invoke(
                    transport,
                    enqueueArguments);
                Assert.That(enqueued, Is.True, enqueueArguments[1] as string);
            }

            var peer = new DeferredSendPeer(transport);
            activeSocket.Peer = peer;
            activeSocket.Status = ConnectionStatus.Connected;

            SetPrivateProperty(typeof(WsClientSocket).GetProperty(nameof(WsClientSocket.IsConnected)), activeSocket, true);
            SetPrivateProperty(typeof(WsClientSocket).GetProperty(nameof(WsClientSocket.Address)), activeSocket, "localhost");
            SetPrivateProperty(typeof(WsClientSocket).GetProperty(nameof(WsClientSocket.Port)), activeSocket, 1);
            socketTransportField.SetValue(activeSocket, transport);
            socketPeerField.SetValue(activeSocket, peer);
            socketWasTransportConnectedField.SetValue(activeSocket, true);

            return peer;
        }

        private void RegisterSocket()
        {
            MstUpdateRunner.Add(activeSocket);
            runner = GetRunner();
            Assert.That(runner, Is.Not.Null);
            Assert.That(MstUpdateRunner.Contains(activeSocket), Is.True);
        }

        private void RunUpdate()
        {
            Assert.That(runnerUpdateMethod, Is.Not.Null);
            runnerUpdateMethod.Invoke(runner, null);
        }

        private static void AddPendingConnectionWait(WsClientSocket socket, ConnectionDelegate callback)
        {
            ConstructorInfo constructor = pendingConnectionWaitType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ConnectionDelegate) },
                null);

            Assert.That(constructor, Is.Not.Null);
            var waits = (IList)pendingConnectionWaitsField.GetValue(socket);
            waits.Add(constructor.Invoke(new object[] { callback }));
        }

        private static void SetPrivateProperty(PropertyInfo property, object target, object value)
        {
            Assert.That(property, Is.Not.Null);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(target, new[] { value });
        }

        private static void ExpectUpdateFailureLogs()
        {
            LogAssert.Expect(LogType.Error, new Regex("Expected message factory failure"));
            LogAssert.Expect(LogType.Error, new Regex("WebSocket client update failed.*Expected message factory failure"));
        }

        private static void AssertReflectionContract()
        {
            Assert.That(socketTransportField, Is.Not.Null);
            Assert.That(socketPeerField, Is.Not.Null);
            Assert.That(socketWasTransportConnectedField, Is.Not.Null);
            Assert.That(pendingConnectionWaitsField, Is.Not.Null);
            Assert.That(pendingConnectionWaitType, Is.Not.Null);
            Assert.That(transportIsConnectedProperty, Is.Not.Null);
            Assert.That(transportResetReceivedMessagesMethod, Is.Not.Null);
            Assert.That(transportTryEnqueueReceivedMessageMethod, Is.Not.Null);
        }

        private static MstUpdateRunner GetRunner()
        {
            Assert.That(runnerInstanceField, Is.Not.Null);
            return (MstUpdateRunner)runnerInstanceField.GetValue(null);
        }

        private static void DestroyRunner()
        {
            MstUpdateRunner currentRunner = GetRunner();

            if (currentRunner != null)
                UnityEngine.Object.DestroyImmediate(currentRunner.gameObject);

            Assert.That(runnerWasCreatedField, Is.Not.Null);
            Assert.That(runnerCreationHasPendingConfigField, Is.Not.Null);
            runnerInstanceField.SetValue(null, null);
            runnerWasCreatedField.SetValue(null, false);
            runnerCreationHasPendingConfigField.SetValue(null, false);
        }

        private sealed class DeferredSendPeer : WsClientPeer
        {
            public DeferredSendPeer(WebSocket socket) : base(socket)
            {
            }

            public override bool IsConnected => true;

            public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
            {
            }

            protected override void SendMessage(
                IOutgoingMessage message,
                DeliveryMethod deliveryMethod,
                Action<bool> completionCallback)
            {
            }
        }

        private sealed class ThrowingMessageFactory : IMessageFactory
        {
            private readonly MessageFactory innerFactory = new MessageFactory();
            private readonly Action beforeThrow;

            public ThrowingMessageFactory(Action beforeThrow = null)
            {
                this.beforeThrow = beforeThrow;
            }

            public IOutgoingMessage Create(ushort opCode)
            {
                return innerFactory.Create(opCode);
            }

            public IOutgoingMessage Create(ushort opCode, byte[] data)
            {
                return innerFactory.Create(opCode, data);
            }

            public IIncomingMessage FromBytes(byte[] buffer, int start, IPeer peer)
            {
                beforeThrow?.Invoke();
                throw new InvalidOperationException("Expected message factory failure");
            }
        }
    }
}
