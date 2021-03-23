using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    public class ValidationFormComponent : MonoBehaviour, IUIViewComponent
    {
        public UnityEvent OnFormValidEvent;
        public UnityEvent OnFormInvalidEvent;

        private IValidatableComponent[] validatableList;

        public IUIView Owner { get; set; }

        public void OnOwnerAwake()
        {
            UpdateValidatables();
        }

        public void UpdateValidatables()
        {
            if (validatableList == null || validatableList.Length == 0)
                validatableList = GetComponentsInChildren<IValidatableComponent>();
        }

        public void OnOwnerHide(IUIView owner) { }

        public void OnOwnerShow(IUIView owner) { }

        public void OnOwnerStart() { }

        public void Validate()
        {
            UpdateValidatables();

            int totalValid = 0;

            for (int i = 0; i < validatableList.Length; i++)
            {
                if (validatableList[i].IsValid())
                {
                    totalValid++;
                }
            }

            if (validatableList.Length == totalValid)
            {
                OnFormValidEvent?.Invoke();
            }
            else
            {
                OnFormInvalidEvent?.Invoke();
            }
        }
    }
}