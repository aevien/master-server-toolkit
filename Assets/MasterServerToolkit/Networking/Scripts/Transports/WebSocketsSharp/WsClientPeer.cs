using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Concurrent;

namespace MasterServerToolkit.Networking
{
    public class WsClientPeer : BasePeer
    {
        private const ushort NormalClosureCode = 1000;

        private readonly WebSocket socket;
        private readonly ConcurrentQueue<Action> sendCompletionQueue = new ConcurrentQueue<Action>();

        public WsClientPeer(WebSocket socket)
        {
            this.socket = socket;
        }

        public override bool IsConnected => socket != null && socket.IsConnected;

        [Obsolete("No longer required. websocket-sharp preserves FIFO SendAsync ordering.")]
        public void BeginAuthentication()
        {
        }

        [Obsolete("No longer required. websocket-sharp preserves FIFO SendAsync ordering.")]
        public void CompleteAuthentication()
        {
        }

        [Obsolete("No longer required. Messages are sent immediately in FIFO order.")]
        public void SendDelayedMessages()
        {
        }

        [Obsolete("No longer required. Messages are sent immediately in FIFO order.")]
        public IEnumerator SendDelayedMessagesCoroutine()
        {
            yield break;
        }

        public override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod)
        {
            if (!IsConnected) return;

            SendNow(message);
        }

        private void SendNow(IOutgoingMessage message)
        {
            Mst.Traffic.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Outgoing);
            socket.Send(message.ToBytes());
        }

        protected override void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod,
            Action<bool> completionCallback)
        {
            if (!IsConnected)
            {
                sendCompletionQueue.Enqueue(() => completionCallback?.Invoke(false));
                return;
            }

            Mst.Traffic.RegisterOpCodeTrafic(message.OpCode, message.Data.LongLength, TrafficType.Outgoing);
            socket.Send(message.ToBytes(), isSuccessful =>
                sendCompletionQueue.Enqueue(() => completionCallback?.Invoke(isSuccessful)));
        }

        public void ProcessSendCompletions()
        {
            while (sendCompletionQueue.TryDequeue(out Action completion))
            {
                completion.Invoke();
            }
        }

        public override void Disconnect(string reason)
        {
            Disconnect(NormalClosureCode, reason);
        }

        public override void Disconnect(ushort code, string reason)
        {
            socket.Close(code, reason);
            BeginDisconnect();
        }

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MstTimer.Instance.StartCoroutine(socket.Connect());
#else
            socket.Connect();
#endif
        }
    }
}
