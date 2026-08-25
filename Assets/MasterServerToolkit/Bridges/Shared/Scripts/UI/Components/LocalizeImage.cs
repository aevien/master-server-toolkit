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
            [Tooltip("Language code matched exactly against Mst.Localization.Lang, for example en, ru, or tr.")]
            public string lang;
            [Tooltip("Sprite assigned to the target Image when this entry's language code is active.")]
            public Sprite sprite;
        }

        #region INSPECTOR

        [Header("Settings"), SerializeField]
        [Tooltip("Optional target Image. Its sprite is cleared before the component searches for a matching language entry.")]
        private Image image;
        [SerializeField]
        [Tooltip("Language-to-sprite mappings searched in array order. No match leaves the target Image without a sprite; assign an empty array rather than null.")]
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
