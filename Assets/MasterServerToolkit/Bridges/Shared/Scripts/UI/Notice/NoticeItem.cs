using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class NoticeItem : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Required text label that displays the notice message while this item is active.")]
        private TextMeshProUGUI messageOutput;

        [Tooltip("Invoked with the notice text when a non-empty message is shown.")]
        public UnityEvent<string> OnMessage;

        #endregion

        public virtual void Show(string message, float time)
        {
            gameObject.SetActive(true);
            messageOutput.text = message;

            if (!string.IsNullOrEmpty(message))
                OnMessage?.Invoke(message);

            StopCoroutine(HideCoroutine(time));
            StartCoroutine(HideCoroutine(time));
        }

        public virtual void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        protected virtual IEnumerator HideCoroutine(float time)
        {
            yield return new WaitForSeconds(time);
            Hide();
        }
    }
}
