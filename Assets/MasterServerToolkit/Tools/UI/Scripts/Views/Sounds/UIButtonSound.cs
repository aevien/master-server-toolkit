using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(Button), typeof(AudioSource))]
    public class UIButtonSound : MonoBehaviour
    {
        [Header("Audio"), SerializeField]
        [Tooltip("Optional one-shot clip played when the assigned button is clicked. Leave empty to keep the button silent.")]
        protected AudioClip clickClip;

        [Header("Components"), SerializeField]
        [Tooltip("AudioSource used for the click one-shot. OnValidate assigns the AudioSource on this GameObject when empty; it must be enabled and active to play.")]
        protected AudioSource audioSource;
        [SerializeField]
        [Tooltip("Button whose onClick event triggers the sound. OnValidate assigns the Button on this GameObject when empty.")]
        protected Button button;

        private void OnValidate()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void Awake()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (CanPlay(audioSource) && clickClip != null)
                audioSource.PlayOneShot(clickClip);
        }

        private static bool CanPlay(AudioSource source)
        {
            return source != null && source.enabled && source.gameObject.activeInHierarchy;
        }
    }
}
