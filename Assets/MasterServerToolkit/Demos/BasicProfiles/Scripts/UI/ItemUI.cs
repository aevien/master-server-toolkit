using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class ItemUI : UIProperty
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
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