using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Aevien.UI
{
    [RequireComponent(typeof(Button))]
    public class UIButton : MonoBehaviour
    {
        private Button button;

        [Header("Components"), SerializeField]
        private TextMeshProUGUI lableText;
        [SerializeField]
        private string lableValue = "Click me";

        public bool IsInteractable => button.interactable;

        private void Awake()
        {
            if (!button)
                button = GetComponent<Button>();
        }

        private void OnValidate()
        {
            SetLable(lableValue);
        }

        public void SetLable(string lableValue)
        {
            if (lableText)
                lableText.text = lableValue;
        }

        public void SetInteractable(bool value)
        {
            button.interactable = value;
        }

        public void AddOnClickListener(UnityAction callback, bool removeIfExists = true)
        {
            if (removeIfExists) RemoveOnClickListener(callback);
            button.onClick.AddListener(callback);
        }

        public void RemoveOnClickListener(UnityAction callback)
        {
            button.onClick.RemoveListener(callback);
        }

        public void RemoveAllOnClickListeners()
        {
            button.onClick.RemoveAllListeners();
        }
    }
}