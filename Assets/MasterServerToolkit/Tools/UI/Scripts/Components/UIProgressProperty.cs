using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class UIProgressProperty : UIProperty
    {
        /// <summary>
        /// Current value of progress
        /// </summary>
        protected float targetValue = 0f;

        [Header("Progress Components"), SerializeField]
        [Tooltip("Filled Image used for the animated progress display. If the assigned image is not Filled, the component configures it as a horizontal Filled image at runtime.")]
        private Image progressImage;

        [Header("Progress Settings"), SerializeField]
        [Tooltip("Maximum accepted progress value. Values passed to SetProgressValue are clamped from 0 to this value. Values of 0 or less are corrected to 1 in the Inspector.")]
        protected float progressMaxValue;

        protected override void Awake()
        {
            base.Awake();

            if (progressImage && progressImage.type != Image.Type.Filled)
            {
                progressImage.type = Image.Type.Filled;
                progressImage.fillMethod = Image.FillMethod.Horizontal;
            }

            targetValue = progressMaxValue;
        }

        protected override void Update()
        {
            base.Update();

            if (progressImage)
            {
                progressImage.fillAmount = Mathf.Lerp(progressImage.fillAmount, targetValue, Time.deltaTime * 2f);
            }
        }

        protected virtual void OnValidate()
        {
            if (progressMaxValue <= 0)
            {
                progressMaxValue = 1;
            }
        }

        /// <summary>
        /// Set new value of progress
        /// </summary>
        /// <param name="newValue"></param>
        public void SetProgressValue(float newValue)
        {
            float rawValue = Mathf.Clamp(newValue, 0f, progressMaxValue);
            Lable = $"{Mathf.RoundToInt(rawValue)}/{Mathf.RoundToInt(progressMaxValue)}";
            targetValue = (rawValue * 100f / progressMaxValue) / 100f;
        }
    }
}
