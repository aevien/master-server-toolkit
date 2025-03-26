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
        private TextMeshProUGUI messageOutput;

        public UnityEvent<string> OnMessage;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public virtual void OutputMessage(string message)
        {
            messageOutput.text = message;

            if (!string.IsNullOrEmpty(message))
            {
                OnMessage?.Invoke(message);
            }
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        public virtual void WaitAndHide(float time)
        {
            StopCoroutine(HideCoroutine(time));
            StartCoroutine(HideCoroutine(time));
        }

        protected virtual IEnumerator HideCoroutine(float time)
        {
            yield return new WaitForSeconds(time);
            Hide();
        }
    }
}
