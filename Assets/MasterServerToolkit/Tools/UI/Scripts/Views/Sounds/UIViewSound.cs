using UnityEngine;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(UIView), typeof(AudioSource))]
    public class UIViewSound : MonoBehaviour, IUIViewComponent
    {
        [Header("Audio"), SerializeField]
        protected AudioClip showClip;
        [SerializeField]
        protected AudioClip hideClip;

        [Header("Components"), SerializeField]
        protected AudioSource audioSource;

        private void OnValidate()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        public void OnOwnerShow(IUIView owner)
        {
            if (audioSource != null && showClip != null)
                audioSource.PlayOneShot(showClip);
        }

        public void OnOwnerHide(IUIView owner)
        {
            if (audioSource != null && hideClip != null)
                audioSource.PlayOneShot(hideClip);
        }
    }
}
