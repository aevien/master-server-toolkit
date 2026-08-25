using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class UIProgressBar : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Componetnts"), SerializeField]
        [Tooltip("Optional TextMeshPro component used to display the current progress percentage.")]
        private TextMeshProUGUI valueLable;
        [SerializeField]
        [Tooltip("Optional Image whose color is updated to Progress Bar Color during Inspector validation.")]
        private Image fill;
        [SerializeField]
        [Tooltip("Slider that receives the normalized current value. This reference is required before Set(...) is called.")]
        private Slider slider;

        [Header("Settings"), SerializeField]
        [Tooltip("Color applied to the assigned Fill image in the Inspector.")]
        private Color progressBarColor = Color.white;

        #endregion

        private float value = 0f;

        private void OnValidate()
        {
            if (fill != null)
                fill.color = progressBarColor;
        }

        public void Set(float currentValue, float maxValue)
        {
            value = currentValue / maxValue;
            slider.value = value;

            if(valueLable != null)
                valueLable.text = $"{value * 100f:F0}";
        }
    }
}
