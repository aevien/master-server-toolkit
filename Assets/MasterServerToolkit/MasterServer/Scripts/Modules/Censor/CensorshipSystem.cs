using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public enum WordFileFormat
    {
        [Tooltip("Words separated by commas: word1,word2,word3")]
        CommaSeparated,
        [Tooltip("Each word on a new line")]
        LineByLine,
        [Tooltip("Automatic format detection")]
        AutoDetect
    }

    [Serializable]
    public class LanguageBadWords
    {
        [Header("Language Settings")]
        public string languageName = "Russian";

        [Header("Words File")]
        public TextAsset badWordsFile;

        [Header("File Format")]
        [Tooltip("Choose file format or use auto-detection")]
        public WordFileFormat fileFormat = WordFileFormat.AutoDetect;

        [Header("Options")]
        public bool isActive = true;

        [Range(1, 3)]
        [Tooltip("1 - Minor violations, 2 - Medium, 3 - Serious")]
        public int severityLevel = 2;

        [Space]
        [TextArea(2, 4)]
        public string description = "Word category description";
    }

    /// <summary>
    /// Сensorship manager with intelligent detection of masked words.
    /// Uses transliteration, normalization and multi-level analysis for effective chat moderation.
    /// </summary>
    public class CensorshipSystem
    {
        // Core data structures for word storage and metadata
        private HashSet<string> allBadWords = new HashSet<string>();
        private Dictionary<string, int> wordsWithSeverity = new Dictionary<string, int>();
        private Dictionary<string, string> wordsWithLanguage = new Dictionary<string, string>();
        private HashSet<string> normalizedBadWords = new HashSet<string>();
        private Dictionary<string, string> normalizedToOriginalMap = new Dictionary<string, string>();

        // System configuration flags
        private bool isInitialized = false;
        private bool logLoadingDetails = true;
        private bool enableSeveritySystem = true;
        private bool enableAdvancedDetection = true;
        private bool enableTransliteration = true;
        private bool enableDigitSubstitution = true;
        private bool enableSeparatorRemoval = true;

        // Cyrillic to Latin transliteration map for unified text processing
        private readonly Dictionary<char, string> transliterationMap = new Dictionary<char, string>
        {
            {'а', "a"}, {'е', "e"}, {'ё', "e"}, {'и', "i"}, {'о', "o"},
            {'у', "u"}, {'ы', "y"}, {'э', "e"}, {'ю', "u"}, {'я', "a"},
            {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"}, {'ж', "z"},
            {'з', "z"}, {'й', "i"}, {'к', "k"}, {'л', "l"}, {'м', "m"},
            {'н', "n"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"},
            {'ф', "f"}, {'х', "h"}, {'ц', "s"}, {'ч', "c"}, {'ш', "s"},
            {'щ', "s"}, {'ъ', ""}, {'ь', ""}
        };

        /// <summary>
        /// Initialize the censorship system with language files and configuration.
        /// Must be called before using any other methods.
        /// </summary>
        public void Initialize(LanguageBadWords[] languageFiles, bool enableAdvanced = true, bool enableLogging = true)
        {
            if (isInitialized)
            {
                return;
            }

            enableAdvancedDetection = enableAdvanced;
            logLoadingDetails = enableLogging;

            LoadAllBadWords(languageFiles);
            isInitialized = true;

            Debug.Log("CensorshipManager: Static initialization completed successfully.");
        }

        /// <summary>
        /// Load and process all profanity word lists from language files.
        /// Creates both original and normalized versions for enhanced detection.
        /// </summary>
        private void LoadAllBadWords(LanguageBadWords[] languageFiles)
        {
            // Clear all data structures for fresh loading
            allBadWords.Clear();
            wordsWithSeverity.Clear();
            wordsWithLanguage.Clear();
            normalizedBadWords.Clear();
            normalizedToOriginalMap.Clear();

            if (languageFiles == null || languageFiles.Length == 0)
            {
                Debug.LogWarning("CensorshipManager: No language files provided!");
                return;
            }

            int totalWordsLoaded = 0;

            foreach (var languageData in languageFiles)
            {
                if (!languageData.isActive)
                {
                    if (logLoadingDetails)
                        Debug.Log($"CensorshipManager: Language '{languageData.languageName}' is disabled, skipping");
                    continue;
                }

                if (languageData.badWordsFile == null)
                {
                    Debug.LogError($"CensorshipManager: No file assigned for language '{languageData.languageName}'!");
                    continue;
                }

                int wordsLoadedForLanguage = LoadWordsFromFile(languageData);
                totalWordsLoaded += wordsLoadedForLanguage;

                if (logLoadingDetails)
                {
                    Debug.Log($"CensorshipManager: Loaded {wordsLoadedForLanguage} words for language '{languageData.languageName}' " +
                             $"(severity level: {languageData.severityLevel})");
                }
            }

            Debug.Log($"CensorshipManager: Total loaded {totalWordsLoaded} profanity words");

            if (enableAdvancedDetection)
            {
                Debug.Log($"CensorshipManager: Advanced detection enabled with {normalizedBadWords.Count} normalized patterns");
            }
        }

        /// <summary>
        /// Load words from specific file with intelligent processing.
        /// Creates both original and normalized versions for each word.
        /// </summary>
        private int LoadWordsFromFile(LanguageBadWords languageData)
        {
            try
            {
                string fileContent = languageData.badWordsFile.text;
                WordFileFormat actualFormat = DetermineFileFormat(fileContent, languageData.fileFormat);
                string[] words = ParseWordsFromFile(fileContent, actualFormat);

                if (logLoadingDetails)
                {
                    Debug.Log($"CensorshipManager: Detected format '{actualFormat}' for language '{languageData.languageName}'");
                }

                int newWordsCount = 0;

                foreach (string word in words)
                {
                    string cleanWord = word.Trim().ToLower();
                    if (string.IsNullOrEmpty(cleanWord))
                        continue;

                    // Add original word to main list
                    if (allBadWords.Add(cleanWord))
                    {
                        newWordsCount++;
                    }

                    // Create normalized version for enhanced detection
                    if (enableAdvancedDetection)
                    {
                        string normalizedWord = NormalizeTextForDetection(cleanWord);
                        if (!string.IsNullOrEmpty(normalizedWord) && normalizedWord != cleanWord)
                        {
                            normalizedBadWords.Add(normalizedWord);
                            // Create reverse mapping from normalized to original
                            if (!normalizedToOriginalMap.ContainsKey(normalizedWord))
                            {
                                normalizedToOriginalMap[normalizedWord] = cleanWord;
                            }
                        }
                    }

                    // Store severity information
                    if (enableSeveritySystem)
                    {
                        if (!wordsWithSeverity.ContainsKey(cleanWord) ||
                            wordsWithSeverity[cleanWord] < languageData.severityLevel)
                        {
                            wordsWithSeverity[cleanWord] = languageData.severityLevel;
                        }
                    }

                    // Remember language for each word
                    wordsWithLanguage[cleanWord] = languageData.languageName;
                }

                return newWordsCount;
            }
            catch (Exception e)
            {
                Debug.LogError($"CensorshipManager: Error loading words for language '{languageData.languageName}': {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Perform intelligent text normalization for masked word detection.
        /// Includes transliteration, digit substitution, separator removal, and cleanup.
        /// </summary>
        private string NormalizeTextForDetection(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var result = new StringBuilder();
            string lowerText = text.ToLower();

            // First pass: transliteration and basic symbol substitution
            foreach (char c in lowerText)
            {
                if (enableTransliteration && transliterationMap.TryGetValue(c, out string transliterated))
                {
                    // Cyrillic character - replace with Latin equivalent
                    result.Append(transliterated);
                }
                else if (char.IsLetter(c) && c >= 'a' && c <= 'z')
                {
                    // Latin character remains unchanged
                    result.Append(c);
                }
                else if (enableDigitSubstitution && char.IsDigit(c))
                {
                    // Digit replaced with similar letter according to leet speak rules
                    string digitReplacement = ConvertDigitToLetter(c);
                    result.Append(digitReplacement);
                }
                else if (char.IsWhiteSpace(c))
                {
                    // Preserve spaces for correct word separation
                    result.Append(' ');
                }
                // All other characters (punctuation, separators) are ignored
                // This effectively removes attempts to break words with symbols
            }

            string normalized = result.ToString();

            // Second pass: cleanup normalization artifacts
            if (enableSeparatorRemoval)
            {
                // Remove excessive character repetitions but preserve natural doubles
                normalized = Regex.Replace(normalized, @"(.)\1{2,}", "$1$1");
            }

            // Final cleanup: normalize spaces and remove excess
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return normalized;
        }

        /// <summary>
        /// Convert digits to corresponding letters according to popular substitution schemes.
        /// Based on analysis of real filter bypass attempts in gaming chats.
        /// </summary>
        private string ConvertDigitToLetter(char digit)
        {
            return digit switch
            {
                '0' => "o",  // zero visually identical to letter O
                '1' => "i",  // one very similar to I
                '3' => "e",  // three resembles mirrored E
                '4' => "a",  // four resembles upper part of A
                '5' => "s",  // five sometimes used as S
                '6' => "b",  // six resembles б
                '7' => "t",  // seven resembles T
                '8' => "b",  // eight resembles B
                _ => digit.ToString()  // other digits remain unchanged
            };
        }

        /// <summary>
        /// Determine file format automatically or use specified format.
        /// </summary>
        private WordFileFormat DetermineFileFormat(string fileContent, WordFileFormat specifiedFormat)
        {
            if (specifiedFormat != WordFileFormat.AutoDetect)
                return specifiedFormat;

            if (string.IsNullOrEmpty(fileContent))
                return WordFileFormat.CommaSeparated;

            int commaCount = fileContent.Count(c => c == ',');
            int lineBreakCount = fileContent.Count(c => c == '\n' || c == '\r');

            if (commaCount > lineBreakCount)
            {
                return WordFileFormat.CommaSeparated;
            }
            else if (lineBreakCount > 3)
            {
                return WordFileFormat.LineByLine;
            }
            else
            {
                return commaCount > 0 ? WordFileFormat.CommaSeparated : WordFileFormat.LineByLine;
            }
        }

        /// <summary>
        /// Parse words from file content depending on format.
        /// </summary>
        private string[] ParseWordsFromFile(string fileContent, WordFileFormat format)
        {
            switch (format)
            {
                case WordFileFormat.CommaSeparated:
                    return fileContent
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => word.Trim())
                        .Where(word => !string.IsNullOrEmpty(word))
                        .ToArray();

                case WordFileFormat.LineByLine:
                    return fileContent
                        .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#") && !line.StartsWith("//"))
                        .ToArray();

                default:
                    throw new ArgumentException($"Unsupported file format: {format}");
            }
        }

        /// <summary>
        /// Check text for profanity words using enhanced algorithms.
        /// Multi-stage detection: direct search, word boundary check, normalization analysis.
        /// </summary>
        public bool ContainsBadWords(string message)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("CensorshipManager: Not initialized. Call Initialize() first.");
                return false;
            }

            if (string.IsNullOrEmpty(message) || allBadWords.Count == 0)
                return false;

            string lowerMessage = message.ToLower();

            // Stage 1: Fast word-by-word check (most accurate method)
            // Split message into individual words and check each one
            string cleanMessage = Regex.Replace(lowerMessage, @"[^\w\s]", " ");
            string[] words = cleanMessage.Split(new char[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                if (allBadWords.Contains(word))
                {
                    return true;
                }
            }

            // Stage 2: Substring check but only for words of certain length
            // Apply this method only to sufficiently long words to avoid false positives
            foreach (string badWord in allBadWords)
            {
                // Apply substring check only for words 4+ characters long
                // This reduces probability of false positives
                if (badWord.Length >= 4 && lowerMessage.Contains(badWord))
                {
                    // Additional check: ensure found word is not part of longer innocent word
                    if (IsStandaloneWordInText(lowerMessage, badWord))
                    {
                        return true;
                    }
                }
            }

            // Stage 3: Enhanced detection through normalization
            if (enableAdvancedDetection)
            {
                string normalizedMessage = NormalizeTextForDetection(message);

                // Check words in normalized text
                string[] normalizedWords = normalizedMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in normalizedWords)
                {
                    if (allBadWords.Contains(word))
                    {
                        return true;
                    }
                }

                // Check against normalized dictionary with caution
                foreach (string normalizedBadWord in normalizedBadWords)
                {
                    if (normalizedBadWord.Length >= 4 && normalizedMessage.Contains(normalizedBadWord))
                    {
                        if (IsStandaloneWordInText(normalizedMessage, normalizedBadWord))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if found substring is a standalone word, not part of a longer word.
        /// Helps avoid false positives when profane word is contained within innocent word.
        /// </summary>
        private bool IsStandaloneWordInText(string text, string word)
        {
            // Use regex to find word with boundaries
            // \b denotes word boundary - position between letter and non-letter character
            string pattern = @"\b" + Regex.Escape(word) + @"\b";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Check text for profanity words with specific severity level.
        /// Allows adjusting filter sensitivity depending on context.
        /// </summary>
        public bool ContainsBadWords(string message, int minSeverityLevel)
        {
            if (!enableSeveritySystem)
                return ContainsBadWords(message);

            var foundWords = GetBadWordsWithDetails(message);
            return foundWords.Any(wordInfo => wordInfo.severityLevel >= minSeverityLevel);
        }

        /// <summary>
        /// Information about found profanity word.
        /// </summary>
        [System.Serializable]
        public class BadWordInfo
        {
            public string word;
            public int severityLevel;
            public string language;

            public BadWordInfo(string word, int severityLevel, string language)
            {
                this.word = word;
                this.severityLevel = severityLevel;
                this.language = language;
            }
        }

        /// <summary>
        /// Check text and return detailed information about found profanity words.
        /// Uses enhanced search algorithms while maintaining original data format.
        /// </summary>
        public List<BadWordInfo> GetBadWordsWithDetails(string message)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("CensorshipManager: Not initialized. Call Initialize() first.");
                return new List<BadWordInfo>();
            }

            List<BadWordInfo> foundBadWords = new List<BadWordInfo>();

            if (string.IsNullOrEmpty(message) || allBadWords.Count == 0)
                return foundBadWords;

            HashSet<string> alreadyFound = new HashSet<string>();

            // Stage 1: Standard search in original text
            PerformStandardSearch(message, foundBadWords, alreadyFound);

            // Stage 2: Enhanced search through normalization (if enabled)
            if (enableAdvancedDetection)
            {
                PerformAdvancedSearch(message, foundBadWords, alreadyFound);
            }

            return foundBadWords;
        }

        /// <summary>
        /// Perform standard search for profanity words in original text.
        /// Implements original logic for compatibility and simple cases without masking.
        /// </summary>
        private void PerformStandardSearch(string message, List<BadWordInfo> foundBadWords, HashSet<string> alreadyFound)
        {
            string lowerMessage = message.ToLower();
            string cleanMessage = Regex.Replace(lowerMessage, @"[^\w\s]", " ");
            string[] words = cleanMessage.Split(new char[] { ' ', '\t', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries);

            // Search by individual words
            foreach (string word in words)
            {
                if (allBadWords.Contains(word) && !alreadyFound.Contains(word))
                {
                    AddFoundWord(word, foundBadWords, alreadyFound);
                }
            }

            // Substring search for cases of merged writing
            foreach (string badWord in allBadWords)
            {
                if (lowerMessage.Contains(badWord) && !alreadyFound.Contains(badWord))
                {
                    AddFoundWord(badWord, foundBadWords, alreadyFound);
                }
            }
        }

        /// <summary>
        /// Perform advanced search using text normalization.
        /// New method designed to detect masked words that standard algorithms might miss.
        /// </summary>
        private void PerformAdvancedSearch(string message, List<BadWordInfo> foundBadWords, HashSet<string> alreadyFound)
        {
            string normalizedMessage = NormalizeTextForDetection(message);
            string[] normalizedWords = normalizedMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Search normalized words against regular dictionary
            foreach (string normalizedWord in normalizedWords)
            {
                if (allBadWords.Contains(normalizedWord) && !alreadyFound.Contains(normalizedWord))
                {
                    AddFoundWord(normalizedWord, foundBadWords, alreadyFound);
                }
            }

            // Search against special normalized dictionary
            foreach (string normalizedBadWord in normalizedBadWords)
            {
                if (normalizedMessage.Contains(normalizedBadWord) && !alreadyFound.Contains(normalizedBadWord))
                {
                    // Try to find original word for more accurate display
                    string originalWord = normalizedToOriginalMap.ContainsKey(normalizedBadWord)
                        ? normalizedToOriginalMap[normalizedBadWord]
                        : normalizedBadWord;

                    if (!alreadyFound.Contains(originalWord))
                    {
                        AddFoundWord(originalWord, foundBadWords, alreadyFound);
                    }
                }
            }
        }

        /// <summary>
        /// Add found word to results with complete metadata.
        /// Enriches found word with severity and language data.
        /// </summary>
        private void AddFoundWord(string word, List<BadWordInfo> foundBadWords, HashSet<string> alreadyFound)
        {
            int severity = enableSeveritySystem && wordsWithSeverity.ContainsKey(word)
                ? wordsWithSeverity[word] : 1;
            string language = wordsWithLanguage.ContainsKey(word)
                ? wordsWithLanguage[word] : "Unknown";

            foundBadWords.Add(new BadWordInfo(word, severity, language));
            alreadyFound.Add(word);
        }

        /// <summary>
        /// Check text and return found profanity words as simple list.
        /// </summary>
        public List<string> GetBadWords(string message)
        {
            return GetBadWordsWithDetails(message).Select(info => info.word).ToList();
        }

        /// <summary>
        /// Replace profanity words with asterisks while preserving original message structure.
        /// Uses enhanced detection logic but ensures replacements apply to original text.
        /// </summary>
        public string CensorMessage(string message, char replacement = '*')
        {
            if (!isInitialized)
            {
                Debug.LogWarning("CensorshipManager: Not initialized. Call Initialize() first.");
                return message;
            }

            if (string.IsNullOrEmpty(message) || allBadWords.Count == 0)
                return message;

            string result = message;
            var foundWords = GetBadWordsWithDetails(message);

            if (foundWords.Count == 0)
                return message;

            // Create set of unique words for processing
            var uniqueWords = foundWords.Select(fw => fw.word).Distinct().ToList();

            // Apply censorship for each found word
            foreach (string badWord in uniqueWords)
            {
                // Use regex for precise word boundary search and replacement
                result = ApplyCensorshipToWord(result, badWord, replacement);
            }

            // Additional processing for cases where words might be masked
            if (enableAdvancedDetection)
            {
                result = ApplyAdvancedCensorship(result, message, replacement);
            }

            return result;
        }

        /// <summary>
        /// Apply censorship to specific word in text.
        /// Searches all word occurrences and replaces with asterisks, considering word boundaries.
        /// </summary>
        private string ApplyCensorshipToWord(string text, string badWord, char replacement)
        {
            // Create pattern for word search considering boundaries
            string pattern = @"\b" + Regex.Escape(badWord) + @"\b";
            string replacementStr = new string(replacement, badWord.Length);

            return Regex.Replace(text, pattern, replacementStr, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Apply advanced censorship for detecting masked words.
        /// Works with normalized text for search but applies replacements to original text.
        /// </summary>
        private string ApplyAdvancedCensorship(string currentResult, string originalMessage, char replacement)
        {
            string normalizedMessage = NormalizeTextForDetection(originalMessage);

            // Search for masked words in normalized text
            foreach (string normalizedBadWord in normalizedBadWords)
            {
                if (normalizedMessage.Contains(normalizedBadWord))
                {
                    // Try to find corresponding fragment in original
                    currentResult = FindAndCensorMaskedWord(currentResult, originalMessage,
                        normalizedBadWord, replacement);
                }
            }

            return currentResult;
        }

        /// <summary>
        /// Find and censor masked word in original text.
        /// One of the most complex parts requiring reverse mapping from normalized to masked original.
        /// </summary>
        private string FindAndCensorMaskedWord(string text, string originalText,
            string normalizedBadWord, char replacement)
        {
            // Simplified implementation: search for words that after normalization match found violation
            string[] words = originalText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                string normalizedWord = NormalizeTextForDetection(word);
                if (normalizedWord == normalizedBadWord)
                {
                    // Found word that after normalization gives violation
                    string cleanWord = Regex.Replace(word, @"[^\w]", "");
                    if (!string.IsNullOrEmpty(cleanWord))
                    {
                        string pattern = Regex.Escape(word);
                        string replacementStr = new string(replacement, word.Length);
                        text = Regex.Replace(text, pattern, replacementStr, RegexOptions.IgnoreCase);
                    }
                }
            }

            return text;
        }

        /// <summary>
        /// Reload all profanity word lists.
        /// Useful for dynamic dictionary updates without application restart.
        /// </summary>
        public void ReloadBadWords(LanguageBadWords[] languageFiles)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("CensorshipManager: Not initialized. Call Initialize() first.");
                return;
            }

            LoadAllBadWords(languageFiles);
            Debug.Log("CensorshipManager: Word lists reloaded successfully.");
        }

        /// <summary>
        /// Configure system settings at runtime.
        /// </summary>
        public void ConfigureSettings(bool advanced = true, 
            bool severity = true, 
            bool transliteration = true,
            bool digitSub = true, 
            bool separatorRemoval = true, 
            bool logging = true)
        {
            enableAdvancedDetection = advanced;
            enableSeveritySystem = severity;
            enableTransliteration = transliteration;
            enableDigitSubstitution = digitSub;
            enableSeparatorRemoval = separatorRemoval;
            logLoadingDetails = logging;

            Debug.Log("CensorshipManager: Settings updated successfully.");
        }

        /// <summary>
        /// Get information about enhanced detection system configuration.
        /// Useful for monitoring and debugging system settings.
        /// </summary>
        public Dictionary<string, object> GetSystemInfo()
        {
            return new Dictionary<string, object>
            {
                {"IsInitialized", isInitialized},
                {"AdvancedDetectionEnabled", enableAdvancedDetection},
                {"TransliterationEnabled", enableTransliteration},
                {"DigitSubstitutionEnabled", enableDigitSubstitution},
                {"SeparatorRemovalEnabled", enableSeparatorRemoval},
                {"TotalBadWords", allBadWords.Count},
                {"NormalizedBadWords", normalizedBadWords.Count},
                {"SeveritySystemEnabled", enableSeveritySystem}
            };
        }

        /// <summary>
        /// Perform comprehensive message analysis with detailed diagnostics.
        /// Provides maximum detailed information about violation detection process.
        /// </summary>
        public Dictionary<string, object> AnalyzeMessageDetailed(string message)
        {
            var result = new Dictionary<string, object>();

            result["OriginalMessage"] = message;
            result["NormalizedMessage"] = enableAdvancedDetection ? NormalizeTextForDetection(message) : "Not normalized";

            var foundWords = GetBadWordsWithDetails(message);
            result["FoundViolations"] = foundWords.Count;
            result["ViolationDetails"] = foundWords.Select(fw => new
            {
                Word = fw.word,
                Severity = fw.severityLevel,
                Language = fw.language
            }).ToList();

            result["ContainsBadWords"] = foundWords.Count > 0;
            result["MaxSeverity"] = foundWords.Count > 0 ? foundWords.Max(fw => fw.severityLevel) : 0;
            result["CensoredMessage"] = CensorMessage(message);

            // Diagnostic information about detection process
            result["DetectionMethods"] = new Dictionary<string, bool>
            {
                {"StandardSearch", true},
                {"AdvancedSearch", enableAdvancedDetection},
                {"Transliteration", enableTransliteration},
                {"DigitSubstitution", enableDigitSubstitution}
            };

            return result;
        }

        /// <summary>
        /// Test normalization algorithm with sample messages.
        /// Useful for debugging and tuning normalization algorithms.
        /// </summary>
        public void TestNormalization(string[] testMessages = null)
        {
            if (testMessages == null)
            {
                testMessages = new string[] {
                    "пр1в3т",
                    "д_у_р_а_к",
                    "дурaк",
                    "hello w0rld",
                    "т3ст с0общ3ни3"
                };
            }

            Debug.Log("=== Normalization Test Results ===");
            foreach (string message in testMessages)
            {
                string normalized = NormalizeTextForDetection(message);
                bool containsBad = ContainsBadWords(message);
                Debug.Log($"'{message}' → '{normalized}' [Bad: {containsBad}]");
            }
            Debug.Log("=====================================");
        }
    }
}