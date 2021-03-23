using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    public interface IUIViewTweener
    {
        IUIView UIView { get; set; }
        void OnFinished(UnityAction callback);
        void PlayShow();
        void PlayHide();
    }
}