using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp;

#if UNITY_WEBGL && !UNITY_EDITOR
        using System.Collections;
        using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.Networking
{
    public class WebSocket
    {
        private readonly Logging.Logger logger;
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
                return null;

            return Encoding.UTF8.GetString(retval);
        }

#if UNITY_WEBGL && !UNITY_EDITOR

        int m_NativeRef = 0;

        [DllImport("__Internal")]
        private static extern int MstSocketCreate(string url);

        [DllImport("__Internal")]
        private static extern int MstSocketState(int socketInstance);

        [DllImport("__Internal")]
        private static extern int MstSocketCode(int socketInstance);

        [DllImport("__Internal")]
        private static extern void MstSocketSend(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern void MstSocketRecv(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        private static extern int MstSocketRecvLength(int socketInstance);

        [DllImport("__Internal")]
        private static extern void MstSocketClose(int socketInstance, ushort code, string reason);

        [DllImport("__Internal")]
        private static extern int MstSocketError(int socketInstance, byte[] ptr, int length);

        public bool IsConnected => MstSocketState(m_NativeRef) == 1;

        public int CloseCode => MstSocketCode(m_NativeRef);

        public void Send(byte[] buffer)
        {
            MstSocketSend(m_NativeRef, buffer, buffer.Length);
        }

        public byte[] Recv()
        {
            int length = MstSocketRecvLength(m_NativeRef);
            if (length == 0)
                return null;
            byte[] buffer = new byte[length];
            MstSocketRecv(m_NativeRef, buffer, length);
            return buffer;
        }

        public IEnumerator Connect()
        {
            m_NativeRef = MstSocketCreate(url.ToString());
            IsConnecting = true;
            while (MstSocketState(m_NativeRef) == 0)
                yield return 0;
            IsConnecting = false;
        }

        /// <summary>
        /// Close websocket client connection
        /// </summary>
        /// <param name="reason"></param>
        public void Close(string reason = "")
        {
            MstSocketClose(m_NativeRef, 1000, reason);
        }

        /// <summary>
        /// Close websocket client connection
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public void Close(ushort code, string reason = "")
        {
            MstSocketClose(m_NativeRef, code, reason);
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
                int result = MstSocketError(m_NativeRef, buffer, bufsize);

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
        /// 
        /// </summary>
        public ushort CloseCode { get; private set; }

        /// <summary>
        /// Current socket error
        /// </summary>
        public string Error { get; private set; } = string.Empty;

        /// <summary>
        /// Connect to server using timeout
        /// </summary>
        /// <returns></returns>
        public void Connect()
        {
            IsConnecting = true;

            // Create WebSocket instance with timeout info
            socket = new WebSocketSharp.WebSocket(url.ToString());

            // Listen to messages
            socket.OnMessage += (sender, e) =>
            {
                if (e.IsPing)
                {
                    logger.Info("Received Pong responce from server");
                }

                messages.Enqueue(e.RawData);
            };

            // Listen to connection open
            socket.OnOpen += (sender, e) =>
            {
                logger.Debug("WebSocket opened connection");
                IsConnected = true;
                IsConnecting = false;
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
                logger.Debug($"WebSocket closed connection with code [{args.Code}:{(CloseStatusCode)args.Code}]. Reason: [{args.Reason}]");

                CloseCode = args.Code;
                IsConnected = false;
                IsConnecting = false;
            };

            socket.ConnectAsync();
        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="buffer"></param>
        public void Send(byte[] buffer)
        {
            if (IsConnected)
                socket?.SendAsync(buffer, null);
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
            Close((ushort)CloseStatusCode.Normal, "Connection closed successfuly");
        }

        /// <summary>
        /// Close websocket client connection
        /// </summary>
        /// <param name="reason"></param>
        public void Close(string reason = "")
        {
            Close((ushort)CloseStatusCode.Normal, reason);
        }

        /// <summary>
        /// Close websocket client connection
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public void Close(ushort code, string reason = "")
        {
            socket?.CloseAsync(code, reason);
        }
#endif
    }
}