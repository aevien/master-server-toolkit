using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Advanced censorship module with multi-threading support, configurable filtering,
    /// leet speak detection, and performance optimizations.
    /// Acts as a bridge between MasterServerToolkit architecture and CensorshipSystem.
    /// </summary>
    public class CensorModule : BaseServerModule
    {
        [Header("Language Files")]
        [SerializeField]
        [Tooltip("Array of language-specific profanity word files to load")]
        private LanguageBadWords[] languageFiles = new LanguageBadWords[0];

        [Header("General Settings")]
        [SerializeField]
        [Tooltip("Enable detailed logging during word list loading process")]
        private bool logLoadingDetails = true;

        [SerializeField]
        [Tooltip("Enable severity-based filtering system for different violation levels")]
        private bool enableSeveritySystem = true;

        [Header("Advanced Detection Settings")]
        [SerializeField]
        [Tooltip("Enable intelligent normalization for detecting masked words")]
        private bool enableAdvancedDetection = true;

        [SerializeField]
        [Tooltip("Enable Cyrillic to Latin transliteration for unified search")]
        private bool enableTransliteration = true;

        [SerializeField]
        [Tooltip("Enable detection of digit substitutions (4 instead of A, 3 instead of E)")]
        private bool enableDigitSubstitution = true;

        [SerializeField]
        [Tooltip("Enable removal of separators between letters")]
        private bool enableSeparatorRemoval = true;

        // Core censorship system instance that handles all detection logic
        private CensorshipSystem censorship;

        /// <summary>
        /// Initialize the censorship module with server instance.
        /// Sets up the underlying censorship system with configured parameters.
        /// </summary>
        /// <param name="server">Server instance for module integration</param>
        public override void Initialize(IServer server)
        {
            // Create and configure the core censorship system
            censorship = new CensorshipSystem();

            // Initialize with language files from Inspector configuration
            censorship.Initialize(languageFiles);

            // Apply all configured settings to the censorship system
            censorship.ConfigureSettings(
                advanced: enableAdvancedDetection,
                severity: enableSeveritySystem,
                transliteration: enableTransliteration,
                digitSub: enableDigitSubstitution,
                separatorRemoval: enableSeparatorRemoval,
                logging: logLoadingDetails
            );

            Debug.Log("CensorModule: Successfully initialized and configured");
        }

        /// <summary>
        /// Check if username contains profanity words.
        /// Typically used for username validation during registration or login.
        /// </summary>
        /// <param name="username">Username to validate</param>
        /// <returns>True if username contains bad words, false otherwise</returns>
        public bool ContainsBadWords(string username)
        {
            // Ensure system is initialized before processing
            if (censorship == null)
            {
                Debug.LogWarning("CensorModule: System not initialized, cannot check username");
                return false;
            }

            return censorship.ContainsBadWords(username);
        }

        /// <summary>
        /// Censor profanity words in chat message by replacing them with asterisks.
        /// Preserves original message structure while hiding inappropriate content.
        /// </summary>
        /// <param name="message">Original chat message</param>
        /// <returns>Censored message with profanity replaced by asterisks</returns>
        public string CensorMessage(string message)
        {
            // Ensure system is initialized before processing
            if (censorship == null)
            {
                Debug.LogWarning("CensorModule: System not initialized, returning original message");
                return message;
            }

            return censorship.CensorMessage(message);
        }

        /// <summary>
        /// Check if message contains profanity with specific severity level.
        /// Useful for implementing different moderation rules based on chat context.
        /// </summary>
        /// <param name="message">Message to check</param>
        /// <param name="minSeverityLevel">Minimum severity level to trigger (1-3)</param>
        /// <returns>True if message contains violations at or above specified severity</returns>
        public bool ContainsBadWords(string message, int minSeverityLevel)
        {
            if (censorship == null)
            {
                Debug.LogWarning("CensorModule: System not initialized, cannot check message");
                return false;
            }

            return censorship.ContainsBadWords(message, minSeverityLevel);
        }

        /// <summary>
        /// Get detailed information about profanity violations in message.
        /// Returns list with specific words found, their severity levels, and languages.
        /// </summary>
        /// <param name="message">Message to analyze</param>
        /// <returns>List of detailed violation information</returns>
        public List<CensorshipSystem.BadWordInfo> GetBadWordsWithDetails(string message)
        {
            if (censorship == null)
            {
                Debug.LogWarning("CensorModule: System not initialized, returning empty list");
                return new List<CensorshipSystem.BadWordInfo>();
            }

            return censorship.GetBadWordsWithDetails(message);
        }

        /// <summary>
        /// Reload all profanity word lists from configured files.
        /// Useful for updating word lists without server restart.
        /// </summary>
        public void ReloadWordLists()
        {
            if (censorship == null)
            {
                Debug.LogWarning("CensorModule: System not initialized, cannot reload");
                return;
            }

            censorship.ReloadBadWords(languageFiles);
            Debug.Log("CensorModule: Word lists reloaded successfully");
        }

        /// <summary>
        /// Get comprehensive statistics about the censorship system.
        /// Includes word counts, active languages, and configuration status.
        /// </summary>
        /// <returns>Dictionary with system information and statistics</returns>
        public Dictionary<string, object> GetSystemStatistics()
        {
            if (censorship == null)
            {
                return new Dictionary<string, object> { { "Error", "System not initialized" } };
            }

            return censorship.GetSystemInfo();
        }

        /// <summary>
        /// Perform comprehensive analysis of message with detailed diagnostics.
        /// Useful for debugging and understanding how detection algorithms work.
        /// </summary>
        /// <param name="message">Message to analyze</param>
        /// <returns>Detailed analysis results including normalization steps and detection methods</returns>
        public Dictionary<string, object> AnalyzeMessage(string message)
        {
            if (censorship == null)
            {
                return new Dictionary<string, object> { { "Error", "System not initialized" } };
            }

            return censorship.AnalyzeMessageDetailed(message);
        }

        /// <summary>
        /// Update system configuration at runtime without reinitialization.
        /// Allows dynamic adjustment of detection sensitivity and features.
        /// </summary>
        /// <param name="advanced">Enable advanced detection features</param>
        /// <param name="severity">Enable severity-based filtering</param>
        /// <param name="transliteration">Enable transliteration</param>
        /// <param name="digitSub">Enable digit substitution detection</param>
        /// <param name="separatorRemoval">Enable separator removal</param>
        public void UpdateConfiguration(bool advanced = true, bool severity = true,
            bool transliteration = true, bool digitSub = true, bool separatorRemoval = true)
        {
            if (censorship == null)
            {
                Debug.LogWarning("CensorModule: System not initialized, cannot update configuration");
                return;
            }

            // Update local settings to match new configuration
            enableAdvancedDetection = advanced;
            enableSeveritySystem = severity;
            enableTransliteration = transliteration;
            enableDigitSubstitution = digitSub;
            enableSeparatorRemoval = separatorRemoval;

            // Apply new configuration to censorship system
            censorship.ConfigureSettings(advanced, severity, transliteration,
                digitSub, separatorRemoval, logLoadingDetails);

            Debug.Log("CensorModule: Configuration updated successfully");
        }
    }
}