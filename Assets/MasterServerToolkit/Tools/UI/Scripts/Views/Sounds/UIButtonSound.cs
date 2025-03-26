using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    [RequireComponent(typeof(Button), typeof(AudioSource))]
    public class UIButtonSound : MonoBehaviour
    {
        [Header("Audio"), SerializeField]
        protected AudioClip clickClip;

        [Header("Components"), SerializeField]
        protected AudioSource audioSource;
        [SerializeField]
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
            if (audioSource != null && clickClip != null)
                audioSource.PlayOneShot(clickClip);
        }
    }
}
