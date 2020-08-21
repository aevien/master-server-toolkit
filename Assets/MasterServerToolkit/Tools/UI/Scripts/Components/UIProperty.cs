using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aevien.UI
{
    public class UIProperty : MonoBehaviour
    {
        [Header("Components"), SerializeField]
        protected Image iconImage;
        [SerializeField]
        protected TextMeshProUGUI lableText;

        public string Lable
        {
            get
            {
                return lableText != null ? lableText.text : string.Empty;
            }
            set
            {
                if (lableText)
                    lableText.text = value;
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
    }
}
