using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class UIAutofillInputFieldItem : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        private TMP_Text lableText;
        [SerializeField]
        private Button button;

        private RectTransform rect;

        public RectTransform Rect
        {
            get
            {
                if (!rect)
                {
                    rect = transform as RectTransform;
                }

                return rect;
            }
        }

        private void OnDestroy()
        {
            if (button)
                button.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="data"></param>
        public void Set(UIAutofillInputField parent, TMP_Dropdown.OptionData data)
        {
            lableText.text = data.text;
            button.onClick.AddListener(() =>
            {
                parent.Value = lableText.text;
                parent.OnItemSelected(data);
            });
        }
    }
}