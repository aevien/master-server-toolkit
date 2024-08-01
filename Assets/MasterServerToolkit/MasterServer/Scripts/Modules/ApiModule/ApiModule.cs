using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public delegate void ApiMessageHandler(ApiMessage message);

    public class ApiModule : BaseServerModule
    {
        #region INSPECTOR

        [SerializeField]
        protected string username = "admin";
        [SerializeField]
        protected string password = "admin";

        #endregion

        private WebSocketServer server;
        private readonly Dictionary<string, ApiMessageHandler> handlers = new Dictionary<string, ApiMessageHandler>();

        protected override void Awake()
        {
            base.Awake();
        }

        private void OnDestroy()
        {
            Stop();
        }

        public override void Initialize(IServer server)
        {
            RegisterHandler(ApiMessageOpCodes.ECHO, OnEchoMessage);
            RegisterHandler(ApiMessageOpCodes.LOGIN, OnLoginMessage);
            RegisterHandler(ApiMessageOpCodes.GET_SERVER_INFO, OnGetServerInfoMessage);
            Listen("127.0.0.1", 5555);
        }

        public void RegisterHandler(string opCode, ApiMessageHandler handler)
        {
            if (!handlers.ContainsKey(opCode))
            {
                handlers[opCode] = handler;
            }
            else
            {
                logger.Error($"Handler with id {opCode} already registered");
            }
        }

        /// <summary>
        /// Opens the socket and starts listening to a given port and IP
        /// </summary>
        /// <param name="port"></param>
        public void Listen(string address, int port)
        {
            try
            {
                server?.Stop();

                if (address.Trim() == "localhost")
                {
                    server = new WebSocketServer(port);
                }
                else if (IPAddress.TryParse(address, out var ipAddress))
                {
                    server = new WebSocketServer(ipAddress, port);
                }
                else
                {
                    string url = $"ws://{address}:{port}";
                    server = new WebSocketServer(url);
                }

                server.KeepClean = true;

                // Setup all services used by server
                SetupService(server);

                // Start server
                server.Start();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }

        /// <summary>
        /// Stops listening
        /// </summary>
        public void Stop()
        {
            server?.Stop();
        }

        /// <summary>
        /// Setup all services used by server
        /// </summary>
        /// <param name="server"></param>
        private void SetupService(WebSocketServer server)
        {
            logger.Info($"Starting service at {server.Address}:{server.Port}");

            server.AddWebSocketService<ApiService>($"/api", (serviceForPeer) =>
            {
                serviceForPeer.IgnoreExtensions = true;
                serviceForPeer.OnOpenEvent += () =>
                {
                    logger.Debug($"New client [{serviceForPeer.ID}] connected to api server");
                };
                serviceForPeer.OnCloseEvent += (code, reason) =>
                {
                    logger.Debug($"Client disconnected from api server. Code [{code}]. Reason: {reason}");
                };
                serviceForPeer.OnErrorEvent += (error) =>
                {
                    logger.Error($"Client has error. {error}");
                };
                serviceForPeer.OnMessageEvent += (message) =>
                {
                    if (handlers.ContainsKey(message.OpCode))
                    {
                        handlers[message.OpCode].Invoke(message);
                    }
                    else
                    {
                        string error = $"Api server does not contain handler with op code {message.OpCode}";
                        message.ResponseError(error);
                        logger.Error(error);
                    }
                };
            });
        }

        #region MESSAGE HANDLERS

        private void OnEchoMessage(ApiMessage message)
        {
            if (message.IsAuthorized())
            {
                message.ResponseOk("Hello from MST API");
            }
            else
            {
                message.ResponseUnauthorized();
            }
        }

        private void OnGetServerInfoMessage(ApiMessage message)
        {
            if (message.IsAuthorized())
            {
                var data = Server.JsonInfo();
                message.ResponseOk(data);
            }
            else
            {
                message.ResponseUnauthorized();
            }
        }

        private void OnLoginMessage(ApiMessage message)
        {
            string username = message.Data["username"].StringValue;
            string password = message.Data["password"].StringValue;

            if (username != this.username || password != this.password)
            {
                message.ResponseError("Invalid username or password");
                return;
            }

            message.Authorize();
            message.ResponseOk();
        }

        #endregion
    }
}