using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.Tests.EditMode
{
    internal sealed class FakeClientSocket : IClientSocket, IConnectionPermissionCredentials
    {
        private readonly Dictionary<ushort, IPacketHandler> handlers = new Dictionary<ushort, IPacketHandler>();
        private int nextAckId = 1;

        public FakeClientSocket(bool isConnected = true)
        {
            Status = isConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected;
        }

        public string Id { get; } = Guid.NewGuid().ToString("N");
        public ushort CloseCode { get; private set; }
        public LogLevel LogLevel { get; set; }
        public ConnectionStatus Status { get; private set; }
        public bool IsConnected => Status == ConnectionStatus.Connected;
        public bool IsConnecting => Status == ConnectionStatus.Connecting || Status == ConnectionStatus.Authenticating;
        public string Address { get; private set; } = "127.0.0.1";
        public int Port { get; private set; }
        public bool UseSecure { get; set; }
        public string Service { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string PermissionCredential { get; set; } = string.Empty;
        public ushort? ThrowOnSendOpcode { get; set; }
        public ushort? ThrowOnRegisterOpcode { get; set; }
        public Action<SentRequest> OnRequestSent { get; set; }
        public List<IOutgoingMessage> SentMessages { get; } = new List<IOutgoingMessage>();
        public List<SentRequest> Requests { get; } = new List<SentRequest>();
        public int RegisteredHandlerCount => handlers.Count;

        public event ConnectionDelegate OnConnectionOpenEvent;
        public event ConnectionDelegate OnConnectionCloseEvent;
        public event ConnectionStatusDelegate OnStatusChangedEvent;

        public IClientSocket Connect(string ip, int port, float timeoutSeconds)
        {
            Address = ip;
            Port = port;
            SetStatus(ConnectionStatus.Connected);
            OnConnectionOpenEvent?.Invoke(this);
            return this;
        }

        public IClientSocket Connect(string ip, int port)
        {
            return Connect(ip, port, 0f);
        }

        public void WaitForConnection(ConnectionDelegate connectionCallback, float timeoutSeconds)
        {
            connectionCallback?.Invoke(this);
        }

        public void WaitForConnection(ConnectionDelegate connectionCallback)
        {
            WaitForConnection(connectionCallback, 0f);
        }

        public void AddConnectionOpenListener(ConnectionDelegate callback, bool invokeInstantlyIfConnected = true)
        {
            OnConnectionOpenEvent += callback;

            if (invokeInstantlyIfConnected && IsConnected)
                callback?.Invoke(this);
        }

        public void RemoveConnectionOpenListener(ConnectionDelegate callback)
        {
            OnConnectionOpenEvent -= callback;
        }

        public void AddConnectionCloseListener(ConnectionDelegate callback, bool invokeInstantlyIfDisconnected = true)
        {
            OnConnectionCloseEvent += callback;

            if (invokeInstantlyIfDisconnected && !IsConnected && !IsConnecting)
                callback?.Invoke(this);
        }

        public void RemoveConnectionCloseListener(ConnectionDelegate callback)
        {
            OnConnectionCloseEvent -= callback;
        }

        public IPacketHandler RegisterMessageHandler(IPacketHandler handler)
        {
            if (ThrowOnRegisterOpcode == handler.OpCode)
                throw new InvalidOperationException($"Registration failed for opcode {handler.OpCode}");

            handlers[handler.OpCode] = handler;
            return handler;
        }

        public IPacketHandler RegisterMessageHandler(ushort opCode, IncommingMessageHandler handlerMethod)
        {
            return RegisterMessageHandler(new PacketHandler(opCode, handlerMethod));
        }

        public void UnregisterMessageHandler(IPacketHandler handler)
        {
            if (handler != null && handlers.TryGetValue(handler.OpCode, out IPacketHandler registered)
                && ReferenceEquals(handler, registered))
            {
                handlers.Remove(handler.OpCode);
            }
        }

        public void UnregisterMessageHandler(ushort opCode)
        {
            handlers.Remove(opCode);
        }

        public void Reconnect(bool fireEvent = true)
        {
            Close(fireEvent);
            SetStatus(ConnectionStatus.Connected);

            if (fireEvent)
                OnConnectionOpenEvent?.Invoke(this);
        }

        public void Close(bool fireEvent = true)
        {
            Close(0, string.Empty, fireEvent);
        }

        public void Close(ushort code, bool fireEvent = true)
        {
            Close(code, string.Empty, fireEvent);
        }

        public void Close(ushort code, string reason, bool fireEvent = true)
        {
            CloseCode = code;
            SetStatus(ConnectionStatus.Disconnected);

            if (fireEvent)
                OnConnectionCloseEvent?.Invoke(this);
        }

        public void SetStatusForTest(ConnectionStatus status)
        {
            SetStatus(status);
        }

        public void SendMessage(IOutgoingMessage message)
        {
            SendMessage(message, DeliveryMethod.Reliable);
        }

        public void SendMessage(IOutgoingMessage message, DeliveryMethod method)
        {
            ThrowIfConfigured(message);
            SentMessages.Add(message);
        }

        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback)
        {
            return SendMessage(message, responseCallback, 0);
        }

        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            ThrowIfConfigured(message);

            var request = new SentRequest(nextAckId++, message, responseCallback, timeoutSecs);
            Requests.Add(request);
            OnRequestSent?.Invoke(request);
            return request.AckId;
        }

        public SentRequest GetPendingRequest(ushort opCode)
        {
            return Requests.First(request => !request.IsCompleted && request.Message.OpCode == opCode);
        }

        public void RespondNext(ushort opCode, ResponseStatus status, byte[] data = null)
        {
            GetPendingRequest(opCode).Respond(status, data);
        }

        public void RespondNext(ushort requestOpCode, ResponseStatus status,
            ushort responseOpCode, byte[] data)
        {
            GetPendingRequest(requestOpCode).Respond(status, responseOpCode, data);
        }

        public void RespondNextWithNull(ushort opCode, ResponseStatus status)
        {
            GetPendingRequest(opCode).RespondWithNull(status);
        }

        public void Deliver(ushort opCode, byte[] data)
        {
            if (!handlers.TryGetValue(opCode, out IPacketHandler handler))
                throw new InvalidOperationException($"No handler is registered for opcode {opCode}");

            handler.Handle(CreateIncomingMessage(opCode, ResponseStatus.Success, data));
        }

        private static IIncomingMessage CreateIncomingMessage(ushort opCode, ResponseStatus status, byte[] data)
        {
            return new IncomingMessage(opCode, 0, data ?? Array.Empty<byte>(), DeliveryMethod.Reliable, null)
            {
                Status = status
            };
        }

        private void ThrowIfConfigured(IOutgoingMessage message)
        {
            if (ThrowOnSendOpcode == message.OpCode)
                throw new InvalidOperationException($"Send failed for opcode {message.OpCode}");
        }

        private void SetStatus(ConnectionStatus status)
        {
            Status = status;
            OnStatusChangedEvent?.Invoke(status);
        }

        internal sealed class SentRequest
        {
            private readonly ResponseCallback callback;

            public SentRequest(int ackId, IOutgoingMessage message, ResponseCallback callback, int timeoutSeconds)
            {
                AckId = ackId;
                Message = message;
                this.callback = callback;
                TimeoutSeconds = timeoutSeconds;
            }

            public int AckId { get; }
            public IOutgoingMessage Message { get; }
            public int TimeoutSeconds { get; }
            public bool IsCompleted { get; private set; }

            public void Respond(ResponseStatus status, byte[] data = null)
            {
                Complete(status, CreateIncomingMessage(Message.OpCode, status, data));
            }

            public void Respond(ResponseStatus status, ushort responseOpCode, byte[] data)
            {
                Complete(status, CreateIncomingMessage(responseOpCode, status, data));
            }

            public void RespondWithNull(ResponseStatus status)
            {
                Complete(status, null);
            }

            private void Complete(ResponseStatus status, IIncomingMessage response)
            {
                if (IsCompleted)
                    throw new InvalidOperationException("Request was already completed");

                IsCompleted = true;
                callback?.Invoke(status, response);
            }
        }
    }
}
