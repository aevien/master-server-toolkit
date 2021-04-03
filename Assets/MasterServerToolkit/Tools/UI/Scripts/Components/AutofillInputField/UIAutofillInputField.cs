using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static TMPro.TMP_Dropdown;

namespace MasterServerToolkit.UI
{
    public class UIAutofillInputField : MonoBehaviour
    {
        [Header("Settings"), SerializeField]
        protected int maxDropdownList = 5;
        [SerializeField]
        protected string defaultPlaceholder = "Введите текст в поле...";

        [Header("Components"), SerializeField]
        protected TMP_InputField inputField;
        [SerializeField]
        protected TMP_Text placeholderText;
        [SerializeField]
        protected RectTransform dropdownContainer;
        //[SerializeField]
        //protected Button clearButton;
        [SerializeField]
        protected UIAutofillInputFieldItem autofillInputFieldItemPrefab;

        /// <summary>
        /// Listy of options
        /// </summary>
        private OptionData[] options;

        /// <summary>
        /// 
        /// </summary>
        private OptionData[] filteredOptions;

        /// <summary>
        /// Invokes when found at least one option
        /// </summary>
        [Space(10)]
        public UnityEvent<IEnumerable<OptionData>> OnOptionsFoundEvent;
        public UnityEvent<OptionData> OnOptionSelectedEvent;

        /// <summary>
        /// 
        /// </summary>
        public string Value
        {
            get
            {
                return inputField.text;
            }

            set
            {
                inputField.text = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Placeholder
        {
            get
            {
                if (placeholderText)
                    return placeholderText.text;
                else return string.Empty;
            }

            set
            {
                if (placeholderText)
                    placeholderText.text = value;
            }
        }

        private void Awake()
        {
            inputField.onValueChanged.AddListener(OnValueChanged);
            inputField.onEndEdit.AddListener(OnEndEdit);
            //clearButton.onClick.AddListener(OnClearButtonClick);
            //clearButton.gameObject.SetActive(false);
            dropdownContainer.gameObject.SetActive(false);
        }

        private void OnEndEdit(string value)
        {
            if (!string.IsNullOrEmpty(value))
                OnItemSelected(options.ToList().Find(i => i.text == value));
        }

        private void OnDestroy()
        {
            inputField.onValueChanged.RemoveAllListeners();
            inputField.onEndEdit.RemoveAllListeners();
            //clearButton.onClick.RemoveAllListeners();
        }

        private void OnValidate()
        {
            Placeholder = defaultPlaceholder;
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnClearButtonClick()
        {
            Value = string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        private void OnValueChanged(string value)
        {
            if (options == null || options.Length == 0) return;

            string trimmedValue = value.Trim();

            if (!string.IsNullOrEmpty(trimmedValue))
            {
                //clearButton.gameObject.SetActive(true);
                filteredOptions = options.Where(o => o.text.ToLower().Contains(trimmedValue.ToLower())).Take(maxDropdownList).ToArray();
            }
            else
            {
                //clearButton.gameObject.SetActive(false);
                ClearFilteredOptions();
            }

            ClearFilteredOptionItems();

            dropdownContainer.gameObject.SetActive(filteredOptions.Length > 0);

            if (filteredOptions.Length > 0)
            {
                DrawFilteredOptionItems();
                OnOptionsFoundEvent?.Invoke(filteredOptions);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void DrawFilteredOptionItems()
        {
            for (int i = 0; i < filteredOptions.Length; i++)
            {
                var optionItem = Instantiate(autofillInputFieldItemPrefab, dropdownContainer, false);
                optionItem.Set(this, filteredOptions[i]);
                dropdownContainer.sizeDelta = new Vector2(dropdownContainer.sizeDelta.x, autofillInputFieldItemPrefab.Rect.sizeDelta.y * i);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void ClearFilteredOptionItems()
        {
            foreach (Transform t in dropdownContainer)
            {
                Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="option"></param>
        public void OnItemSelected(OptionData option)
        {
            OnOptionSelectedEvent?.Invoke(option);

            ClearFilteredOptions();
            ClearFilteredOptionItems();
            dropdownContainer.gameObject.SetActive(filteredOptions.Length > 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        public void SetOptions(IEnumerable<OptionData> options)
        {
            ClearOptions();
            this.options = options.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearFilteredOptions()
        {
            filteredOptions = Array.Empty<OptionData>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearOptions()
        {
            options = Array.Empty<OptionData>();
        }
    }
}
