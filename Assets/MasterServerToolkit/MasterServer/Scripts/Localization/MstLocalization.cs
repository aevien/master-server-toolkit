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
        #region Constants and Fields

        private const string Comments = "#";
        private const string RowsSeparator = "\n";
        private const string ColsSeparator = ";";
        private const string DefaultUndefinedValue = "undefined";
        private const string PreferredDefaultLanguage = "en";

        // Pre-compiled regex patterns for better performance
        private static readonly Regex NewlinePattern = new Regex(@"\n+", RegexOptions.Compiled);
        private static readonly Regex WhitespacePattern = new Regex(@"\s+", RegexOptions.Compiled);

        private string selectedLang = PreferredDefaultLanguage;
        private readonly Dictionary<string, Dictionary<string, string>> _localization = new Dictionary<string, Dictionary<string, string>>();

        #endregion

        #region Properties

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
                var newLanguage = DetermineValidLanguage(value);

                if (selectedLang != newLanguage)
                {
                    selectedLang = newLanguage;
                    LanguageChangedEvent?.Invoke(selectedLang);
                }
            }
        }

        /// <summary>
        /// Returns translated string by key with fallback mechanism
        /// </summary>
        /// <param name="key">Translation key</param>
        /// <returns>Translated string or fallback value</returns>
        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                    return DefaultUndefinedValue;

                // Try to find translation in current language
                if (TryGetTranslation(selectedLang, key, out string translation))
                    return translation;

                // Fallback to preferred default language if current language is different
                if (selectedLang != PreferredDefaultLanguage &&
                    TryGetTranslation(PreferredDefaultLanguage, key, out translation))
                    return translation;

                // Fallback to any available language with the key
                foreach (var langDict in _localization.Values)
                {
                    if (langDict.TryGetValue(key, out translation) && !string.IsNullOrEmpty(translation))
                        return translation;
                }

                // Final fallback: return the key itself
                return key;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Invoked when the language changes
        /// </summary>
        public event Action<string> LanguageChangedEvent;

        #endregion

        #region Constructor

        public MstLocalization()
        {
            // Get initial language from command line args
            var initialLanguage = Mst.Args.AsString(Mst.Args.Names.DefaultLanguage, PreferredDefaultLanguage);

            // Load localization data first
            LoadLocalization();

            // Set the language after loading (this will trigger validation and fallback if needed)
            Lang = initialLanguage;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers localization key-value by given language
        /// </summary>
        /// <param name="lang">Language code</param>
        /// <param name="key">Translation key</param>
        /// <param name="value">Translation value</param>
        public void RegisterKey(string lang, string key, string value)
        {
            if (string.IsNullOrEmpty(lang) || string.IsNullOrEmpty(key))
                return;

            string langValue = lang.ToLower().Trim();

            if (!_localization.ContainsKey(langValue))
                _localization[langValue] = new Dictionary<string, string>();

            _localization[langValue][key] = value ?? string.Empty;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Determines a valid language based on available localizations
        /// </summary>
        /// <param name="requestedLanguage">Requested language code</param>
        /// <returns>Valid language code</returns>
        private string DetermineValidLanguage(string requestedLanguage)
        {
            // Check if requested language is available and has content
            if (!string.IsNullOrEmpty(requestedLanguage))
            {
                var cleanLanguage = requestedLanguage.ToLower().Trim();
                if (_localization.ContainsKey(cleanLanguage) && _localization[cleanLanguage].Count > 0)
                    return cleanLanguage;
            }

            // Fallback to preferred default language if available and has content
            if (_localization.ContainsKey(PreferredDefaultLanguage) &&
                _localization[PreferredDefaultLanguage].Count > 0)
                return PreferredDefaultLanguage;

            // Fallback to first available language with content
            var firstAvailableLanguage = _localization
                .FirstOrDefault(kvp => kvp.Value.Count > 0).Key;

            return firstAvailableLanguage ?? PreferredDefaultLanguage;
        }

        /// <summary>
        /// Attempts to get translation for specific language and key
        /// </summary>
        /// <param name="language">Language code</param>
        /// <param name="key">Translation key</param>
        /// <param name="translation">Output translation if found</param>
        /// <returns>True if translation was found</returns>
        private bool TryGetTranslation(string language, string key, out string translation)
        {
            translation = null;
            return _localization.TryGetValue(language, out var dictionary) &&
                   dictionary?.TryGetValue(key, out translation) == true &&
                   !string.IsNullOrEmpty(translation);
        }

        /// <summary>
        /// Loads localization from resource files
        /// </summary>
        private void LoadLocalization()
        {
            var localizationFile = Resources.Load<TextAsset>("Localization/localization");
            var customLocalizationFile = Resources.Load<TextAsset>("Localization/custom_localization");

            ParseLocalization(localizationFile, "main localization");
            ParseLocalization(customLocalizationFile, "custom localization");
        }

        /// <summary>
        /// Parses localization data from TextAsset with improved error handling
        /// </summary>
        /// <param name="localizationFile">TextAsset containing localization data</param>
        /// <param name="fileName">File name for logging purposes</param>
        private void ParseLocalization(TextAsset localizationFile, string fileName)
        {
            if (localizationFile == null || string.IsNullOrEmpty(localizationFile.text))
            {
                Logs.Warn($"Localization file '{fileName}' is empty or missing");
                return;
            }

            try
            {
                var cleanRows = PrepareRows(localizationFile.text);

                if (cleanRows.Count == 0)
                {
                    Logs.Warn($"No valid rows found in {fileName} file");
                    return;
                }

                var languageHeaders = ParseLanguageHeaders(cleanRows[0]);
                if (languageHeaders.Length < 2) // Need at least key column + one language column
                {
                    Logs.Error($"Invalid header structure in {fileName} file. Expected at least 2 columns.");
                    return;
                }

                ParseDataRows(cleanRows.Skip(1), languageHeaders, fileName);

                Logs.Info($"Successfully loaded {fileName} with {languageHeaders.Length - 1} languages");
            }
            catch (FormatException e)
            {
                Logs.Error($"Invalid {fileName} file format: {e.Message}");
            }
            catch (IndexOutOfRangeException e)
            {
                Logs.Error($"Structure mismatch in {fileName} file: {e.Message}");
            }
            catch (Exception e)
            {
                Logs.Error($"Unexpected error during {fileName} parsing: {e.Message}");
                Logs.Error(e);
            }
        }

        /// <summary>
        /// Prepares and cleans rows from raw localization text
        /// </summary>
        /// <param name="rawText">Raw text from localization file</param>
        /// <returns>List of cleaned rows</returns>
        private List<string> PrepareRows(string rawText)
        {
            return rawText.Split(RowsSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(r => !r.StartsWith(Comments) && !r.StartsWith(ColsSeparator))
                .Select(CleanRow)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();
        }

        /// <summary>
        /// Cleans individual row from extra whitespace and newlines
        /// </summary>
        /// <param name="row">Raw row string</param>
        /// <returns>Cleaned row string</returns>
        private string CleanRow(string row)
        {
            var cleanRow = NewlinePattern.Replace(row, "");
            cleanRow = WhitespacePattern.Replace(cleanRow, " ");
            return cleanRow.Trim();
        }

        /// <summary>
        /// Parses language headers from the first row
        /// </summary>
        /// <param name="headerRow">First row containing language codes</param>
        /// <returns>Array of language headers</returns>
        private string[] ParseLanguageHeaders(string headerRow)
        {
            return headerRow.Split(ColsSeparator)
                .Select(header => header.Trim())
                .ToArray();
        }

        /// <summary>
        /// Parses data rows and registers translations
        /// </summary>
        /// <param name="dataRows">Enumerable of data rows</param>
        /// <param name="languageHeaders">Array of language headers</param>
        /// <param name="fileName">File name for logging</param>
        private void ParseDataRows(IEnumerable<string> dataRows, string[] languageHeaders, string fileName)
        {
            int rowIndex = 1; // Start from 1 since 0 is header row

            foreach (var row in dataRows)
            {
                rowIndex++;

                try
                {
                    var valueCols = row.Split(ColsSeparator);

                    // Validate row structure
                    if (valueCols.Length > languageHeaders.Length)
                    {
                        Logs.Warn($"Row {rowIndex} in {fileName} has more columns than expected. " +
                                    $"Extra separator '{ColsSeparator}' might be present in the text.");
                        continue;
                    }

                    if (valueCols.Length < 2) // Need at least key + one translation
                    {
                        Logs.Warn($"Row {rowIndex} in {fileName} has insufficient columns. Skipping.");
                        continue;
                    }

                    var key = valueCols[0].Trim();
                    if (string.IsNullOrEmpty(key))
                    {
                        Logs.Warn($"Row {rowIndex} in {fileName} has empty key. Skipping.");
                        continue;
                    }

                    // Register translations for each available language column
                    for (int j = 1; j < Math.Min(valueCols.Length, languageHeaders.Length); j++)
                    {
                        var language = languageHeaders[j].Trim();
                        var translation = valueCols[j].Trim();

                        if (!string.IsNullOrEmpty(language))
                        {
                            RegisterKey(language, key, translation);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logs.Warn($"Error processing row {rowIndex} in {fileName}: {e.Message}");
                }
            }
        }

        #endregion
    }
}