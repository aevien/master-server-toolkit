using System;
using System.Runtime.InteropServices;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Extensions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MasterServerToolkit.MasterServer
{
    public class MstRuntime
    {
#if UNITY_STANDALONE_WIN
        /// <summary>
        /// WinAPI import for changing Windows console title.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetConsoleTitle(string lpConsoleTitle);
#endif
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
#elif UNITY_STANDALONE || UNITY_SERVER
            UnityEngine.Application.Quit();
#elif !UNITY_EDITOR && UNITY_WEBGL
            MstAlert(webGlQuitMessage);
            Logs.Info(webGlQuitMessage);
#endif
        }

        public void SetTitle(string title)
        {
#if UNITY_STANDALONE_WIN
            title = title.Unescape();
            SetConsoleTitle(title);
#elif UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
            title = title.Unescape();
            Console.Write($"\u001b]0;{title}\u0007");
#endif
        }
    }
}
