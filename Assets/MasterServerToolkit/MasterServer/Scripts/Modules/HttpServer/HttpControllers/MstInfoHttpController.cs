using MasterServerToolkit.MasterServer.Web;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class MasterServerInfo
    {
        public string Name;
        public int ModulesQty;
    }

    public class MstInfoHttpController : HttpController
    {
        public override void Initialize(HttpServerModule server)
        {
            base.Initialize(server);
            server.RegisterHttpRequestHandler("info", OnGetMstInfoJsonHttpRequestHandler);
        }

        private void OnGetMstInfoJsonHttpRequestHandler(HttpRequestEventArgs eventArgs)
        {
            var response = eventArgs.Response;

            HtmlDocument html = new HtmlDocument();
            html.Title("MST Info");
            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));

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

            html.Body.AddClass("body");

            var container = html.CreateElement("div");
            container.AddClass("container");
            html.Body.AppendChild(container);

            var row = html.CreateElement("div");
            row.AddClass("row");
            container.AppendChild(row);

            var col = html.CreateElement("div");
            col.AddClass("col");
            row.AppendChild(col);

            var h1 = html.CreateElement("h1");
            h1.InnerText = "Hello World!";
            col.AppendChild(h1);

            var table = html.CreateElement("table");
            table.AddClass("table table-bordered");
            col.AppendChild(table);

            var thead = html.CreateElement("thead");
            table.AppendChild(thead);

            var tbody = html.CreateElement("tbody");
            table.AppendChild(tbody);

            var thr = html.CreateElement("tr");
            thead.AppendChild(thr);

            var thr1 = html.CreateElement("th");
            thr1.InnerText = "#";
            thr1.SetAttribute("scope", "col");
            thr.AppendChild(thr1);

            var thr2 = html.CreateElement("th");
            thr2.InnerText = "Name";
            thr.AppendChild(thr2);

            int index = 1;

            foreach (var module in MasterServer.GetInitializedModules())
            {
                var tr = html.CreateElement("tr");
                tbody.AppendChild(tr);

                var th1 = html.CreateElement("th");
                th1.InnerText = index.ToString();
                th1.SetAttribute("scope", "row");
                tr.AppendChild(th1);

                var th2 = html.CreateElement("td");
                th2.InnerText = module.GetType().Name;
                tr.AppendChild(th2);

                index++;
            }

            byte[] contents = Encoding.UTF8.GetBytes(html.ToString());

            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }
    }
}