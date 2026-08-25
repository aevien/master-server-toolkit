using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.Demos.BasicProfile
{
    public class ItemUI : UIProperty
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        [Tooltip("Required purchase or sell button. Its child TMP_Text receives the price label, and its click listeners are replaced when an offer is bound.")]
        private Button button;

        #endregion

        public void SetButtonLable(string value)
        {
            button.GetComponentInChildren<TMP_Text>().text = value;
        }

        public void OnClick(UnityAction callback)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }
    }
}
