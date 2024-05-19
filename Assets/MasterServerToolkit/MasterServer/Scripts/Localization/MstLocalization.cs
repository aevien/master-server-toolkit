using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MasterServerToolkit.Localization
{
    public class MstLocalization
    {
        private string selectedLang = "en";
        private readonly Dictionary<string, Dictionary<string, string>> _localization = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// Current selected language
        /// </summary>
        public string Lang
        {
            get
            {
                return selectedLang;
            }
            set
            {
                string prevLanguage = selectedLang;
                selectedLang = !string.IsNullOrEmpty(value) ? value.ToLower() : "en";

                if (prevLanguage != selectedLang)
                {
                    LanguageChangedEvent?.Invoke(selectedLang);
                }
            }
        }

        /// <summary>
        /// Returns translated string by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                if (_localization.TryGetValue(selectedLang, out var dictionary) && dictionary != null)
                {
                    if (dictionary.ContainsKey(key) && !string.IsNullOrEmpty(dictionary[key]))
                    {
                        return dictionary[key];
                    }
                    else
                    {
                        return key;
                    }
                }
                else
                {
                    return key;
                }
            }
        }

        /// <summary>
        /// Invoked when the language changes
        /// </summary>
        public event Action<string> LanguageChangedEvent;

        public MstLocalization()
        {
            selectedLang = Mst.Args.AsString(Mst.Args.Names.DefaultLanguage, selectedLang);
            _localization[selectedLang] = new Dictionary<string, string>();
            LoadLocalization();
        }

        private void LoadLocalization()
        {
            var localizationFile = Resources.Load<TextAsset>("Localization/localization");
            var customLocalizationFile = Resources.Load<TextAsset>("Localization/custom_localization");

            ParseLocalization(localizationFile);
            ParseLocalization(customLocalizationFile);
        }

        private void ParseLocalization(TextAsset localizationFile)
        {
            try
            {
                if (localizationFile != null && !string.IsNullOrEmpty(localizationFile.text))
                {
                    string nPattern = @"\n+";
                    string sPattern = @"\s+";

                    List<string> rows = localizationFile.text.Split("\n", StringSplitOptions.RemoveEmptyEntries)
                        .Where(r => !r.StartsWith("#") && !r.StartsWith(";"))
                        .Select(r =>
                        {
                            var cleanRow = Regex.Replace(r, nPattern, "");
                            cleanRow = Regex.Replace(cleanRow, sPattern, " ");
                            return cleanRow;
                        })
                        .ToList();

                    List<string> langCols = rows[0].Trim().Split(";").ToList();

                    for (int i = 1; i < rows.Count; i++)
                    {
                        string[] valueCols = rows[i].Split(";");

                        for (int j = 1; j < valueCols.Length; j++)
                        {
                            RegisterKey(langCols[j].Trim(), valueCols[0].Trim(), valueCols[j].Trim());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logs.Error("An error occurred during localization parsing");
                Logs.Error(e);
            }
        }

        /// <summary>
        /// Registers localization key-value by given language
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void RegisterKey(string lang, string key, string value)
        {
            if (string.IsNullOrEmpty(lang) || string.IsNullOrEmpty(key)) return;

            string langValue = lang.ToLower();

            if (!_localization.ContainsKey(langValue))
                _localization[langValue] = new Dictionary<string, string>();

            _localization[langValue][key] = value;
        }
    }
}