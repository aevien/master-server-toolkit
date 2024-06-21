using UnityEngine;

namespace MasterServerToolkit.UI
{
    public class UIViewPanel : MonoBehaviour, IUIViewComponent
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected GameObject panel;

        [Header("Settings"), SerializeField]
        protected bool hideOnStart = true;

        #endregion

        public IUIView Owner { get; set; }
        public Logging.Logger Logger { get; set; }
        public bool IsVisible => panel.activeSelf;

        public void SetVisible(bool visible)
        {
            panel.SetActive(visible);

            if (IsVisible != visible)
            {
                OnSetVisible(visible);
            }
        }

        public virtual void OnOwnerAwake()
        {
            if (hideOnStart)
            {
                SetVisible(false);
            }
        }

        public virtual void OnOwnerStart() { }
        public virtual void OnOwnerUpdate() { }
        public virtual void OnOwnerHide(IUIView owner) { }
        public virtual void OnOwnerShow(IUIView owner) { }
        protected virtual void OnSetVisible(bool visible) { }
    }
}