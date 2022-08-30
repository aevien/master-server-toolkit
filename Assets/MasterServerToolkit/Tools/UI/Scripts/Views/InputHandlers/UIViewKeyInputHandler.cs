using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(UIView))]
    public class UIViewKeyInputHandler : MonoBehaviour, IUIViewInputHandler
    {
        [Header("Settings"), SerializeField]
        private KeyCode key = KeyCode.None;
        [SerializeField]
        private bool toggleUIView = true;
        [SerializeField]
        private bool toggleInstantly = false;

        [Header("Events")]
        public UnityEvent OnInputEvent;

        private UIView view;

        private void Awake()
        {
            view = GetComponent<UIView>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                if (toggleUIView && view)
                    view.Toggle(toggleInstantly);

                OnInputEvent?.Invoke();
            }
        }
    }
}