using System.Collections;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class NoticeItem : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private TMP_Text lableText;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetLable(string value)
        {
            lableText.text = value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        public virtual void WaitAndHide(float time)
        {
            StopCoroutine(Hide(time));
            StartCoroutine(Hide(time));
        }

        protected virtual IEnumerator Hide(float time)
        {
            yield return new WaitForSeconds(time);
            gameObject.SetActive(false);
        }
    }
}
