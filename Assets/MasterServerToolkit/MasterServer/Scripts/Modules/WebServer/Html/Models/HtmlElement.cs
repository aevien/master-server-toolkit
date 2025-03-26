using System.Collections.Generic;
using System.Xml;

namespace MasterServerToolkit.MasterServer.Web
{
    public abstract class HtmlElement : IHtmlElement
    {
        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        protected void AssignAttributes(XmlElement node)
        {
            foreach (var kvp in Attributes)
            {
                node.SetAttribute(kvp.Key, kvp.Value);
            }
        }

        public abstract XmlElement ToNode(XmlDocument context);
    }
}
