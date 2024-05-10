using MasterServerToolkit.Utils;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class UIProperty : MonoBehaviour
    {
        public enum UIPropertyValueFormat { F0, F1, F2, F3, F4, F5 }

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
        protected Color minColor = Color.red;
        [SerializeField]
        protected Color maxColor = Color.green;

        [Header("Settings"), SerializeField]
        protected string id = "propertyId";
        [SerializeField]
        protected float minValue = 0f;
        [SerializeField]
        protected float currentValue = 50f;
        [SerializeField]
        protected float maxValue = float.MaxValue;
        [SerializeField, Range(1f, 10f)]
        protected float progressSpeed = 1f;
        [SerializeField]
        protected bool smoothValue = true;
        [SerializeField]
        protected string lable = "";
        [SerializeField]
        protected UIPropertyValueFormat formatValue = UIPropertyValueFormat.F1;
        [SerializeField]
        protected bool invertValue = false;

        [Header("Editor Settings"), SerializeField]
        protected bool useValue = true;
        [SerializeField]
        protected bool useLable = true;
        [SerializeField]
        protected bool useIcon = true;
        [SerializeField]
        protected bool useProgress = true;
        [SerializeField]
        protected bool useColors = true;

        #endregion

        private float currentProgressValue = 0f;
        private float lastTargetProgressValue = 0f;
        private float targetProgressValue = 0f;
        private TweenerActionInfo tweenerAction;

        public string Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
            }
        }

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

        private void OnValidate()
        {
            SetMin(minValue);
            SetMax(maxValue);
            SetValue(currentValue);
            Update();

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

            if (lableText)
            {
                lableText.gameObject.SetActive(useLable);
            }

            if (iconImage)
            {
                iconImage.gameObject.SetActive(useIcon);
            }

            if (progressBar)
            {
                progressBar.gameObject.SetActive(useProgress);
            }
        }

        protected virtual void Awake()
        {
            SetMin(minValue);
            SetMax(maxValue);
            SetValue(currentValue);
        }

        protected virtual void Update()
        {
            if (minValue < maxValue)
            {
                if (progressBar && progressBar.isActiveAndEnabled)
                {
                    progressBar.fillAmount = currentProgressValue;
                    progressBar.color = Color.Lerp(minColor, maxColor, currentProgressValue);
                }

                if (useColors && iconImage)
                {
                    iconImage.color = Color.Lerp(minColor, maxColor, currentProgressValue);
                }

                if (valueText)
                    valueText.text = (currentProgressValue * maxValue).ToString(formatValue.ToString());
            }
        }

        public void SetMin(float value)
        {
            minValue = value;

            if (minValue > maxValue)
                maxValue = minValue;

            currentProgressValue = 0f;
            targetProgressValue = 0f;
        }

        public void SetMax(float value)
        {
            maxValue = value;

            if (minValue > maxValue)
                maxValue = minValue;

            currentProgressValue = 0f;
            targetProgressValue = 0f;
        }

        /// <summary>
        /// Sets current value of progress
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(float value)
        {
            if (minValue == maxValue) return;

            currentValue = Mathf.Clamp(value, minValue, maxValue);

            float totalDifference;
            float currentDifference;

            if (invertValue)
            {
                totalDifference = Mathf.Abs(minValue - maxValue);
                currentDifference = Mathf.Abs(currentValue - maxValue);
            }
            else
            {
                totalDifference = Mathf.Abs(minValue - maxValue);
                currentDifference = Mathf.Abs(minValue - currentValue);
            }

            targetProgressValue = currentDifference / totalDifference;

            if (smoothValue && Application.isPlaying && lastTargetProgressValue != targetProgressValue)
            {
                lastTargetProgressValue = targetProgressValue;

                tweenerAction?.Cancel();

                tweenerAction = Tweener.Float(currentProgressValue, targetProgressValue, progressSpeed, (newValue) =>
                {
                    currentProgressValue = newValue;
                });
            }
            else
            {
                currentProgressValue = targetProgressValue;
            }
        }
    }
}
