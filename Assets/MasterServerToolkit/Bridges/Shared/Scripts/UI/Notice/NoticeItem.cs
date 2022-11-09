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
        private RectTransform rectTransform;

        /// <summary>
        /// 
        /// </summary>
        public Vector2 Size
        {
            get
            {
                if (!rectTransform) rectTransform = GetComponent<RectTransform>();
                return rectTransform.sizeDelta;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void SetLable(string value)
        {
            lableText.text = value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        public void WaitAndHide(float time)
        {
            StopCoroutine(Hide(time));
            StartCoroutine(Hide(time));
        }

        private IEnumerator Hide(float time)
        {
            yield return new WaitForSeconds(time);

            gameObject.SetActive(false);
        }
    }
}
