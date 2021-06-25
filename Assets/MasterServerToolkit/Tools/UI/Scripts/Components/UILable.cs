using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Aevien.UI
{
    public class UILable : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected TextMeshProUGUI lableText;

        [Header("Settings"), SerializeField]
        private string lable = "";

        #endregion

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(lable))
            {
                Lable = name;
            }
            else
            {
                Lable = lable;
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
    }
}