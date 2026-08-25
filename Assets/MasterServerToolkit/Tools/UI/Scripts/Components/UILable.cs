using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class UILable : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("TextMeshPro component that displays the label. Assign the text component controlled by this widget.")]
        protected TextMeshProUGUI lableText;

        [Header("Settings"), SerializeField]
        [Tooltip("Text displayed by the assigned label component. Leave empty to use this GameObject's name while editing.")]
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
