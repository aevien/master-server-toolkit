using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    /// <summary>
    /// Base view for Inspector-configured forms that submit only after successful validation.
    /// </summary>
    [RequireComponent(typeof(ValidationFormComponent))]
    public abstract class UIViewForm : UIView
    {
        [Header("Form Events")]
        [Tooltip("Invoked after Submit validates every form field successfully. Connect the view's request or apply method here.")]
        public UnityEvent OnSubmitEvent = new UnityEvent();

        /// <summary>
        /// Validation component attached to the same GameObject as the form view.
        /// </summary>
        protected ValidationFormComponent ValidationForm { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            ValidationForm = GetComponent<ValidationFormComponent>();
            ValidationForm.OnFormValidEvent.AddListener(SubmitValidatedForm);
        }

        protected override void OnDestroy()
        {
            if (ValidationForm != null)
                ValidationForm.OnFormValidEvent.RemoveListener(SubmitValidatedForm);

            OnSubmitEvent.RemoveAllListeners();
            base.OnDestroy();
        }

        /// <summary>
        /// Validates the form. This parameterless method can be connected to buttons and input submit events in the Inspector.
        /// </summary>
        public void Submit()
        {
            ValidateForm();
        }

        /// <summary>
        /// Starts validation without invoking the submit event when any field is invalid.
        /// </summary>
        protected void ValidateForm()
        {
            ValidationForm.Validate();
        }

        private void SubmitValidatedForm()
        {
            OnSubmitEvent?.Invoke();
        }
    }
}
