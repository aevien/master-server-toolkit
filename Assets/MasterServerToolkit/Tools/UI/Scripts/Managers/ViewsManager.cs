using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.UI
{
    public static class ViewsManager
    {
        private readonly static Dictionary<string, IUIView> views = new Dictionary<string, IUIView>();

        public static bool IsInputBlocked => views.Values.Where(i => i != null && i.BlockInput && i.IsVisible).Count() > 0;

        public static void Register(string viewId, IUIView view)
        {
            views[viewId] = view;
        }

        public static T GetView<T>(string viewId) where T : class, IUIView
        {
            if (views.TryGetValue(viewId, out IUIView view))
            {
                return (T)view;
            }
            else
            {
                Debug.LogError($"View with Id {viewId} is not registered");
                return null;
            }
        }

        public static void Show(string viewId)
        {
            if (views.TryGetValue(viewId, out IUIView view))
            {
                view.Show();
            }
            else
            {
                Debug.LogError($"View with Id {viewId} is not registered");
            }
        }

        public static void Hide(string viewId)
        {
            if (views.TryGetValue(viewId, out IUIView view))
            {
                view.Hide();
            }
            else
            {
                Debug.LogError($"View with Id {viewId} is not registered");
            }
        }

        public static void NotifyNoViewFound(params string[] viewIds)
        {
            if (viewIds != null && viewIds.Length > 0)
            {
                foreach (string id in viewIds)
                {
                    Debug.LogError($"You are trying to use {id}. But it is not found in scene. Please add {id} to scene");
                }
            }
        }

        public static void HideAllViews(bool instantly = false)
        {
            foreach (var view in views.Values)
            {
                if (!view.IgnoreHideAll)
                    view.Hide(instantly);
            }
        }

        public static void HideViewsByName(bool instantly = false, params string[] names)
        {
            if (names.Length == 0) return;

            foreach (string n in names)
            {
                if (views.TryGetValue(n, out IUIView view))
                {
                    if (view.IgnoreHideAll)
                    {
                        Debug.LogWarning("You closed view that is marked as IgnoreHideAll");
                    }

                    view.Hide(instantly);
                }
            }
        }
    }
}