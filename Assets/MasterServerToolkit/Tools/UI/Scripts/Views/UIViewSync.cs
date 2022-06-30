using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(UIView))]
    public class UIViewSync : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected UIView[] syncedViews = new UIView[0];

        [Header("Settings"), SerializeField]
        protected bool listenToShowEvent = true;
        [SerializeField]
        protected bool listenToHideEvent = true;
        [SerializeField]
        protected bool invokeInstantly = false;

        #endregion

        protected UIView view;

        private void Awake()
        {
            view = GetComponent<UIView>();

            view.OnShowEvent.AddListener(OnShowEventHandler);
            view.OnHideEvent.AddListener(OnHideEventHandler);
        }

        private void OnShowEventHandler()
        {
            if (listenToShowEvent)
            {
                foreach (UIView view in syncedViews.ToList().Where(v => v))
                {
                    view.Show(invokeInstantly);
                }
            }
        }

        private void OnHideEventHandler()
        {
            if (listenToHideEvent)
            {
                foreach (UIView view in syncedViews.ToList().Where(v => v))
                {
                    view.Show(invokeInstantly);
                }
            }
        }
    }
}