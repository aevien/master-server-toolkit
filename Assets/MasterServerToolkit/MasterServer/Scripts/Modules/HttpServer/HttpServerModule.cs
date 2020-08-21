using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public delegate void OnHttpRequestDelegate(HttpListenerRequest request, HttpListenerResponse response);

    public class HttpServerModule : BaseServerModule
    {
        /// <summary>
        /// Current http server
        /// </summary>
        private HttpServer httpServer;

        private string pageNotFoundHtml = "404:Page Not Found";

        /// <summary>
        /// List of surface controllers
        /// </summary>
        private Dictionary<Type, IHttpController> surfaceControllers;

        /// <summary>
        /// List of http request handlers
        /// </summary>
        private Dictionary<string, OnHttpRequestDelegate> httpRequestHandlers;

        [Header("Http Server Settings"), SerializeField]
        protected int httpPort = 8080;

        public override void Initialize(IServer server)
        {
            // Initialize server
            httpServer = new HttpServer(httpPort);

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

            // Start http server
            httpServer.Start();

            if (httpServer.IsListening)
            {
                logger.Info($"Http server is started and listening port: {httpServer.Port}");
            }
        }

        public void Stop()
        {
            if (httpServer != null)
            {
                httpServer.OnGet -= HttpServer_OnGet;
                httpServer.OnPost -= HttpServer_OnPost;
                httpServer.Stop();
            }
        }

        public void RegisterHttpRequestHandler(string path, OnHttpRequestDelegate handler)
        {
            if (httpRequestHandlers.ContainsKey(path))
            {
                throw new Exception("Handler already exists");
            }

            httpRequestHandlers[path] = handler;
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void OnApplicationQuit()
        {
            Stop();
        }

        private void HttpServerRequest(object sender, HttpRequestEventArgs e)
        {
            // Get request
            var request = e.Request;

            // Get responce
            var response = e.Response;

            // Split our url to parts
            string[] pathParts = request.RawUrl.Trim().Split('/');

            if (pathParts.Length > 1 && string.IsNullOrEmpty(pathParts[1]))
            {
                if (!httpRequestHandlers.ContainsKey("home"))
                {
                    if (httpRequestHandlers.ContainsKey("404"))
                    {
                        httpRequestHandlers["404"].Invoke(request, response);
                    }
                    else
                    {
                        byte[] contents = Encoding.UTF8.GetBytes(pageNotFoundHtml);

                        response.ContentType = "text/html";
                        response.ContentEncoding = Encoding.UTF8;
                        response.ContentLength64 = contents.LongLength;
                        response.Close(contents, true);
                    }
                }
                else
                {
                    httpRequestHandlers["home"].Invoke(request, response);
                }
            }
            else
            {
                // Crear all spaces from path
                string cleanedPath = string.Join("/", pathParts.Where(s => !string.IsNullOrEmpty(s)).ToArray());

                // Find question mark
                int indexOfQuestionMark = cleanedPath.IndexOf('?');

                // If question markis found
                if (indexOfQuestionMark >= 0)
                {
                    // Get path without question mark
                    cleanedPath = cleanedPath.Substring(0, indexOfQuestionMark);
                }

                // If path contains 
                if (cleanedPath.EndsWith("/"))
                {
                    // Remove it
                    cleanedPath = cleanedPath.Substring(0, cleanedPath.Length - 1);
                }

                if (!httpRequestHandlers.ContainsKey(cleanedPath))
                {
                    if(request.HttpMethod.ToLower() == "get")
                    {
                        if (httpRequestHandlers.ContainsKey("404"))
                        {
                            httpRequestHandlers["404"].Invoke(request, response);
                        }
                        else
                        {
                            byte[] contents = Encoding.UTF8.GetBytes(pageNotFoundHtml);

                            response.ContentType = "text/html";
                            response.ContentEncoding = Encoding.UTF8;
                            response.ContentLength64 = contents.LongLength;
                            response.Close(contents, true);
                        }
                    }
                    else
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        response.Close();
                    }
                }
                else
                {
                    httpRequestHandlers[cleanedPath].Invoke(request, response);
                }
            }
        }

        private void HttpServer_OnGet(object sender, HttpRequestEventArgs e)
        {
            HttpServerRequest(sender, e);
        }

        private void HttpServer_OnPost(object sender, HttpRequestEventArgs e)
        {
            HttpServerRequest(sender, e);
        }
    }
}