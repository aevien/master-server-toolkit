#if UNITY_EDITOR
using MasterServerToolkit.Logging;
using UnityEditor;
#endif
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
        using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.MasterServer
{
    public class MstRuntime
    {
        /// <summary>
        /// Check if we are in editor
        /// </summary>
        public bool IsEditor { get; private set; }

        /// <summary>
        /// Product key
        /// </summary>
        public string ProductKey(string key = "")
        {
            string pk = $"{Application.companyName}_{Application.productName}";

            if (!string.IsNullOrEmpty(key))
                pk = $"{pk}_{key}";

            return pk.Replace(" ", string.Empty).ToLower();
        }

        /// <summary>
        /// Check if multithreading is supported
        /// </summary>
        public bool SupportsThreads { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        private readonly string webGlQuitMessage = "You are in web browser window. The Quit command is not supported!";

        [DllImport("__Internal")]
        private static extern void MsfAlert(string msg);
#endif

        public void Quit()
        {
#if UNITY_EDITOR && !UNITY_WEBGL
            EditorApplication.isPlaying = false;
#elif !UNITY_EDITOR && !UNITY_WEBGL
            Application.Quit();
#elif !UNITY_EDITOR && UNITY_WEBGL
            MsfAlert(webGlQuitMessage);
            Logs.Info(webGlQuitMessage);
#endif
        }

        public MstRuntime()
        {
#if !UNITY_EDITOR
            IsEditor = false;
#else
            IsEditor = true;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            SupportsThreads = false;
#else
            SupportsThreads = true;
#endif
        }
    }
}