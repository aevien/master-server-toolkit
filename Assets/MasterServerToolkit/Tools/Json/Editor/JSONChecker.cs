#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;

#if UNITY_2017_1_OR_NEWER
using UnityEngine.Networking;
#endif

#if JSONOBJECT_PERFORMANCE_TEST && UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace MasterServerToolkit.Json.Editor
{
    public class JSONChecker : EditorWindow
    {
        string testJsonString = MstJsonTestStrings.PrettyJsonString;
        string url = "";
        MstJson jsonObject;

        [MenuItem(MstConstants.WindowMenu + "Json validator")]
        static void Init()
        {
            GetWindow<JSONChecker>("Json Validator").Show();
        }

        void OnGUI()
        {
            testJsonString = EditorGUILayout.TextArea(testJsonString);
            GUI.enabled = !string.IsNullOrEmpty(testJsonString);
            if (GUILayout.Button("Validate"))
            {
#if JSONOBJECT_PERFORMANCE_TEST
				Profiler.BeginSample("JSONParse");
				jsonObject = MstJson.Create(testJsonString);
				Profiler.EndSample();
				Profiler.BeginSample("JSONStringify");
				jsonObject.ToString(true);
				Profiler.EndSample();
#else
                jsonObject = new MstJson(testJsonString);
#endif

                Debug.Log(jsonObject.ToString(true));
            }

            EditorGUILayout.Separator();

            url = EditorGUILayout.TextField("URL", url);

            if (GUILayout.Button("Load and validate"))
            {
                Debug.Log(url);
                var json = NetWebRequests.Get(url);
                Debug.Log(json);
                jsonObject = json;
                Debug.Log(jsonObject.ToString(true));
            }

            if (jsonObject)
            {
                GUILayout.Label(jsonObject.Type == MstJson.ValueType.Null
                    ? string.Format("JSON fail:\n{0}", jsonObject.ToString(true))
                    : string.Format("JSON success:\n{0}", jsonObject.ToString(true)));
            }
        }
    }
}
#endif