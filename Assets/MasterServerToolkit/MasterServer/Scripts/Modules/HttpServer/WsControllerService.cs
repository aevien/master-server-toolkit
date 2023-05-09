using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public class WsControllerService : WebSocketBehavior
    {
        private Queue<string> messageQueueData = new Queue<string>();
        private Dictionary<string, WsControllerMessageHandler> messageHandlers = new Dictionary<string, WsControllerMessageHandler>();
        private HttpServerModule httpServerModule;

        public void SetHttpServer(HttpServerModule httpServerModule)
        {
            this.httpServerModule = httpServerModule;
        }

        private void HttpServerModule_OnUpdateEvent()
        {
            if (messageQueueData.Count <= 0)
            {
                return;
            }

            lock (messageQueueData)
            {
                while (messageQueueData.Count > 0)
                {
                    try
                    {
                        WsControllerMessage msg = new WsControllerMessage();
                        MstJson messageData = new MstJson(messageQueueData.Dequeue());

                        if (!msg.HasOpCode())
                            throw new ArgumentNullException("This message has no opcode");

                        if (!messageHandlers.ContainsKey(msg.OpCode))
                            throw new ArgumentNullException("No handler with opcode [{msg.OpCode}] found");

                        messageHandlers[msg.OpCode].Handle(msg, this);
                    }
                    catch (Exception e)
                    {
                        var msg = new WsControllerMessage
                        {
                            Error = e.Message,
                            Data = null
                        };

                        Send(msg.ToString());
                    }
                }
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (!e.IsBinary)
            {
                lock (messageQueueData)
                {
                    messageQueueData.Enqueue(e.Data);
                }
            }
            else
            {
                Logs.Error("Only text data can be used");
                Send("Only text data can be used");
            }
        }

        protected override void OnOpen()
        {
            Logs.Info($"Ws controller service is opened for {ID}");

            // Find all websocket controllers and add them to server
            //foreach (var controller in httpServerModule.WsControllers.Values)
            //{
            //    controller.Initialize(httpServerModule, this);
            //}
        }

        protected override void OnClose(CloseEventArgs e)
        {
            //httpServerModule.OnUpdateEvent -= HttpServerModule_OnUpdateEvent;

            if (!e.WasClean)
            {
                Logs.Info($"Ws controller service is closed for {ID} with error");
                Logs.Error(e.Reason);
            }
            else
            {
                Logs.Info($"Ws controller service is closed for {ID} normaly");
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            //httpServerModule.OnUpdateEvent -= HttpServerModule_OnUpdateEvent;
            Logs.Error(e.Exception);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="handler"></param>
        public void RegisterHandler(string id, WsControllerMessageEventHandler handler)
        {
            var msgHandler = new WsControllerMessageHandler();
            msgHandler.SetHandler(handler);
            messageHandlers[id] = msgHandler;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool UnregisterHandler(string id)
        {
            return messageHandlers.Remove(id);
        }

        public void SendAsync(string data)
        {
            SendAsync(data, null);
        }

        public void CloseAsync(string reason)
        {
            CloseAsync(CloseStatusCode.Normal, reason);
        }
    }
}