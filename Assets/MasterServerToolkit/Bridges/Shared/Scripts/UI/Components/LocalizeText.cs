using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class LocalizeText : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private TextMeshProUGUI lableText;
        [SerializeField]
        private string localizationKey = "localizationKey";

        #endregion

        private void Awake()
        {
            Mst.Localization.LanguageChangedEvent += Localization_OnLanguageChangedEventHandler;
        }

        private void Start()
        {
            UpdateLocalization();
        }

        private void OnDestroy()
        {
            Mst.Localization.LanguageChangedEvent -= Localization_OnLanguageChangedEventHandler;
        }

        private void OnEnable()
        {
            UpdateLocalization();
        }

        private void Localization_OnLanguageChangedEventHandler(string language)
        {
            UpdateLocalization();
        }

        private void UpdateLocalization()
        {
            try
            {
                if (lableText != null)
                {
                    string text = Mst.Localization[localizationKey];
                    lableText.text = text;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Sets new localizations key
        /// </summary>
        /// <param name="key"></param>
        public void SetKey(string key)
        {
            localizationKey = key;
            UpdateLocalization();
        }
    }
}
