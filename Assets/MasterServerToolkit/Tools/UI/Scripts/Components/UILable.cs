using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class UILable : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected TextMeshProUGUI lableText;

        [Header("Settings"), SerializeField]
        private string lable = "";

        #endregion

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(lable))
            {
                Text = name;
            }
            else
            {
                Text = lable;
            }
        }

        public string Text
        {
            get
            {
                return lable;
            }
            set
            {
                lable = value;

                if (lableText)
                    lableText.text = lable;
            }
        }
    }
}