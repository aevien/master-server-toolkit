using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Web socket service, designed to work with unitys main thread
    /// </summary>
    public class WsService : WebSocketBehavior, IDisposable
    {
        private bool disposedValue = false;

        /// <summary>
        /// 
        /// </summary>
        public new WebSocketState ReadyState => base.ReadyState;

        /// <summary>
        /// 
        /// </summary>
        public event Action OnOpenEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<ushort, string> OnCloseEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<string> OnErrorEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<byte[]> OnMessageEvent;

        protected override void OnOpen()
        {
            OnOpenEvent?.Invoke();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            OnCloseEvent?.Invoke(e.Code, e.Reason);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            OnErrorEvent?.Invoke(e.Message);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            OnMessageEvent?.Invoke(e.RawData);
        }

        public void SendAsync(byte[] data)
        {
            SendAsync(data, null);
        }

        public void Disconnect(string reason = "")
        {
            CloseAsync((ushort)CloseStatusCode.Normal, reason);
        }

        public void Disconnect(ushort code, string reason)
        {
            CloseAsync(code, reason);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    OnOpenEvent = null;
                    OnCloseEvent = null;
                    OnErrorEvent = null;
                    OnMessageEvent = null;
                }

                disposedValue = true;
            }
        }
    }
}