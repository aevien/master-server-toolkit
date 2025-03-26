using System.Xml;

namespace MasterServerToolkit.MasterServer.Web
{
    public interface IHtmlElement
    {
        /// <summary>
        /// Converts <see cref="IHtmlElement"/> to <see cref="XmlElement"/>
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        XmlElement ToNode(XmlDocument context);
    }
}