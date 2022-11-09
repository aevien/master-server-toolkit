using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class LocalizeText : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private TMP_Text lableText;
        [SerializeField]
        private string localizationKey = "localizationKey";

        #endregion

        private void Awake()
        {
            UpdateLocalization();
            Mst.Localization.LanguageChangedEvent += Localization_OnLanguageChangedEventHandler;
        }

        private void OnDestroy()
        {
            Mst.Localization.LanguageChangedEvent -= Localization_OnLanguageChangedEventHandler;
        }

        private void Localization_OnLanguageChangedEventHandler(string language)
        {
            UpdateLocalization();
        }

        private void UpdateLocalization()
        {
            if (lableText != null)
            {
                lableText.text = Mst.Localization[localizationKey];
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
