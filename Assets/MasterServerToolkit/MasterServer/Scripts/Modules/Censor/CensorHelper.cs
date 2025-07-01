using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Helper class providing utility methods for working with censor results
    /// and implementing common censorship policies
    /// </summary>
    public static class CensorHelper
    {
        /// <summary>
        /// Determines if a message should be blocked based on weight thresholds
        /// </summary>
        /// <param name="result">Censor result to evaluate</param>
        /// <param name="blockThreshold">Minimum weight that triggers message blocking</param>
        /// <returns>True if message should be blocked</returns>
        public static bool ShouldBlockMessage(CensorResult result, CensorWeight blockThreshold = CensorWeight.Heavy)
        {
            return result.hasCensoredWords && result.highestWeight >= blockThreshold;
        }

        /// <summary>
        /// Determines if a user should receive a warning based on detected words
        /// </summary>
        /// <param name="result">Censor result to evaluate</param>
        /// <param name="warningThreshold">Minimum weight that triggers warning</param>
        /// <returns>True if user should be warned</returns>
        public static bool ShouldWarnUser(CensorResult result, CensorWeight warningThreshold = CensorWeight.Medium)
        {
            return result.hasCensoredWords && result.highestWeight >= warningThreshold;
        }

        /// <summary>
        /// Gets a user-friendly description of the censorship action taken
        /// </summary>
        /// <param name="result">Censor result</param>
        /// <returns>Description string for logging or user feedback</returns>
        public static string GetActionDescription(CensorResult result)
        {
            if (!result.hasCensoredWords)
                return "No inappropriate content detected";

            int wordCount = result.detectedWords.Count;
            string weightDesc = GetWeightDescription(result.highestWeight);

            return $"Detected {wordCount} inappropriate word(s) with {weightDesc} severity";
        }

        /// <summary>
        /// Gets a human-readable description of a censor weight
        /// </summary>
        /// <param name="weight">Weight to describe</param>
        /// <returns>User-friendly description</returns>
        public static string GetWeightDescription(CensorWeight weight)
        {
            switch (weight)
            {
                case CensorWeight.Light:
                    return "mild";
                case CensorWeight.Medium:
                    return "moderate";
                case CensorWeight.Heavy:
                    return "strong";
                case CensorWeight.Extreme:
                    return "severe";
                case CensorWeight.Banned:
                    return "extreme";
                default:
                    return "unknown";
            }
        }

        /// <summary>
        /// Creates a detailed log entry for censorship events
        /// </summary>
        /// <param name="userId">ID of the user who sent the message</param>
        /// <param name="originalText">Original uncensored text</param>
        /// <param name="result">Censor result</param>
        /// <returns>Formatted log entry</returns>
        public static string CreateLogEntry(string userId, string originalText, CensorResult result)
        {
            if (!result.hasCensoredWords)
                return $"[CENSOR] User {userId}: Clean message passed";

            var detectedWords = string.Join(", ", result.detectedWords.Select(w =>
                $"{w.originalWord}({w.weight})"));

            return $"[CENSOR] User {userId}: Detected words [{detectedWords}] | " +
                   $"Highest: {result.highestWeight} | " +
                   $"Original length: {originalText.Length} | " +
                   $"Filtered length: {result.filteredText.Length}";
        }

        /// <summary>
        /// Groups detected words by their weight for analysis
        /// </summary>
        /// <param name="result">Censor result</param>
        /// <returns>Dictionary grouped by weight</returns>
        public static Dictionary<CensorWeight, List<CensoredWordInfo>> GroupWordsByWeight(CensorResult result)
        {
            return result.detectedWords
                .GroupBy(w => w.weight)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Checks if the text contains only leet speak violations (less severe)
        /// </summary>
        /// <param name="result">Censor result</param>
        /// <returns>True if only leet speak words were detected</returns>
        public static bool IsOnlyLeetSpeak(CensorResult result)
        {
            return result.hasCensoredWords && result.detectedWords.All(w => w.wasLeetSpeak);
        }

        /// <summary>
        /// Calculates a severity score for the message based on detected words
        /// </summary>
        /// <param name="result">Censor result</param>
        /// <returns>Severity score (0-100)</returns>
        public static int CalculateSeverityScore(CensorResult result)
        {
            if (!result.hasCensoredWords)
                return 0;

            int totalScore = 0;
            foreach (var word in result.detectedWords)
            {
                int baseScore = GetWeightScore(word.weight);

                // Reduce score for leet speak (considered less intentional)
                if (word.wasLeetSpeak)
                    baseScore = (int)(baseScore * 0.7f);

                totalScore += baseScore;
            }

            // Cap at 100
            return Mathf.Min(100, totalScore);
        }

        /// <summary>
        /// Gets numeric score for a weight value
        /// </summary>
        private static int GetWeightScore(CensorWeight weight)
        {
            switch (weight)
            {
                case CensorWeight.Light: return 10;
                case CensorWeight.Medium: return 25;
                case CensorWeight.Heavy: return 50;
                case CensorWeight.Extreme: return 75;
                case CensorWeight.Banned: return 100;
                default: return 5;
            }
        }

        /// <summary>
        /// Creates a sanitized version of detected words for safe logging
        /// </summary>
        /// <param name="result">Censor result</param>
        /// <param name="showFirstLetter">Whether to show first letter of detected words</param>
        /// <returns>Safe string representation</returns>
        public static string GetSafeWordList(CensorResult result, bool showFirstLetter = true)
        {
            if (!result.hasCensoredWords)
                return "None";

            var safeWords = result.detectedWords.Select(w =>
            {
                if (showFirstLetter && w.originalWord.Length > 0)
                {
                    return w.originalWord[0] + new string('*', w.originalWord.Length - 1);
                }
                return new string('*', w.originalWord.Length);
            });

            return string.Join(", ", safeWords);
        }
    }

    /// <summary>
    /// Extension methods for easier integration with existing systems
    /// </summary>
    public static class CensorExtensions
    {
        /// <summary>
        /// Quick check if text is safe for public display
        /// </summary>
        /// <param name="censorModule">Censor module instance</param>
        /// <param name="text">Text to check</param>
        /// <param name="threshold">Maximum allowed weight</param>
        /// <returns>True if text is safe</returns>
        public static bool IsTextSafe(this CensorModule censorModule, string text,
            CensorWeight threshold = CensorWeight.Medium)
        {
            var result = censorModule.CensorText(text);
            return !result.hasCensoredWords || result.highestWeight < threshold;
        }

        /// <summary>
        /// Gets filtered text or blocks message based on policy
        /// </summary>
        /// <param name="censorModule">Censor module instance</param>
        /// <param name="text">Text to process</param>
        /// <param name="blockThreshold">Weight that triggers blocking</param>
        /// <param name="blockedMessage">Message to return when blocked</param>
        /// <returns>Filtered text or blocked message</returns>
        public static string ProcessWithPolicy(this CensorModule censorModule, string text,
            CensorWeight blockThreshold = CensorWeight.Heavy,
            string blockedMessage = "[MESSAGE BLOCKED DUE TO INAPPROPRIATE CONTENT]")
        {
            var result = censorModule.CensorText(text);

            if (CensorHelper.ShouldBlockMessage(result, blockThreshold))
            {
                return blockedMessage;
            }

            return result.filteredText;
        }
    }
}