using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.UI
{
    public class UIProgressBar : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Componetnts"), SerializeField]
        private TextMeshProUGUI statValue;
        [SerializeField]
        private Image icon;
        [SerializeField]
        private Slider progressBar;
        [SerializeField]
        private Image progressBarFill;

        [Header("Settings"), SerializeField]
        private Color progressBarColor = Color.white;

        #endregion

        private void OnValidate()
        {
            if (progressBarFill != null)
            {
                progressBarFill.color = progressBarColor;
            }
        }

        public void Set(string value, float progress)
        {
            statValue.text = value;
            progressBar.value = progress;
        }
    }
}
