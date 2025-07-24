using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class TemplateWebController : WebController
    {
        [SerializeField]
        private TextAsset templateAsset;
        [SerializeField]
        private string panelTitle = "Panel";
        [SerializeField]
        private string aboutTitle = "About";
        [SerializeField, TextArea(10,20)]
        private string aboutText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

        private string template = string.Empty;

        protected virtual void Awake()
        {
            if (templateAsset != null)
            {
                template = templateAsset.text;
                template = template.Replace("#PANEL-TITLE#", PanelTitle());
                template = template.Replace("#ABOUT_TITLE#", AboutTitle());
                template = template.Replace("#ABOUT#", AboutText());
            }
        }

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

        protected virtual string Combine(string text)
        {
            string result = template;

            if (!string.IsNullOrEmpty(template))
            {
                result = result.Replace("#INNER_HTML#", text);
                result = result.Replace("#MST-TITLE#", $"{Mst.Name} v.{Mst.Version}");
            }
            else
            {
                result = text;
            }

            return result;
        }
    }
}