using UnityEngine;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(UIView))]
    public class UIViewSync : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Views synchronized when the owner view emits an enabled show or hide event. Null entries are ignored; do not include the owner view itself.")]
        protected UIView[] syncedViews = new UIView[0];

        [Header("Settings"), SerializeField]
        [Tooltip("When enabled, showing the owner view calls Show on every assigned synchronized view.")]
        protected bool listenToShowEvent = true;
        [SerializeField]
        [Tooltip("When enabled, hiding the owner view calls Hide on every assigned synchronized view.")]
        protected bool listenToHideEvent = true;
        [SerializeField]
        [Tooltip("Passes the instant flag to synchronized Show and Hide calls. Enable to skip configured view transitions; disable to use their normal transitions.")]
        protected bool invokeInstantly = false;

        #endregion

        protected UIView view;

        protected void Awake()
        {
            view = GetComponent<UIView>();

            view.OnShowEvent.AddListener(OnShowEventHandler);
            view.OnHideEvent.AddListener(OnHideEventHandler);
        }

        private void OnDestroy()
        {
            if (view == null)
                return;

            view.OnShowEvent.RemoveListener(OnShowEventHandler);
            view.OnHideEvent.RemoveListener(OnHideEventHandler);
        }

        private void OnShowEventHandler()
        {
            if (listenToShowEvent)
            {
                foreach (UIView syncedView in syncedViews)
                    if (syncedView != null)
                        syncedView.Show(invokeInstantly);
            }
        }

        private void OnHideEventHandler()
        {
            if (listenToHideEvent)
            {
                foreach (UIView syncedView in syncedViews)
                    if (syncedView != null)
                        syncedView.Hide(invokeInstantly);
            }
        }
    }
}
