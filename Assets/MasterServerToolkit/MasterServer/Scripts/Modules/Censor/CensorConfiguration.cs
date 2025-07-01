using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Weight levels for censored words, determining severity and filtering actions
    /// </summary>
    public enum CensorWeight
    {
        Light = 0,      // Minor inappropriate words
        Medium = 1,     // Moderate profanity
        Heavy = 2,      // Strong profanity
        Extreme = 3,    // Highly offensive content
        Banned = 4      // Words that result in immediate action
    }

    /// <summary>
    /// Different methods of filtering detected words
    /// </summary>
    public enum FilterMethod
    {
        Remove,         // Completely remove the word
        Asterisk,       // Replace with asterisks (****)
        Placeholder,    // Replace with [CENSORED]
        FirstLetter,    // Show first letter (F***)
        Warning,        // Keep word but flag for warning
        Block           // Block entire message
    }

    /// <summary>
    /// Configuration for a word list file with associated weight and settings
    /// </summary>
    [Serializable]
    public class WordListConfiguration
    {
        [Tooltip("Text asset containing words separated by commas or new lines")]
        public TextAsset[] wordList;

        [Tooltip("Weight/severity level of words in this list")]
        public CensorWeight weight = CensorWeight.Medium;

        [Tooltip("Whether this word list should be used for filtering")]
        public bool isActive = true;

        [Tooltip("Search for whole words only (recommended for most cases)")]
        public bool wholeWordsOnly = true;

        [Tooltip("Custom filter method for this specific word list (overrides global setting if enabled)")]
        public bool useCustomFilter = false;

        [Tooltip("Filter method to use when useCustomFilter is enabled")]
        public FilterMethod customFilterMethod = FilterMethod.Asterisk;

        [Tooltip("Custom replacement text when using Placeholder filter method")]
        public string customPlaceholder = "[CENSORED]";
    }

    /// <summary>
    /// Leet speak character mappings for detecting obfuscated words
    /// </summary>
    [Serializable]
    public class LeetMapping
    {
        [Tooltip("Leet character (e.g., '3', '@', '4')")]
        public char leetChar;

        [Tooltip("Normal character it represents (e.g., 'e', 'a', 'a')")]
        public char normalChar;

        [Tooltip("Whether this mapping should be used")]
        public bool isActive = true;
    }

    /// <summary>
    /// Main configuration asset for the censor system
    /// </summary>
    [CreateAssetMenu(fileName = "CensorConfiguration", menuName = MstConstants.CreateMenu + "Censor/Configuration")]
    public class CensorConfiguration : ScriptableObject
    {
        [Header("Word Lists")]
        [Tooltip("List of word files with their associated weights and settings")]
        public List<WordListConfiguration> wordLists = new List<WordListConfiguration>();

        [Header("Global Filter Settings")]
        [Tooltip("Default filter method to use when word list doesn't specify custom method")]
        public FilterMethod defaultFilterMethod = FilterMethod.Asterisk;

        [Tooltip("Global setting for case sensitivity (false = case insensitive)")]
        public bool caseSensitive = false;

        [Header("Leet Speak Detection")]
        [Tooltip("Enable detection of leet speak obfuscation (3 -> e, 4 -> a, etc.)")]
        public bool enableLeetDetection = true;

        [Tooltip("Character mappings for leet speak detection")]
        public List<LeetMapping> leetMappings = new List<LeetMapping>()
        {
            new LeetMapping { leetChar = '3', normalChar = 'e', isActive = true },
            new LeetMapping { leetChar = '4', normalChar = 'a', isActive = true },
            new LeetMapping { leetChar = '5', normalChar = 's', isActive = true },
            new LeetMapping { leetChar = '7', normalChar = 't', isActive = true },
            new LeetMapping { leetChar = '0', normalChar = 'o', isActive = true },
            new LeetMapping { leetChar = '1', normalChar = 'i', isActive = true },
            new LeetMapping { leetChar = '@', normalChar = 'a', isActive = true },
            new LeetMapping { leetChar = '$', normalChar = 's', isActive = true },
        };

        [Header("Context Filtering")]
        [Tooltip("Enable contextual analysis to reduce false positives")]
        public bool enableContextFiltering = true;

        [Tooltip("Minimum word length to apply context filtering (shorter words are always filtered)")]
        [Range(2, 10)]
        public int contextFilterMinLength = 4;

        [Tooltip("Words that when found nearby reduce the confidence of censoring (whitelist context)")]
        [TextArea(3, 6)]
        public string contextWhitelistWords = "class, classic, assignment, grass, glass, mass, pass";

        [Header("Performance Settings")]
        [Tooltip("Maximum number of words to cache compiled patterns for (higher = more memory, faster lookups)")]
        [Range(100, 10000)]
        public int maxCachedPatterns = 1000;

        [Tooltip("Enable multi-threading support (use locks for thread safety)")]
        public bool enableMultiThreading = true;

        [Header("Debug Settings")]
        [Tooltip("Enable detailed logging for debugging purposes")]
        public bool enableDebugLogging = false;

        /// <summary>
        /// Validates the configuration and logs warnings for potential issues
        /// </summary>
        public void ValidateConfiguration()
        {
            if (wordLists == null || wordLists.Count == 0)
            {
                Debug.LogWarning("CensorConfiguration: No word lists configured!");
                return;
            }

            int activeListsCount = 0;
            foreach (var wordList in wordLists)
            {
                if (wordList.isActive)
                {
                    activeListsCount++;
                    if (wordList.wordList == null)
                    {
                        Debug.LogWarning($"CensorConfiguration: Active word list has null TextAsset!");
                    }
                }
            }

            if (activeListsCount == 0)
            {
                Debug.LogWarning("CensorConfiguration: No active word lists found!");
            }

            if (enableLeetDetection && (leetMappings == null || leetMappings.Count == 0))
            {
                Debug.LogWarning("CensorConfiguration: Leet detection enabled but no mappings configured!");
            }
        }

        private void OnValidate()
        {
            ValidateConfiguration();
        }
    }
}