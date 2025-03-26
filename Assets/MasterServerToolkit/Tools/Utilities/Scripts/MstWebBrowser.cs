using MasterServerToolkit.Json;
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using System;
using System.Runtime.InteropServices;
#endif

namespace MasterServerToolkit.Utils
{
    public class MstWebBrowser
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern IntPtr MstGetQueryString();
        [DllImport("__Internal")]
        private static extern IntPtr MstGetCurrentUrl();
#endif

        public static MstJson GetQueryStringData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            IntPtr jsonPtr = MstGetQueryString();
            string jsonString = Marshal.PtrToStringUTF8(jsonPtr);
            return new MstJson(jsonString);
#else
            var demo = MstJson.EmptyObject;
            demo.AddField("param1", "Master");
            demo.AddField("param2", "Server");
            demo.AddField("param3", "Toolkit");
            return demo;
#endif
        }

        public static MstJson GetCurrentUrlData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr jsonPtr = MstGetCurrentUrl();
            string jsonString = Marshal.PtrToStringUTF8(jsonPtr);
            return new MstJson(jsonString);
#else
            var demo = MstJson.EmptyObject;
            demo.AddField("currentUrl", "http://localhost/");
            return demo;
#endif
        }
    }
}