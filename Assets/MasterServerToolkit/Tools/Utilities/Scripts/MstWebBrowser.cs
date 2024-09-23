using MasterServerToolkit.Json;
using System;
using System.Runtime.InteropServices;

namespace MasterServerToolkit.Utils
{
    public class MstWebBrowser
    {
        [DllImport("__Internal")]
        private static extern IntPtr GetQueryStringFromBrowser();

        public static MstJson GetQueryStringData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr jsonPtr = GetQueryStringFromBrowser();
            string jsonString = Marshal.PtrToStringUTF8(jsonPtr);
            return new MstJson(jsonString);
#else
            var demo = MstJson.EmptyObject;
            demo.AddField("john_doe_message", "Hello, World!");
            demo.AddField("world_message", "Hello, John Doe!");
            return demo;
#endif
        }
    }
}