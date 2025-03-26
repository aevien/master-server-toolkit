using System.Xml;

namespace MasterServerToolkit.MasterServer.Web
{
    public class HtmlScriptElement : HtmlElement
    {
        public string Src { get; set; }
        public string Integrity { get; set; }
        public string Crossorigin { get; set; }
        public string Script { get; set; }

        public override XmlElement ToNode(XmlDocument context)
        {
            var node = context.CreateElement("script");
            AssignAttributes(node);

            if (!string.IsNullOrEmpty(Src))
                node.SetAttribute("src", Src);

            if (!string.IsNullOrEmpty(Integrity))
                node.SetAttribute("integrity", Integrity);

            if (!string.IsNullOrEmpty(Crossorigin))
                node.SetAttribute("crossorigin", Crossorigin);

            if (!string.IsNullOrEmpty(Script))
                node.InnerText = Script;

            return node;
        }
    }
}