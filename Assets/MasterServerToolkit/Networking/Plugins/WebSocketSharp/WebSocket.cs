using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Text;
#if !UNITY_WEBGL || UNITY_EDITOR
using WebSocketSharp;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        using System.Collections;
        using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.Networking
{
    public class WebSocket : IDisposable
    {
        private readonly Logging.Logger logger;
        private readonly Uri url;

        public bool IsConnecting { get; private set; } = false;
        public long MaxFramePayloadLength { get; set; } = MstNetworkLimits.MaxFramePayloadByteCount;
        public long MaxMessagePayloadLength { get; set; } = MstNetworkLimits.MaxWireMessageByteCount;

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

#if UNITY_WEBGL && !UNITY_EDITOR

        int m_NativeRef = -1;

        [DllImport("__Internal")]
        private static extern int MstSocketCreate(
            string url,
            int maxMessageBytes,
            int maxQueuedMessages,
            int maxQueuedBytes);

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
        private static extern void MstSocketRelease(int socketInstance);

        [DllImport("__Internal")]
        private static extern int MstSocketError(int socketInstance, byte[] ptr, int length);

        public bool IsConnected => MstSocketState(m_NativeRef) == 1;

        public int CloseCode => MstSocketCode(m_NativeRef);

        public void Send(byte[] buffer)
        {
            Send(buffer, null);
        }

        public void Send(byte[] buffer, Action<bool> completed)
        {
            bool isSuccessful = false;

            try
            {
                if (IsConnected)
                {
                    MstSocketSend(m_NativeRef, buffer, buffer.Length);
                    isSuccessful = true;
                }
            }
            finally
            {
                completed?.Invoke(isSuccessful);
            }
        }

        public byte[] Recv()
        {
            int length = MstSocketRecvLength(m_NativeRef);
            if (length == 0)
                return null;

            if (length < 0 || length > MaxMessagePayloadLength)
            {
                MstSocketClose(m_NativeRef, 1009, "Message payload is too large");
                return null;
            }

            byte[] buffer = new byte[length];
            MstSocketRecv(m_NativeRef, buffer, length);
            return buffer;
        }

        public IEnumerator Connect()
        {
            m_NativeRef = MstSocketCreate(
                url.ToString(),
                checked((int)MaxMessagePayloadLength),
                MstNetworkLimits.MaxQueuedIncomingMessageCount,
                MstNetworkLimits.MaxQueuedIncomingMessageByteCount);
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
            if (m_NativeRef >= 0)
                MstSocketClose(m_NativeRef, 1000, NormalizeCloseReason(reason));
        }

        /// <summary>
        /// Close websocket client connection
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public void Close(ushort code, string reason = "")
        {
            if (m_NativeRef >= 0)
                MstSocketClose(m_NativeRef, code, NormalizeCloseReason(reason));
        }

        public void Dispose()
        {
            int nativeRef = m_NativeRef;
            m_NativeRef = -1;

            if (nativeRef >= 0)
                MstSocketRelease(nativeRef);
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

                if (result <= 0)
                    return null;

                return Encoding.UTF8.GetString(buffer, 0, Math.Min(result, buffer.Length));
            }
        }
#else

        /// <summary>
        /// List of messages in queue
        /// </summary>
        private readonly Queue<byte[]> messages = new Queue<byte[]>();
        private readonly object messagesSync = new object();
        private long queuedMessageByteCount;
        private bool acceptsIncomingMessages;

        /// <summary>
        /// Socket instanse
        /// </summary>
        private WebSocketSharp.WebSocket socket;
        private bool closeRequested;

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
            closeRequested = false;
            ResetReceivedMessages(true);

            // Create WebSocket instance with timeout info
            var activeSocket = new WebSocketSharp.WebSocket(url.ToString());
            activeSocket.MaxFramePayloadLength = MaxFramePayloadLength;
            activeSocket.MaxMessagePayloadLength = MaxMessagePayloadLength;
            socket = activeSocket;

            activeSocket.Log.Level = logger.LogLevel switch
            {
                Logging.LogLevel.Trace or Logging.LogLevel.All or Logging.LogLevel.Global => LogLevel.Trace,
                Logging.LogLevel.Debug => LogLevel.Debug,
                Logging.LogLevel.Info => LogLevel.Info,
                Logging.LogLevel.Warn => LogLevel.Warn,
                Logging.LogLevel.Error => LogLevel.Error,
                Logging.LogLevel.Fatal => LogLevel.Fatal,
                _ => LogLevel.None,
            };

            activeSocket.Log.Output = (logData, value) =>
            {
                if (!ReferenceEquals(socket, activeSocket))
                    return;

                if (IsWebSocketKeepAliveTrace(logData))
                    return;

                switch (logData.Level)
                {
                    case WebSocketSharp.LogLevel.Error:
                        logger.Error(logData);
                        break;
                    case WebSocketSharp.LogLevel.Fatal:
                        if (IsExpectedConnectionLifecycleLog(logData))
                            logger.Warn(logData);
                        else
                            logger.Fatal(logData);
                        break;
                    case WebSocketSharp.LogLevel.Info:
                        logger.Info(logData);
                        break;
                    case WebSocketSharp.LogLevel.Debug:
                        logger.Debug(logData);
                        break;
                    case WebSocketSharp.LogLevel.Warn:
                        logger.Warn(logData);
                        break;
                    case WebSocketSharp.LogLevel.Trace:
                        logger.Trace(logData);
                        break;
                    default:
                        logger.Info(logData);
                        break;
                }
            };

            // Listen to messages
            activeSocket.OnMessage += (sender, e) =>
            {
                if (!ReferenceEquals(socket, activeSocket))
                    return;

                if (e.IsPing)
                {
                    return;
                }

                if (!TryEnqueueReceivedMessage(e.RawData, out string rejectionReason) &&
                    !string.IsNullOrEmpty(rejectionReason))
                {
                    Error = rejectionReason;
                    closeRequested = true;
                    activeSocket.CloseAsync(1009, rejectionReason);
                }
            };

            // Listen to connection open
            activeSocket.OnOpen += (sender, e) =>
            {
                if (!ReferenceEquals(socket, activeSocket))
                    return;

                logger.Debug("WebSocket opened connection");
                IsConnected = true;
                IsConnecting = false;
            };

            // Listen to errors
            activeSocket.OnError += (sender, e) =>
            {
                if (!ReferenceEquals(socket, activeSocket))
                    return;

                Error = e.Message;
                if (!IsConnected)
                    IsConnecting = false;

                logger.Error(e.Message);
            };

            // Listen to connection close
            activeSocket.OnClose += (sender, args) =>
            {
                if (!ReferenceEquals(socket, activeSocket))
                    return;

                logger.Debug($"WebSocket closed connection with code [{args.Code}:{(CloseStatusCode)args.Code}]. Reason: [{args.Reason}]");

                CloseCode = args.Code;
                IsConnected = false;
                IsConnecting = false;
                StopAcceptingReceivedMessages();
            };

            activeSocket.ConnectAsync();
        }

        private bool IsExpectedConnectionLifecycleLog(LogData logData)
        {
            return closeRequested
                || (!IsConnected
                    && IsConnecting
                    && !string.IsNullOrEmpty(logData.Message)
                    && logData.Message.IndexOf("non-connected sockets", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsWebSocketKeepAliveTrace(LogData logData)
        {
            if (logData.Level != LogLevel.Trace || string.IsNullOrEmpty(logData.Message))
                return false;

            return logData.Message.IndexOf("processPingFrame", StringComparison.OrdinalIgnoreCase) >= 0
                || logData.Message.IndexOf("processPongFrame", StringComparison.OrdinalIgnoreCase) >= 0
                || logData.Message.IndexOf("A ping was received", StringComparison.OrdinalIgnoreCase) >= 0
                || logData.Message.IndexOf("A pong to this ping has been sent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="buffer"></param>
        public void Send(byte[] buffer)
        {
            Send(buffer, null);
        }

        public void Send(byte[] buffer, Action<bool> completed)
        {
            if (IsConnected && socket != null)
                socket.SendAsync(buffer, completed);
            else
                completed?.Invoke(false);
        }

        /// <summary>
        /// Receive message
        /// </summary>
        /// <returns></returns>
        public byte[] Recv()
        {
            lock (messagesSync)
            {
                if (messages.Count == 0)
                    return null;

                byte[] message = messages.Dequeue();
                queuedMessageByteCount -= message.LongLength;
                return message;
            }
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
            IsConnecting = false;
            ResetReceivedMessages(false);
            closeRequested = true;

            try
            {
                socket?.CloseAsync(code, NormalizeCloseReason(reason));
            }
            catch
            {
                closeRequested = false;
                throw;
            }
        }

        public void Dispose()
        {
            WebSocketSharp.WebSocket activeSocket = socket;
            socket = null;

            if (!closeRequested)
            {
                closeRequested = true;

                try
                {
                    activeSocket?.CloseAsync(
                        (ushort)CloseStatusCode.Normal,
                        "WebSocket released");
                }
                catch (Exception exception)
                {
                    logger.Warn($"Failed to close released WebSocket transport: {exception.Message}");
                }
            }

            IsConnected = false;
            IsConnecting = false;
            ResetReceivedMessages(false);
        }

        private bool TryEnqueueReceivedMessage(byte[] message, out string rejectionReason)
        {
            rejectionReason = null;

            lock (messagesSync)
            {
                if (!acceptsIncomingMessages)
                    return false;

                int messageLength = message?.Length ?? 0;

                if (message == null || messageLength > MaxMessagePayloadLength)
                {
                    rejectionReason = "Message payload is too large";
                    ClearReceivedMessagesLocked();
                    acceptsIncomingMessages = false;
                    return false;
                }

                if (messages.Count + 1 > MstNetworkLimits.MaxQueuedIncomingMessageCount ||
                    queuedMessageByteCount + messageLength >
                    MstNetworkLimits.MaxQueuedIncomingMessageByteCount)
                {
                    rejectionReason = "Incoming message backlog is too large";
                    ClearReceivedMessagesLocked();
                    acceptsIncomingMessages = false;
                    return false;
                }

                messages.Enqueue(message);
                queuedMessageByteCount += messageLength;
                return true;
            }
        }

        private void ResetReceivedMessages(bool acceptMessages)
        {
            lock (messagesSync)
            {
                ClearReceivedMessagesLocked();
                acceptsIncomingMessages = acceptMessages;
            }
        }

        private void StopAcceptingReceivedMessages()
        {
            lock (messagesSync)
                acceptsIncomingMessages = false;
        }

        private void ClearReceivedMessagesLocked()
        {
            messages.Clear();
            queuedMessageByteCount = 0;
        }
#endif

        private static string NormalizeCloseReason(string reason)
        {
            const int maxReasonByteCount = 123;

            if (string.IsNullOrEmpty(reason) ||
                Encoding.UTF8.GetByteCount(reason) <= maxReasonByteCount)
            {
                return reason ?? string.Empty;
            }

            int acceptedCharacterCount = 0;
            int acceptedByteCount = 0;

            while (acceptedCharacterCount < reason.Length)
            {
                int characterCount =
                    char.IsHighSurrogate(reason[acceptedCharacterCount]) &&
                    acceptedCharacterCount + 1 < reason.Length &&
                    char.IsLowSurrogate(reason[acceptedCharacterCount + 1])
                        ? 2
                        : 1;
                int characterByteCount = Encoding.UTF8.GetByteCount(
                    reason,
                    acceptedCharacterCount,
                    characterCount);

                if (acceptedByteCount + characterByteCount > maxReasonByteCount)
                    break;

                acceptedByteCount += characterByteCount;
                acceptedCharacterCount += characterCount;
            }

            return reason.Substring(0, acceptedCharacterCount);
        }
    }
}
