using UnityEngine;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(UIView), typeof(AudioSource))]
    public class UIViewSound : MonoBehaviour, IUIViewComponent
    {
        [Header("Audio"), SerializeField]
        [Tooltip("Optional one-shot clip played when the owner UIView starts showing. Leave empty for no show sound.")]
        protected AudioClip showClip;
        [SerializeField]
        [Tooltip("Optional one-shot clip played when the owner UIView starts hiding. Leave empty for no hide sound.")]
        protected AudioClip hideClip;

        [Header("Components"), SerializeField]
        [Tooltip("AudioSource used for show and hide one-shots. OnValidate assigns the source on this GameObject and forces 2D playback with no Doppler effect.")]
        protected AudioSource audioSource;

        private void Awake()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
        }

        public void OnOwnerShow(IUIView owner)
        {
            if (CanPlay(audioSource) && showClip != null)
                audioSource.PlayOneShot(showClip);
        }

        public void OnOwnerHide(IUIView owner)
        {
            if (CanPlay(audioSource) && hideClip != null)
                audioSource.PlayOneShot(hideClip);
        }

        private static bool CanPlay(AudioSource source)
        {
            return source != null && source.enabled && source.gameObject.activeInHierarchy;
        }
    }
}
