using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class LocalizeUIButton : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        private UIButton button;
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
            if (button != null)
            {
                button.SetLable(Mst.Localization[localizationKey]);
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