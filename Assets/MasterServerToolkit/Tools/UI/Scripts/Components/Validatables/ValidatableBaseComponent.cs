using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class ValidatableBaseComponent : MonoBehaviour, IValidatableComponent
    {
        private Color[] validationColorAtStart;

        [Header("Base Settings"), SerializeField]
        [Tooltip("Color applied to Validation Target Graphic elements when validation fails and Change Validation Color is enabled.")]
        protected Color invalidColor = Color.red;
        [SerializeField]
        [Tooltip("When enabled, failed validation colors the configured target graphics and then gradually returns them to their original colors.")]
        protected bool changeValidationColor = true;

        [Header("Graphics"), SerializeField]
        [Tooltip("Graphics that provide visual feedback after validation fails. Leave empty when color feedback is not required.")]
        protected Graphic[] validationTargetGraphic;

        [Header("Required Validation"), SerializeField]
        [Tooltip("When enabled, derived validators reject an empty or otherwise unselected required value. Non-interactable controls are treated as valid.")]
        protected bool isRequired = false;
        [SerializeField, TextArea(2, 10)]
        [Tooltip("Error written through MST logging when required validation fails. Leave empty to use the validator's default message.")]
        protected string requiredErrorMessage;

        protected virtual void Awake()
        {
            RememberStartValidationGraphicColor();
        }

        protected virtual void Update()
        {
            TransitionToStartColor();
        }

        protected virtual void OnValidate() { }

        public virtual bool IsValid()
        {
            return true;
        }

        protected void SetInvalidColor()
        {
            if (validationTargetGraphic == null || validationTargetGraphic.Length == 0 || !changeValidationColor) return;

            for (int i = 0; i < validationTargetGraphic.Length; i++)
            {
                validationTargetGraphic[i].color = invalidColor;
            }
        }

        protected void TransitionToStartColor()
        {
            if (validationTargetGraphic == null || validationTargetGraphic.Length == 0 || !changeValidationColor) return;

            for (int i = 0; i < validationColorAtStart.Length; i++)
            {
                validationTargetGraphic[i].color = Color.Lerp(validationTargetGraphic[i].color, validationColorAtStart[i], Time.deltaTime);
            }
        }

        protected void RememberStartValidationGraphicColor()
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
