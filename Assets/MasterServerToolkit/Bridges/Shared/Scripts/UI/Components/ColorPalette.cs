using MasterServerToolkit.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MasterServerToolkit.Games
{
    public class ColorPalette : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        public Color[] colors = new Color[]
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.white,
            Color.yellow,
            Color.cyan,
            Color.magenta,
            Color.grey,
        };

        [Header("Components"), SerializeField]
        private Toggle togglePrefab;
        [SerializeField]
        private RectTransform container;

        public UnityEvent<Color> OnColorChangeEvent;

        #endregion

        private List<Toggle> toggles = new List<Toggle>();

        private void Start()
        {
            DrawPalette();
        }

        private void DrawPalette()
        {
            togglePrefab.gameObject.SetActive(true);

            toggles.Clear();
            container.RemoveChildren(togglePrefab.transform);

            for (int i = 0; i < colors.Length; i++)
            {
                var toggle = Instantiate(togglePrefab, container, false);
                toggle.transform.Find("Background").GetComponent<Image>().color = colors[i];

                var color = colors[i];

                toggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn)
                        OnColorChangeEvent?.Invoke(color);
                });

                toggles.Add(toggle);
            }

            togglePrefab.gameObject.SetActive(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public void SelectColor(string value)
        {
            value = !value.StartsWith("#") ? $"#{value}" : value;

            if (ColorUtility.TryParseHtmlString(value, out Color color))
                SelectColor(color);
            else
                Debug.LogError($"{value} is invalid color value");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="color"></param>
        public void SelectColor(Color color)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i] == color)
                {
                    toggles[i].isOn = true;
                }
            }
        }
    }
}