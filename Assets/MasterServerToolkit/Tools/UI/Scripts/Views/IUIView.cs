using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    /// <summary>
    /// Basic contract for a UI view.
    /// 
    /// Responsibilities:
    /// - Show and hide the view (with optional transitions).
    /// - Report its visibility state.
    /// - Allow interruption of an active transition.
    /// 
    /// This is the minimal API expected by ViewsManager and other systems
    /// that need to control views without depending on their implementation details.
    /// </summary>
    public interface IUIView
    {
        /// <summary>
        /// Here is the data that is transmitted as a payload when the view is opened.
        /// </summary>
        EventPayload Payload { get; set; }
        /// <summary>
        /// True if the view is fully visible (after a show transition completes).
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// True if a show/hide transition is currently running.
        /// </summary>
        bool InTransition { get; }

        /// <summary>
        /// Starts the show transition.
        /// If already visible, this call is a NO-OP.
        /// </summary>
        /// <param name="instantly">If true, skip animation and show immediately.</param>
        void Show(bool instantly = false);

        /// <summary>
        /// Starts the hide transition.
        /// If already hidden, this call is a NO-OP.
        /// </summary>
        /// <param name="instantly">If true, skip animation and hide immediately.</param>
        void Hide(bool instantly = false);

        /// <summary>
        /// Convenience toggle:
        /// - Shows the view if currently hidden.
        /// - Hides the view if currently visible.
        /// During a transition, may interrupt and reverse direction.
        /// </summary>
        /// <param name="instantly">If true, skip animation.</param>
        void Toggle(bool instantly = false);

        /// <summary>
        /// Cancels the current transition (if any).
        /// - Does not fire finished events.
        /// - Typically used when switching direction mid-transition.
        /// </summary>
        void Cancel();
    }

    /// <summary>
    /// Declarative policy flags for a view.
    /// 
    /// These do not directly change CanvasGroup or Cursor state,
    /// but serve as signals for ViewsManager or other global systems
    /// to apply policies (e.g. disable background input).
    /// </summary>
    public interface IUIViewPolicy
    {
        /// <summary>
        /// If true, this view will not be hidden
        /// when ViewsManager.HideAllViews() is called.
        /// </summary>
        bool IgnoreHideAll { get; set; }

        /// <summary>
        /// If true, this view is considered to block global input.
        /// Manager may use this to disable gameplay interaction.
        /// </summary>
        bool BlockInput { get; set; }

        /// <summary>
        /// If true, this view is considered to request cursor unlock.
        /// Manager may use this to release the system cursor.
        /// </summary>
        bool UnlockCursor { get; set; }
    }

    /// <summary>
    /// Layout contract for uGUI views.
    /// 
    /// Provides access to RectTransform and CanvasGroup
    /// for positioning, sizing, and interactivity management.
    /// </summary>
    public interface IUIViewLayout
    {
        /// <summary>
        /// CanvasGroup used to control interactivity and transparency.
        /// UIView sets the FINAL state of alpha and interaction,
        /// while tweens may animate alpha during transitions.
        /// </summary>
        CanvasGroup CanvasGroup { get; }

        /// <summary>
        /// RectTransform of the view (for positioning, anchors, hierarchy).
        /// </summary>
        RectTransform Rect { get; }

        /// <summary>
        /// Brings this view to the front of its parent hierarchy
        /// (typically implemented with transform.SetAsLastSibling()).
        /// </summary>
        void BringToFront();
    }
}
