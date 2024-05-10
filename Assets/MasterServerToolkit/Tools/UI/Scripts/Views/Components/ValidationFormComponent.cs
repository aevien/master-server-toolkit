using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    public class ValidationFormComponent : MonoBehaviour
    {
        public UnityEvent OnFormValidEvent;
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