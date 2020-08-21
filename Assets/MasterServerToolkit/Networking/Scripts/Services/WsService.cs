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
        private ServerSocketWs _serverSocket;
        private Queue<byte[]> _messageQueue;

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

        public WsService()
        {
            IgnoreExtensions = true;
            _messageQueue = new Queue<byte[]>();
        }

        public WsService(ServerSocketWs serverSocket)
        {
            IgnoreExtensions = true;
            _messageQueue = new Queue<byte[]>();

            _serverSocket = serverSocket;
            _serverSocket.OnUpdateEvent += Update;
        }

        public void SetServerSocket(ServerSocketWs serverSocket)
        {
            if (_serverSocket == null)
            {
                _serverSocket = serverSocket;
                _serverSocket.OnUpdateEvent += Update;
            }
        }

        private void Update()
        {
            if (_messageQueue.Count <= 0)
            {
                return;
            }

            lock (_messageQueue)
            {
                // Notify about new messages
                while (_messageQueue.Count > 0)
                {
                    OnMessageEvent?.Invoke(_messageQueue.Dequeue());
                }
            }
        }

        protected override void OnOpen()
        {
            _serverSocket.ExecuteOnUpdate(() =>
            {
                OnOpenEvent?.Invoke();
            });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            _serverSocket.OnUpdateEvent -= Update;

            _serverSocket.ExecuteOnUpdate(() =>
            {
                OnCloseEvent?.Invoke(e.Reason);
            });
        }

        protected override void OnError(ErrorEventArgs e)
        {
            _serverSocket.OnUpdateEvent -= Update;

            _serverSocket.ExecuteOnUpdate(() =>
            {
                OnErrorEvent?.Invoke(e.Message);
            });
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            lock (_messageQueue)
            {
                _messageQueue.Enqueue(e.RawData);
            }
        }

        public void SendData(byte[] data)
        {
            Send(data);
        }

        public void Disconnect()
        {
            Sessions.CloseSession(ID);
        }
    }
}