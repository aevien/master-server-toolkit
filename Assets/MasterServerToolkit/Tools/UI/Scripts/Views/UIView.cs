using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup))]
    public class UIView : MonoBehaviour, IUIView
    {
        #region INSPECTOR

        [Header("Identity Settings"), SerializeField]
        protected string id = "New View Id";
        [SerializeField]
        protected string title = "";

        [Header("Shared Settings"), SerializeField]
        protected bool hideOnStart = true;
        [SerializeField]
        protected bool allwaysOnTop = false;
        [SerializeField]
        protected bool ignoreHideAll = false;
        [SerializeField]
        protected bool useRaycastBlock = true;
        [SerializeField]
        protected bool blockInput = false;
        [SerializeField]
        protected bool unlockCursor = false;

        [Header("Logger Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        [Header("Components"), SerializeField]
        protected UILable titleLable;

        [Header("Events")]
        public UnityEvent OnShowEvent;
        public UnityEvent OnHideEvent;
        public UnityEvent OnShowFinishedEvent;
        public UnityEvent OnHideFinishedEvent;

        #endregion

        protected Logging.Logger logger;

        private readonly Dictionary<string, Component> children = new Dictionary<string, Component>();
        protected readonly Dictionary<string, IUIViewComponent> uiViewComponents = new Dictionary<string, IUIViewComponent>();
        protected IUIViewTweener uiViewTweener;
        protected CanvasGroup canvasGroup;
        protected bool isVisible = true;

        public string Id => id;
        public bool IsVisible => isVisible;
        public RectTransform Rect => transform as RectTransform;
        public bool IgnoreHideAll { get => ignoreHideAll; set => ignoreHideAll = value; }
        public bool BlockInput { get => blockInput; set => blockInput = value; }
        public bool UnlockCursor { get => unlockCursor; set => unlockCursor = value; }
        public Logging.Logger Logger => logger;

        protected virtual void Awake()
        {
            if (!canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            Rect.anchoredPosition = Vector2.zero;

            if (!string.IsNullOrEmpty(id))
            {
                ViewsManager.Register(id, this);
            }
            else
            {
                Logs.Warn($"Id field is empty therefore this UIView cannot be registered in {nameof(ViewsManager)}");
            }

            if (hideOnStart)
            {
                Hide(true);
            }
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(title))
                title = name;

            if (string.IsNullOrEmpty(Id))
                id = name;

            if (titleLable != null)
                titleLable.Text = title;
        }

        protected virtual void OnDestroy()
        {
            ViewsManager.Unregister(id);

            OnShowEvent.RemoveAllListeners();
            OnHideEvent.RemoveAllListeners();
            OnShowFinishedEvent.RemoveAllListeners();
            OnHideFinishedEvent.RemoveAllListeners();
        }

        public virtual void Show(bool instantly = false)
        {
            if (isVisible) return;

            if (uiViewTweener != null && !instantly)
            {
                if (allwaysOnTop)
                {
                    transform.SetAsLastSibling();
                }

                isVisible = true;
                OnShowEvent?.Invoke();
                NotifyComponentsOnShow(true);

                uiViewTweener.OnFinished(() =>
                {
                    SetCanvasActive(true);
                    OnShowFinishedEvent?.Invoke();
                });

                uiViewTweener.PlayShow();
            }
            else
            {
                if (allwaysOnTop)
                {
                    transform.SetAsLastSibling();
                }

                isVisible = true;
                OnShowEvent?.Invoke();
                SetCanvasActive(true);
                NotifyComponentsOnShow(true);
                OnShowFinishedEvent?.Invoke();
            }
        }

        public virtual void Hide(bool instantly = false)
        {
            if (!isVisible) return;

            if (uiViewTweener != null && !instantly)
            {
                isVisible = false;
                OnHideEvent?.Invoke();
                NotifyComponentsOnShow(false);

                uiViewTweener.OnFinished(() =>
                {
                    SetCanvasActive(false);
                    OnHideFinishedEvent?.Invoke();
                });

                uiViewTweener.PlayHide();
            }
            else
            {
                isVisible = false;
                OnHideEvent?.Invoke();
                NotifyComponentsOnShow(false);
                SetCanvasActive(false);
                OnHideFinishedEvent?.Invoke();
            }
        }

        public virtual void Toggle(bool instantly = false)
        {
            if (isVisible)
            {
                Hide(instantly);
            }
            else
            {
                Show(instantly);
            }
        }

        private Transform FindChild(Transform parent, string childName)
        {
            if (parent.childCount == 0)
            {
                return null;
            }

            Transform result = null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child.name == childName)
                {
                    result = child;
                    break;
                }
                else
                {
                    result = FindChild(child, childName);

                    if (result != null) break;
                }
            }

            return result;
        }

        private void SetCanvasActive(bool active)
        {
            if (canvasGroup)
            {
                canvasGroup.interactable = active;
                canvasGroup.blocksRaycasts = useRaycastBlock && active;
                canvasGroup.alpha = active ? 1f : 0f;
            }
        }

        private void NotifyComponentsOnShow(bool show)
        {
            if (show)
            {
                OnShow();

                foreach (var uiComponent in GetComponentsInChildren<IUIViewComponent>())
                {
                    uiComponent.OnOwnerShow(this);
                }
            }
            else
            {
                OnHide();

                foreach (var uiComponent in GetComponentsInChildren<IUIViewComponent>())
                {
                    uiComponent.OnOwnerHide(this);
                }
            }
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }
}