using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(Toggle), typeof(AudioSource))]
    public class UIToggleSound : MonoBehaviour
    {
        [Header("Audio"), SerializeField]
        [Tooltip("Optional one-shot clip played only when the assigned Toggle changes to On. Changes to Off are silent.")]
        protected AudioClip onClip;

        [Header("Components"), SerializeField]
        [Tooltip("AudioSource used for the toggle one-shot. OnValidate assigns the AudioSource on this GameObject when empty; it must be enabled and active to play.")]
        protected AudioSource audioSource;
        [SerializeField]
        [Tooltip("Toggle whose onValueChanged event triggers the sound when its value becomes On. OnValidate assigns the Toggle on this GameObject when empty.")]
        protected Toggle toggle;

        private void OnValidate()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (toggle == null)
            {
                toggle = GetComponent<Toggle>();
            }
        }

        private void Awake()
        {
            toggle.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDestroy()
        {
            toggle.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(bool isOn)
        {
            if (isOn && CanPlay(audioSource) && onClip != null)
                audioSource.PlayOneShot(onClip);
        }

        private static bool CanPlay(AudioSource source)
        {
            return source != null && source.enabled && source.gameObject.activeInHierarchy;
        }
    } 
}
