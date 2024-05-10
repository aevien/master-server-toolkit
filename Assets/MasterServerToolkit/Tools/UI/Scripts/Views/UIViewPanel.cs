using UnityEngine;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIViewPanel : MonoBehaviour, IUIViewComponent
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        protected bool hideOnStart = true;

        #endregion

        private CanvasGroup canvasGroup;

        public IUIView Owner { get; set; }
        public Logging.Logger Logger { get; set; }
        public bool IsVisible { get; protected set; } = true;

        public void SetVisible(bool visible)
        {
            if (!Owner.IsVisible)
            {
                return;
            }

            if (IsVisible != visible)
            {
                canvasGroup.alpha = visible ? 1 : 0;
                canvasGroup.blocksRaycasts = visible;
                IsVisible = visible;
                OnSetVisible(visible);
            }
        }

        public virtual void OnOwnerAwake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

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