using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(UIView))]
    public class UIViewKeyInputHandler : MonoBehaviour, IUIViewInputHandler
    {
        [Header("Settings"), SerializeField]
        [Tooltip("Keyboard key checked once per frame with Input.GetKeyDown. None disables keyboard activation.")]
        private KeyCode key = KeyCode.None;
        [SerializeField]
        [Tooltip("Enables toggling the UIView attached to this GameObject when the configured key is pressed. When disabled, this handler performs no action and does not invoke On Input Event.")]
        private bool toggleUIView = true;
        [SerializeField]
        [Tooltip("Passes the instant flag to UIView.Toggle. Enable to skip the view's normal show or hide transition.")]
        private bool toggleInstantly = false;

        [Header("Events")]
        [Tooltip("Invoked after the configured key successfully toggles the attached UIView. It is not invoked when Toggle UIView is disabled.")]
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
                {
                    view.Toggle(toggleInstantly);
                    OnInputEvent?.Invoke();
                }
            }
        }
    }
}
