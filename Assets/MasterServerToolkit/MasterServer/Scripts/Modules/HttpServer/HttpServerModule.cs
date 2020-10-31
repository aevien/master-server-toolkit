using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public delegate void OnHttpRequestDelegate(HttpListenerRequest request, HttpListenerResponse response);
    public enum HttpMethod { GET, POST, PUT, DELETE }

    public class HttpServerModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Http Server Settings"), SerializeField]
        protected int httpPort = 5056;
        [SerializeField]
        protected string[] defaultIndexPage = new string[] { "index", "home" };

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
        /// List of surface controllers
        /// </summary>
        private Dictionary<Type, IHttpController> surfaceControllers;

        /// <summary>
        /// List of http request handlers
        /// </summary>
        private Dictionary<string, OnHttpRequestDelegate> httpRequestHandlers;

        public bool UserSecure { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }

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
            UserSecure = MstApplicationConfig.Instance.UseSecure;
            CertificatePath = MstApplicationConfig.Instance.CertificatePath;
            CertificatePassword = MstApplicationConfig.Instance.CertificatePassword;

            httpPort = Mst.Args.AsInt(Mst.Args.Names.WebPort, httpPort);

            // Initialize server
            httpServer = new HttpServer(httpPort, UserSecure)
            {
                AuthenticationSchemes = authenticationSchemes,
                Realm = realm,
                UserCredentialsFinder = UserCredentialsFinder
            };

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

            // Initialize controllers list
            surfaceControllers = new Dictionary<Type, IHttpController>();

            // Initialize handlers list
            httpRequestHandlers = new Dictionary<string, OnHttpRequestDelegate>();

            // Find all controllers and add them to server
            foreach (var controller in GetComponentsInChildren<IHttpController>())
            {
                if (surfaceControllers.ContainsKey(controller.GetType()))
                {
                    throw new Exception("A controller already exists in the server: " + controller.GetType());
                }

                surfaceControllers[controller.GetType()] = controller;
                controller.Initialize(this);
            }

            // Start listen to Get request
            httpServer.OnGet += HttpServer_OnGet;

            // Start listen to Post request
            httpServer.OnPost += HttpServer_OnPost;

            // Start listen to Put request
            httpServer.OnPut += HttpServer_OnPut;

            // Start http server
            httpServer.Start();

            if (httpServer.IsListening)
            {
                logger.Info($"Http server is started and listening port: {httpServer.Port}");
            }
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
            StringBuilder html = new StringBuilder();
            html.Append("<!doctype html>");
            html.Append("<html lang=\"en\">");
            html.Append("<head>");
            html.Append("<meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, shrink-to-fit=no\">");
            html.Append("<meta name=\"description\" content=\"Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems.\">");
            html.Append($"<meta name=\"author\" content=\"{Mst.Name}\">");
            html.Append("<link rel=\"stylesheet\" href=\"https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css\" integrity=\"sha384-JcKb8q3iqJ61gNV9KGb8thSsNjpSL0n8PARn9HuZOnIxN0hoP+VmmDGMN5t9UJ0Z\" crossorigin=\"anonymous\">");
            html.Append($"<title>404 | {Mst.Name} {Mst.Version}</title>");
            html.Append("</head>");
            html.Append("<body class=\"vh-100\">");
            html.Append("<div class=\"container h-100\">");
            html.Append("<div class=\"row h-100\">");
            html.Append("<div class=\"col align-self-center text-center\">");
            html.Append($"<h2>{Mst.Name} {Mst.Version}</h2>");
            html.Append("<h3 class=\"display-3\">404:Page Not Found</h3>");
            html.Append("<p>This is default 404 page. You can override it by overloading Default404Page() method in HttpServerModule</p>");
            html.Append("<p><a href=\"/\">Open Home page...</a></p>");
            html.Append("</div>");
            html.Append("</div>");
            html.Append("</div>");
            html.Append("<script src=\"https://code.jquery.com/jquery-3.5.1.slim.min.js\" integrity=\"sha384-DfXdz2htPH0lsSSs5nCTpuj/zy4C+OGpamoFVy38MVBnE+IbbVYUew+OrCXaRkfj\" crossorigin=\"anonymous\"></script>");
            html.Append("<script src=\"https://cdn.jsdelivr.net/npm/popper.js@1.16.1/dist/umd/popper.min.js\" integrity=\"sha384-9/reFTGAW83EW2RDu2S0VKaIzap3H66lZH81PoYlFhbGU+6BZp6G7niu735Sk7lN\" crossorigin=\"anonymous\"></script>");
            html.Append("<script src=\"https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/js/bootstrap.min.js\" integrity=\"sha384-B4gt1jrGC7Jh4AgTPSdUtOBvfO8shuf57BaghqFfPlYxofvL8/KUEfYiJOMMV+rV\" crossorigin=\"anonymous\"></script>");
            html.Append("</body>");
            html.Append("</html>");

            return html.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual string DefaultIndexPageHtml()
        {
            StringBuilder html = new StringBuilder();
            html.Append("<!doctype html>");
            html.Append("<html lang=\"en\">");
            html.Append("<head>");
            html.Append("<meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, shrink-to-fit=no\">");
            html.Append("<meta name=\"description\" content=\"Master Server Toolkit is designed to kickstart your back-end server development. It contains solutions to some of the common problems.\">");
            html.Append($"<meta name=\"author\" content=\"{Mst.Name}\">");
            html.Append("<link rel=\"stylesheet\" href=\"https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css\" integrity=\"sha384-JcKb8q3iqJ61gNV9KGb8thSsNjpSL0n8PARn9HuZOnIxN0hoP+VmmDGMN5t9UJ0Z\" crossorigin=\"anonymous\">");
            html.Append($"<title>Index | {Mst.Name} {Mst.Version}</title>");
            html.Append("</head>");
            html.Append("<body class=\"vh-100\">");
            html.Append("<div class=\"container h-100\">");
            html.Append("<div class=\"row h-100\">");
            html.Append("<div class=\"col align-self-center text-center\">");
            html.Append($"<h2>{Mst.Name} {Mst.Version}</h2>");
            html.Append("<h3 class=\"display-3\">Index</h3>");
            html.Append("<p>This is default Index page. You can override it by overloading DefaultIndexPageHtml() method in HttpServerModule</p>");
            html.Append("<p><a href=\"somewhere\">Open 404 page...</a></p>");
            html.Append("</div>");
            html.Append("</div>");
            html.Append("</div>");
            html.Append("<script src=\"https://code.jquery.com/jquery-3.5.1.slim.min.js\" integrity=\"sha384-DfXdz2htPH0lsSSs5nCTpuj/zy4C+OGpamoFVy38MVBnE+IbbVYUew+OrCXaRkfj\" crossorigin=\"anonymous\"></script>");
            html.Append("<script src=\"https://cdn.jsdelivr.net/npm/popper.js@1.16.1/dist/umd/popper.min.js\" integrity=\"sha384-9/reFTGAW83EW2RDu2S0VKaIzap3H66lZH81PoYlFhbGU+6BZp6G7niu735Sk7lN\" crossorigin=\"anonymous\"></script>");
            html.Append("<script src=\"https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/js/bootstrap.min.js\" integrity=\"sha384-B4gt1jrGC7Jh4AgTPSdUtOBvfO8shuf57BaghqFfPlYxofvL8/KUEfYiJOMMV+rV\" crossorigin=\"anonymous\"></script>");
            html.Append("</body>");
            html.Append("</html>");

            return html.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="urlParts"></param>
        /// <returns></returns>
        protected bool IsDefaultPage(string page)
        {
            if (string.IsNullOrEmpty(page.Trim())) return true;

            foreach (string i in defaultIndexPage)
            {
                if (CreateUrlKey(i.ToLower(), HttpMethod.GET) == page.ToLower())
                {
                    return true;
                }
            }

            return false;
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

                if (defaultIndexPage.Length > 0)
                {
                    foreach (var page in defaultIndexPage)
                    {
                        if (httpRequestHandlers.ContainsKey(urlKey))
                        {
                            defaultPageHandler = httpRequestHandlers[urlKey];
                            break;
                        }
                    }
                }

                if (defaultPageHandler != null)
                {
                    defaultPageHandler.Invoke(request, response);
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
                httpRequestHandlers[urlKey].Invoke(request, response);
            }
            else
            {
                byte[] contents = Encoding.UTF8.GetBytes(Default404Page());

                response.StatusCode = 404;
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
                httpRequestHandlers[urlKey].Invoke(request, response);
            }
            else
            {
                byte[] contents = Encoding.UTF8.GetBytes("There is no such method");

                response.StatusCode = 400;
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
                httpRequestHandlers[urlKey].Invoke(request, response);
            }
            else
            {
                byte[] contents = Encoding.UTF8.GetBytes("There is no such method");

                response.StatusCode = 400;
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