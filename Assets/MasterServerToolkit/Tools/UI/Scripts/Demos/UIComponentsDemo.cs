using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class UIComponentsDemo : MonoBehaviour
    {
        public UIAutofillInputField autofillInputField;

        //private void Start()
        //{
        //    StartCoroutine(SetAutofillInputField());
        //}

        //private IEnumerator SetAutofillInputField()
        //{
        //    UnityWebRequest www = UnityWebRequest.Get("https://jsonplaceholder.typicode.com/todos");
        //    yield return www.SendWebRequest();

        //    if (www.isNetworkError || www.isHttpError)
        //    {
        //        Debug.LogError(www.error);
        //    }
        //    else
        //    {
        //        var infoArray = JsonConvert.DeserializeObject<JArray>(www.downloadHandler.text);
        //        autofillInputField.SetOptions(infoArray.Select(t => new TMPro.TMP_Dropdown.OptionData(t.Value<string>("title"))));
        //    }
        //}
    }
}