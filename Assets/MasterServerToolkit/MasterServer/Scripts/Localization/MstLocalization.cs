using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Localization
{
    public class MstLocalization
    {
        private string _selectedLang = "en";
        private Dictionary<string, Dictionary<string, string>> _localization = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Current selected language
        /// </summary>
        public string Lang => _selectedLang;

        /// <summary>
        /// Returns translated string by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                if (_localization.TryGetValue(_selectedLang, out var dictionary) && dictionary != null && dictionary.ContainsKey(key))
                {
                    return dictionary[key];
                }
                else
                {
                    return key;
                }
            }
        }

        public MstLocalization()
        {
            _localization[_selectedLang] = new Dictionary<string, string>();

            LoadLocalization();
        }

        private void LoadLocalization()
        {
            var localizationFile = Resources.Load<TextAsset>("Localization/localization");

            if (localizationFile != null && !string.IsNullOrEmpty(localizationFile.text))
            {
                string[] rows = localizationFile.text.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                string[] langCols = rows[0].Split(";", StringSplitOptions.RemoveEmptyEntries);

                for (int i = 1; i < rows.Length; i++)
                {
                    string[] valueCols = rows[i].Trim().Split(";", StringSplitOptions.RemoveEmptyEntries);

                    for (int j = 1; j < valueCols.Length; j++)
                    {
                        RegisterKey(langCols[j].Trim(), valueCols[0].Trim(), valueCols[j].Trim());
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lang"></param>
        public void SelectLang(string lang)
        {
            _selectedLang = lang;
        }

        /// <summary>
        /// Registers localization key-value by given language
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void RegisterKey(string lang, string key, string value)
        {
            if (!_localization.ContainsKey(lang))
                _localization[lang] = new Dictionary<string, string>();

            _localization[lang][key] = value;
        }
    }
}