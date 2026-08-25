using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    public class ValidationFormComponent : MonoBehaviour
    {
        [Tooltip("Invoked by Validate when every IValidatableComponent found in this object's children reports a valid state. An empty form is treated as valid.")]
        public UnityEvent OnFormValidEvent;
        [Tooltip("Invoked by Validate when at least one IValidatableComponent found in this object's children reports an invalid state.")]
        public UnityEvent OnFormInvalidEvent;

        private IValidatableComponent[] validatableList;

        private void Awake()
        {
            UpdateValidatables();
        }

        public void UpdateValidatables()
        {
            if (validatableList == null || validatableList.Length == 0)
                validatableList = GetComponentsInChildren<IValidatableComponent>();
        }

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
