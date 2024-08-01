using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public class ApiService : WebSocketBehavior, IDisposable
    {
        private bool disposedValue = false;
        private string token = string.Empty;

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
        public event Action<ApiMessage> OnMessageEvent;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsAuthorized(string token)
        {
            if (string.IsNullOrEmpty(this.token) || string.IsNullOrEmpty(token))
            {
                return false;
            }

            return this.token == token;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string CreateToken()
        {
            token = Mst.Helper.CreateGuidString();
            return token;
        }

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
            Logs.Error(e.Exception.StackTrace);
            OnErrorEvent?.Invoke(e.Message);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                if (e.IsText)
                {
                    var rawData = new MstJson(e.Data);

                    if (!rawData.HasField("opcode"))
                        throw new Exception("Id field not found");

                    if (!rawData.HasField("data"))
                        throw new Exception("Data field not found");

                    if (!rawData.HasField("token"))
                        throw new Exception("Token field not found");

                    string opcode = rawData["opcode"].StringValue;
                    string token = rawData["token"].StringValue;
                    MstJson data = rawData["data"];

                    var apiMessage = new ApiMessage(opcode, data, "", token, this);

                    OnMessageEvent?.Invoke(apiMessage);
                }
            }
            catch (Exception ex)
            {
                Logs.Error(ex);
                SendError(ex.Message);
            }
        }

        public void SendMessage(ApiMessage message)
        {
            var response = MstJson.EmptyObject;
            response.AddField("opcode", message.OpCode);
            response.AddField("token", message.Token);

            if (message.Data != null)
            {
                response.AddField("data", message.Data);
            }

            if (!string.IsNullOrEmpty(message.Error))
            {
                response.AddField("error", message.Error);
            }

            SendAsync(response);
        }

        public void SendError(string message)
        {
            SendMessage(new ApiMessage("", null, message, "", null));
        }

        public void SendAsync(MstJson data)
        {
            string message = data.ToString();
            SendAsync(message, (success) =>
            {
                Logs.Info(success);
            });
        }

        public void Disconnect(string reason = "")
        {
            CloseAsync((ushort)CloseStatusCode.Normal, reason);
        }

        public void Disconnect(ushort code, string reason)
        {
            CloseAsync(code, reason);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Освобождение управляемых ресурсов
                    OnOpenEvent = null;
                    OnCloseEvent = null;
                    OnErrorEvent = null;
                    OnMessageEvent = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ApiService()
        {
            Dispose(false);
        }
    }
}