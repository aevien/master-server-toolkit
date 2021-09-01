using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aevien.UI
{
    public class UIProperty : MonoBehaviour
    {
        public enum UIPropertyValueFormat { F0, F1, F2, F3, F4, F5}

        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected Image iconImage;
        [SerializeField]
        protected TextMeshProUGUI lableText;
        [SerializeField]
        protected TextMeshProUGUI valueText;
        [SerializeField]
        protected Image progressBar;
        [SerializeField]
        protected Color startColor = Color.red;
        [SerializeField]
        protected Color maxColor = Color.green;

        [Header("Settings"), SerializeField]
        protected float startValue = 0f;
        [SerializeField]
        protected float currentValue = 50f;
        [SerializeField]
        protected float maxValue = 100f;
        [SerializeField, Range(1f, 10f)]
        protected float progressSpeed = 1f;
        [SerializeField]
        protected bool smoothValue = true;
        [SerializeField]
        protected string lable = "";
        [SerializeField]
        protected UIPropertyValueFormat formatValue = UIPropertyValueFormat.F1;
        [SerializeField]
        protected bool useValue = true;
        [SerializeField]
        protected bool useProgress = true;

        #endregion

        private float currentProgressValue = 0f;
        private float targetProgressValue = 0f;

        public string Lable
        {
            get
            {
                return lable;
            }
            set
            {
                lable = value;

                if (lableText)
                    lableText.text = lable;
            }
        }

        public Sprite Icon
        {
            get
            {
                return iconImage != null ? iconImage.sprite : null;
            }
            set
            {
                if (iconImage)
                    iconImage.sprite = value;
            }
        }

        private void Awake()
        {
            SetValues(startValue, currentValue, maxValue);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(lable))
            {
                Lable = "Lable";
            }
            else
            {
                Lable = lable;
            }

            if (valueText)
            {
                valueText.gameObject.SetActive(useValue);
            }

            if (progressBar)
            {
                progressBar.gameObject.SetActive(useProgress);
                SetValues(startValue, currentValue, maxValue);
                Update();
            }
        }

        private void Update()
        {
            if (startValue < maxValue)
            {
                if (smoothValue && Application.isPlaying)
                    currentProgressValue = Mathf.Lerp(currentProgressValue, targetProgressValue, Time.deltaTime * progressSpeed);
                else
                    currentProgressValue = targetProgressValue;

                if (progressBar && progressBar.isActiveAndEnabled)
                {
                    progressBar.fillAmount = currentProgressValue;
                    progressBar.color = Color.Lerp(startColor, maxColor, currentProgressValue);
                }

                if (valueText)
                    valueText.text = (currentProgressValue * maxValue).ToString(formatValue.ToString());
            }
        }

        /// <summary>
        /// Sets initial values
        /// </summary>
        /// <param name="start"></param>
        /// <param name="current"></param>
        /// <param name="max"></param>
        public void SetValues(float start, float current, float max)
        {
            startValue = start;
            currentValue = current;
            maxValue = max;

            SetValue(currentValue);
        }

        /// <summary>
        /// Sets current value of progress
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(float value)
        {
            if (startValue == maxValue) return;

            currentValue = Mathf.Clamp(value, startValue, maxValue);
            targetProgressValue = currentValue / maxValue;
        }
    }
}
