using System.Net;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class DashboardPageWebController : HtmlWebController
    {
        [SerializeField, Tooltip("Page heading inserted into the dashboard panel-title token and breadcrumb.")]
        private string panelTitle = "Panel";
        [SerializeField, Tooltip("Heading inserted into the dashboard about section.")]
        private string aboutTitle = "About";
        [SerializeField, TextArea(10, 20), Tooltip("HTML/text inserted into the dashboard about section. Treat authored content as trusted because it is not automatically HTML-escaped.")]
        private string aboutText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
        [SerializeField, Tooltip("CSS icon class used by the dashboard navigation item, for example a Font Awesome class supported by the template.")]
        private string iconClass;
        [SerializeField, Tooltip("Adds this controller to the generated dashboard navigation bar. The route remains registered when disabled.")]
        protected bool showInNavbar = true;

        public string IconClass => iconClass;
        public bool ShowInNavbar => showInNavbar;

        protected virtual string PanelTitle()
        {
            return panelTitle;
        }

        protected virtual string AboutTitle()
        {
            return aboutTitle;
        }

        protected virtual string AboutText()
        {
            return aboutText;
        }

        protected virtual string BreadcrumbHtml()
        {
            string currentTitle = WebUtility.HtmlEncode(PanelTitle());
            string currentRoute = (Route ?? string.Empty).Trim('/');

            if (string.Equals(currentRoute, "dashboard", System.StringComparison.OrdinalIgnoreCase))
                return $"<li class=\"breadcrumb-item active\" aria-current=\"page\">{currentTitle}</li>";

            return "<li class=\"breadcrumb-item\"><a href=\"/dashboard\" class=\"text-decoration-none\">Dashboard</a></li>"
                   + $"<li class=\"breadcrumb-item active\" aria-current=\"page\">{currentTitle}</li>";
        }

        protected override string Combine(string text)
        {
            var result = base.Combine(text);
            result = result.Replace(WebServerTokens.PANEL_TITLE, PanelTitle());
            result = result.Replace(WebServerTokens.ABOUT_TITLE, AboutTitle());
            result = result.Replace(WebServerTokens.ABOUT_HTML, AboutText());
            result = result.Replace(WebServerTokens.BREADCRUMB_HTML, BreadcrumbHtml());

            if (WebServer is DashboardModule module)
                result = result.Replace(WebServerTokens.NAV_HTML, module.Navbar);

            return result;
        }
    }
}
