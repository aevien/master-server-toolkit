using System.Linq;
using System.Net;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    public class DashboardModule : HtmlServerModule
    {
        /// <summary>
        /// Nav bar html
        /// </summary>
        public string Navbar { get; private set; }

        protected override void Awake()
        {
            // Creates navbar hrml
            CreateNavbar();
            base.Awake();
        }

        public override void Initialize(IServer server)
        {
            base.Initialize(server);
        }

        protected virtual void CreateNavbar()
        {
            StringBuilder navHtml = new();
            var controllers = GetComponentsInChildren<DashboardPageWebController>().Where(c => c.ShowInNavbar);

            if (controllers.Count() > 0)
            {
                foreach (var htmlController in controllers)
                {
                    string route = (htmlController.Route ?? string.Empty).Trim('/');
                    string routeHtml = WebUtility.HtmlEncode(route);
                    string titleHtml = WebUtility.HtmlEncode(htmlController.Title ?? string.Empty);
                    string iconClassHtml = WebUtility.HtmlEncode(htmlController.IconClass ?? string.Empty);
                    string hrefHtml = WebUtility.HtmlEncode($"/{route}");

                    navHtml.AppendLine($"<a class=\"list-group-item list-group-item-action\" data-dashboard-route=\"{routeHtml}\" href=\"{hrefHtml}\">");

                    if (!string.IsNullOrEmpty(htmlController.IconClass))
                    {
                        navHtml.AppendLine($"<i class=\"{iconClassHtml}\"></i> {titleHtml}");
                    }
                    else
                    {
                        navHtml.AppendLine($"{titleHtml}");
                    }

                    navHtml.AppendLine("</a>");
                }
            }

            Navbar = navHtml.ToString();
        }
    }
}
