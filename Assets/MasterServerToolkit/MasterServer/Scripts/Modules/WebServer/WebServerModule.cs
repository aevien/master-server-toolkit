using MasterServerToolkit.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// HTTP web server module for Unity applications with authentication, CORS support, and lifecycle management
    /// </summary>
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

        [Header("CORS Settings"), SerializeField]
        protected bool enableCors = true;
        [SerializeField]
        protected string allowedOrigins = "*"; // Comma-separated list or * for all
        [SerializeField]
        protected string allowedMethods = "GET, POST, PUT, DELETE, OPTIONS";
        [SerializeField]
        protected string allowedHeaders = "Content-Type, Authorization";
        [SerializeField]
        protected bool allowCredentials = true;
        [SerializeField]
        protected int maxAge = 86400; // Preflight cache time in seconds (24 hours)

        [Header("Intro"), SerializeField]
        private TextAsset templateAsset;
        [SerializeField]
        protected TextAsset introAsset;
        [SerializeField]
        private string panelTitle = "Dashboard";
        [SerializeField]
        private string aboutTitle = "About";
        [SerializeField, TextArea(10, 20)]
        private string aboutText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

        #endregion

        // Server lifecycle management
        private CancellationTokenSource cancellationTokenSource;
        private HttpListener httpServer;
        private string template = "#INNER_HTML#";
        private string intro = string.Empty;
        private Task requestHandlingTask; // Tracks request processing task
        private bool isServerRunning = false; // Server state flag

        // HTTP method handlers organized by request type
        private readonly ConcurrentDictionary<string, HttpRequestHandler> getHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();
        private readonly ConcurrentDictionary<string, HttpRequestHandler> postHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();
        private readonly ConcurrentDictionary<string, HttpRequestHandler> putHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();
        private readonly ConcurrentDictionary<string, HttpRequestHandler> deleteHandlers = new ConcurrentDictionary<string, HttpRequestHandler>();

        /// <summary>
        /// Collection of registered web controllers
        /// </summary>
        public ConcurrentDictionary<Type, IWebController> Controllers { get; protected set; } = new ConcurrentDictionary<Type, IWebController>();

        /// <summary>
        /// Complete server URL for external access
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

            // Override settings with command line arguments if provided
            username = Mst.Args.AsString(Mst.Args.Names.WebUsername, username);
            password = Mst.Args.AsString(Mst.Args.Names.WebPassword, password);
            httpPort = Mst.Args.AsInt(Mst.Args.Names.WebPort, httpPort);
            httpAddress = Mst.Args.AsString(Mst.Args.Names.WebAddress, httpAddress);

            if (templateAsset != null)
            {
                template = templateAsset.text;
                template = template.Replace("#PANEL-TITLE#", panelTitle);
                template = template.Replace("#ABOUT_TITLE#", aboutTitle);
                template = template.Replace("#ABOUT#", aboutText);
            }

            // Load start page content if provided
            if (introAsset != null)
            {
                intro = introAsset.text;

                if (string.IsNullOrEmpty(intro))
                    intro = "Welcome to the home page";
            }

            // CRITICAL: Register Unity lifecycle event handlers for proper server shutdown
#if UNITY_EDITOR
            // Editor-specific events for play mode changes
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            // Standard Unity events for builds
            Application.quitting += OnApplicationQuitting;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Handles Unity editor play mode state changes
        /// </summary>
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Stop server when exiting play mode or during compilation
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            {
                logger.Info("Unity Editor: Exiting play mode, stopping web server...");
                Stop();
            }
        }
