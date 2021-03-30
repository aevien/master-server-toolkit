using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Web socket service, designed to work with unitys main thread
    /// </summary>
    public class WsService : WebSocketBehavior
    {
        private WsServerSocket serverSocket;
        private Queue<byte[]> messageQueue = new Queue<byte[]>();

        /// <summary>
        /// 
        /// </summary>
        public event Action OnOpenEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<string> OnCloseEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<string> OnErrorEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<byte[]> OnMessageEvent;

        public void SetServerSocket(WsServerSocket serverSocket)
        {
            if (this.serverSocket == null)
            {
                this.serverSocket = serverSocket;
                this.serverSocket.OnUpdateEvent += Update;
            }
        }

        private void Update()
        {
            if (messageQueue.Count <= 0)
            {
                return;
            }

            lock (messageQueue)
            {
                while (messageQueue.Count > 0)
                {
                    OnMessageEvent?.Invoke(messageQueue.Dequeue());
                }
            }
        }

        protected override void OnOpen()
        {
            serverSocket.ExecuteOnUpdate(() =>
            {
                OnOpenEvent?.Invoke();
            });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            serverSocket.OnUpdateEvent -= Update;

            serverSocket.ExecuteOnUpdate(() =>
            {
                OnCloseEvent?.Invoke(e.Reason);
            });
        }

        protected override void OnError(ErrorEventArgs e)
        {
            serverSocket.OnUpdateEvent -= Update;

            serverSocket.ExecuteOnUpdate(() =>
            {
                OnErrorEvent?.Invoke(e.Message);
            });
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            lock (messageQueue)
            {
                messageQueue.Enqueue(e.RawData);
            }
        }

        public void SendAsync(byte[] data)
        {
            SendAsync(data, null);
        }

        public void CloseAsync(string reason)
        {
            CloseAsync(CloseStatusCode.Normal, reason);
        }
    }
}