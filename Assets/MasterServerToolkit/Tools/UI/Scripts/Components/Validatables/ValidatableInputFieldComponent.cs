using MasterServerToolkit.Logging;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class ValidatableInputFieldComponent : ValidatableBaseComponent
    {
        [Header("Text Field Components"), SerializeField]
        [Tooltip("Input field validated by this component. Leave empty to use a TMP_InputField attached to the same GameObject.")]
        private TMP_InputField currentInputField;
        [SerializeField]
        [Tooltip("Optional input field whose text must exactly match the current field, for example a password confirmation field.")]
        private TMP_InputField compareToInputField;
        [SerializeField, TextArea(2, 10)]
        [Tooltip("Error written through MST logging when the comparison fields do not match. Leave empty to use the validator's default message.")]
        protected string compareErrorMessage;

        [Header("Text Field RegExp Validation"), SerializeField, TextArea(2, 10)]
        [Tooltip("Optional .NET regular expression applied to the current text by Regex.IsMatch. Leave empty to disable pattern validation.")]
        protected string regExpPattern;
        [SerializeField, TextArea(2, 10)]
        [Tooltip("Error written through MST logging when the regular expression does not match. Leave empty to use the validator's default message.")]
        protected string regExpErrorMessage;

        protected override void Awake()
        {
            base.Awake();

            if (!currentInputField)
                currentInputField = GetComponent<TMP_InputField>();
        }

        public override bool IsValid()
        {
            if (!currentInputField.interactable)
            {
                return true;
            }

            if (isRequired && string.IsNullOrEmpty(currentInputField.text.Trim()))
            {
                Logs.Error(string.IsNullOrEmpty(requiredErrorMessage) ? $"Field {name} is required" : requiredErrorMessage);

                SetInvalidColor();
                return false;
            }

            regExpPattern = regExpPattern.Trim();

            if (!string.IsNullOrEmpty(regExpPattern) && !Regex.IsMatch(currentInputField.text, regExpPattern))
            {
                Logs.Error(string.IsNullOrEmpty(regExpErrorMessage) ? $"Field {name} is not match to RegExp pattern" : regExpErrorMessage);

                SetInvalidColor();
                return false;
            }

            if (compareToInputField && currentInputField.text != compareToInputField.text)
            {
                Logs.Error(string.IsNullOrEmpty(compareErrorMessage) ? $"Field {name} is not equals to {compareToInputField.name}" : compareErrorMessage);

                SetInvalidColor();
                return false;
            }

            return true;
        }
    }
}
