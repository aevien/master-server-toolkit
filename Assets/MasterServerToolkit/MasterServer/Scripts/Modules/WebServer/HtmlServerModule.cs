using MasterServerToolkit.Extensions;
using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class HtmlServerModule : HttpServerModule
    {
        #region INSPECTOR

        [Header("Template"), SerializeField, Tooltip("HTML template asset used to render the main page. If null, a built-in template token is used.")]
        private TextAsset templateAsset;
        [SerializeField, Tooltip("Shared JavaScript inserted into HTML controller templates. Leave None when pages do not require the shared dashboard/browser helpers.")]
        private TextAsset javascriptAsset;

        [SerializeField, Tooltip("Brand icon or favicon for the dashboard. Texture must be Read/Write enabled to encode as Base64.")]
        private Sprite icon;

        #endregion

        private string iconBase64 = string.Empty;

        public Sprite Icon => icon;
        public string IconBase64 => iconBase64;
        public TextAsset Template => templateAsset;
        public TextAsset Javascript => javascriptAsset;

        protected override void Awake()
        {
            base.Awake();

            // Try to convert the icon to Base64 (requires Read/Write on the texture import settings).
            if (icon != null)
            {
                try
                {
                    iconBase64 = icon.texture.ToBase64Url();
                }
                catch (Exception e)
                {
                    logger.Warn($"Icon encode failed: {e.Message}. Enable Read/Write on the texture import settings.");
                }
            }
        }
    } 
}
