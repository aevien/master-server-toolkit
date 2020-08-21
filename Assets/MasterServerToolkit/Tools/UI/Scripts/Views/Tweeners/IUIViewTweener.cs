using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Aevien.UI
{
    public interface IUIViewTweener
    {
        IUIView UIView { get; set; }
        void OnFinished(UnityAction callback);
        void PlayShow();
        void PlayHide();
    }
}