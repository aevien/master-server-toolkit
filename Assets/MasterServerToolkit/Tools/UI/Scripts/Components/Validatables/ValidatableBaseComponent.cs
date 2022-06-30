using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class ValidatableBaseComponent : MonoBehaviour, IValidatableComponent
    {
        private Color[] validationColorAtStart;

        [Header("Base Settings"), SerializeField]
        protected Color invalidColor = Color.red;
        [SerializeField]
        protected bool changeValidationColor = true;

        [Header("Graphics"), SerializeField]
        protected Graphic[] validationTargetGraphic;

        [Header("Required Validation"), SerializeField]
        protected bool isRequired = false;
        [SerializeField, TextArea(2, 10)]
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