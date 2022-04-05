using MasterServerToolkit.MasterServer.Web;
using MasterServerToolkit.Networking;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using UnityEngine;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace MasterServerToolkit.MasterServer
{
    public class MstInfoHttpController : HttpController
    {
        private Dictionary<string, string> systemInfo;
        private string publicIp = "";

        public override void Initialize(HttpServerModule server)
        {
            base.Initialize(server);

            server.RegisterHttpRequestHandler("info", OnGetMstInfoHttpRequestHandler);
            server.RegisterHttpRequestHandler("info.json", OnGetMstInfoJsonHttpRequestHandler);

            systemInfo = new Dictionary<string, string>
            {
                { "Device Id", SystemInfo.deviceUniqueIdentifier },
                { "Device Model", SystemInfo.deviceModel },
                { "Device Name", SystemInfo.deviceName },
                { "Graphics Device Name", SystemInfo.graphicsDeviceName },
                { "Graphics Device Version", SystemInfo.graphicsDeviceVersion }
            };

            Mst.Helper.GetPublicIp((ip, error) =>
            {
                publicIp = !string.IsNullOrEmpty(ip) ? ip.Trim() : MasterServer.Address;
            });
        }

        private void OnGetMstInfoJsonHttpRequestHandler(HttpRequestEventArgs eventArgs)
        {
            var response = eventArgs.Response;

            JObject json = new JObject();
            JArray modulesJson = new JArray();

            foreach (var module in MasterServer.GetInitializedModules())
            {
                modulesJson.Add(module.JsonInfo());
            }

            json.Add("modules", modulesJson);

            byte[] contents = Encoding.UTF8.GetBytes(json.ToString());

            response.SetHeader("Access-Control-Allow-Origin", "*");
            response.SetHeader("Access-Control-Allow-Methods", "*");
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }

        private void OnGetMstInfoHttpRequestHandler(HttpRequestEventArgs eventArgs)
        {
            if (!HttpServer.TryGetQueryValue(eventArgs.Request, "ignoreLog", out string value))
                logger.Debug($"Лог если нет игнора");

            var response = eventArgs.Response;

            HtmlDocument html = new HtmlDocument
            {
                Title = "MST Info"
            };
            html.AddMeta(new KeyValuePair<string, string>("charset", "utf-8"));
            html.AddMeta(new KeyValuePair<string, string>("name", "viewport"), new KeyValuePair<string, string>("content", "width=device-width, initial-scale=1"));

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

            html.Body.AddClass("body");

            var container = html.CreateElement("div");
            container.AddClass("container-fluid pt-5");
            html.Body.AppendChild(container);

            var h1 = html.CreateElement("h1");
            h1.InnerText = $"{Mst.Name} {Mst.Version}";
            container.AppendChild(h1);

            var row = html.CreateElement("div");
            row.AddClass("row");
            container.AppendChild(row);

            var col1 = html.CreateElement("div");
            col1.AddClass("col-md-4");
            row.AppendChild(col1);

            var col2 = html.CreateElement("div");
            col2.AddClass("col-md-8");
            row.AppendChild(col2);

            GenerateMachineInfo(html, col1);
            GenerateServerInfo(html, col1);
            GenerateListOfModules(html, col2);

            byte[] contents = Encoding.UTF8.GetBytes(html.ToString());

            response.ContentType = "text/html";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }

        private void GenerateServerInfo(HtmlDocument html, XmlElement parent)
        {
            var h2 = html.CreateElement("h2");
            h2.InnerText = "Server Info";
            parent.AppendChild(h2);

            var table = html.CreateElement("table");
            table.AddClass("table table-bordered table-striped table-hover table-sm mb-5");
            parent.AppendChild(table);

            var thead = html.CreateElement("thead");
            table.AppendChild(thead);

            var tbody = html.CreateElement("tbody");
            table.AppendChild(tbody);

            var thr = html.CreateElement("tr");
            thead.AppendChild(thr);

            var th1 = html.CreateElement("th");
            th1.InnerText = "#";
            th1.SetAttribute("scope", "col");
            th1.SetAttribute("width", "200");
            thr.AppendChild(th1);

            var thr2 = html.CreateElement("th");
            thr2.InnerText = "Value";
            thr.AppendChild(thr2);

            var properties = MasterServer.Info();
            properties.Set("Public Ip", publicIp);

            foreach (var property in properties.ToDictionary())
            {
                var tr = html.CreateElement("tr");
                tbody.AppendChild(tr);

                var th = html.CreateElement("th");
                th.InnerText = property.Key;
                th.SetAttribute("scope", "row");
                tr.AppendChild(th);

                var th2 = html.CreateElement("td");
                th2.InnerText = property.Value;
                tr.AppendChild(th2);
            }
        }

        private void GenerateMachineInfo(HtmlDocument html, XmlElement parent)
        {
            var h2 = html.CreateElement("h2");
            h2.InnerText = "System Info";
            parent.AppendChild(h2);

            var table = html.CreateElement("table");
            table.AddClass("table table-bordered table-striped table-hover table-sm mb-5");
            parent.AppendChild(table);

            var thead = html.CreateElement("thead");
            table.AppendChild(thead);

            var tbody = html.CreateElement("tbody");
            table.AppendChild(tbody);

            var thr = html.CreateElement("tr");
            thead.AppendChild(thr);

            var th1 = html.CreateElement("th");
            th1.InnerText = "#";
            th1.SetAttribute("scope", "col");
            th1.SetAttribute("width", "200");
            thr.AppendChild(th1);

            var thr2 = html.CreateElement("th");
            thr2.InnerText = "Name";
            thr.AppendChild(thr2);

            foreach (var property in systemInfo)
            {
                var tr = html.CreateElement("tr");
                tbody.AppendChild(tr);

                var th = html.CreateElement("th");
                th.InnerText = property.Key;
                th.SetAttribute("scope", "row");
                tr.AppendChild(th);

                var th2 = html.CreateElement("td");
                th2.InnerText = property.Value;
                tr.AppendChild(th2);
            }
        }

        private void GenerateListOfModules(HtmlDocument html, XmlElement parent)
        {
            var h2 = html.CreateElement("h2");
            h2.InnerText = "Modules";
            parent.AppendChild(h2);

            var table = html.CreateElement("table");
            table.AddClass("table table-bordered table-striped table-hover table-sm");
            parent.AppendChild(table);

            var thead = html.CreateElement("thead");
            table.AppendChild(thead);

            var tbody = html.CreateElement("tbody");
            table.AppendChild(tbody);

            var thr = html.CreateElement("tr");
            thead.AppendChild(thr);

            var th1 = html.CreateElement("th");
            th1.InnerText = "#";
            th1.SetAttribute("scope", "col");
            th1.SetAttribute("width", "50");
            thr.AppendChild(th1);

            var th2 = html.CreateElement("th");
            th2.InnerText = "Name";
            thr.AppendChild(th2);

            var th3 = html.CreateElement("th");
            th3.InnerText = "Info";
            thr.AppendChild(th3);

            int index = 1;

            foreach (var module in MasterServer.GetInitializedModules())
            {
                // Row
                var tr = html.CreateElement("tr");
                tbody.AppendChild(tr);

                // First cell
                var th = html.CreateElement("th");
                th.InnerText = index.ToString();
                th.SetAttribute("scope", "row");
                tr.AppendChild(th);

                // Second cell
                var td = html.CreateElement("td");
                td.InnerText = module.GetType().Name;
                tr.AppendChild(td);

                // Third cell
                var td1 = html.CreateElement("td");
                tr.AppendChild(td1);

                // Subtable
                var table2 = html.CreateElement("table");
                table2.AddClass("table table-bordered table-striped table-hover table-sm");

                td1.AppendChild(table2);

                // Subtable body
                var tbody2 = html.CreateElement("tbody");
                table2.AppendChild(tbody2);

                var infos = module.Info().ToDictionary();

                foreach (string key in infos.Keys)
                {
                    // Subtable row
                    tr = html.CreateElement("tr");
                    tbody2.AppendChild(tr);

                    // First cell
                    th = html.CreateElement("th");
                    th.InnerText = key;
                    th.SetAttribute("scope", "row");
                    th.SetAttribute("width", "30%");
                    tr.AppendChild(th);

                    td = html.CreateElement("td");
                    td.InnerText = infos[key];
                    tr.AppendChild(td);

                    //var li = html.CreateElement("li");
                    //li.AddClass("list-group-item");
                    //li.InnerXml = info;
                    //ul.AppendChild(li);
                }

                index++;
            }
        }
    }
}