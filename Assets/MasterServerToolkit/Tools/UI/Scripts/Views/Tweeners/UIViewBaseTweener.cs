using MasterServerToolkit.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIViewBaseTweener : MonoBehaviour, IUIViewTweener
    {

#if LEAN_TWEEN
        private int tweenerId = -1;
#endif
        private UnityAction callback;
        private CanvasGroup canvasGroup;

#if LEAN_TWEEN
        [Header("Settings"), SerializeField]
        private float tweenTime = 0.3f;
#else
        [SerializeField]
        private HelpBox hpLeanTweenInfo = new HelpBox()
        {
            Text = "Use LEAN_TWEEN in Scripting Define Symbols to use Tweener effect. You also need LeanTween",
            Type = HelpBoxType.Warning
        };
#endif

        public IUIView UIView { get; set; }

        private void Awake()
        {
            if (!canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnFinished(UnityAction callback)
        {
            this.callback = callback;
        }

        public void PlayShow()
        {
#if LEAN_TWEEN
            if (LeanTween.isTweening(tweenerId))
            {
                LeanTween.cancel(tweenerId);
            }

            tweenerId = canvasGroup.LeanAlpha(1f, tweenTime).setOnComplete(() =>
            {
                callback?.Invoke();
            }).id;
#else
            callback?.Invoke();
#endif
        }

        public void PlayHide()
        {
#if LEAN_TWEEN
            if (LeanTween.isTweening(tweenerId))
            {
                LeanTween.cancel(tweenerId);
            }

            tweenerId = canvasGroup.LeanAlpha(0f, tweenTime).setOnComplete(() =>
            {
                callback?.Invoke();
            }).id;
#else
            callback?.Invoke();
#endif
        }
    }
}