using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public delegate void OnHttpRequestDelegate(HttpListenerRequest request, HttpListenerResponse response);

    public enum HttpMethod { GET, POST, PUT, DELETE }

    public class HttpServerModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Accessibility Settings"), SerializeField]
        private float heartBeatCheckInterval = 5f;

        [Header("Http Server Settings"), SerializeField]
        protected bool autostart = true;
        [SerializeField]
        protected string httpAddress = "127.0.0.1";
        [SerializeField]
        protected int httpPort = 5056;
        [SerializeField]
        protected bool useSecure = false;
        [SerializeField]
        protected string[] defaultIndexPage = new string[] { "index", "home" };

        [Header("User Credentials Settings"), SerializeField]
        protected bool useCredentials = true;
        [SerializeField]
        protected string username = "admin";
        [SerializeField]
        protected string password = "admin";

        #endregion

        private string checkHeartBeatUrl = "";

        /// <summary>
        /// Current http server
        /// </summary>
        private HttpListener httpServer;

        /// <summary>
        /// 
        /// </summary>
        private Thread httpThread;

        /// <summary>
        /// List of http request handlers
        /// </summary>
        private readonly ConcurrentDictionary<string, OnHttpRequestDelegate> httpRequestActions = new ConcurrentDictionary<string, OnHttpRequestDelegate>();

        /// <summary>
        /// 
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> httpRequestsWithCredentials = new ConcurrentDictionary<string, bool>();

        /// <summary>
        /// List of surface controllers
        /// </summary>
        public Dictionary<Type, IHttpController> Controllers { get; protected set; } = new Dictionary<Type, IHttpController>();

        /// <summary>
        /// 
        /// </summary>
        public bool UseSecure
        {
            get => useSecure;
            set => useSecure = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public string CertificatePath { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string CertificatePassword { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Username
        {
            get => username;
            set => username = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public string Password
        {
            get => password;
            set => password = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public string HttpAddress
        {
            get => httpAddress;
            set => httpAddress = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public int HttpPort
        {
            get => httpPort;
            set => httpPort = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public string[] DefaultIndexPages
        {
            get => defaultIndexPage;
            set => defaultIndexPage = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public float HeartBeatCheckInterval
        {
            get => heartBeatCheckInterval;
            set => heartBeatCheckInterval = value;
        }

        protected override void Awake()
        {
            base.Awake();

            username = Mst.Args.AsString(Mst.Args.Names.WebUsername, username);
            password = Mst.Args.AsString(Mst.Args.Names.WebPassword, password);

            // Set heartbeat check interval
            heartBeatCheckInterval = Mathf.Clamp(Mst.Args.AsFloat(Mst.Args.Names.WebServerHeartbeatCheckInterval, heartBeatCheckInterval), 2f, 120f);
            checkHeartBeatUrl = Mst.Args.AsString($"live-{Mst.Args.Names.WebServerHeartbeatCheckPage}", $"live-{Mst.Helper.CreateGuidString()}");

            // Set port
            httpPort = Mst.Args.AsInt(Mst.Args.Names.WebPort, httpPort);

            // Set port
            httpAddress = Mst.Args.AsString(Mst.Args.Names.WebAddress, httpAddress);
        }

        private void OnValidate()
        {
            heartBeatCheckInterval = Mathf.Clamp(heartBeatCheckInterval, 2f, 120f);
        }

        private void OnDestroy()
        {
            logger.Info($"Http server stopped");
            Stop();
        }

        public override void Initialize(IServer server)
        {
            CancelInvoke();

            // Start heartbeat checking
            InvokeRepeating(nameof(CheckHeartBeat), heartBeatCheckInterval, heartBeatCheckInterval);

            if (autostart)
            {
                // Start
                Listen();
            }
        }

        /// <summary>
        /// Starts listening web requests
        /// </summary>
        public void Listen()
        {
            // Stop if started
            Stop();

            logger.Info($"Starting http server: {(UseSecure ? "https://" : "http://")}{httpAddress}:{httpPort}");

            // Initialize server
            httpServer = new HttpListener();

            httpServer.AuthenticationSchemeSelectorDelegate =
                new AuthenticationSchemeSelector(AuthenticationSchemeSelectorHandler);

            // Registers default pages
            RegisterDefaultControllers();

            // Find all surface controllers and add them to server
            foreach (var controller in GetComponentsInChildren<IHttpController>())
            {
                if (!Controllers.ContainsKey(controller.GetType()))
                {
                    Controllers[controller.GetType()] = controller;
                    controller.Initialize(this);
                }
            }

            httpThread = new Thread(new ThreadStart(async () =>
            {
                try
                {
                    // Start http server
                    httpServer.Start();

                    if (httpServer.IsListening)
                    {
                        logger.Info($"Http server is started and listening: {(UseSecure ? "https://" : "http://")}{httpAddress}:{httpPort}");
                    }
                    else
                    {
                        logger.Error($"Http server is not started");
                    }

                    while (httpServer.IsListening)
                    {
                        // The GetContext method blocks while waiting for a request.
                        HttpListenerContext context = await httpServer?.GetContextAsync();

                        if (context == null) continue;

                        ValidateRequest(context, () =>
                        {
                            // Obtain a request object.
                            HttpListenerRequest request = context.Request;
                            // Obtain a response object.
                            HttpListenerResponse response = context.Response;

                            switch (request.HttpMethod.ToLower())
                            {
                                case "get":
                                    HttpGetRequestHandler(request, response);
                                    break;
                                case "post":
                                    HttpPostRequestHandler(request, response);
                                    break;
                            }
                        });
                    }
                }
                catch (ObjectDisposedException) { }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            }));

            httpThread.IsBackground = true;
            httpThread.Name = "MstWebServerThread";
            httpThread.Start();
        }

        private AuthenticationSchemes AuthenticationSchemeSelectorHandler(HttpListenerRequest request)
        {
            try
            {
                // Let's parse user
                string cleanRawUrl = ClearRawUrl(request);

                // Let's ceate url key
                string urlKey = CreateHttpRequestHandlerKey(cleanRawUrl, Enum.Parse<HttpMethod>(request.HttpMethod));

                if (httpRequestsWithCredentials.TryGetValue(urlKey, out var useCredentials))
                {
                    if (useCredentials)
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
            catch (Exception e)
            {
                logger.Error(e);
                return AuthenticationSchemes.None;
            }
        }

        private void ValidateRequest(HttpListenerContext context, Action successCallback)
        {
            if (context.User != null)
            {
                var identity = (HttpListenerBasicIdentity)context.User.Identity;

                string clientUsername = identity.Name;
                string clientPassword = identity.Password;

                if (username == clientUsername && password == clientPassword)
                {
                    successCallback?.Invoke();
                }
                else
                {
                    byte[] contents = Encoding.UTF8.GetBytes(Default401Page());

                    context.Response.ContentType = "text/html";
                    context.Response.ContentEncoding = Encoding.UTF8;
                    context.Response.ContentLength64 = contents.LongLength;
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.Close(contents, true);
                }
            }
            else
            {
                successCallback?.Invoke();
            }
        }

        private async void CheckHeartBeat()
        {
            if (httpServer == null || !httpServer.IsListening) return;

            HttpWebResponse hbWebResponse = null;

            try
            {
                string url = CreateHttpRequestPrefix(checkHeartBeatUrl, httpAddress);

                if (url.EndsWith("/"))
                    url = url.Substring(0, url.Length - 1);

                HttpWebRequest hbWebRequest = (HttpWebRequest)WebRequest.Create(url);
                hbWebRequest.Credentials = new NetworkCredential(username, password);
                hbWebRequest.Timeout = 5000;
                hbWebRequest.Method = "GET";
                hbWebRequest.ContentType = "text/html";

                hbWebResponse = (HttpWebResponse)await hbWebRequest.GetResponseAsync();
                hbWebResponse.Close();
            }
            catch (Exception e)
            {
                if (hbWebResponse != null)
                    hbWebResponse.Close();

                logger.Error($"Web server is dead: {e.Message}. Restarting...");
                Listen();
            }
        }

        private void RegisterDefaultControllers()
        {
            RegisterHttpGetRequestHandler("/", (request, response) =>
            {
                byte[] contents = Encoding.UTF8.GetBytes(DefaultIndexPageHtml());

                response.ContentType = "text/html";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.Close(contents, true);
            });

            RegisterHttpGetRequestHandler(checkHeartBeatUrl, (request, response) =>
            {
                byte[] contents = Encoding.UTF8.GetBytes("Ok");

                response.ContentType = "text/html";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.Close(contents, true);
            });

            foreach (string page in defaultIndexPage)
            {
                RegisterHttpGetRequestHandler(page, (request, response) =>
                {
                    byte[] contents = Encoding.UTF8.GetBytes(DefaultIndexPageHtml());

                    response.ContentType = "text/html";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = contents.LongLength;
                    response.Close(contents, true);
                });
            }
        }

        protected virtual string Default401Page()
        {
            HtmlDocument html = new HtmlDocument
            {
                Title = $"401 | {Mst.Name} {Mst.Version}"
            };

            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));
            html.AddMeta(new KeyValuePair<string, string>("name", "description"), new KeyValuePair<string, string>("content", "Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems."));
            html.AddMeta(new KeyValuePair<string, string>("name", "author"), new KeyValuePair<string, string>("content", "Master Server Toolkit"));

            html.Links.Add(new HtmlLinkElement()
            {
                Href = HtmlLibs.BOOTSTRAP_CSS_SRC,
                Rel = "stylesheet",
                Integrity = HtmlLibs.BOOTSTRAP_CSS_INTEGRITY,
                Crossorigin = "anonymous"
            });

            html.Scripts.Add(new HtmlScriptElement()
            {
                Src = HtmlLibs.BOOTSTRAP_JS_SRC,
                Integrity = HtmlLibs.BOOTSTRAP_JS_INTEGRITY,
                Crossorigin = "anonymous"
            });

            html.Body.AddClass("vh-100");

            var container = html.CreateElement("div");
            container.AddClass("container h-100");
            html.Body.AppendChild(container);

            var row = html.CreateElement("div");
            row.AddClass("row h-100");
            container.AppendChild(row);

            var col = html.CreateElement("div");
            col.AddClass("col align-self-center text-center");
            row.AppendChild(col);

            var h2 = html.CreateElement("h2");
            h2.InnerText = $"{Mst.Name} {Mst.Version}";
            col.AppendChild(h2);

            var h3 = html.CreateElement("h3");
            h3.AddClass("display-3");
            h3.InnerText = "401 Unauthorized request";
            col.AppendChild(h3);

            var p1 = html.CreateElement("p");
            p1.InnerText = $"This is default 401 page. You can override it by overloading {nameof(Default401Page)} method in {nameof(HttpServerModule)}";
            col.AppendChild(p1);

            var p2 = html.CreateElement("p");
            col.AppendChild(p2);

            var ul = html.CreateElement("ul");
            ul.AddClass("list-unstyled");
            col.AppendChild(ul);

            var li1 = html.CreateElement("li");
            ul.AppendChild(li1);

            var li2 = html.CreateElement("li");
            ul.AppendChild(li2);

            var href2 = html.CreateElement("a");
            href2.InnerText = "Input valid credentials";
            href2.SetAttribute("href", "/");
            li2.AppendChild(href2);

            return html.ToString();
        }

        /// <summary>
        /// Default 404 Page. Overload this method to create your own design.
        /// </summary>
        /// <returns></returns>
        protected virtual string Default404Page()
        {
            HtmlDocument html = new HtmlDocument
            {
                Title = $"404 | {Mst.Name} {Mst.Version}"
            };

            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));
            html.AddMeta(new KeyValuePair<string, string>("name", "description"), new KeyValuePair<string, string>("content", "Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems."));
            html.AddMeta(new KeyValuePair<string, string>("name", "author"), new KeyValuePair<string, string>("content", "Master Server Toolkit"));

            html.Links.Add(new HtmlLinkElement()
            {
                Href = HtmlLibs.BOOTSTRAP_CSS_SRC,
                Rel = "stylesheet",
                Integrity = HtmlLibs.BOOTSTRAP_CSS_INTEGRITY,
                Crossorigin = "anonymous"
            });

            html.Scripts.Add(new HtmlScriptElement()
            {
                Src = HtmlLibs.BOOTSTRAP_JS_SRC,
                Integrity = HtmlLibs.BOOTSTRAP_JS_INTEGRITY,
                Crossorigin = "anonymous"
            });

            html.Body.AddClass("vh-100");

            var container = html.CreateElement("div");
            container.AddClass("container h-100");
            html.Body.AppendChild(container);

            var row = html.CreateElement("div");
            row.AddClass("row h-100");
            container.AppendChild(row);

            var col = html.CreateElement("div");
            col.AddClass("col align-self-center text-center");
            row.AppendChild(col);

            var h2 = html.CreateElement("h2");
            h2.InnerText = $"{Mst.Name} {Mst.Version}";
            col.AppendChild(h2);

            var h3 = html.CreateElement("h3");
            h3.AddClass("display-3");
            h3.InnerText = "404 Page";
            col.AppendChild(h3);

            var p1 = html.CreateElement("p");
            p1.InnerText = $"This is default 404 page. You can override it by overloading {nameof(Default404Page)} method in {nameof(HttpServerModule)}";
            col.AppendChild(p1);

            var p2 = html.CreateElement("p");
            col.AppendChild(p2);

            var ul = html.CreateElement("ul");
            ul.AddClass("list-unstyled");
            col.AppendChild(ul);

            var li1 = html.CreateElement("li");
            ul.AppendChild(li1);

            var href1 = html.CreateElement("a");
            href1.InnerText = "Open master server info page";
            href1.SetAttribute("href", "info");
            li1.AppendChild(href1);

            var li2 = html.CreateElement("li");
            ul.AppendChild(li2);

            var href2 = html.CreateElement("a");
            href2.InnerText = "Open home page";
            href2.SetAttribute("href", "/");
            li2.AppendChild(href2);

            return html.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual string DefaultIndexPageHtml()
        {
            HtmlDocument html = new HtmlDocument
            {
                Title = $"Home | {Mst.Name} {Mst.Version}"
            };

            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));
            html.AddMeta(new KeyValuePair<string, string>("name", "description"), new KeyValuePair<string, string>("content", "Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems."));
            html.AddMeta(new KeyValuePair<string, string>("name", "author"), new KeyValuePair<string, string>("content", "Master Server Toolkit"));

            html.Links.Add(new HtmlLinkElement()
            {
                Href = HtmlLibs.BOOTSTRAP_CSS_SRC,
                Rel = "stylesheet",
                Integrity = HtmlLibs.BOOTSTRAP_CSS_INTEGRITY,
                Crossorigin = "anonymous"
            });

            html.Scripts.Add(new HtmlScriptElement()
            {
                Src = HtmlLibs.BOOTSTRAP_JS_SRC,
                Integrity = HtmlLibs.BOOTSTRAP_JS_INTEGRITY,
                Crossorigin = "anonymous"
            });

            html.Body.AddClass("vh-100");

            var container = html.CreateElement("div");
            container.AddClass("container h-100");
            html.Body.AppendChild(container);

            var row = html.CreateElement("div");
            row.AddClass("row h-100");
            container.AppendChild(row);

            var col = html.CreateElement("div");
            col.AddClass("col align-self-center text-center");
            row.AppendChild(col);

            var h2 = html.CreateElement("h2");
            h2.InnerText = $"{Mst.Name} {Mst.Version}";
            col.AppendChild(h2);

            var h3 = html.CreateElement("h3");
            h3.AddClass("display-3");
            h3.InnerText = "Home Page";
            col.AppendChild(h3);

            var p1 = html.CreateElement("p");
            p1.InnerText = $"This is default Index page. You can override it by overloading {nameof(DefaultIndexPageHtml)} method in {nameof(HttpServerModule)}";
            col.AppendChild(p1);

            var p2 = html.CreateElement("p");
            col.AppendChild(p2);

            var ul = html.CreateElement("ul");
            ul.AddClass("list-unstyled");
            col.AppendChild(ul);

            var li1 = html.CreateElement("li");
            ul.AppendChild(li1);

            var href1 = html.CreateElement("a");
            href1.InnerText = "Open master server info page";
            href1.SetAttribute("href", "info");
            li1.AppendChild(href1);

            var li2 = html.CreateElement("li");
            ul.AppendChild(li2);

            var href2 = html.CreateElement("a");
            href2.InnerText = "Open 404 page";
            href2.SetAttribute("href", "somwhere");
            li2.AppendChild(href2);

            return html.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected string ClearRawUrl(HttpListenerRequest request)
        {
            return request.RawUrl.Split(new char[] { '?' }, StringSplitOptions.RemoveEmptyEntries)[0];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        protected bool TryGetExtension(string path, out string extension)
        {
            extension = string.Empty;
            int indexOfExtension = path.LastIndexOf('.');

            if (indexOfExtension >= 0)
                extension = path.Substring(indexOfExtension);

            return !string.IsNullOrEmpty(extension);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void HttpGetRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                // Let's parse user
                string cleanRawUrl = ClearRawUrl(request);

                // Let's ceate url key
                string urlKey = CreateHttpRequestHandlerKey(cleanRawUrl, HttpMethod.GET);

                if (httpRequestActions.TryGetValue(urlKey, out var action) && action != null)
                {
                    action.Invoke(request, response);
                }
                else
                {
                    byte[] contents = Encoding.UTF8.GetBytes(Default404Page());

                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.ContentType = "text/html";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = contents.LongLength;
                    response.Close(contents, true);
                }
            }
            catch (Exception e)
            {
                byte[] contents = Encoding.UTF8.GetBytes(e.ToString());

                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.ContentType = "text/html";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.Close(contents, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void HttpPostRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Let's parse user
            string cleanRawUrl = ClearRawUrl(request);

            logger.Debug($"HTTP Post Request: [{cleanRawUrl}]");

            // Let's ceate url key
            string urlKey = CreateHttpRequestHandlerKey(cleanRawUrl, HttpMethod.POST);

            if (httpRequestActions.TryGetValue(urlKey, out var action) && action != null)
            {
                action.Invoke(request, response);
            }
            else
            {
                byte[] contents = Encoding.UTF8.GetBytes("There is no such controller");

                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.Close(contents, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void HttpPutRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            // Let's parse user
            string cleanRawUrl = ClearRawUrl(request);

            logger.Debug($"HTTP Put Request: [{cleanRawUrl}]");

            // Let's ceate url key
            string urlKey = CreateHttpRequestHandlerKey(cleanRawUrl, HttpMethod.PUT);

            if (httpRequestActions.TryGetValue(urlKey, out var action) && action != null)
            {
                action.Invoke(request, response);
            }
            else
            {
                byte[] contents = Encoding.UTF8.GetBytes("There is no such controller");

                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.ContentType = "application/json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = contents.LongLength;
                response.Close(contents, true);
            }
        }

        /// <summary>
        /// Stop web server
        /// </summary>
        public void Stop()
        {
            httpThread?.Abort();

            foreach (var controller in Controllers.Values)
                controller.Dispose();

            Controllers.Clear();
            httpRequestActions.Clear();

            httpServer?.Stop();
            httpServer?.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="httpMethod"></param>
        /// <returns></returns>
        public string CreateHttpRequestHandlerKey(string path, HttpMethod httpMethod)
        {
            return $"{httpMethod}/{path.Trim()}".Replace("//", "/").ToLower();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string CreateHttpRequestPrefix(string path)
        {
            return CreateHttpRequestPrefix(path, httpAddress);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="httpAddress"></param>
        /// <returns></returns>
        public string CreateHttpRequestPrefix(string path, string httpAddress)
        {
            return CreateHttpRequestPrefix(path, httpAddress, httpPort);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="httpAddress"></param>
        /// <param name="httpPort"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public string CreateHttpRequestPrefix(string path, string httpAddress, int httpPort)
        {
            path = path.Trim();

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string protocol = UseSecure ? "https://" : "http://";
            string url = $"{httpAddress}:{httpPort}/{path}/".Replace("//", "/");
            return $"{protocol}{url}".ToLower();
        }

        /// <summary>
        /// Registers http get request method
        /// </summary>
        /// <param name="path"></param>
        /// <param name="handler"></param>
        /// <param name="useCredentials"></param>
        public void RegisterHttpGetRequestHandler(string path, OnHttpRequestDelegate handler, bool useCredentials = false)
        {
            RegisterHttpRequestHandler(path, HttpMethod.GET, handler, useCredentials);
        }

        /// <summary>
        /// Registers http get request method
        /// </summary>
        /// <param name="path"></param>
        /// <param name="handler"></param>
        /// <param name="useCredentials"></param>
        public void RegisterHttpPostRequestHandler(string path, OnHttpRequestDelegate handler, bool useCredentials = false)
        {
            RegisterHttpRequestHandler(path, HttpMethod.POST, handler, useCredentials);
        }

        /// <summary>
        /// Registers http request method
        /// </summary>
        /// <param name="path"></param>
        /// <param name="handler"></param>
        public void RegisterHttpRequestHandler(string path, HttpMethod httpMethod, OnHttpRequestDelegate handler, bool useCredentials = false)
        {
            string url;
            string prefix = CreateHttpRequestPrefix(path);
            url = CreateHttpRequestHandlerKey(path, httpMethod);

            if (httpRequestActions.ContainsKey(url))
            {
                logger.Error($"Handler [{url}] already exists");
                return;
            }

            httpServer.Prefixes.Add(prefix);
            httpRequestActions.TryAdd(url, handler);
            httpRequestsWithCredentials.TryAdd(url, useCredentials);
        }

        public override MstJson JsonInfo()
        {
            var info = base.JsonInfo();

            info.AddField("localIp", httpAddress);
            info.AddField("port", httpPort);
            info.AddField("checkHeartBeatUrl", checkHeartBeatUrl);

            var controllers = MstJson.EmptyArray;

            foreach (var controller in Controllers.Keys.Select(x => x.Name))
                controllers.Add(controller);

            info.AddField("controllers", controllers);

            var requestActions = MstJson.EmptyArray;

            foreach (var requestAction in httpRequestActions.Keys)
                controllers.Add(requestAction);

            info.AddField("requestActions", requestActions);

            return info;
        }

        public override MstProperties Info()
        {
            MstProperties info = base.Info();
            info.Add("Local Ip", httpAddress);
            info.Add("Port", httpPort);
            info.Add("Surface Controllers", string.Join(", ", Controllers.Keys));
            return info;
        }
    }
}