using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class UIMultiLable : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TextMeshProUGUI[] lablesText;

        public void Text(params string[] values)
        {
            if (values.Length >= lablesText.Length)
            {
                for (int i = 0; i < lablesText.Length; i++)
                {
                    lablesText[i].text = values[i];
                }
            }
            else if (values.Length <= lablesText.Length)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    lablesText[i].text = values[i];
                }
            }
        }
    }
}