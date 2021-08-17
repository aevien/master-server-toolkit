using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer.Web;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using UnityEngine;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public delegate void OnHttpRequestDelegate(HttpRequestEventArgs eventArgs);

    public enum HttpMethod { GET, POST, PUT, DELETE }

    public class HttpServerModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Http Server Settings"), SerializeField]
        protected string httpAddress = "127.0.0.1";
        [SerializeField]
        protected int httpPort = 5056;
        [SerializeField]
        protected string[] defaultIndexPage = new string[] { "index", "home" };
        [SerializeField]
        protected List<MimeTypeInfo> mimeTypes;

        [SerializeField]
        protected string rootDirectory = "wwwroot";

        [Header("User Credentials Settings"), SerializeField]
        protected AuthenticationSchemes authenticationSchemes = AuthenticationSchemes.Digest;
        [SerializeField]
        protected string realm = "adminusers@masterservertoolkit.com";
        [SerializeField]
        protected string username = "admin";
        [SerializeField]
        protected string password = "admin";

        #endregion

        /// <summary>
        /// Current http server
        /// </summary>
        private HttpServer httpServer;

        /// <summary>
        /// List of http request handlers
        /// </summary>
        private Dictionary<string, OnHttpRequestDelegate> httpRequestHandlers;

        /// <summary>
        /// 
        /// </summary>
        private string wsServicePath = "ws";

        /// <summary>
        /// List of surface controllers
        /// </summary>
        public Dictionary<Type, IHttpController> SurfaceControllers { get; protected set; }

        /// <summary>
        /// List of web socket controllers
        /// </summary>
        public Dictionary<Type, IWsController> WsControllers { get; protected set; }

        /// <summary>
        /// Invokes before server frame is updated
        /// </summary>
        public event Action OnUpdateEvent;

        public bool UserSecure { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }

        private void OnValidate()
        {
            if (mimeTypes == null || mimeTypes.Count == 0)
            {
                mimeTypes = new List<MimeTypeInfo>
                {
                    new MimeTypeInfo() { name = ".html", type = "text/html" },
                    new MimeTypeInfo() { name = ".htm", type = "text/html" },
                    new MimeTypeInfo() { name = ".txt", type = "text/plain" },
                    new MimeTypeInfo() { name = ".css", type = "text/css" },
                    new MimeTypeInfo() { name = ".xml", type = "text/xml" },
                    new MimeTypeInfo() { name = ".js", type = "application/javascript" },
                    new MimeTypeInfo() { name = ".json", type = "application/json" },
                    new MimeTypeInfo() { name = ".gif", type = "image/gif" },
                    new MimeTypeInfo() { name = ".jpeg", type = "image/jpeg" },
                    new MimeTypeInfo() { name = ".jpg", type = "image/jpeg" },
                    new MimeTypeInfo() { name = ".png", type = "image/png" },
                    new MimeTypeInfo() { name = ".tif", type = "image/tiff" }
                };
            }
        }

        private void Update()
        {
            OnUpdateEvent?.Invoke();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void OnApplicationQuit()
        {
            Stop();
        }

        public override void Initialize(IServer server)
        {
            // Setup secure connection
            UserSecure = MstApplicationConfig.Singleton.UseSecure;
            CertificatePath = MstApplicationConfig.Singleton.CertificatePath;
            CertificatePassword = MstApplicationConfig.Singleton.CertificatePassword;

            // Set port
            httpPort = Mst.Args.AsInt(Mst.Args.Names.WebPort, httpPort);

            // Set port
            httpAddress = Mst.Args.AsString(Mst.Args.Names.WebAddress, httpAddress);

            // Set root directory
            rootDirectory = Mst.Args.AsString(Mst.Args.Names.WebRootDir, rootDirectory);

            // Initialize server
            httpServer = new HttpServer(System.Net.IPAddress.Parse(httpAddress), httpPort, UserSecure)
            {
                AuthenticationSchemes = authenticationSchemes == AuthenticationSchemes.None ? AuthenticationSchemes.Anonymous : authenticationSchemes,
                Realm = realm,
                UserCredentialsFinder = UserCredentialsFinder
            };

            // Init root directory. Create if exists
            InitRooDirectory();

            if (UserSecure)
            {
                if (string.IsNullOrEmpty(CertificatePath.Trim()))
                {
                    logger.Error("You are using secure connection, but no path to certificate defined. Stop connection process.");
                    return;
                }

                if (string.IsNullOrEmpty(CertificatePassword.Trim()))
                    httpServer.SslConfiguration.ServerCertificate = new X509Certificate2(CertificatePath);
                else
                    httpServer.SslConfiguration.ServerCertificate = new X509Certificate2(CertificatePath, CertificatePassword);

                httpServer.SslConfiguration.EnabledSslProtocols =
                    System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Ssl3
                    | System.Security.Authentication.SslProtocols.Default;
            }

            // Initialize controllers lists
            SurfaceControllers = new Dictionary<Type, IHttpController>();
            WsControllers = new Dictionary<Type, IWsController>();

            // Initialize handlers list
            httpRequestHandlers = new Dictionary<string, OnHttpRequestDelegate>();

            // Find all surface controllers and add them to server
            foreach (var controller in GetComponentsInChildren<IHttpController>())
            {
                if (SurfaceControllers.ContainsKey(controller.GetType()))
                {
                    throw new Exception("A controller already exists in the server: " + controller.GetType());
                }

                SurfaceControllers[controller.GetType()] = controller;
                controller.Initialize(this);
            }

            // Find all web socket controllers and add them to server
            foreach (var controller in GetComponentsInChildren<IWsController>())
            {
                if (WsControllers.ContainsKey(controller.GetType()))
                {
                    throw new Exception("A controller already exists in the server: " + controller.GetType());
                }

                WsControllers[controller.GetType()] = controller;
            }

            // Start listen to Get request
            httpServer.OnGet += HttpServer_OnGet;

            // Start listen to Post request
            httpServer.OnPost += HttpServer_OnPost;

            // Start listen to Put request
            httpServer.OnPut += HttpServer_OnPut;

            // Register web socket controller service
            httpServer.AddWebSocketService<WsControllerService>($"/{wsServicePath}", (service) =>
            {
                service.IgnoreExtensions = true;
                service.SetHttpServer(this);
            });

            // Start http server
            httpServer.Start();

            if (httpServer.IsListening)
            {
                logger.Info($"Web socket server is started and listening: {httpServer.Address}:{httpServer.Port}/{wsServicePath}");
                logger.Info($"Http server is started and listening: {httpServer.Address}:{httpServer.Port}");
            }
        }

        /// <summary>
        /// Init root directory. Create if exists
        /// </summary>
        private void InitRooDirectory()
        {
            // Get app directory
            string appDirectory = Directory.GetCurrentDirectory();

            // Root directory path
            string rootDirPath = Path.Combine(appDirectory, rootDirectory);

            // Create if not eaxist
            if (!Directory.Exists(rootDirPath))
            {
                logger.Info($"No web root directory found. Lets create it as \"{rootDirPath}\"");

                Directory.CreateDirectory(rootDirPath);

                logger.Info($"Root directory is created as \"{rootDirPath}\"");
            }
            else
            {
                logger.Info($"Root directory found in \"{rootDirPath}\"");
            }

            httpServer.DocumentRootPath = rootDirPath;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        protected virtual NetworkCredential UserCredentialsFinder(IIdentity identity)
        {
            if (identity.Name == username)
            {
                return new NetworkCredential(identity.Name, password);
            }

            return null;
        }

        protected virtual void HttpServer_OnGet(object sender, HttpRequestEventArgs e)
        {
            HttpGetRequestHandler(sender, e);
        }

        protected virtual void HttpServer_OnPost(object sender, HttpRequestEventArgs e)
        {
            HttpPostRequestHandler(sender, e);
        }

        protected virtual void HttpServer_OnPut(object sender, HttpRequestEventArgs e)
        {
            HttpPutRequestHandler(sender, e);
        }

        /// <summary>
        /// Default 404 Page. Overload this method to create your own design.
        /// </summary>
        /// <returns></returns>
        protected virtual string Default404Page()
        {
            HtmlDocument html = new HtmlDocument
            {
                Title = "404 | Master Server Toolkit"
            };

            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));
            html.AddMeta(new KeyValuePair<string, string>("name", "description"), new KeyValuePair<string, string>("content", "Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems."));
            html.AddMeta(new KeyValuePair<string, string>("name", "author"), new KeyValuePair<string, string>("content", "Master Server Toolkit"));

            html.Links.Add(new HtmlLinkElement()
            {
                Href = "https://cdn.jsdelivr.net/npm/bootstrap@5.0.0-beta2/dist/css/bootstrap.min.css",
                Rel = "stylesheet",
                Integrity = "sha384-BmbxuPwQa2lc/FVzBcNJ7UAyJxM6wuqIj61tLrc4wSX0szH/Ev+nYRRuWlolflfl",
                Crossorigin = "anonymous"
            });

            html.Scripts.Add(new HtmlScriptElement()
            {
                Src = "https://cdn.jsdelivr.net/npm/bootstrap@5.0.0-beta2/dist/js/bootstrap.bundle.min.js",
                Integrity = "sha384-b5kHyXgcpbZJO/tY9Ul7kGkf1S0CWuKcCD38l8YkeH8z8QjE0GmW1gYU5S9FOnJ0",
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
            p1.InnerText = "This is default 404 page. You can override it by overloading Default404Page() method in HttpServerModule";
            col.AppendChild(p1);

            var p2 = html.CreateElement("p");
            col.AppendChild(p2);

            var href1 = html.CreateElement("a");
            href1.InnerText = "Open Home page...";
            href1.SetAttribute("href", "/");
            p2.AppendChild(href1);

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
                Title = "Home | Master Server Toolkit"
            };

            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));
            html.AddMeta(new KeyValuePair<string, string>("name", "description"), new KeyValuePair<string, string>("content", "Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems."));
            html.AddMeta(new KeyValuePair<string, string>("name", "author"), new KeyValuePair<string, string>("content", "Master Server Toolkit"));

            html.Links.Add(new HtmlLinkElement()
            {
                Href = "https://cdn.jsdelivr.net/npm/bootstrap@5.0.0-beta2/dist/css/bootstrap.min.css",
                Rel = "stylesheet",
                Integrity = "sha384-BmbxuPwQa2lc/FVzBcNJ7UAyJxM6wuqIj61tLrc4wSX0szH/Ev+nYRRuWlolflfl",
                Crossorigin = "anonymous"
            });

            html.Scripts.Add(new HtmlScriptElement()
            {
                Src = "https://cdn.jsdelivr.net/npm/bootstrap@5.0.0-beta2/dist/js/bootstrap.bundle.min.js",
                Integrity = "sha384-b5kHyXgcpbZJO/tY9Ul7kGkf1S0CWuKcCD38l8YkeH8z8QjE0GmW1gYU5S9FOnJ0",
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
            p1.InnerText = "This is default Index page. You can override it by overloading DefaultIndexPageHtml() method in HttpServerModule";
            col.AppendChild(p1);

            var p2 = html.CreateElement("p");
            col.AppendChild(p2);

            var href1 = html.CreateElement("a");
            href1.InnerText = "Open 404 page...";
            href1.SetAttribute("href", "somewhere");
            p2.AppendChild(href1);

            return html.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        protected bool IsDefaultPage(string page)
        {
            if (string.IsNullOrEmpty(page.Trim())) return true;
            if (defaultIndexPage == null || defaultIndexPage.Length == 0) return false;

            foreach (string pageName in defaultIndexPage)
            {
                if (CreateUrlKey(pageName.ToLower(), HttpMethod.GET) == page.ToLower())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public bool TryGetExtension(HttpListenerRequest request, out string extension)
        {
            string path = UrlToPath(request);
            return TryGetExtension(path, out extension);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public bool TryGetExtension(string path, out string extension)
        {
            extension = string.Empty;
            int indexOfExtension = path.LastIndexOf('.');

            if (indexOfExtension >= 0)
            {
                extension = path.Substring(indexOfExtension);
            }

            return !string.IsNullOrEmpty(extension);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rawUrl"></param>
        /// <returns></returns>
        public string UrlToPath(HttpListenerRequest request)
        {
            string[] parts = UrlToParts(request);
            return string.Join("/", parts);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public string[] UrlToParts(HttpListenerRequest request)
        {
            string temp = request.RawUrl;
            int indexOfQuestion = temp.IndexOf('?');
            char[] separators = new char[] { '/' };

            if (indexOfQuestion >= 0)
            {
                temp = temp.Substring(0, indexOfQuestion);
            }

            return temp.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void HttpGetRequestHandler(object sender, HttpRequestEventArgs e)
        {
            // Get request
            var request = e.Request;

            // Get responce
            var response = e.Response;

            // Let's parse user
            string parsedUrl = UrlToPath(request);

            logger.Debug($"HTTP Get Request: [{parsedUrl}]");

            // Let's ceate url key
            string urlKey = CreateUrlKey(parsedUrl, HttpMethod.GET);

            // If our url has index page
            if (IsDefaultPage(urlKey))
            {
                OnHttpRequestDelegate defaultPageHandler = null;

                if (httpRequestHandlers.ContainsKey(urlKey))
                    defaultPageHandler = httpRequestHandlers[urlKey];

                if (defaultPageHandler != null)
                {
                    defaultPageHandler.Invoke(e);
                }
                else
                {
                    byte[] contents = Encoding.UTF8.GetBytes(DefaultIndexPageHtml());

                    response.ContentType = "text/html";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = contents.LongLength;
                    response.Close(contents, true);
                }
            }
            else if (httpRequestHandlers.ContainsKey(urlKey))
            {
                httpRequestHandlers[urlKey].Invoke(e);
            }
            else if (TryGetExtension(parsedUrl, out string extension))
            {
                HandlePathWithExtension(e, extension);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="extension"></param>
        private void HandlePathWithExtension(HttpRequestEventArgs e, string extension)
        {
            // Let's parse user
            string path = UrlToPath(e.Request);

            if (e.TryReadFile(path, out byte[] contents))
            {
                var mt = mimeTypes.Find(i => i.name.ToLower() == extension.ToLower());

                if (mt != null)
                {
                    e.Response.ContentType = mt.type;
                }
                else
                {
                    e.Response.ContentType = "text/plain";
                }
            }
            else
            {
                e.Response.StatusCode = (int)HttpStatusCode.NotFound;
                contents = Encoding.UTF8.GetBytes($"File \"{path}\" not found");
                e.Response.ContentType = "text/html";
            }

            e.Response.ContentEncoding = Encoding.UTF8;
            e.Response.ContentLength64 = contents.LongLength;
            e.Response.Close(contents, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void HttpPostRequestHandler(object sender, HttpRequestEventArgs e)
        {
            // Get request
            var request = e.Request;

            // Get responce
            var response = e.Response;

            // Let's parse user
            string parsedUrl = UrlToPath(request);

            logger.Debug($"HTTP Post Request: [{parsedUrl}]");

            // Let's ceate url key
            string urlKey = CreateUrlKey(parsedUrl, HttpMethod.POST);

            if (httpRequestHandlers.ContainsKey(urlKey))
            {
                httpRequestHandlers[urlKey].Invoke(e);
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
        protected virtual void HttpPutRequestHandler(object sender, HttpRequestEventArgs e)
        {
            // Get request
            var request = e.Request;

            // Get responce
            var response = e.Response;

            // Let's parse user
            string parsedUrl = UrlToPath(request);

            logger.Debug($"HTTP Put Request: [{parsedUrl}]");

            // Let's ceate url key
            string urlKey = CreateUrlKey(parsedUrl, HttpMethod.PUT);

            if (httpRequestHandlers.ContainsKey(urlKey))
            {
                httpRequestHandlers[urlKey].Invoke(e);
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
            if (httpServer != null)
            {
                httpServer.OnGet -= HttpServer_OnGet;
                httpServer.OnPost -= HttpServer_OnPost;
                httpServer.Stop();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="httpMethod"></param>
        /// <returns></returns>
        public string CreateUrlKey(string path, HttpMethod httpMethod)
        {
            if (string.IsNullOrEmpty(path.Trim())) return string.Empty;
            else return $"{httpMethod}/{path.Trim()}".ToLower();
        }

        /// <summary>
        /// Registers http get request method
        /// </summary>
        /// <param name="path"></param>
        /// <param name="httpMethod"></param>
        /// <param name="handler"></param>
        public void RegisterHttpRequestHandler(string path, OnHttpRequestDelegate handler)
        {
            RegisterHttpRequestHandler(path, HttpMethod.GET, handler);
        }

        /// <summary>
        /// Registers http request method
        /// </summary>
        /// <param name="path"></param>
        /// <param name="handler"></param>
        public void RegisterHttpRequestHandler(string path, HttpMethod httpMethod, OnHttpRequestDelegate handler)
        {
            string url;

            switch (httpMethod)
            {
                case HttpMethod.POST:
                    url = CreateUrlKey(path, httpMethod);
                    break;
                case HttpMethod.PUT:
                    url = CreateUrlKey(path, httpMethod);
                    break;
                default:
                    url = CreateUrlKey(path, HttpMethod.GET);
                    break;
            }

            if (httpRequestHandlers.ContainsKey(url))
            {
                throw new Exception($"Handler [{url}] already exists");
            }

            httpRequestHandlers[url] = handler;
        }
    }
}