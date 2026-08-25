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
        [Tooltip("Language-specific moderation dictionaries loaded when the module initializes. All active entries are evaluated for every checked text; duplicate rules are merged by the censorship system.")]
        private LanguageBadWords[] languageFiles = new LanguageBadWords[0];

        [SerializeField]
        [Tooltip("Enables severity values from dictionary entries. When disabled, matching still occurs but severity thresholds are not used to distinguish rule levels.")]
        private bool enableSeveritySystem = true;

        [Header("Advanced Detection Settings")]
        [SerializeField]
        [Tooltip("Normalizes text before matching to detect supported masked spellings. Disable only when exact dictionary matching is required.")]
        private bool enableAdvancedDetection = true;

        [SerializeField]
        [Tooltip("Treats supported Cyrillic and Latin look-alike spellings as equivalent during advanced matching. Used only when Advanced Detection is enabled.")]
        private bool enableTransliteration = true;

        [SerializeField]
        [Tooltip("Normalizes supported digit substitutions such as 4 for A and 3 for E. Used only when Advanced Detection is enabled.")]
        private bool enableDigitSubstitution = true;

        [SerializeField]
        [Tooltip("Ignores supported separators inserted between letters when evaluating masked words. Used only when Advanced Detection is enabled.")]
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
            censorship.Initialize(languageFiles, logLevel: logger.LogLevel);

            // Apply all configured settings to the censorship system
            censorship.ConfigureSettings(
                advanced: enableAdvancedDetection,
                severity: enableSeveritySystem,
                transliteration: enableTransliteration,
                digitSub: enableDigitSubstitution,
                separatorRemoval: enableSeparatorRemoval,
                logLevel: logger.LogLevel
            );

            logger.Info("Successfully initialized and configured");
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
                logger.Warn("System not initialized, cannot check username");
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
                logger.Warn("System not initialized, returning original message");
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
                logger.Warn("System not initialized, cannot check message");
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
                logger.Warn("System not initialized, returning empty list");
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
                logger.Warn("System not initialized, cannot reload");
                return;
            }

            censorship.ReloadBadWords(languageFiles);
            logger.Info("Word lists reloaded successfully");
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
                logger.Warn("System not initialized, cannot update configuration");
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
                digitSub, separatorRemoval, logger.LogLevel);

            logger.Info("Configuration updated successfully");
        }
    }
}
