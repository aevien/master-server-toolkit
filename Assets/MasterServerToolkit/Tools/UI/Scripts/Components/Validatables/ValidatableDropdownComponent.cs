using MasterServerToolkit.Logging;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class ValidatableDropdownComponent : ValidatableBaseComponent
    {
        [Header("Dropdown Components"), SerializeField]
        private TMP_Dropdown currentDropdown;

        [Header("Dropdown Required Settings"), SerializeField]
        private int minRequiredValue = 0;

        protected override void Awake()
        {
            base.Awake();

            if (!currentDropdown)
                currentDropdown = GetComponent<TMP_Dropdown>();
        }

        protected override void OnValidate()
        {
            minRequiredValue = Mathf.Clamp(minRequiredValue, 0, int.MaxValue);
        }

        public override bool IsValid()
        {
            if (!currentDropdown.interactable)
            {
                return true;
            }

            if (isRequired && currentDropdown.value < minRequiredValue)
            {
                Logs.Error(string.IsNullOrEmpty(requiredErrorMessage) ? $"Field {name} is required" : requiredErrorMessage);

                SetInvalidColor();
                return false;
            }

            return true;
        }
    }
}