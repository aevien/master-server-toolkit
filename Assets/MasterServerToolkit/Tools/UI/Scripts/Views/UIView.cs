using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    /// <summary>
    /// Base class for 2D uGUI views controlled by ViewsManager.
    ///
    /// Contract:
    /// - Show/Hide are NO-OP if called repeatedly toward the same target while a transition is active.
    /// - If a contrary command arrives during a transition (Show while Hiding, or Hide while Showing),
    ///   the current tween is cancelled and a new transition starts immediately.
    /// - Bringing to front (SetAsLastSibling) happens only when transitioning hidden -> shown AND alwaysOnTop is true.
    /// - Final interactivity and alpha (CanvasGroup) are applied ONLY at the END of a transition (or at initialization).
    /// - Start/End owner hooks (OnStartShow/OnEndShow/etc.) wrap child notifications and UnityEvents for clear ordering.
    /// - A transition token invalidates stale tween callbacks after cancellation to avoid double-finishes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup))]
    public abstract class UIView : MonoBehaviour, IUIView, IUIViewPolicy, IUIViewLayout
    {
        #region Inspector

        [Header("Identity Settings")]
        [SerializeField, Tooltip("Optional human-readable title. If a title label is assigned, it will mirror this value in the editor.")]
        protected string title = "";

        [Header("Shared Settings")]
        [SerializeField, Tooltip("If true, the view starts hidden and non-interactive. No Show/Hide events are fired on startup.")]
        protected bool hideOnStart = true;

        [SerializeField, Tooltip("If true, when transitioning from hidden to shown, the view will be moved to the last sibling (top within its parent).")]
        protected bool alwaysOnTop = false;

        [SerializeField, Tooltip("If true, ViewsManager.HideAllViews() will NOT hide this view.")]
        protected bool ignoreHideAll = false;

        [SerializeField, Tooltip("If true, CanvasGroup.blocksRaycasts will be enabled while the view is interactive/visible.")]
        protected bool useRaycastBlock = true;

        [SerializeField, Tooltip("Declarative flag: this view blocks input for global policy checks. Does NOT change CanvasGroup by itself.")]
        protected bool blockInput = false;

        [SerializeField, Tooltip("Declarative flag: this view requests cursor unlock for global policy checks. Does NOT change Cursor state by itself.")]
        protected bool unlockCursor = false;

        [Header("Logger Settings")]
        [SerializeField, Tooltip("Minimum log level for this view's internal logger.")]
        protected LogLevel logLevel = LogLevel.Info;

        [Header("Components")]
        [SerializeField, Tooltip("Optional label that mirrors the 'title' field for convenience in the editor.")]
        protected TextMeshProUGUI titleLabel;

        [Header("Events")]
        [Tooltip("Raised at the START of the show transition (before the view becomes interactive).")]
        public UnityEvent OnShowEvent = new UnityEvent();

        [Tooltip("Raised at the START of the hide transition (before the view becomes non-interactive).")]
        public UnityEvent OnHideEvent = new UnityEvent();

        [Tooltip("Raised at the END of the show transition (after the view becomes interactive).")]
        public UnityEvent OnShowFinishedEvent = new UnityEvent();

        [Tooltip("Raised at the END of the hide transition (after the view becomes non-interactive).")]
        public UnityEvent OnHideFinishedEvent = new UnityEvent();

        #endregion

        /// <summary>
        /// Per-instance logger (respects <see cref="logLevel"/>).
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Optional tweener implementation to animate show/hide. Must support Cancel() and OnFinished().
        /// </summary>
        protected IUIViewTweener tweener;

        /// <summary>
        /// Desired final visibility (used to decide starts/finishes and to allow interruption).
        /// </summary>
        protected bool desiredVisible;

        /// <summary>
        /// Version/token of the active transition to invalidate stale tween callbacks on Cancel().
        /// </summary>
        private int transitionToken = 0;

        // -------- Public API / Properties --------

        /// <inheritdoc/>
        public bool IsVisible { get; protected set; }

        /// <inheritdoc/>
        public bool InTransition { get; protected set; }

        /// <summary>
        /// Cached RectTransform of this view.
        /// </summary>
        public RectTransform Rect => transform as RectTransform;

        /// <inheritdoc/>
        public bool IgnoreHideAll { get => ignoreHideAll; set => ignoreHideAll = value; }

        /// <inheritdoc/>
        public bool BlockInput { get => blockInput; set => blockInput = value; }

        /// <inheritdoc/>
        public bool UnlockCursor { get => unlockCursor; set => unlockCursor = value; }

        /// <inheritdoc/>
        public EventPayload Payload { get; set; }

        /// <summary>
        /// Access to the per-instance logger.
        /// </summary>
        public Logging.Logger Logger => logger;

        /// <summary>
        /// CanvasGroup used to control interactivity and final alpha.
        /// </summary>
        public CanvasGroup CanvasGroup { get; protected set; }

        // -------- Unity Lifecycle --------

        /// <summary>
        /// Unity Awake: resolves components, registers the view, and applies initial visual state without firing events.
        /// </summary>
        protected virtual void Awake()
        {
            if (CanvasGroup == null)
                CanvasGroup = GetComponent<CanvasGroup>();

            tweener = GetComponent<IUIViewTweener>();

            if (tweener != null)
            {
                // Allow tweener to reference its owner layout if it needs to
                tweener.UIViewLayout = this;
            }

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            // NOTE: For prefab-authored layouts this may be undesirable.
            // Consider removing or guarding this if your layout is set in the prefab.
            Rect.anchoredPosition = Vector2.zero;

            // Register with the global manager.
            ViewsManager.Register(this);

            // Initialize visual/interactive state WITHOUT firing any events.
            SetCanvasActive(!hideOnStart);
            IsVisible = !hideOnStart;
            desiredVisible = IsVisible;
        }

        /// <summary>
        /// Unity OnDisable: cancels any running transition and resets interactivity.
        /// No events are fired here by design.
        /// </summary>
        protected virtual void OnDisable()
        {
            // Cancel any running tween and invalidate its callbacks
            desiredVisible = false;
            Cancel();

            IsVisible = false;
            SetCanvasActive(false);
        }

        /// <summary>
        /// Unity OnValidate: keeps ID/title sane in the editor and mirrors title to an optional label.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(title))
                title = GetType().Name;

            if (titleLabel != null)
                titleLabel.text = title;
        }

        /// <summary>
        /// Unity OnDestroy: unregisters from ViewsManager and removes event listeners.
        /// </summary>
        protected virtual void OnDestroy()
        {
            ViewsManager.Unregister(this);

            OnShowEvent.RemoveAllListeners();
            OnHideEvent.RemoveAllListeners();
            OnShowFinishedEvent.RemoveAllListeners();
            OnHideFinishedEvent.RemoveAllListeners();
        }

        // -------- Show / Hide API --------

        /// <summary>
        /// Starts the show transition if not already visible or transitioning toward show.
        /// If hiding is in progress, it will be cancelled and show will start immediately.
        /// Ordering:
        /// 1) Bring-to-front (if alwaysOnTop).
        /// 2) Owner start hook -> Notify children -> OnShowEvent (START).
        /// 3) Tween (if any).
        /// 4) SetCanvasActive(true) -> Owner end hook -> OnShowFinishedEvent (END).
        /// </summary>
        /// <param name="instantly">If true, skip animation and complete the transition immediately.</param>
        public virtual void Show(bool instantly = false)
        {
            // Already visible and no transition? NO-OP (but keep position policy)
            if (IsVisible && !InTransition)
            {
                BringToFront();
                return;
            }

            // If a contrary transition is in progress (hiding), cancel it and proceed.
            if (InTransition)
            {
                if (!desiredVisible) // we were hiding, now want to show
                {
                    Cancel(); // cancels tween + invalidates previous callbacks + clears inTransition
                }
                else
                {
                    // Already showing; repeated call toward the same target is a NO-OP
                    return;
                }
            }

            bool wasDesired = desiredVisible;
            desiredVisible = true;

            // Fire start events only on intention change (hidden -> show)
            if (!wasDesired)
            {
                BringToFront();
                OnStartShow();
                NotifyChildrenOnStart(true);
                OnShowEvent?.Invoke();
            }

            if (tweener != null && !instantly)
            {
                InTransition = true;
                int myToken = ++transitionToken;

                tweener.OnFinished(() =>
                {
                    // Ignore stale finishes (after Cancel or intention flip)
                    if (myToken != transitionToken || !desiredVisible) return;

                    SetCanvasActive(true); // final alpha/interactivity applied here
                    IsVisible = true;
                    InTransition = false;
                    OnEndShow();
                    OnShowFinishedEvent?.Invoke();
                });

                tweener.PlayShow();
            }
            else
            {
                // Instant finish
                SetCanvasActive(true);
                IsVisible = true;
                InTransition = false;
                OnEndShow();
                OnShowFinishedEvent?.Invoke();
            }
        }

        /// <summary>
        /// Starts the hide transition if currently visible or transitioning toward show.
        /// If showing is in progress, it will be cancelled and hide will start immediately.
        /// Ordering mirrors Show():
        /// 1) Owner start hook -> Notify children -> OnHideEvent (START).
        /// 2) Tween (if any).
        /// 3) SetCanvasActive(false) -> Owner end hook -> OnHideFinishedEvent (END).
        /// </summary>
        /// <param name="instantly">If true, skip animation and complete the transition immediately.</param>
        public virtual void Hide(bool instantly = false)
        {
            // Already hidden and no transition? NO-OP
            if (!IsVisible && !InTransition)
                return;

            // If a contrary transition is in progress (showing), cancel it and proceed.
            if (InTransition)
            {
                if (desiredVisible) // we were showing, now want to hide
                {
                    Cancel(); // cancels tween + invalidates previous callbacks + clears inTransition
                }
                else
                {
                    // Already hiding; repeated call toward the same target is a NO-OP
                    return;
                }
            }

            bool wasDesired = desiredVisible;
            desiredVisible = false;

            // Fire start events only on intention change (show -> hide)
            if (wasDesired)
            {
                OnStartHide();
                NotifyChildrenOnStart(false);
                OnHideEvent?.Invoke();
            }

            if (tweener != null && !instantly)
            {
                InTransition = true;
                int myToken = ++transitionToken;

                tweener.OnFinished(() =>
                {
                    if (myToken != transitionToken || desiredVisible) return;

                    SetCanvasActive(false); // final alpha/interactivity applied here
                    IsVisible = false;
                    InTransition = false;
                    OnEndHide();
                    OnHideFinishedEvent?.Invoke();
                });

                tweener.PlayHide();
            }
            else
            {
                // Instant finish
                SetCanvasActive(false);
                IsVisible = false;
                InTransition = false;
                OnEndHide();
                OnHideFinishedEvent?.Invoke();
            }
        }

        /// <summary>
        /// Convenience toggle. If visible -> hides; if hidden -> shows.
        /// During transitions this will either NO-OP (same direction) or interrupt and reverse (opposite direction).
        /// </summary>
        /// <param name="instantly">If true, skip animation and complete immediately.</param>
        public virtual void Toggle(bool instantly = false)
        {
            if (IsVisible)
            {
                Hide(instantly);
            }
            else
            {
                Show(instantly);
            }
        }

        // -------- Internals --------

        /// <summary>
        /// Applies CanvasGroup interactivity and FINAL alpha.
        /// This is called ONLY at the end of a transition or during initial Awake setup
        /// to keep the contract consistent (tween animates alpha; UIView owns the final state).
        /// </summary>
        /// <param name="active">True for interactive &amp; opaque; false for non-interactive &amp; transparent.</param>
        private void SetCanvasActive(bool active)
        {
            if (CanvasGroup)
            {
                CanvasGroup.interactable = active;
                CanvasGroup.blocksRaycasts = useRaycastBlock && active;
                CanvasGroup.alpha = active ? 1f : 0f;
            }
        }

        /// <summary>
        /// Notifies child IUIViewComponent(s) at the START of a transition.
        /// Owner hooks (OnStartShow/OnStartHide) are called separately, before children.
        /// </summary>
        /// <param name="show">True if the owner is starting to show; false if starting to hide.</param>
        private void NotifyChildrenOnStart(bool show)
        {
            if (show)
            {
                foreach (var uiComponent in GetComponentsInChildren<IUIViewComponent>(includeInactive: false))
                    uiComponent.OnOwnerShow(this);
            }
            else
            {
                foreach (var uiComponent in GetComponentsInChildren<IUIViewComponent>(includeInactive: false))
                    uiComponent.OnOwnerHide(this);
            }
        }

        // -------- Owner Hooks (override in subclasses) --------

        /// <summary> Owner hook called at the START of the show transition. </summary>
        protected virtual void OnStartShow() { }

        /// <summary> Owner hook called at the START of the hide transition. </summary>
        protected virtual void OnStartHide() { }

        /// <summary> Owner hook called at the END of the show transition. </summary>
        protected virtual void OnEndShow() { }

        /// <summary> Owner hook called at the END of the hide transition. </summary>
        protected virtual void OnEndHide() { }

        // -------- IUIView/IUIViewLayout --------

        /// <summary>
        /// Cancels the current tween/transition (if any) and invalidates its finish callback.
        /// Does NOT fire finished events and does NOT change desired intention by itself.
        /// Use when switching direction mid-transition.
        /// </summary>
        public void Cancel()
        {
            // Invalidate all previously registered OnFinished callbacks
            transitionToken++;

            // Soft-cancel the current tween (if present)
            tweener?.Cancel();

            // Allow a new transition to start right away
            InTransition = false;
        }

        /// <summary>
        /// Brings the view to the front within its parent if <see cref="alwaysOnTop"/> is true.
        /// </summary>
        public void BringToFront()
        {
            if (alwaysOnTop)
                transform.SetAsLastSibling();
        }
    }
}
