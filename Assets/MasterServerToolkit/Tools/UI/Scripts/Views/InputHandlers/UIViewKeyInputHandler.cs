using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    public class UIViewKeyInputHandler : MonoBehaviour, IUIViewInputHandler
    {
        [Header("Settings"), SerializeField]
        private KeyCode key = KeyCode.None;

        [Header("Events")]
        public UnityEvent OnInputEvent;

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                OnInputEvent?.Invoke();
            }
        }
    }
}