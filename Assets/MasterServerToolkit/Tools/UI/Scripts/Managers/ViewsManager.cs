using MasterServerToolkit.Logging;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    /// <summary>
    /// Global registry for UIView instances by ID.
    /// - Register/Unregister views created in the scene.
    /// - Query views by ID or by type.
    /// - Issue high-level Show/Hide commands (single or multiple).
    /// Contract notes:
    /// * IDs must be unique. Registering an already-registered ID will log a warning and overwrite.
    /// * All methods tolerate missing IDs and log helpful messages instead of throwing.
    /// </summary>
    public static class ViewsManager
    {
        /// <summary>
        /// Central storage of views by their unique IDs.
        /// </summary>
        private static readonly Dictionary<string, UIView> views = new();

        /// <summary>
        /// True if there is at least one visible view that requests input blocking.
        /// </summary>
        public static bool HasVisibleInputBlockView()
        {
            PruneDestroyedViews();
            return views.Values.Any(v => v != null && v.BlockInput && v.IsVisible);
        }

        /// <summary>
        /// True if there is at least one visible view that requests cursor unlock.
        /// </summary>
        public static bool HasVisibleCursorUnlockView()
        {
            PruneDestroyedViews();
            return views.Values.Any(v => v != null && v.UnlockCursor && v.IsVisible);
        }

        /// <summary>
        /// Registers a view by its ID. Overwrites existing entry with a warning.
        /// </summary>
        public static void Register<T>(T view) where T : UIView
        {
            PruneDestroyedViews();

            if (view == null)
            {
                Logs.Error("[ViewsManager] Cannot register view: view is null.");
                return;
            }

            if (views.TryGetValue(view.GetType().Name, out var existing) && existing != null && !ReferenceEquals(existing, view))
            {
                Logs.Warn($"[ViewsManager] View id '{view.GetType().Name}' is already registered by '{existing}'. It will be overwritten by '{view}'.");
            }

            views[view.GetType().Name] = view;
        }

        /// <summary>
        /// Unregisters a view by its ID. No-op if ID is null/empty or not found.
        /// </summary>
        public static void Unregister<T>(T view) where T : UIView
        {
            if (view == null)
                return;

            string id = view.GetType().Name;

            if (views.TryGetValue(id, out UIView registeredView) && ReferenceEquals(registeredView, view))
                views.Remove(id);
        }

        /// <summary>
        /// Gets a view by ID (non-generic). Returns null and logs an error if not found.
        /// </summary>
        public static T GetView<T>() where T : UIView
        {
            if (!Application.isPlaying)
                return null;

            PruneDestroyedViews();

            if (!views.TryGetValue(typeof(T).Name, out var view) || view == null)
            {
                Logs.Error($"[ViewsManager] View with Id '{typeof(T).Name}' is not registered.");
                return null;
            }

            return (T)view;
        }

        /// <summary>
        /// Tries to get a view (non-generic). Returns true if found.
        /// </summary>
        public static bool TryGetView<T>(out UIView view) where T : UIView
        {
            view = GetView<T>();
            return view != null;
        }

        /// <summary>
        /// Shows a view by ID with default animation settings.
        /// </summary>
        public static void Show<T>(object payload = null) where T : UIView => Show<T>(false, payload);

        /// <summary>
        /// Shows a view by ID, controlling whether it should show instantly.
        /// </summary>
        public static void Show<T>(bool instantly, object payload = null) where T : UIView
        {
            var view = GetView<T>();

            if (view == null)
                return;

            if (payload != null)
                view.Payload = new MasterServer.EventPayload(payload);

            view.Show(instantly);
        }

        /// <summary>
        /// Hides a view by ID with default animation settings.
        /// </summary>
        public static void Hide<T>() where T : UIView => Hide<T>(instantly: false);

        /// <summary>
        /// Hides a view by ID, controlling whether it should hide instantly.
        /// </summary>
        public static void Hide<T>(bool instantly) where T : UIView
        {
            var view = GetView<T>();
            view?.Hide(instantly);
        }

        /// <summary>
        /// Logs a helpful error for missing views (utility for external callers).
        /// </summary>
        public static void NotifyNoViewFound(params string[] viewIds)
        {
            if (viewIds == null || viewIds.Length == 0)
                return;

            foreach (var id in viewIds)
            {
                Logs.Error($"[ViewsManager] You are trying to use '{id}', but it is not registered. Please add/register the view '{id}'.");
            }
        }

        /// <summary>
        /// Hides all registered views except those marked with IgnoreHideAll.
        /// </summary>
        public static void HideAllViews(bool instantly = false)
        {
            PruneDestroyedViews();

            foreach (var view in views.Values)
            {
                if (view == null)
                    continue;

                if (!view.IgnoreHideAll)
                    view.Hide(instantly);
            }
        }

        /// <summary>
        /// Legacy alias: hides by IDs passed as "names".
        /// </summary>
        [System.Obsolete("Use HideViewsById(bool instantly, params string[] ids) instead.")]
        public static void HideViewsByName(bool instantly = false, params string[] names)
        {
            HideViewsById(instantly, names);
        }

        /// <summary>
        /// Hides a set of views by their IDs. Logs warnings for missing IDs.
        /// </summary>
        public static void HideViewsById(bool instantly = false, params string[] ids)
        {
            if (ids == null || ids.Length == 0)
                return;

            PruneDestroyedViews();

            foreach (var id in ids)
            {
                if (views.TryGetValue(id, out var view) && view != null)
                {
                    if (view.IgnoreHideAll)
                        Logs.Warn($"[ViewsManager] Hiding a view ('{id}') that is marked as IgnoreHideAll.");

                    view.Hide(instantly);
                }
                else
                {
                    Logs.Warn($"[ViewsManager] View with Id '{id}' is not registered; nothing to hide.");
                }
            }
        }

        /// <summary>
        /// Shows a set of views by their IDs. Logs warnings for missing IDs.
        /// </summary>
        public static void ShowViewsById(bool instantly = false, params string[] ids)
        {
            if (ids == null || ids.Length == 0) return;

            PruneDestroyedViews();

            foreach (var id in ids)
            {
                if (views.TryGetValue(id, out var view) && view != null)
                    view.Show(instantly);
                else
                    Logs.Warn($"[ViewsManager] View with Id '{id}' is not registered; nothing to show.");
            }
        }

        private static void PruneDestroyedViews()
        {
            if (views.Count == 0)
                return;

            List<string> destroyedViewIds = null;

            foreach (var entry in views)
            {
                if (entry.Value == null)
                {
                    destroyedViewIds ??= new List<string>();
                    destroyedViewIds.Add(entry.Key);
                }
            }

            if (destroyedViewIds == null)
                return;

            foreach (string id in destroyedViewIds)
            {
                views.Remove(id);
            }
        }
    }
}
