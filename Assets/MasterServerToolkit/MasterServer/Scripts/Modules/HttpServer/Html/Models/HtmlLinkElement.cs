using System.Xml;

namespace MasterServerToolkit.MasterServer.Web
{
    public class HtmlLinkElement : HtmlElement
    {
        public string Href { get; set; }
        public string Rel { get; set; }
        public string Integrity { get; set; }
        public string Crossorigin { get; set; }

        public override XmlElement ToNode(XmlDocument context)
        {
            var node = context.CreateElement("link");
            AssignAttributes(node);

            if (!string.IsNullOrEmpty(Href))
                node.SetAttribute("href", Href);

            if (!string.IsNullOrEmpty(Rel))
                node.SetAttribute("rel", Rel);

            if (!string.IsNullOrEmpty(Integrity))
                node.SetAttribute("integrity", Integrity);

            if (!string.IsNullOrEmpty(Crossorigin))
                node.SetAttribute("crossorigin", Crossorigin);

            return node;
        }
    }
}