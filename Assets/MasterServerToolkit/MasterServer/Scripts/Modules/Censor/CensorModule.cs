using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Result of text censoring operation containing filtered text and detection info
    /// </summary>
    public class CensorResult
    {
        public string filteredText;
        public bool hasCensoredWords;
        public List<CensoredWordInfo> detectedWords;
        public CensorWeight highestWeight;

        public CensorResult()
        {
            detectedWords = new List<CensoredWordInfo>();
            highestWeight = CensorWeight.Light;
        }
    }

    /// <summary>
    /// Information about a detected censored word
    /// </summary>
    public class CensoredWordInfo
    {
        public string originalWord;
        public string detectedPattern;
        public CensorWeight weight;
        public int startIndex;
        public int length;
        public bool wasLeetSpeak;
    }

    /// <summary>
    /// Advanced censorship module with multi-threading support, configurable filtering,
    /// leet speak detection, and performance optimizations
    /// </summary>
    public class CensorModule : BaseServerModule
    {
        [Header("Configuration")]
        [Tooltip("Main configuration asset containing all censor settings and word lists")]
        [SerializeField]
        private CensorConfiguration configuration;

        // Thread-safe collections for multi-threading support
        private readonly ConcurrentDictionary<string, CompiledWordPattern> compiledPatterns
            = new ConcurrentDictionary<string, CompiledWordPattern>();

        private readonly ConcurrentDictionary<CensorWeight, List<CompiledWordPattern>> wordsByWeight
            = new ConcurrentDictionary<CensorWeight, List<CompiledWordPattern>>();

        // Leet speak conversion dictionary for performance
        private Dictionary<char, char> leetToNormalMap;
        private HashSet<string> contextWhitelistWords;

        // Thread synchronization
        private readonly object initializationLock = new object();
        private bool isInitialized = false;

        /// <summary>
        /// Compiled pattern information for efficient regex matching
        /// </summary>
        private class CompiledWordPattern
        {
            public string originalWord;
            public Regex compiledRegex;
            public CensorWeight weight;
            public FilterMethod filterMethod;
            public string customPlaceholder;
            public bool wholeWordOnly;
        }

        public override void Initialize(IServer server)
        {
            if (!isInitialized)
            {
                lock (initializationLock)
                {
                    if (!isInitialized)
                    {
                        InitializeConfiguration();
                        ParseAndCompileWordLists();
                        isInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes configuration and validates settings
        /// </summary>
        private void InitializeConfiguration()
        {
            if (configuration == null)
            {
                Debug.LogError("CensorModule: No configuration assigned! Please assign a CensorConfiguration asset.");
                return;
            }

            configuration.ValidateConfiguration();
            BuildLeetSpeakMap();
            BuildContextWhitelist();
        }

        /// <summary>
        /// Builds the leet speak character mapping dictionary for efficient lookups
        /// </summary>
        private void BuildLeetSpeakMap()
        {
            leetToNormalMap = new Dictionary<char, char>();

            if (configuration.enableLeetDetection && configuration.leetMappings != null)
            {
                foreach (var mapping in configuration.leetMappings)
                {
                    if (mapping.isActive)
                    {
                        leetToNormalMap[mapping.leetChar] = mapping.normalChar;
                    }
                }
            }
        }

        /// <summary>
        /// Builds context whitelist for reducing false positives
        /// </summary>
        private void BuildContextWhitelist()
        {
            contextWhitelistWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (configuration.enableContextFiltering && !string.IsNullOrEmpty(configuration.contextWhitelistWords))
            {
                var words = configuration.contextWhitelistWords.Split(new[] { ',', '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    var trimmedWord = word.Trim();
                    if (!string.IsNullOrEmpty(trimmedWord))
                    {
                        contextWhitelistWords.Add(trimmedWord);
                    }
                }
            }
        }

        /// <summary>
        /// Parses word lists and compiles regex patterns for efficient matching
        /// </summary>
        private void ParseAndCompileWordLists()
        {
            compiledPatterns.Clear();
            wordsByWeight.Clear();

            foreach (var weightValue in Enum.GetValues(typeof(CensorWeight)).Cast<CensorWeight>())
            {
                wordsByWeight[weightValue] = new List<CompiledWordPattern>();
            }

            if (configuration.wordLists == null) return;

            foreach (var wordListConfig in configuration.wordLists)
            {
                if (!wordListConfig.isActive || wordListConfig.wordList == null) continue;

                ProcessWordList(wordListConfig);
            }

            // Log initialization stats
            int totalPatterns = compiledPatterns.Count;
            Debug.Log($"CensorModule: Initialized with {totalPatterns} compiled patterns");
        }

        /// <summary>
        /// Processes a single word list configuration and compiles patterns
        /// </summary>
        private void ProcessWordList(WordListConfiguration wordListConfig)
        {
            var words = ParseWordsFromTextAssets(wordListConfig.wordList);

            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word)) continue;

                var trimmedWord = word.Trim().ToLowerInvariant();

                // Skip if we've already processed this word
                if (compiledPatterns.ContainsKey(trimmedWord))
                {
                    var existingPattern = compiledPatterns[trimmedWord];
                    Debug.LogWarning($"CensorModule: Word '{trimmedWord}' already exists with weight {existingPattern.weight}, skipping {wordListConfig.weight}");
                    continue;
                }

                // Create compiled pattern
                var pattern = CreateCompiledPattern(trimmedWord, wordListConfig);
                if (pattern != null)
                {
                    compiledPatterns[trimmedWord] = pattern;
                    wordsByWeight[wordListConfig.weight].Add(pattern);
                }

                // Respect cache limit for performance
                if (compiledPatterns.Count >= configuration.maxCachedPatterns)
                {
                    Debug.LogWarning($"CensorModule: Reached maximum cached patterns limit ({configuration.maxCachedPatterns})");
                    break;
                }
            }
        }

        /// <summary>
        /// Parses words from multiple text assets, supporting both comma and newline separation
        /// </summary>
        /// <param name="textAssets">Array of text assets containing word lists</param>
        /// <returns>Combined list of all words from all files</returns>
        private List<string> ParseWordsFromTextAssets(TextAsset[] textAssets)
        {
            var result = new List<string>();

            if (textAssets == null || textAssets.Length == 0)
                return result;

            int estimatedCapacity = textAssets.Length * 100;
            result.Capacity = estimatedCapacity;

            foreach (var textAsset in textAssets)
            {
                if (textAsset == null || string.IsNullOrEmpty(textAsset.text))
                    continue;

                var separators = new[] { ',', '\n', '\r' };
                var wordsFromCurrentFile = textAsset.text.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in wordsFromCurrentFile)
                {
                    var cleanWord = word.Trim();
                    if (!string.IsNullOrEmpty(cleanWord) && cleanWord.Length >= 2)
                    {
                        result.Add(cleanWord);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a compiled regex pattern for efficient word matching
        /// </summary>
        private CompiledWordPattern CreateCompiledPattern(string word, WordListConfiguration config)
        {
            try
            {
                string pattern;

                if (config.wholeWordsOnly)
                {
                    // Use word boundaries for whole word matching
                    pattern = $@"\b{Regex.Escape(word)}\b";
                }
                else
                {
                    // Simple contains matching
                    pattern = Regex.Escape(word);
                }

                var regexOptions = RegexOptions.Compiled;
                if (!configuration.caseSensitive)
                {
                    regexOptions |= RegexOptions.IgnoreCase;
                }

                var compiledRegex = new Regex(pattern, regexOptions);

                return new CompiledWordPattern
                {
                    originalWord = word,
                    compiledRegex = compiledRegex,
                    weight = config.weight,
                    filterMethod = config.useCustomFilter ? config.customFilterMethod : configuration.defaultFilterMethod,
                    customPlaceholder = config.customPlaceholder,
                    wholeWordOnly = config.wholeWordsOnly
                };
            }
            catch (ArgumentException ex)
            {
                Debug.LogError($"CensorModule: Failed to compile regex for word '{word}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines whether the text contains censored words (thread-safe)
        /// </summary>
        /// <param name="text">Text to check for censored words</param>
        /// <returns>True if text contains censored words, false otherwise</returns>
        public virtual bool HasCensoredWord(string text)
        {
            if (string.IsNullOrEmpty(text) || !isInitialized)
                return false;

            var result = CensorText(text);
            return result.hasCensoredWords;
        }

        /// <summary>
        /// Performs comprehensive text censoring with detailed results (thread-safe)
        /// </summary>
        /// <param name="text">Text to censor</param>
        /// <returns>CensorResult containing filtered text and detection information</returns>
        public virtual CensorResult CensorText(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var result = new CensorResult { filteredText = text };

            if (string.IsNullOrEmpty(text) || !isInitialized)
                return result;

            // Create variants of text for leet speak detection
            var textVariants = new List<string> { text };

            if (configuration.enableLeetDetection)
            {
                textVariants.Add(ConvertLeetSpeak(text));
            }

            // Process each text variant
            foreach (var textVariant in textVariants)
            {
                bool isLeetVariant = textVariant != text;
                ProcessTextVariant(textVariant, result, isLeetVariant);
            }

            // Apply context filtering if enabled
            if (configuration.enableContextFiltering)
            {
                ApplyContextFiltering(text, result);
            }

            // Determine final state
            result.hasCensoredWords = result.detectedWords.Count > 0;
            if (result.hasCensoredWords)
            {
                result.highestWeight = result.detectedWords.Max(w => w.weight);
                result.filteredText = ApplyFiltering(text, result.detectedWords);
            }

            return result;
        }

        /// <summary>
        /// Processes a single text variant for censored words
        /// </summary>
        private void ProcessTextVariant(string text, CensorResult result, bool isLeetVariant)
        {
            PerformPatternMatching(text, result, isLeetVariant);
        }

        /// <summary>
        /// Performs the actual pattern matching against compiled regexes
        /// </summary>
        private void PerformPatternMatching(string text, CensorResult result, bool isLeetVariant)
        {
            int totalMatches = 0;

            foreach (var pattern in compiledPatterns.Values)
            {
                var matches = pattern.compiledRegex.Matches(text);
                totalMatches += matches.Count;

                foreach (Match match in matches)
                {
                    // Check if we already detected this word at this position
                    bool alreadyDetected = result.detectedWords.Any(w =>
                        w.startIndex == match.Index && w.length == match.Length);

                    if (!alreadyDetected)
                    {
                        result.detectedWords.Add(new CensoredWordInfo
                        {
                            originalWord = pattern.originalWord,
                            detectedPattern = match.Value,
                            weight = pattern.weight,
                            startIndex = match.Index,
                            length = match.Length,
                            wasLeetSpeak = isLeetVariant
                        });
                    }
                }
            }

            if (totalMatches > 0 && configuration.enableDebugLogging)
            {
                Debug.Log($"CensorModule: Found {totalMatches} potential matches");
            }
        }

        /// <summary>
        /// Converts leet speak characters to normal characters
        /// </summary>
        private string ConvertLeetSpeak(string text)
        {
            if (leetToNormalMap == null || leetToNormalMap.Count == 0)
                return text;

            var sb = new StringBuilder(text);

            for (int i = 0; i < sb.Length; i++)
            {
                char currentChar = char.ToLowerInvariant(sb[i]);

                if (leetToNormalMap.TryGetValue(currentChar, out char normalChar))
                {
                    sb[i] = char.IsUpper(sb[i]) ? char.ToUpperInvariant(normalChar) : normalChar;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Applies context filtering to reduce false positives
        /// </summary>
        private void ApplyContextFiltering(string originalText, CensorResult result)
        {
            if (contextWhitelistWords == null || contextWhitelistWords.Count == 0)
                return;

            var wordsToRemove = new List<CensoredWordInfo>();

            foreach (var detectedWord in result.detectedWords)
            {
                if (detectedWord.detectedPattern.Length >= configuration.contextFilterMinLength)
                {
                    // Check if any whitelist words are near this detected word
                    if (HasNearbyWhitelistWord(originalText, detectedWord))
                    {
                        wordsToRemove.Add(detectedWord);
                    }
                }
            }

            // Remove false positives
            foreach (var wordToRemove in wordsToRemove)
            {
                result.detectedWords.Remove(wordToRemove);
            }
        }

        /// <summary>
        /// Checks if there are whitelisted context words near the detected word
        /// </summary>
        private bool HasNearbyWhitelistWord(string text, CensoredWordInfo detectedWord)
        {
            // Define search window (30 characters before and after)
            int windowSize = 30;
            int startIndex = Math.Max(0, detectedWord.startIndex - windowSize);
            int endIndex = Math.Min(text.Length, detectedWord.startIndex + detectedWord.length + windowSize);

            string contextWindow = text.Substring(startIndex, endIndex - startIndex);

            return contextWhitelistWords.Any(whitelistWord =>
                contextWindow.IndexOf(whitelistWord, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Applies the appropriate filtering method to the text
        /// </summary>
        private string ApplyFiltering(string originalText, List<CensoredWordInfo> detectedWords)
        {
            if (detectedWords.Count == 0)
                return originalText;

            // Sort by position in reverse order to maintain correct indices during replacement
            var sortedWords = detectedWords.OrderByDescending(w => w.startIndex).ToList();

            var sb = new StringBuilder(originalText);

            foreach (var detectedWord in sortedWords)
            {
                if (!compiledPatterns.TryGetValue(detectedWord.originalWord, out var pattern))
                {
                    Debug.LogWarning($"CensorModule: Pattern not found for word '{detectedWord.originalWord}', skipping");
                    continue;
                }

                string replacement = GetReplacementText(detectedWord, pattern);

                if (pattern.filterMethod == FilterMethod.Block)
                {
                    // Block entire message
                    return "[MESSAGE BLOCKED]";
                }

                // Replace the detected word
                sb.Remove(detectedWord.startIndex, detectedWord.length);
                sb.Insert(detectedWord.startIndex, replacement);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the appropriate replacement text based on filter method
        /// </summary>
        private string GetReplacementText(CensoredWordInfo detectedWord, CompiledWordPattern pattern)
        {
            switch (pattern.filterMethod)
            {
                case FilterMethod.Remove:
                    return "";

                case FilterMethod.Asterisk:
                    return new string('*', detectedWord.length);

                case FilterMethod.Placeholder:
                    return !string.IsNullOrEmpty(pattern.customPlaceholder)
                        ? pattern.customPlaceholder
                        : "[CENSORED]";

                case FilterMethod.FirstLetter:
                    if (detectedWord.length > 0)
                    {
                        char firstChar = detectedWord.detectedPattern[0];
                        return firstChar + new string('*', Math.Max(0, detectedWord.length - 1));
                    }
                    return "*";

                case FilterMethod.Warning:
                    return detectedWord.detectedPattern; // Keep original but flag for warning

                default:
                    return new string('*', detectedWord.length);
            }
        }

        /// <summary>
        /// Gets statistics about the loaded word patterns
        /// </summary>
        public Dictionary<CensorWeight, int> GetWordStatistics()
        {
            var stats = new Dictionary<CensorWeight, int>();

            foreach (var weight in Enum.GetValues(typeof(CensorWeight)).Cast<CensorWeight>())
            {
                stats[weight] = wordsByWeight.ContainsKey(weight) ? wordsByWeight[weight].Count : 0;
            }

            return stats;
        }

        /// <summary>
        /// Reloads the configuration and recompiles all patterns
        /// </summary>
        public void ReloadConfiguration()
        {
            lock (initializationLock)
            {
                isInitialized = false;
                Initialize(null);
            }
        }
    }
}