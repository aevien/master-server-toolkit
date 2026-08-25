using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    public interface IUIViewTweener
    {
        IUIViewLayout UIViewLayout { get; set; }
        IUIViewTweener OnFinished(UnityAction callback);
        IUIViewTweener PlayShow();
        IUIViewTweener PlayHide();
        IUIViewTweener Cancel();
    }
}