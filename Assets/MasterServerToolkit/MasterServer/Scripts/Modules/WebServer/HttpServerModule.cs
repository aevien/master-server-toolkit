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
    /// HTTP web server module for Unity applications with authentication, CORS support, and lifecycle management.
    /// </summary>
    public class HttpServerModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Http Server Settings"), SerializeField, Tooltip("Starts the HTTP listener automatically when the server module initializes. Disable when project code must control the Listen and Stop lifecycle explicitly.")]
        protected bool autostart = true;

        [SerializeField, Tooltip("IP address or host name used to build the HTTP listener prefix. The -mstWebAddress argument overrides this value; localhost and 127.0.0.1 register both loopback prefixes.")]
        protected string httpAddress = "127.0.0.1";

        [SerializeField, Tooltip("HTTP listener TCP port. Valid values are 1 to 65535; the -mstWebPort argument overrides this value.")]
        protected int httpPort = 5056;

        [Header("User Credentials Settings"), SerializeField, Tooltip("Requires HTTP Basic Auth for every registered route. When disabled, individual controllers can still require credentials; the -mstWebUseCredentials argument overrides this value.")]
        protected bool useCredentials = true;

        [SerializeField, Tooltip("Username accepted by HTTP Basic Auth when credentials are required. The -mstAdminUsername argument overrides this value.")]
        protected string adminUsername = "admin";

        [SerializeField, Tooltip("Password accepted by HTTP Basic Auth when credentials are required. The -mstAdminPassword argument overrides this value.")]
        protected string adminPassword = "admin";

        [SerializeField, Tooltip("Realm included in HTTP Basic Auth challenges and browser prompts. The -mstWebRealm argument overrides this value.")]
        protected string realm = "admin";

        [Header("CORS Settings"), SerializeField, Tooltip("Adds Cross-Origin Resource Sharing headers and handles preflight requests. The -mstWebCorsEnabled argument overrides this value.")]
        protected bool enableCors = true;

        [SerializeField, Tooltip("Allowed CORS origins. Use '*' for any origin or a comma-separated list of exact origins; the -mstWebAllowedOrigins argument overrides this value.")]
        protected string allowedOrigins = "*"; // Comma-separated list or * for all

        [SerializeField, Tooltip("Comma-separated HTTP methods returned by CORS preflight responses. The -mstWebAllowedMethods argument overrides this value.")]
        protected string allowedMethods = "GET, POST, PUT, DELETE, OPTIONS, HEAD";

        [SerializeField, Tooltip("Comma-separated request headers returned by CORS preflight responses. The -mstWebAllowedHeaders argument overrides this value.")]
        protected string allowedHeaders = "Content-Type, Authorization, Accept, X-Requested-With";

        [SerializeField, Tooltip("Allows credentialed cross-origin requests for explicit origins. It is disabled when Allowed Origins contains '*'; the -mstWebAllowCredentials argument overrides this value.")]
        protected bool allowCredentials = true;

        [SerializeField, Tooltip("Duration in seconds browsers may cache a CORS preflight response. 0 disables preflight caching; the -mstWebCorsMaxAge argument overrides this value.")]
        protected int maxAge = 86400; // Preflight cache time in seconds (24 hours)

        [Header("Safety"), SerializeField, Tooltip("Maximum number of HTTP requests processed concurrently. Use a positive value; higher values increase parallel work and resource usage.")]
        private int maxTasksCount = 10;

        #endregion

        // Server lifecycle management
        private CancellationTokenSource cancellationTokenSource;
        private HttpListener httpServer;
        private Task requestHandlingTask; // Tracks request processing task
        private bool isServerRunning = false; // Server state flag
        private SemaphoreSlim concurrencySemaphore;
        private readonly object serverLifecycleSync = new object();
        private Task stopTask = Task.CompletedTask;

        // HTTP method handlers organized by request type
        private readonly ConcurrentDictionary<string, HttpRequestHandler> getHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HttpRequestHandler> postHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HttpRequestHandler> putHandlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HttpRequestHandler> deleteHandlers = new(StringComparer.OrdinalIgnoreCase);

        private long activeRequestCount = 0;
        private int realmVersion = 1;
        private const string ApiV1PathPrefix = "api/v1";

        /// <summary>
        /// Gets the collection of registered web controllers. Controllers are discovered in children and initialized on Listen().
        /// </summary>
        public ConcurrentDictionary<Type, IWebController> Controllers { get; protected set; } = new ConcurrentDictionary<Type, IWebController>();

        /// <summary>
        /// Gets the complete server URL for external access, e.g. http://127.0.0.1:5056 .
        /// </summary>
        public string Url
        {
            get
            {
                return $"http://{httpAddress}:{httpPort}/".TrimEnd('/');
            }
        }

        /// <summary>
        /// Gets the admin username configured for HTTP Basic Auth.
        /// </summary>
        public string AdminUsername => adminUsername;

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();

            // Declare optional dependencies available at runtime.
            AddOptionalDependency<AuthModule>();
            AddOptionalDependency<ProfilesModule>();

            // Override settings with command line arguments if provided.
            string legacyAdminUsername = Mst.Args.AsString(Mst.Args.Names.WebUsername, adminUsername);
            string legacyAdminPassword = Mst.Args.AsString(Mst.Args.Names.WebPassword, adminPassword);
            adminUsername = Mst.Args.AsString(Mst.Args.Names.AdminUsername, legacyAdminUsername);
            adminPassword = Mst.Args.AsString(Mst.Args.Names.AdminPassword, legacyAdminPassword);
            httpPort = Mst.Args.AsInt(Mst.Args.Names.WebPort, httpPort);
            httpAddress = Mst.Args.AsString(Mst.Args.Names.WebAddress, httpAddress);
            useCredentials = Mst.Args.AsBool(Mst.Args.Names.WebUseCredentials, useCredentials);
            realm = Mst.Args.AsString(Mst.Args.Names.WebRealm, realm);
            enableCors = Mst.Args.AsBool(Mst.Args.Names.WebCorsEnabled, enableCors);
            allowedOrigins = Mst.Args.AsString(Mst.Args.Names.WebAllowedOrigins, allowedOrigins);
            allowedMethods = Mst.Args.AsString(Mst.Args.Names.WebAllowedMethods, allowedMethods);
            allowedHeaders = Mst.Args.AsString(Mst.Args.Names.WebAllowedHeaders, allowedHeaders);
            allowCredentials = Mst.Args.AsBool(Mst.Args.Names.WebAllowCredentials, allowCredentials);
            maxAge = Mst.Args.AsInt(Mst.Args.Names.WebCorsMaxAge, maxAge);
            NormalizeCorsSettings();

        }

        /// <summary>
        /// Unity validation callback executed when values change in the inspector.
        /// Ensures that the Basic Auth realm has a non-empty string.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(realm))
            {
                realm = $"{Mst.Name} Admin";
            }
        }

        /// <summary>
        /// Unity lifecycle cleanup when object is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            Stop();
        }

        /// <summary>
        /// Initializes the module. If <see cref="autostart"/> is true, starts listening immediately.
        /// </summary>
        /// <param name="server">Master Server instance passed by the framework.</param>
        public override void Initialize(IServer server)
        {
            if (autostart)
            {
                Listen();
            }
        }

        /// <summary>
        /// Starts listening on the configured <see cref="Url"/>.
        /// </summary>
        public void Listen()
        {
            Listen(Url);
        }

        /// <summary>
        /// Starts the HTTP server on the specified URL.
        /// </summary>
        /// <param name="url">Fully-qualified HTTP base URL prefix, e.g. http://0.0.0.0:5056</param>
        public void Listen(string url)
        {
            // Prevent multiple server instances.
            if (isServerRunning || !stopTask.IsCompleted)
            {
                logger.Warn("Web server is already running or stopping. Wait for StopAsync() before starting again.");
                return;
            }

            url = url.TrimEnd('/');

            try
            {
                httpServer = new HttpListener
                {
                    AuthenticationSchemeSelectorDelegate = new AuthenticationSchemeSelector(AuthenticationSchemeSelectorHandler)
                };

                foreach (string prefix in GetHttpListenerPrefixes(url))
                {
                    httpServer.Prefixes.Add(prefix);
                }

                // Initialize child controllers.
                foreach (var controller in GetComponentsInChildren<IWebController>())
                {
                    Controllers.TryAdd(controller.GetType(), controller);
                    controller.Initialize(this);
                }

                // Start server and begin request processing.
                httpServer.Start();
                isServerRunning = true;
                logger.Info("Web Server started. Listening for requests...");

                // Start async request handling task with bounded concurrency.
                concurrencySemaphore = new SemaphoreSlim(maxTasksCount, maxTasksCount);
                cancellationTokenSource = new CancellationTokenSource();
                requestHandlingTask = Task.Run(() => HandleRequests(cancellationTokenSource.Token));
            }
            catch (Exception exception)
            {
                CleanupFailedStart();
                logger.Error($"Failed to start web server at '{url}': {exception}");
                throw;
            }
        }

        private void CleanupFailedStart()
        {
            isServerRunning = false;

            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch (Exception exception)
            {
                logger.Error($"Failed to cancel web server startup: {exception}");
            }

            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            requestHandlingTask = null;

            concurrencySemaphore?.Dispose();
            concurrencySemaphore = null;

            try
            {
                httpServer?.Abort();
                httpServer?.Close();
            }
            catch (Exception exception)
            {
                logger.Error($"Failed to close web server after startup error: {exception}");
            }
            finally
            {
                httpServer = null;
            }

            DisposeControllersAndClearRoutes();
        }

        private void DisposeControllersAndClearRoutes()
        {
            foreach (var controller in Controllers.Values)
            {
                try
                {
                    controller.Dispose();
                }
                catch (Exception exception)
                {
                    logger.Error($"Error disposing controller {controller.GetType().Name}: {exception.Message}");
                }
            }

            Controllers.Clear();
            getHandlers.Clear();
            postHandlers.Clear();
            putHandlers.Clear();
            deleteHandlers.Clear();
        }

        private static IEnumerable<string> GetHttpListenerPrefixes(string url)
        {
            string defaultPrefix = $"{url}/";

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                yield return defaultPrefix;
                yield break;
            }

            yield return defaultPrefix;

            if (!IsLocalhostAlias(uri.Host))
                yield break;

            string aliasHost = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                ? "127.0.0.1"
                : "localhost";

            var aliasBuilder = new UriBuilder(uri)
            {
                Host = aliasHost
            };

            string aliasPrefix = $"{aliasBuilder.Uri.AbsoluteUri.TrimEnd('/')}/";

            if (!string.Equals(defaultPrefix, aliasPrefix, StringComparison.OrdinalIgnoreCase))
                yield return aliasPrefix;
        }

        private static bool IsLocalhostAlias(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies CORS headers to the HTTP response when enabled and the request origin is allowed.
        /// </summary>
        /// <param name="response">HTTP response to mutate.</param>
        /// <param name="request">Incoming request carrying Origin and method information.</param>
        private void ApplyCorsHeaders(HttpListenerResponse response, HttpListenerRequest request)
        {
            if (!enableCors)
                return;

            string requestOrigin = request.Headers["Origin"];
            string[] configuredOrigins = GetAllowedCorsOrigins();
            bool wildcardAllowed = configuredOrigins.Any(o => o == "*");
            bool originAllowed = wildcardAllowed || (requestOrigin != null && configuredOrigins
                .Any(o => string.Equals(o, requestOrigin, StringComparison.OrdinalIgnoreCase)));

            if (!originAllowed)
                return;

            string allowedOrigin = wildcardAllowed ? (requestOrigin ?? "*") : requestOrigin;
            response.Headers["Access-Control-Allow-Origin"] = allowedOrigin;

            if (allowCredentials && !wildcardAllowed && allowedOrigin != "*")
                response.Headers["Access-Control-Allow-Credentials"] = "true";

            response.Headers["Vary"] = "Origin";

            if (request.HttpMethod == "OPTIONS")
            {
                response.Headers["Access-Control-Allow-Methods"] = allowedMethods;
                response.Headers["Access-Control-Allow-Headers"] = allowedHeaders;
                response.Headers["Access-Control-Max-Age"] = maxAge.ToString();
            }
        }

        private string[] GetAllowedCorsOrigins()
        {
            return (allowedOrigins ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => !string.IsNullOrEmpty(o))
                .ToArray();
        }

        private void NormalizeCorsSettings()
        {
            if (!enableCors)
                return;

            string[] configuredOrigins = GetAllowedCorsOrigins();

            if (!configuredOrigins.Any())
            {
                logger.Warn("CORS is enabled but no allowed origins are configured. CORS headers will not be emitted.");
                return;
            }

            if (allowCredentials && configuredOrigins.Any(o => o == "*"))
            {
                allowCredentials = false;
                logger.Warn("CORS credentials were disabled because allowed origins contains '*'. Configure explicit origins to allow credentialed CORS requests.");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string CurrentRealm() => $"{realm}-v{Volatile.Read(ref realmVersion)}";

        private bool IsApiV1Request(HttpListenerRequest request)
        {
            string path = request?.Url?.AbsolutePath?.Trim('/') ?? string.Empty;
            return path.Equals(ApiV1PathPrefix, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith($"{ApiV1PathPrefix}/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies basic security headers to the response (MIME sniffing protection, clickjacking reduction, referrer policy).
        /// </summary>
        /// <param name="response">HTTP response to mutate.</param>
        private void ApplySecurityHeaders(HttpListenerResponse response)
        {
            // 1) Disable MIME-sniffing: browser won't try to guess content types.
            response.Headers["X-Content-Type-Options"] = "nosniff";

            // 2) Clickjacking protection: restrict framing.
            // Use "DENY" if you NEVER embed the panel in an iframe.
            // Use "SAMEORIGIN" if you sometimes embed it on the same origin.
            response.Headers["X-Frame-Options"] = "SAMEORIGIN";

            // 3) Reduce referrer leakage.
            response.Headers["Referrer-Policy"] = "no-referrer";
        }

        /// <summary>
        /// Determines the authentication scheme for a request based on the matched handler.
        /// </summary>
        /// <param name="httpRequest">Incoming request being authenticated.</param>
        /// <returns>Basic if the route requires credentials, otherwise Anonymous.</returns>
        private AuthenticationSchemes AuthenticationSchemeSelectorHandler(HttpListenerRequest httpRequest)
        {
            HttpRequestHandler handler = FindHandler(httpRequest);

            if (handler != null)
            {
                if (RequiresCredentials(handler))
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
                return AuthenticationSchemes.Anonymous;
            }
        }

        private bool RequiresCredentials(HttpRequestHandler handler)
        {
            return handler != null && (useCredentials || handler.UseCredentials);
        }

        /// <summary>
        /// Validates HTTP Basic credentials (if required) and executes the appropriate callback.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="requireCredentials">Whether credentials are required for this route.</param>
        /// <param name="successCallback">Executed when authentication passes.</param>
        /// <param name="unauthorizedCallback">Executed when authentication fails or credentials are missing.</param>
        private async Task ValidateRequestAsync(
            HttpListenerContext context,
            bool requireCredentials,
            Func<Task> successCallback,
            Func<Task> unauthorizedCallback)
        {
            // If the route requires credentials but the identity is missing - return 401.
            if (requireCredentials && context.User?.Identity is not HttpListenerBasicIdentity)
            {
                await unauthorizedCallback();
                return;
            }

            if (context.User != null && context.User.Identity is HttpListenerBasicIdentity identity)
            {
                string clientUsername = identity.Name;
                string clientPassword = identity.Password;

                if (adminUsername == clientUsername && adminPassword == clientPassword)
                {
                    await successCallback();
                }
                else
                {
                    logger.Warn($"Unauthorized access attempt from {context.Request.RemoteEndPoint} as '{clientUsername}'");
                    await unauthorizedCallback();
                }
            }
            else
            {
                // Public routes (no credentials required).
                await successCallback();
            }
        }

        /// <summary>
        /// Helper to register a handler for a given HTTP verb collection.
        /// </summary>
        /// <param name="api">Route path without leading slash (e.g. "index", "api/status").</param>
        /// <param name="action">Asynchronous handler returning an <see cref="IHttpResult"/>.</param>
        /// <param name="useCredentials">Whether this route requires HTTP Basic Auth.</param>
        /// <param name="handlers">Dictionary to register into (GET, POST, etc.).</param>
        private void RegisterHandler(string api, HttpResultHandler action, bool useCredentials, ConcurrentDictionary<string, HttpRequestHandler> handlers)
        {
            api = api.Trim('/');
            if (!handlers.TryAdd(api, new HttpRequestHandler(api, useCredentials, action, null)))
            {
                logger.Warn($"Handler for API '{api}' already exists. Skipping registration.");
            }
        }

        /// <summary>
        /// Registers a GET request handler.
        /// </summary>
        /// <param name="api">Route path without leading slash.</param>
        /// <param name="action">Handler delegate.</param>
        /// <param name="useCredentials">Require Basic Auth for this route.</param>
        public void RegisterGetHandler(string api, HttpResultHandler action, bool useCredentials = false) =>
            RegisterHandler(api, action, useCredentials, getHandlers);

        /// <summary>
        /// Registers a POST request handler.
        /// </summary>
        public void RegisterPostHandler(string api, HttpResultHandler action, bool useCredentials = false) =>
            RegisterHandler(api, action, useCredentials, postHandlers);

        /// <summary>
        /// Registers a PUT request handler.
        /// </summary>
        public void RegisterPutHandler(string api, HttpResultHandler action, bool useCredentials = false) =>
            RegisterHandler(api, action, useCredentials, putHandlers);

        /// <summary>
        /// Registers a DELETE request handler.
        /// </summary>
        public void RegisterDeleteHandler(string api, HttpResultHandler action, bool useCredentials = false) =>
            RegisterHandler(api, action, useCredentials, deleteHandlers);

        /// <summary>
        /// Finds the matching handler for a given HTTP context.
        /// </summary>
        /// <param name="context">HTTP context to inspect.</param>
        /// <returns>Matched request handler or null.</returns>
        private HttpRequestHandler FindHandler(HttpListenerContext context)
        {
            return FindHandler(context.Request);
        }

        /// <summary>
        /// Finds the appropriate handler based on request method and path.
        /// </summary>
        /// <param name="httpRequest">Incoming HTTP request.</param>
        /// <returns>Matched request handler or null.</returns>
        private HttpRequestHandler FindHandler(HttpListenerRequest httpRequest)
        {
            string urlPathWithoutQuery = httpRequest.Url.AbsolutePath;
            string method = httpRequest.HttpMethod;
            string api = (urlPathWithoutQuery.Trim() == "/") ? "index" : urlPathWithoutQuery.Trim('/');

            if (method == "POST")
            {
                postHandlers.TryGetValue(api, out var h); return h;
            }
            if (method == "PUT")
            {
                putHandlers.TryGetValue(api, out var h); return h;
            }
            if (method == "DELETE")
            {
                deleteHandlers.TryGetValue(api, out var h); return h;
            }

            getHandlers.TryGetValue(api, out var g);
            return g;
        }

        /// <summary>
        /// Main request processing loop. Accepts contexts, applies throttling, and dispatches per-request tasks.
        /// </summary>
        /// <param name="token">Cancellation token to stop the loop gracefully.</param>
        private async Task HandleRequests(CancellationToken token)
        {
            var semaphore = concurrencySemaphore;

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
                    logger.Info("Http listener stopped due to cancellation.");
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error($"Error when getting context: {ex}");
                    continue;
                }

                try
                {
                    await semaphore.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break; // shutdown in progress
                }
                catch (ObjectDisposedException)
                {
                    break; // semaphore disposed during shutdown
                }

                Interlocked.Increment(ref activeRequestCount);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessSingleRequestSafely(context, token);
                    }
                    finally
                    {
                        semaphore.Release();
                        Interlocked.Decrement(ref activeRequestCount);
                    }
                });
            }

            logger.Info("Request handling loop terminated.");
        }

        /// <summary>
        /// Applies standard headers and dispatches the matched route with exception safety.
        /// </summary>
        /// <param name="context">HTTP context to process.</param>
        /// <param name="token">Cancellation token.</param>
        private async Task ProcessSingleRequestSafely(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                ApplyCorsHeaders(context.Response, context.Request);
                ApplySecurityHeaders(context.Response);

                if (context.Request.HttpMethod == "OPTIONS" ||
                    context.Request.HttpMethod == "HEAD")
                {
                    await ProcessRequestAsync(context, () => NoContent(context.Request));
                    return;
                }

                HttpRequestHandler handler = FindHandler(context);

                if (handler == null)
                {
                    await ProcessRequestAsync(context, () =>
                        NotFound(context.Request, $"Route {context.Request.RawUrl} not found"));
                }
                else
                {
                    await ValidateRequestAsync(
                        context,
                        requireCredentials: RequiresCredentials(handler),
                        successCallback: async () =>
                        {
                            await ProcessRequestAsync(context, () => handler.Action(context.Request));
                        },
                        unauthorizedCallback: async () =>
                        {
                            await ProcessRequestAsync(context, () => Unauthorized(context.Request, "Unauthorized"));
                        });
                }

            }
            catch (Exception ex)
            {
                logger.Error($"Error processing request {context.Request.Url}: {ex}");
                await ProcessRequestAsync(context, () => InternalServerError(context.Request, ex.Message));
            }
        }

        /// <summary>
        /// Executes a handler and writes the result to the client.
        /// </summary>
        /// <param name="context">HTTP context for the response.</param>
        /// <param name="handler">Delegate returning an <see cref="IHttpResult"/>.</param>
        private async Task ProcessRequestAsync(HttpListenerContext context, Func<Task<IHttpResult>> handler)
        {
            var result = await handler();
            await result.Execute(context);
        }

        /// <summary>
        /// Requests web server shutdown without blocking the caller.
        /// </summary>
        public void Stop()
        {
            _ = StopAsync();
        }

        /// <summary>
        /// Stops accepting requests immediately and completes after request cleanup finishes.
        /// </summary>
        /// <returns>A task shared by all callers while the current HTTP server run is stopping.</returns>
        public Task StopAsync()
        {
            lock (serverLifecycleSync)
            {
                if (!isServerRunning)
                    return stopTask;

                isServerRunning = false;

                CancellationTokenSource runCancellation = cancellationTokenSource;
                HttpListener listener = httpServer;
                Task requestLoop = requestHandlingTask;
                SemaphoreSlim semaphore = concurrencySemaphore;

                try
                {
                    runCancellation?.Cancel();
                    listener?.Stop();
                }
                catch (Exception exception)
                {
                    logger.Error($"Failed to request web server shutdown: {exception}");
                }

                stopTask = CompleteStopAsync(runCancellation, listener, requestLoop, semaphore);
                return stopTask;
            }
        }

        private async Task CompleteStopAsync(CancellationTokenSource runCancellation,
            HttpListener listener, Task requestLoop, SemaphoreSlim semaphore)
        {
            logger.Info("Stopping web server...");

            try
            {
                if (requestLoop != null)
                    await requestLoop;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Error while stopping web server: {exception}");
            }

            while (Interlocked.Read(ref activeRequestCount) > 0)
                await Task.Delay(50);

            try
            {
                listener?.Abort();
                listener?.Close();
            }
            catch (Exception exception)
            {
                logger.Error($"Error while closing web server listener: {exception}");
            }
            finally
            {
                DisposeControllersAndClearRoutes();
                semaphore?.Dispose();
                runCancellation?.Dispose();

                lock (serverLifecycleSync)
                {
                    if (ReferenceEquals(httpServer, listener))
                        httpServer = null;

                    if (ReferenceEquals(cancellationTokenSource, runCancellation))
                        cancellationTokenSource = null;

                    if (ReferenceEquals(concurrencySemaphore, semaphore))
                        concurrencySemaphore = null;

                    if (ReferenceEquals(requestHandlingTask, requestLoop))
                        requestHandlingTask = null;
                }

                logger.Info("Web server stopped successfully.");
            }
        }

        /// <summary>
        /// Generates a JSON object with server status, configuration, controllers and registered routes.
        /// </summary>
        /// <returns><see cref="MstJson"/> describing the module state.</returns>
        public override MstJson Details()
        {
            var info = base.Details();

            info["properties"].AddField("address", httpAddress);
            info["properties"].AddField("port", httpPort);
            info["properties"].AddField("activeRequests", activeRequestCount);
            info["properties"].AddField("maxConcurrentRequests", maxTasksCount);
            info["properties"].AddField("serverRunning", isServerRunning);
            info["properties"].AddField("controllers", MstJson.CreateArray());

            foreach (var controller in Controllers.Values)
                info["properties"]["controllers"].Add(controller.JsonInfo());

            var requestActions = MstJson.CreateObject();
            requestActions.AddField("get", MstJson.CreateArray());
            requestActions.AddField("post", MstJson.CreateArray());
            requestActions.AddField("put", MstJson.CreateArray());
            requestActions.AddField("delete", MstJson.CreateArray());

            foreach (var requestAction in getHandlers.Values)
                requestActions["get"].Add(requestAction.ToJson());

            foreach (var requestAction in postHandlers.Values)
                requestActions["post"].Add(requestAction.ToJson());

            foreach (var requestAction in putHandlers.Values)
                requestActions["put"].Add(requestAction.ToJson());

            foreach (var requestAction in deleteHandlers.Values)
                requestActions["delete"].Add(requestAction.ToJson());

            info["properties"].AddField("requestActions", requestActions);
            return info;
        }

        #region RESPONSE HANDLERS

        /// <summary>
        /// Returns HTTP 200 with an HTML body.
        /// </summary>
        /// <param name="Request">Original request (unused).</param>
        /// <param name="message">HTML string to return.</param>
        protected virtual Task<IHttpResult> Ok(HttpListenerRequest Request, string message)
        {
            return Task.FromResult<IHttpResult>(new HtmlResult(message));
        }

        /// <summary>
        /// Returns HTTP 204 with no content.
        /// </summary>
        /// <param name="Request">Original request (unused).</param>
        protected virtual Task<IHttpResult> NoContent(HttpListenerRequest Request)
        {
            return Task.FromResult<IHttpResult>(new NoContentResult());
        }

        /// <summary>
        /// Returns HTTP 401 with a WWW-Authenticate challenge using the configured realm.
        /// </summary>
        /// <param name="Request">Original request (unused).</param>
        /// <param name="message">Reason message.</param>
        protected virtual Task<IHttpResult> Unauthorized(HttpListenerRequest Request, string message)
        {
            IHttpResult result = IsApiV1Request(Request)
                ? new UnauthorizedJson(message, CurrentRealm())
                : new Unauthorized(message, CurrentRealm());

            return Task.FromResult(result);
        }

        /// <summary>
        /// Returns HTTP 400 for malformed or invalid requests.
        /// </summary>
        /// <param name="Request">Original request (unused).</param>
        /// <param name="message">Reason message.</param>
        protected virtual Task<IHttpResult> BadRequest(HttpListenerRequest Request, string message)
        {
            IHttpResult result = IsApiV1Request(Request)
                ? new BadRequestJson(message)
                : new BadRequest(message);

            return Task.FromResult(result);
        }

        /// <summary>
        /// Returns HTTP 404 when the requested route cannot be matched.
        /// </summary>
        /// <param name="Request">Original request (unused).</param>
        /// <param name="message">Reason message.</param>
        protected virtual Task<IHttpResult> NotFound(HttpListenerRequest Request, string message)
        {
            IHttpResult result = IsApiV1Request(Request)
                ? new NotFoundJson(message)
                : new NotFound(message);

            return Task.FromResult(result);
        }

        /// <summary>
        /// Returns HTTP 500 when an unhandled error occurs.
        /// </summary>
        /// <param name="Request">Original request (unused).</param>
        /// <param name="message">Reason message.</param>
        protected virtual Task<IHttpResult> InternalServerError(HttpListenerRequest Request, string message)
        {
            IHttpResult result = IsApiV1Request(Request)
                ? new InternalServerErrorJson(message)
                : new InternalServerError(message);

            return Task.FromResult(result);
        }

        #endregion
    }
}
