using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer.Web;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MstInfoHttpController : HttpController
    {
        private MstProperties systemInfo;
        private string publicIp = "";

        public override void Initialize(HttpServerModule server)
        {
            base.Initialize(server);

            server.RegisterHttpGetRequestHandler("info-json", OnGetMstInfoJsonHttpRequestHandler, UseCredentials);
            server.RegisterHttpGetRequestHandler("info", OnGetMstInfoHttpRequestHandler, UseCredentials);

            systemInfo = new MstProperties();
            systemInfo.Add("Device Id", SystemInfo.deviceUniqueIdentifier);
            systemInfo.Add("Device Model", SystemInfo.deviceModel);
            systemInfo.Add("Device Name", SystemInfo.deviceName);
            systemInfo.Add("Device Type", SystemInfo.deviceType);
            systemInfo.Add("Graphics Device Id", SystemInfo.graphicsDeviceID);
            systemInfo.Add("Graphics Device Name", SystemInfo.graphicsDeviceName);
            systemInfo.Add("Graphics Device Version", SystemInfo.graphicsDeviceVersion);
            systemInfo.Add("Graphics Device Type", SystemInfo.graphicsDeviceType);
            systemInfo.Add("Graphics Device Vendor Id", SystemInfo.graphicsDeviceVendorID);
            systemInfo.Add("Graphics Device Vendor", SystemInfo.graphicsDeviceVendor);
            systemInfo.Add("Graphics Device Memory", SystemInfo.graphicsMemorySize);
            systemInfo.Add("OS", SystemInfo.operatingSystem);
            systemInfo.Add("OS Family", SystemInfo.operatingSystemFamily);
            systemInfo.Add("CPU Type", SystemInfo.processorType);
            systemInfo.Add("CPU Frequency", SystemInfo.processorFrequency);
            systemInfo.Add("CPU Count", SystemInfo.processorCount);
            systemInfo.Add("RAM", SystemInfo.systemMemorySize);

            publicIp = Mst.Helper.GetPublicIp();
        }

        private void OnGetMstInfoJsonHttpRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
            MstJson json = MstJson.EmptyObject;
            MstJson modulesJson = MstJson.EmptyArray;

            foreach (var module in MasterServer.GetInitializedModules())
            {
                modulesJson.Add(module.JsonInfo());
            }

            json.AddField("modules", modulesJson);

            byte[] contents = Encoding.UTF8.GetBytes(json.ToString());

            response.Headers.Set("Access-Control-Allow-Origin", "*");
            response.Headers.Set("Access-Control-Allow-Methods", "*");
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = contents.LongLength;
            response.Close(contents, true);
        }

        private void OnGetMstInfoHttpRequestHandler(HttpListenerRequest request, HttpListenerResponse response)
        {
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

            try
            {
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
            catch (Exception e)
            {
                var thr = html.CreateElement("tr");
                thead.AppendChild(thr);
                var th = html.CreateElement("td");
                th.SetAttribute("colspan", "2");
                th.AddClass("text-break");
                th.InnerText = e.ToString();
                thr.AppendChild(th);
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

                try
                {
                    // Subtable
                    var table2 = html.CreateElement("table");
                    table2.AddClass("table table-bordered table-striped table-hover table-sm");

                    td1.AppendChild(table2);

                    // Subtable body
                    var tbody2 = html.CreateElement("tbody");
                    table2.AppendChild(tbody2);

                    foreach (var kvp in module.Info())
                    {
                        // Subtable row
                        tr = html.CreateElement("tr");
                        tbody2.AppendChild(tr);

                        // First cell
                        th = html.CreateElement("th");
                        th.InnerText = kvp.Key;
                        th.SetAttribute("scope", "row");
                        th.SetAttribute("width", "30%");
                        tr.AppendChild(th);

                        td = html.CreateElement("td");
                        td.InnerText = kvp.Value;
                        tr.AppendChild(td);
                    }
                }
                catch (Exception e)
                {
                    tr.AddClass("table-danger");
                    td1.InnerText = e.ToString();
                    td1.AddClass("text-break");
                }

                index++;
            }
        }
    }
}