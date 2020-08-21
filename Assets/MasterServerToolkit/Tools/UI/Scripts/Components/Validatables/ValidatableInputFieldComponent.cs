using MasterServerToolkit.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aevien.UI
{
    public class ValidatableInputFieldComponent : MonoBehaviour, IValidatableComponent
    {
        private Color[] validationColorAtStart;

        [Header("Base Settings"), SerializeField]
        protected Color invalidColor = Color.red;
        [SerializeField]
        protected bool changeValidationColor = true;

        [Header("Graphics"), SerializeField]
        protected Graphic[] validationTargetGraphic;

        [Header("Components"), SerializeField]
        private TMP_InputField currentInputField;

        [Header("Required Validation"), SerializeField]
        protected bool isRequired = false;
        [SerializeField, TextArea(2, 10)]
        protected string requiredErrorMessage;

        [Header("RegExp Validation"), SerializeField, TextArea(2, 10)]
        protected string regExpPattern;
        [SerializeField, TextArea(2, 10)]
        protected string regExpErrorMessage;

        [Header("Comparison Validation"), SerializeField]
        private TMP_InputField compareToInputField;
        [SerializeField, TextArea(2, 10)]
        protected string compareErrorMessage;

        protected virtual void Awake()
        {
            if (!currentInputField)
                currentInputField = GetComponent<TMP_InputField>();

            RememberStartValidationGraphicColor();
        }

        protected virtual void Update()
        {
            TransitionToStartColor();
        }

        public bool IsValid()
        {
            if (isRequired && string.IsNullOrEmpty(currentInputField.text))
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

        private void SetInvalidColor()
        {
            if (validationTargetGraphic == null || validationTargetGraphic.Length == 0 || !changeValidationColor) return;

            for (int i = 0; i < validationTargetGraphic.Length; i++)
            {
                validationTargetGraphic[i].color = invalidColor;
            }
        }

        private void TransitionToStartColor()
        {
            if (validationTargetGraphic == null || validationTargetGraphic.Length == 0 || !changeValidationColor) return;

            for (int i = 0; i < validationColorAtStart.Length; i++)
            {
                validationTargetGraphic[i].color = Color.Lerp(validationTargetGraphic[i].color, validationColorAtStart[i], Time.deltaTime);
            }
        }

        private void RememberStartValidationGraphicColor()
        {
            if (validationTargetGraphic == null || validationTargetGraphic.Length == 0 || !changeValidationColor) return;

            validationColorAtStart = new Color[validationTargetGraphic.Length];

            for (int i = 0; i < validationTargetGraphic.Length; i++)
            {
                validationColorAtStart[i] = validationTargetGraphic[i].color;
            }
        }
    }
}