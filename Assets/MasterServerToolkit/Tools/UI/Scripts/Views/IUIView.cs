using UnityEngine;

namespace MasterServerToolkit.UI
{
    public interface IUIView
    {
        string Id { get; }
        RectTransform Rect { get; }
        bool IsVisible { get; }
        bool BlockInput { get; set; }
        bool IgnoreHideAll { get; set; }
        T ViewComponent<T>() where T : class, IUIViewComponent;
        T ChildComponent<T>(string childName) where T : Component;
        void Show(bool instantly = false);
        void Hide(bool instantly = false);
        void Toggle(bool instantly = false);
    }
}