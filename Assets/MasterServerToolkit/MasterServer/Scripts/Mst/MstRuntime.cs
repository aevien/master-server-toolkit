#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        using System.Runtime.InteropServices;
        using MasterServerToolkit.Logging;
#endif

namespace MasterServerToolkit.MasterServer
{
    public class MstRuntime
    {
        /// <summary>
        /// Check if we are in editor
        /// </summary>
#if UNITY_EDITOR
        public bool IsEditor => true;
#else
        public bool IsEditor => false;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        private readonly string webGlQuitMessage = "You are in web browser window. The Quit command is not supported!";

        [DllImport("__Internal")]
        private static extern void MstAlert(string msg);
#endif

        public void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#elif !UNITY_EDITOR && !UNITY_WEBGL
            UnityEngine.Application.Quit();
#elif !UNITY_EDITOR && UNITY_WEBGL
            MstAlert(webGlQuitMessage);
            Logs.Info(webGlQuitMessage);
#endif
        }

        public MstRuntime() { }
    }
}