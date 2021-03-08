using MasterServerToolkit.MasterServer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
        using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.Networking
{
    public class WebSocket
    {
        private Logging.Logger logger;
        private readonly Uri url;

        public bool IsConnecting { get; private set; } = false;

        /// <summary>
        /// Web socket instance
        /// </summary>
        /// <param name="url"></param>
        public WebSocket(Uri url)
        {
            logger = Mst.Create.Logger(typeof(WebSocket).Name);

            this.url = url;
            string protocol = this.url.Scheme;

            if (!protocol.Equals("ws") && !protocol.Equals("wss"))
            {
                throw new ArgumentException($"Unsupported protocol: {protocol}");
            }
        }

        /// <summary>
        /// Send string data
        /// </summary>
        /// <param name="str"></param>
        public void SendString(string str)
        {
            Send(Encoding.UTF8.GetBytes(str));
        }

        /// <summary>
        /// Receive string data
        /// </summary>
        /// <returns></returns>
        public string RecvString()
        {
            byte[] retval = Recv();
            if (retval == null)
            {
                return null;
            }

            return Encoding.UTF8.GetString(retval);
        }

#if UNITY_WEBGL && !UNITY_EDITOR

        int m_NativeRef = 0;

        public bool IsConnected { get { return MsfSocketState(m_NativeRef) == 1; } }

        [DllImport("__Internal")]
        private static extern int MsfSocketCreate(string url);

        [DllImport("__Internal")]
        private static extern int MsfSocketState(int socketInstance);

        [DllImport("__Internal")]
        private static extern void MsfSocketSend(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern void MsfSocketRecv(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern int MsfSocketRecvLength(int socketInstance);

        [DllImport("__Internal")]
        private static extern void MsfSocketClose(int socketInstance);

        [DllImport("__Internal")]
        private static extern int MsfSocketError(int socketInstance, byte[] ptr, int length);

        public void Send(byte[] buffer)
        {
            MsfSocketSend(m_NativeRef, buffer, buffer.Length);
        }

        public byte[] Recv()
        {
            int length = MsfSocketRecvLength(m_NativeRef);
            if (length == 0)
                return null;
            byte[] buffer = new byte[length];
            MsfSocketRecv(m_NativeRef, buffer, length);
            return buffer;
        }

        public IEnumerator Connect()
        {
            m_NativeRef = MsfSocketCreate(url.ToString());
            IsConnecting = true;
            while (MsfSocketState(m_NativeRef) == 0)
                yield return 0;
            IsConnecting = false;
        }

        /// <summary>
        /// Close web socket connection
        /// </summary>
        public void Close()
        {
            MsfSocketClose(m_NativeRef);
        }

        /// <summary>
        /// Websocket error
        /// </summary>
        public string Error
        {
            get
            {
                const int bufsize = 1024;
                byte[] buffer = new byte[bufsize];
                int result = MsfSocketError(m_NativeRef, buffer, bufsize);

                if (result == 0)
                    return null;

                return Encoding.UTF8.GetString(buffer);
            }
        }
#else
        /// <summary>
        /// List of messages in queue
        /// </summary>
        private readonly Queue<byte[]> messages = new Queue<byte[]>();

        /// <summary>
        /// Socket instanse
        /// </summary>
        private WebSocketSharp.WebSocket socket;

        /// <summary>
        /// Connection status Connected/Disconnected
        /// </summary>
        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Current socket error
        /// </summary>
        public string Error { get; private set; } = string.Empty;

        /// <summary>
        /// Connect to server using timeout
        /// </summary>
        /// <returns></returns>
        public IEnumerator Connect()
        {
            // Create WebSocket instance with timeout info
            socket = new WebSocketSharp.WebSocket(url.ToString());

            // Listen to messages
            socket.OnMessage += (sender, e) =>
            {
                messages.Enqueue(e.RawData);
            };

            // Listen to connection open
            socket.OnOpen += (sender, e) =>
            {
                logger.Debug("WebSocket opened connection");
                IsConnected = true;
            };

            // Listen to errors
            socket.OnError += (sender, e) =>
            {
                Error = e.Message;
                logger.Error(e.Exception);
            };

            // Listen to connection close
            socket.OnClose += (sender, args) =>
            {
                logger.Debug("WebSocket closed connection");
                IsConnected = false;
            };

            socket.ConnectAsync();

            IsConnecting = true;

            while (!IsConnected && Error == null)
            {
                yield return null;
            }

            IsConnecting = false;
        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="buffer"></param>
        public void Send(byte[] buffer)
        {
            socket.Send(buffer);
        }

        /// <summary>
        /// Receive message
        /// </summary>
        /// <returns></returns>
        public byte[] Recv()
        {
            if (messages.Count == 0)
            {
                return null;
            }

            return messages.Dequeue();
        }

        /// <summary>
        /// Close websocket client connection
        /// </summary>
        public void Close()
        {
            socket.Close();
        }
#endif
    }
}