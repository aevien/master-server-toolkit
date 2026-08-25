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
        [Tooltip("Image used to display the property's icon. Leave empty when the widget does not show an icon.")]
        protected Image iconImage;
        [SerializeField]
        [Tooltip("Text component used to display the property label.")]
        protected TextMeshProUGUI lableText;
        [SerializeField]
        [Tooltip("Text component used to display the formatted current value.")]
        protected TextMeshProUGUI valueText;
        [SerializeField]
        [Tooltip("Filled Image used as the property's progress indicator. Its fill amount is calculated from the configured minimum, current, and maximum values.")]
        protected Image progressBar;
        [SerializeField]
        [Tooltip("Color used for the icon and progress bar when the normalized value is at its minimum.")]
        protected Color minColor = Color.red;
        [SerializeField]
        [Tooltip("Color used for the icon and progress bar when the normalized value is at its maximum.")]
        protected Color maxColor = Color.green;

        [Header("Settings"), SerializeField]
        [Tooltip("Stable identifier used by callers to find or distinguish this property widget.")]
        protected string id = "propertyId";
        [SerializeField]
        [Tooltip("Lowest accepted value. Values passed to SetValue are clamped to this boundary.")]
        protected float minValue = 0f;
        [SerializeField]
        [Tooltip("Value shown by the widget. It is clamped between Min Value and Max Value.")]
        protected float currentValue = 50f;
        [SerializeField]
        [Tooltip("Highest accepted value. It must be greater than Min Value for the progress and value text to update.")]
        protected float maxValue = float.MaxValue;
        [SerializeField]
        [Tooltip("Label displayed by the assigned label text component.")]
        protected string lable = "";
        [SerializeField]
        [Tooltip("Number of fractional digits used when formatting the displayed value, from F0 through F5.")]
        protected UIPropertyValueFormat formatValue = UIPropertyValueFormat.F1;
        [SerializeField]
        [Tooltip("When enabled, the progress runs from full at Min Value to empty at Max Value.")]
        protected bool invertValue = false;

        [Header("Editor Settings"), SerializeField]
        [Tooltip("Shows the value text component when one is assigned.")]
        protected bool useValue = true;
        [SerializeField]
        [Tooltip("Shows the label text component when one is assigned.")]
        protected bool useLable = true;
        [SerializeField]
        [Tooltip("Shows the icon image when one is assigned.")]
        protected bool useIcon = true;
        [SerializeField]
        [Tooltip("Shows the progress image when one is assigned.")]
        protected bool useProgress = true;
        [SerializeField]
        [Tooltip("Applies the Min Color to Max Color gradient to the icon. The progress bar always uses this gradient.")]
        protected bool useColors = true;

        #endregion

        private float currentProgressValue = 0f;

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
                    valueText.text = currentValue.ToString(formatValue.ToString());
            }
        }

        public void SetMin(float value)
        {
            minValue = value;

            if (minValue > maxValue)
                maxValue = minValue;

            currentProgressValue = 0f;
        }

        public void SetMax(float value)
        {
            maxValue = value;

            if (minValue > maxValue)
                maxValue = minValue;

            currentProgressValue = 0f;
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

            currentProgressValue = currentDifference / totalDifference;
        }
    }
}
