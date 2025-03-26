using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(Toggle), typeof(AudioSource))]
    public class UIToggleSound : MonoBehaviour
    {
        [Header("Audio"), SerializeField]
        protected AudioClip onClip;

        [Header("Components"), SerializeField]
        protected AudioSource audioSource;
        [SerializeField]
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
            if (isOn && audioSource != null && onClip != null)
                audioSource.PlayOneShot(onClip);
        }
    } 
}
