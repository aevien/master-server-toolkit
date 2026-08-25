using UnityEngine;

namespace MasterServerToolkit.UI
{
    public abstract class UIViewPanel : MonoBehaviour, IUIViewComponent
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Panel GameObject whose active state represents this component's visibility. Assign the visual root controlled by SetVisible.")]
        protected GameObject panel;

        [Header("Settings"), SerializeField]
        [Tooltip("Hides the assigned panel during Awake. Disable this when the panel must keep its scene-authored initial visibility.")]
        protected bool hideOnStart = true;

        #endregion

        public bool IsVisible => panel.activeSelf;

        public void SetVisible(bool visible)
        {
            if (IsVisible != visible)
            {
                panel.SetActive(visible);
                OnSetVisible(visible);
            }
        }

        protected virtual void Awake()
        {
            if (hideOnStart)
            {
                SetVisible(false);
            }
        }

        public virtual void OnOwnerHide(IUIView owner) { }
        public virtual void OnOwnerShow(IUIView owner) { }
        protected virtual void OnSetVisible(bool visible) { }
    }
}
