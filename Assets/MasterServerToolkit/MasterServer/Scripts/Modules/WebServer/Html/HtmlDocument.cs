using System.Collections.Generic;
using System.Xml;

namespace MasterServerToolkit.MasterServer.Web
{
    public class HtmlDocument
    {
        private XmlDocument document;
        private XmlElement title;
        private List<XmlElement> metas;

        /// <summary>
        /// 
        /// </summary>
        public XmlElement Head { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public XmlElement Body { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public List<HtmlScriptElement> Scripts { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public List<HtmlLinkElement> Links { get; private set; }

        public HtmlDocument()
        {
            // Create document
            document = new XmlDocument();

            var html = CreateElement("html");
            html.SetAttribute("lang", "en");
            AppendChild(html);

            Head = CreateElement("head");
            html.AppendChild(Head);

            title = CreateElement("title");
            title.InnerText = "New html document";

            Body = CreateElement("body");
            html.AppendChild(Body);

            metas = new List<XmlElement>();
            Links = new List<HtmlLinkElement>();
            Scripts = new List<HtmlScriptElement>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public string Title
        {
            get
            {
                return title.InnerText;
            }
            set
            {
                title.InnerText = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="attributes"></param>
        public void AddMeta(params KeyValuePair<string, string>[] attributes)
        {
            var meta = CreateElement("meta", false);

            foreach (var kvp in attributes)
            {
                meta.SetAttribute(kvp.Key, kvp.Value);
            }

            metas.Add(meta);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public XmlElement AppendChild(XmlElement element)
        {
            document.AppendChild(element);
            return element;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public XmlElement PrependChild(XmlElement element)
        {
            document.PrependChild(element);
            return element;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public XmlElement CreateElement(IHtmlElement element)
        {
            return element.ToNode(document);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public XmlElement CreateElement(string name, bool isBlock = true)
        {
            var el = document.CreateElement(name);

            if (isBlock)
            {
                el.InnerText = string.Empty;
            }

            return el;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // Append metas
            foreach (var meta in metas)
            {
                Head.AppendChild(meta);
            }

            // Append links
            foreach (var link in Links)
            {
                Head.AppendChild(link.ToNode(document));
            }

            // Append title
            Head.AppendChild(title);

            foreach (var script in Scripts)
            {
                Body.AppendChild(script.ToNode(document));
            }

            return $"<!doctype html>{document.OuterXml}";
        }
    }
}