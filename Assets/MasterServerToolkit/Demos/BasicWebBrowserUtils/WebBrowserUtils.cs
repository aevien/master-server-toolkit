using MasterServerToolkit.Utils;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace MasterServerToolkit.Demos.WebBrowserUtils
{
    public class WebBrowserUtils : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI output;

        public void OnClickGetQueryStringData()
        {
            var json = MstWebBrowser.GetQueryStringData();
            output.text = json.Print(true);

            if (json.HasField("pw3_auth"))
            {
                string url = $"https://playweb3.io/api/v1/users/get_wallet_by_key?auth_key={json["pw3_auth"].StringValue}";
                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "X-API-Key", "I8r6QVCEDTDUTvecOF5421YRhGG7ThvrG2G64+Mh1IHhtDFDYxMLVE/z5dpHeHIwXwzM8C6gCbun2fua1hsrAkFTw4jlBOAiJy7kocQMW5dM1vTzG8j0mL3Iki8WlsqE" }
                };

                StartCoroutine(SendRequest(url, headers, (handler) =>
                {
                    output.text = handler.text;
                }));
            }
        }

        private IEnumerator SendRequest(string url, Dictionary<string, string> headers, UnityAction<DownloadHandler> callback)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> kvp in headers)
                {
                    request.SetRequestHeader(kvp.Key, kvp.Value);
                }
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error: {request.error}");
            }
            else
            {
                callback?.Invoke(request.downloadHandler);
            }
        }
    }
}