#endif

        /// <summary>
        /// Handles application quit events (works in builds)
        /// </summary>
        private void OnApplicationQuitting()
        {
            logger.Info("Application quitting, stopping web server...");
            Stop();
        }

        /// <summary>
        /// Unity lifecycle cleanup when object is destroyed
        /// </summary>
        private void OnDestroy()
        {
            logger.Info("WebServerModule destroyed, stopping web server...");
            Stop();

#if UNITY_EDITOR
            // Unsubscribe from editor events
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            Application.quitting -= OnApplicationQuitting;
        }

        public override void Initialize(IServer server)
        {
            if (autostart)
            {
                Listen();
            }
        }

        /// <summary>
        /// Start listening on the configured URL
        /// </summary>
        public void Listen()
        {
            Listen(Url);
        }

        /// <summary>
        /// Start HTTP server on specified URL
        /// </summary>
        public void Listen(string url)
        {
            // Prevent multiple server instances
            if (isServerRunning)
            {
                logger.Warn("Web server is already running. Call Stop() first before starting again.");
                return;
            }

            url = url.TrimEnd('/');
            httpServer = new HttpListener
            {
                AuthenticationSchemeSelectorDelegate = new AuthenticationSchemeSelector(AuthenticationSchemeSelectorHandler)
            };

            // Register base index route
            string indexPrefix = $"{url}/";
            httpServer.Prefixes.Add(indexPrefix);
            RegisterGetHandler("index", Index, useCredentials);

            // Initialize child controllers
            foreach (var controller in GetComponentsInChildren<IWebController>())
            {
                Controllers.TryAdd(controller.GetType(), controller);
                controller.Initialize(this);
            }

            // Register URL prefixes for all HTTP methods
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

            foreach (var handler in putHandlers)
            {
                string prefix = $"{url}/{handler.Key}/";
                httpServer.Prefixes.Add(prefix);
            }

            foreach (var handler in deleteHandlers)
            {
                string prefix = $"{url}/{handler.Key}/";
                httpServer.Prefixes.Add(prefix);
            }

            // Start server and begin request processing
            httpServer.Start();
            isServerRunning = true;
            logger.Info("Server started. Listening for requests...");

            // Start async request handling task
            cancellationTokenSource = new CancellationTokenSource();
            requestHandlingTask = Task.Run(() => HandleRequests(cancellationTokenSource.Token));
        }

        /// <summary>
        /// Apply CORS headers to HTTP response based on configuration
        /// </summary>
        private void ApplyCorsHeaders(HttpListenerResponse response, HttpListenerRequest request)
        {
            if (!enableCors)
                return;

            string requestOrigin = request.Headers["Origin"];

            // Check if origin is allowed (either * for all or specific origin)
            bool originAllowed = allowedOrigins == "*" ||
                                (requestOrigin != null && allowedOrigins.Split(',')
                                    .Select(o => o.Trim())
                                    .Contains(requestOrigin));

            if (originAllowed)
            {
                // Use specific origin if available, otherwise use wildcard
                string allowedOrigin = requestOrigin ?? "*";
                response.AppendHeader("Access-Control-Allow-Origin", allowedOrigin);

                if (allowCredentials && requestOrigin != null)
                    response.AppendHeader("Access-Control-Allow-Credentials", "true");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.AppendHeader("Access-Control-Allow-Methods", allowedMethods);
                    response.AppendHeader("Access-Control-Allow-Headers", allowedHeaders);
                    response.AppendHeader("Access-Control-Max-Age", maxAge.ToString());
                }
            }
        }

        /// <summary>
        /// Determines authentication scheme based on handler requirements
        /// </summary>
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

        /// <summary>
        /// Validates request credentials against configured username/password
        /// </summary>
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

        /// <summary>
        /// Helper method to register handlers in specified collection
        /// </summary>
        private void RegisterHandler(string api, HttpResultHandler action, bool useCredentials, ConcurrentDictionary<string, HttpRequestHandler> handlers)
        {
            api = api.Trim('/');
            if (!handlers.TryAdd(api, new HttpRequestHandler(api, useCredentials, action, null)))
            {
                logger.Warn($"Handler for API '{api}' already exists. Skipping registration.");
            }
        }

        /// <summary>
        /// Register GET request handler
        /// </summary>
        public void RegisterGetHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, getHandlers);

        /// <summary>
        /// Register POST request handler
        /// </summary>
        public void RegisterPostHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, postHandlers);

        /// <summary>
        /// Register PUT request handler
        /// </summary>
        public void RegisterPutHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, putHandlers);

        /// <summary>
        /// Register DELETE request handler
        /// </summary>
        public void RegisterDeleteHandler(string api, HttpResultHandler action, bool useCredentials = false, MstJson extra = null) =>
            RegisterHandler(api, action, useCredentials, deleteHandlers);

        /// <summary>
        /// Find handler for HTTP context
        /// </summary>
        private HttpRequestHandler FindHandler(HttpListenerContext context)
        {
            return FindHandler(context.Request);
        }

        /// <summary>
        /// Find appropriate handler based on request method and path
        /// </summary>
        private HttpRequestHandler FindHandler(HttpListenerRequest httpRequest)
        {
            string urlPathWithQuery = httpRequest.Url.PathAndQuery;
            string urlPathWithoutQuery = urlPathWithQuery.Split('?')[0];
            string method = httpRequest.HttpMethod;
            string api = "";

            // Handle root path as index
            if (urlPathWithoutQuery.Trim() == "/")
            {
                api = $"index";
            }
            else
            {
                api = $"{urlPathWithoutQuery}".Trim('/');
            }

            HttpRequestHandler handler;

            // Route to appropriate handler collection based on HTTP method
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

        /// <summary>
        /// Main request processing loop - handles incoming HTTP requests
        /// </summary>
        private async Task HandleRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Verify server is still listening
                if (httpServer == null || !httpServer.IsListening)
                {
                    logger.Warn("HttpListener is not listening. Stopping request handling.");
                    break;
                }

                HttpListenerContext context;

                try
                {
                    // Wait for incoming request
                    context = await httpServer.GetContextAsync();
                }
                catch (ObjectDisposedException)
                {
                    logger.Warn("HttpListener was disposed. Stopping request handling.");
                    break;
                }
                catch (HttpListenerException) when (token.IsCancellationRequested)
                {
                    logger.Info("Http listener stopped due to cancellation.");
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error when getting context: {ex}");
                    continue;
                }

                // Check for cancellation before processing
                if (token.IsCancellationRequested)
                {
                    context?.Response?.Close();
                    Stop();
                    break;
                }

                // Process request synchronously to maintain stability
                using (context.Response)
                {
                    // Apply CORS headers for all requests
                    ApplyCorsHeaders(context.Response, context.Request);

                    // Handle preflight OPTIONS requests
                    if (context.Request.HttpMethod == "OPTIONS")
                    {
                        logger.Info($"[OPTIONS] Preflight request to: {context.Request.Url.AbsolutePath}");
                        context.Response.StatusCode = 200;
                        continue; // using block automatically closes response
                    }

                    logger.Info($"[{context.Request.HttpMethod}] Request to: {context.Request.Url.AbsolutePath}");

                    HttpRequestHandler handler = FindHandler(context);

                    try
                    {
                        // Route to appropriate handler or return 404
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
                        logger.Error($"Unhandled exception in request processing: {ex}");

                        try
                        {
                            await ProcessRequestAsync(context, () => InternalServerError(context.Request, ex.Message));
                        }
                        catch (Exception innerEx)
                        {
                            logger.Error($"Error when sending error response: {innerEx}");
                        }
                    }
                }
            }

            logger.Info("Request handling loop terminated.");
        }

        /// <summary>
        /// Execute handler and send response to client
        /// </summary>
        private async Task ProcessRequestAsync(HttpListenerContext context, Func<Task<IHttpResult>> handler)
        {
            var result = await handler();
            await result.Execute(context);
        }

        /// <summary>
        /// Stop the web server gracefully with proper resource cleanup
        /// </summary>
        public void Stop()
        {
            // Prevent multiple stop calls
            if (!isServerRunning)
            {
                logger.Info("Web server is already stopped.");
                return;
            }

            logger.Info("Stopping web server...");
            isServerRunning = false;

            try
            {
                // Step 1: Signal shutdown via cancellation token
                cancellationTokenSource?.Cancel();

                // Step 2: Stop accepting new connections immediately
                if (httpServer?.IsListening == true)
                {
                    httpServer.Stop();
                    logger.Info("HttpListener stopped accepting new connections.");
                }

                // Step 3: Wait for graceful completion of current requests
                if (requestHandlingTask != null && !requestHandlingTask.IsCompleted)
                {
                    logger.Info("Waiting for request handling task to complete...");

                    // Wait up to 2 seconds for graceful shutdown
                    bool taskCompleted = requestHandlingTask.Wait(TimeSpan.FromSeconds(2));

                    if (taskCompleted)
                    {
                        logger.Info("Request handling task completed gracefully.");
                    }
                    else
                    {
                        logger.Warn("Request handling task did not complete within timeout. Forcing shutdown.");
                    }
                }

                // Step 4: Force close all connections
                if (httpServer != null)
                {
                    httpServer.Abort();
                    httpServer.Close();
                    httpServer = null;
                    logger.Info("HttpListener forcefully closed.");
                }

                // Step 5: Dispose controllers
                foreach (var controller in Controllers.Values)
                {
                    try
                    {
                        controller.Dispose();
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Error disposing controller {controller.GetType().Name}: {ex.Message}");
                    }
                }

                Controllers.Clear();

                // Step 6: Clean up cancellation token
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }

                requestHandlingTask = null;
                logger.Info("Web server stopped successfully.");
            }
            catch (Exception ex)
            {
                logger.Error($"Error during server shutdown: {ex}");

                // Force cleanup even if errors occur
                try
                {
                    httpServer?.Abort();
                    httpServer?.Close();
                    httpServer = null;
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                    requestHandlingTask = null;
                }
                catch (Exception cleanupEx)
                {
                    logger.Error($"Error during cleanup: {cleanupEx}");
                }
            }
        }

        /// <summary>
        /// Generate JSON information about server status and configuration
        /// </summary>
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

        /// <summary>
        /// Default index page handler
        /// </summary>
        protected virtual Task<IHttpResult> Index(HttpListenerRequest Request)
        {
            var handler = new HtmlResult(template);
            handler.Value = handler.Value.Replace("#INNER_HTML#", intro);
            handler.Value = handler.Value.Replace("#MST-TITLE#", $"{Mst.Name} v.{Mst.Version}");
            return Task.FromResult<IHttpResult>(handler);
        }

        /// <summary>
        /// HTTP 401 Unauthorized response
        /// </summary>
        protected virtual Task<IHttpResult> Unauthorized(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new Unauthorized());
        }

        /// <summary>
        /// HTTP 400 Bad Request response
        /// </summary>
        protected virtual Task<IHttpResult> BadRequest(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new BadRequest());
        }

        /// <summary>
        /// HTTP 404 Not Found response
        /// </summary>
        protected virtual Task<IHttpResult> NotFound(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new NotFound());
        }

        /// <summary>
        /// HTTP 500 Internal Server Error response
        /// </summary>
        protected virtual Task<IHttpResult> InternalServerError(HttpListenerRequest Request, string message)
        {
            return Task.FromResult<IHttpResult>(new InternalServerError(message));
        }

        #endregion
    }
}