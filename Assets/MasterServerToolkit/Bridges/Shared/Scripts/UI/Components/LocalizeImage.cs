using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace MasterServerToolkit.Bridges
{
    public class LocalizeImage : MonoBehaviour
    {
        [Serializable]
        public struct LocalizeImageLang
        {
            public string lang;
            public Sprite sprite;
        }

        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private Image image;
        [SerializeField]
        private LocalizeImageLang[] localizationKeys;

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
            if (image != null)
            {
                image.sprite = null;

                for (int i = 0; i < localizationKeys.Length; i++)
                {
                    if (localizationKeys[i].lang == Mst.Localization.Lang)
                    {
                        image.sprite = localizationKeys[i].sprite;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Sets new localizations key
        /// </summary>
        /// <param name="keys"></param>
        public void SetKeys(LocalizeImageLang[] keys)
        {
            localizationKeys = keys;
            UpdateLocalization();
        }
    }
}
