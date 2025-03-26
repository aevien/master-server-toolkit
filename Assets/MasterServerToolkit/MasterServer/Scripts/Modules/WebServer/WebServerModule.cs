using MasterServerToolkit.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class WebServerModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Http Server Settings"), SerializeField]
        protected bool autostart = true;
        [SerializeField]
        protected string httpAddress = "127.0.0.1";
        [SerializeField]
        protected int httpPort = 5056;
        [SerializeField]
        protected string[] defaultIndexPage = new string[] { "index", "home" };

        [Header("User Credentials Settings"), SerializeField]
        protected bool useCredentials = true;
        [SerializeField]
        protected string username = "admin";
        [SerializeField]
        protected string password = "admin";

        [Header("Assets"), SerializeField]
        protected TextAsset startPage;

        #endregion

        private CancellationTokenSource cancellationTokenSource;
        private HttpListener httpServer;
        private string startPageHtml;

        private readonly ConcurrentDictionary<string, HttpRequestHandler> getHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();
        private readonly ConcurrentDictionary<string, HttpRequestHandler> postHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();
        private readonly ConcurrentDictionary<string, HttpRequestHandler> putHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();
        private readonly ConcurrentDictionary<string, HttpRequestHandler> deleteHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();

        public ConcurrentDictionary<Type, IWebController> Controllers { get; protected set; } = new ConcurrentDictionary<Type, IWebController>();

        /// <summary>
        /// 
        /// </summary>
        public string Url
        {
            get
            {
                return $"http://{httpAddress}:{httpPort}/".TrimEnd('/');
            }
        }

        protected override void Awake()
        {
            base.Awake();

            username = Mst.Args.AsString(Mst.Args.Names.WebUsername, username);
            password = Mst.Args.AsString(Mst.Args.Names.WebPassword, password);
            httpPort = Mst.Args.AsInt(Mst.Args.Names.WebPort, httpPort);
            httpAddress = Mst.Args.AsString(Mst.Args.Names.WebAddress, httpAddress);

            if (startPage != null)
            {
                startPageHtml = startPage.text;
            }
        }

        public override void Initialize(IServer server)
        {
            if (autostart)
            {
                Listen();
            }
        }

        public void Listen()
        {
            Listen(Url);
        }

        public void Listen(string url)
        {
            url = url.TrimEnd('/');
            httpServer = new HttpListener
            {
                AuthenticationSchemeSelectorDelegate = new AuthenticationSchemeSelector(AuthenticationSchemeSelectorHandler)
            };

            string indexPrefix = $"{url}/";
            httpServer.Prefixes.Add(indexPrefix);
            RegisterGetHandler("index", Index, useCredentials);

            foreach (var controller in GetComponentsInChildren<IWebController>())
            {
                Controllers.TryAdd(controller.GetType(), controller);
                controller.Initialize(this);
            }

            foreach (var handler in getHandlers)
            {
                string prefix = $"{url}/{handler.Key}/";
                httpServer.Prefixes.Add(prefix);
            }

            foreach (var handler in postHandlers)
            {
                string prefix = $"{url}/{handler.Key}/";
                httpServer.Prefixes.Add(prefix);
            }

            httpServer.Start();

            logger.Info("Server started. Listening for requests...");

            cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => HandleRequests(cancellationTokenSource.Token));
        }

        private AuthenticationSchemes AuthenticationSchemeSelectorHandler(HttpListenerRequest httpRequest)
        {
            HttpRequestHandler handler = FindHandler(httpRequest);

            if (handler != null)
            {
                if (handler.UseCredentials)
                {
                    return AuthenticationSchemes.Basic;
                }
                else
                {
                    return AuthenticationSchemes.Anonymous;
                }
            }
            else
            {
                return AuthenticationSchemes.None;
            }
        }

        private async Task ValidateRequestAsync(HttpListenerContext context, Func<Task> successCallback, Func<Task> unauthorizedCallback)
        {
            if (context.User != null && context.User.Identity is HttpListenerBasicIdentity identity)
            {
                string clientUsername = identity.Name;
                string clientPassword = identity.Password;

                if (username == clientUsername && password == clientPassword)
                {
                    await successCallback();
                }
                else
                {
                    logger.Warn($"Unauthorized access attempt: {clientUsername}");
                    await unauthorizedCallback();
                }
            }
            else
            {
                await successCallback();
            }
        }

        private void RegisterHandler(string api, HttpResultHandler action, bool useCredentials, ConcurrentDictionary<string, HttpRequestHandler> handlers)
        {
            api = api.Trim('/');
            handlers.TryAdd(api, new HttpRequestHandler(api, useCredentials, action, null));
        }

        public void RegisterGetHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) => 
            RegisterHandler(api, action, useCredentials, getHandlers);

        public void RegisterPostHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, postHandlers);

        public void RegisterPutHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, putHandlers);

        public void RegisterDeleteHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, deleteHandlers);

        private HttpRequestHandler FindHandler(HttpListenerContext context)
        {
            return FindHandler(context.Request);
        }

        private HttpRequestHandler FindHandler(HttpListenerRequest httpRequest)
        {
            string urlPathWithQuery = httpRequest.Url.PathAndQuery;
            string urlPathWithoutQuery = urlPathWithQuery.Split('?')[0];
            string method = httpRequest.HttpMethod;
            string api = "";

            if (urlPathWithoutQuery.Trim() == "/")
            {
                api = $"index";
            }
            else
            {
                api = $"{urlPathWithoutQuery}".Trim('/');
            }

            HttpRequestHandler handler;

            if (method == "POST")
            {
                postHandlers.TryGetValue(api, out handler);
            }
            else if (method == "PUT")
            {
                putHandlers.TryGetValue(api, out handler);
            }
            else if (method == "DELETE")
            {
                deleteHandlers.TryGetValue(api, out handler);
            }
            else
            {
                getHandlers.TryGetValue(api, out handler);
            }

            return handler;
        }

        private async Task HandleRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (httpServer == null || !httpServer.IsListening)
                {
                    logger.Warn("HttpListener is not listening. Stopping request handling.");
                    break;
                }

                HttpListenerContext context;

                try
                {
                    context = await httpServer.GetContextAsync();
                }
                catch (ObjectDisposedException)
                {
                    logger.Warn("HttpListener was disposed. Stopping request handling.");
                    break;
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error when getting context: {ex}");
                    continue;
                }

                if (token.IsCancellationRequested)
                {
                    Stop();
                    break;
                }

                using (context.Response)
                {
                    logger.Info($"[{context.Request.HttpMethod}] Request to: {context.Request.Url.AbsolutePath}");

                    HttpRequestHandler handler = FindHandler(context);

                    try
                    {
                        if (handler == null)
                        {
                            await ProcessRequestAsync(context, () => NotFound(context.Request));
                        }
                        else
                        {
                            await ValidateRequestAsync(context, async () =>
                            {
                                try
                                {
                                    await ProcessRequestAsync(context, () => handler.Action(context.Request));
                                }
                                catch (Exception ex)
                                {
                                    logger.Error($"Error when handling request: {ex}");
                                    await ProcessRequestAsync(context, () => InternalServerError(context.Request, ex.Message));
                                }
                            },
                            async () =>
                            {
                                await ProcessRequestAsync(context, () => Unauthorized(context.Request));
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Unhandled exception in `HandleRequests`: {ex}");

                        try
                        {
                            await ProcessRequestAsync(context, () => InternalServerError(context.Request, ex.Message));
                        }
                        catch (Exception innerEx)
                        {
                            logger.Error($"Error when sending request: {innerEx}");
                        }
                    }
                }
            }
        }


        private async Task ProcessRequestAsync(HttpListenerContext context, Func<Task<IHttpResult>> handler)
        {
            var result = await handler();
            await result.Execute(context);
        }

        public void Stop()
        {
            if (httpServer?.IsListening == true)
            {
                cancellationTokenSource?.Cancel();
                httpServer.Stop();
                httpServer.Abort();
                httpServer.Close();
                httpServer = null;

                foreach (var controller in Controllers.Values)
                    controller.Dispose();

                Controllers.Clear();
                logger.Info("Server stopped.");
            }
        }

        public override MstJson JsonInfo()
        {
            var info = base.JsonInfo();

            info.AddField("address", httpAddress);
            info.AddField("port", httpPort);

            info.AddField("controllers", MstJson.EmptyArray);

            foreach (var controller in Controllers.Values)
                info["controllers"].Add(controller.JsonInfo());

            var requestActions = MstJson.EmptyObject;
            requestActions.AddField("get", MstJson.EmptyArray);
            requestActions.AddField("post", MstJson.EmptyArray);
            requestActions.AddField("put", MstJson.EmptyArray);
            requestActions.AddField("delete", MstJson.EmptyArray);

            foreach (var requestAction in getHandlers.Values)
                requestActions["get"].Add(requestAction.ToJson());

            foreach (var requestAction in postHandlers.Values)
                requestActions["post"].Add(requestAction.ToJson());

            foreach (var requestAction in putHandlers.Values)
                requestActions["put"].Add(requestAction.ToJson());

            foreach (var requestAction in deleteHandlers.Values)
                requestActions["delete"].Add(requestAction.ToJson());

            info.AddField("requestActions", requestActions);
            return info;
        }

        #region RESPONSE HANDLERS

        protected virtual Task<IHttpResult> Index(HttpListenerRequest Request)
        {
            var handler = new StringResult("Home page");

            if (!string.IsNullOrEmpty(startPageHtml))
            {
                handler.ContentType = "text/html";
                handler.Value = startPageHtml.Replace("#MST-TITLE#", $"{Mst.Name} v.{Mst.Version}");
                handler.Value = handler.Value.Replace("#MST-GREETINGS#", $"{Mst.Name} v.{Mst.Version}");
            }

            return Task.FromResult<IHttpResult>(handler);
        }

        protected virtual Task<IHttpResult> Unauthorized(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new Unauthorized());
        }

        protected virtual Task<IHttpResult> BadRequest(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new BadRequest());
        }

        protected virtual Task<IHttpResult> NotFound(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new NotFound());
        }

        protected virtual Task<IHttpResult> InternalServerError(HttpListenerRequest Request, string message)
        {
            return Task.FromResult<IHttpResult>(new InternalServerError(message));
        }

        #endregion
    }
}