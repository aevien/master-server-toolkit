using MasterServerToolkit.Logging;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class ValidatableDropdownComponent : ValidatableBaseComponent
    {
        [Header("Dropdown Components"), SerializeField]
        [Tooltip("Dropdown validated by this component. Leave empty to use a TMP_Dropdown attached to the same GameObject.")]
        private TMP_Dropdown currentDropdown;

        [Header("Dropdown Required Settings"), SerializeField]
        [Tooltip("Lowest dropdown option index accepted when Is Required is enabled. Use 1 to reserve option 0 as a placeholder; values below 0 are corrected to 0.")]
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
