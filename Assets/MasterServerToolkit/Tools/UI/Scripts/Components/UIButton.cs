using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(Button))]
    public class UIButton : MonoBehaviour
    {
        private Button button;

        [Header("Components"), SerializeField]
        private Image iconImage;
        [SerializeField]
        private TextMeshProUGUI lableText;
        [SerializeField]
        private Sprite iconValue;
        [SerializeField]
        private string lableValue = "Click me";

        /// <summary>
        /// 
        /// </summary>
        public bool IsInteractable => button.interactable;

        /// <summary>
        /// 
        /// </summary>
        public Button UnityButton
        {
            get
            {
                if (!button)
                    button = GetComponent<Button>();

                return button;
            }
        }

        private void OnValidate()
        {
            SetLable(lableValue);
            SetIcon(iconValue);
        }

        public void SetLable(string lableValue)
        {
            if (lableText)
                lableText.text = lableValue;
        }

        public void SetIcon(Sprite icon)
        {
            if (iconImage)
            {
                iconImage.sprite = null;
                iconImage.sprite = icon;
            }
        }

        public void SetInteractable(bool value)
        {
            UnityButton.interactable = value;
        }

        public void AddOnClickListener(UnityAction callback, bool removeIfExists = true)
        {
            if (removeIfExists) RemoveOnClickListener(callback);
            UnityButton.onClick.AddListener(callback);
        }

        public void RemoveOnClickListener(UnityAction callback)
        {
            UnityButton.onClick.RemoveListener(callback);
        }

        public void RemoveAllOnClickListeners()
        {
            UnityButton.onClick.RemoveAllListeners();
        }
    }
}