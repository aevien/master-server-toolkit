#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
using MasterServerToolkit.MasterServer;
using System.Runtime.InteropServices;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MasterServerToolkit.WebGL
{
    [RequireComponent(typeof(TMP_InputField))]
    public class WebGlTextMeshProInput : MonoBehaviour, IPointerClickHandler
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern void MstPrompt(string name, string title, string defaultValue);
#endif

        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private string title = "Input Field";

        #endregion

        public void OnPointerClick(PointerEventData eventData)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            var input = GetComponent<TMP_InputField>();
            MstPrompt(name, Mst.Localization[title], input.text);
#endif
        }

        public void OnPromptOk(string message)
        {
            GetComponent<TMP_InputField>().text = message;
        }

        public void OnPromptCancel()
        {
            GetComponent<TMP_InputField>().text = "";
        }
    }
}
