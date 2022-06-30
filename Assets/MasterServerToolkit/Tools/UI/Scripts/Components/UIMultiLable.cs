using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class UIMultiLable : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TMP_Text[] lablesText;

        public void SetLable(params string[] lables)
        {
            if (lables.Length >= lablesText.Length)
            {
                for (int i = 0; i < lablesText.Length; i++)
                {
                    lablesText[i].text = lables[i];
                }
            }
            else if (lables.Length <= lablesText.Length)
            {
                for (int i = 0; i < lables.Length; i++)
                {
                    lablesText[i].text = lables[i];
                }
            }
        }
    }
}