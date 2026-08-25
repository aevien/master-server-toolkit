using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Determines how leaderboard entries are ordered.
    /// </summary>
    public enum LeaderboardSortOrder : byte
    {
        Ascending = 0,
        Descending = 1
    }

    /// <summary>
    /// Describes whether a leaderboard currently accepts score submissions.
    /// </summary>
    public enum LeaderboardAvailability : byte
    {
        Upcoming = 0,
        Active = 1,
        Ended = 2
    }

    /// <summary>
    /// One localized leaderboard title configured in the Unity Inspector.
    /// </summary>
    [Serializable]
    public sealed class LeaderboardLocalizedTitle
    {
        [SerializeField, Tooltip("Language code used to select this title, for example en, ru or tr. Codes are matched without case sensitivity.")]
        private string language = "en";

        [SerializeField, Tooltip("Leaderboard title shown to players using the selected language. This may also be a localization key if the consuming UI resolves keys itself.")]
        private string value = string.Empty;

        /// <summary>
        /// Language code normalized for case-insensitive lookup.
        /// </summary>
        public string Language => (language ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>
        /// Configured title value.
        /// </summary>
        public string Value => value?.Trim() ?? string.Empty;

        /// <summary>
        /// Creates an empty localized title for Unity serialization.
        /// </summary>
        public LeaderboardLocalizedTitle() { }

        /// <summary>
        /// Creates a localized title.
        /// </summary>
        public LeaderboardLocalizedTitle(string language, string value)
        {
            this.language = language;
            this.value = value;
        }
    }

    /// <summary>
    /// Inspector-owned definition of one leaderboard season.
    /// </summary>
    [Serializable]
    public sealed class LeaderboardDefinition
    {
        public const string AllTimeSeasonId = "all_time";
        public const int MaxKeyLength = 64;
        public const int MaxSeasonIdLength = 64;

        [SerializeField, Tooltip("Stable leaderboard identifier used by client and server APIs, for example survivalTime. Maximum length: 64 characters. Do not change it after results have been stored.")]
        private string key = string.Empty;

        [SerializeField, Tooltip("Localized titles returned to clients. Language codes must be unique inside this leaderboard. The en value is used as the first fallback.")]
        private List<LeaderboardLocalizedTitle> title = new List<LeaderboardLocalizedTitle>();

        [SerializeField, Tooltip("Stable season identifier stored with every result. Maximum length: 64 characters. Leave empty to use all_time. Changing this value starts a separate leaderboard without deleting previous seasons.")]
        private string seasonId = AllTimeSeasonId;

        [SerializeField, Tooltip("Controls score ordering. Descending places larger values first; Ascending places smaller values first.")]
        private LeaderboardSortOrder sortOrder = LeaderboardSortOrder.Descending;

        [SerializeField, Tooltip("When enabled, a submission replaces the stored score only when it is better according to Sort Order. When disabled, every accepted submission replaces the previous score.")]
        private bool keepBest = true;

        [SerializeField, Range(0, 18), Tooltip("Number of decimal digits implied by the stored integer score. 0 displays the score as an integer; 2 interprets 1234 as 12.34. Formatting is performed by the consuming UI.")]
        private int decimalPlaces;

        [SerializeField, Tooltip("Smallest score accepted by this leaderboard. Submissions below this value are rejected.")]
        private long minimumScore;

        [SerializeField, Tooltip("Largest score accepted by this leaderboard. Submissions above this value are rejected.")]
        private long maximumScore = long.MaxValue;

        [SerializeField, Tooltip("When enabled, only trusted room/server connections with the room_server permission can submit scores. When disabled, an authenticated client may submit only its own score.")]
        private bool serverOnly = true;

        [SerializeField, Tooltip("Allows guest accounts to participate. When disabled, submissions from accounts marked as guests are rejected; existing guest entries remain readable.")]
        private bool allowGuests = true;

        [SerializeField, Tooltip("Optional UTC start time in ISO 8601 format, for example 2026-06-01T00:00:00Z. Leave empty to accept submissions without a lower time boundary.")]
        private string startsAt = string.Empty;

        [SerializeField, Tooltip("Optional UTC end time in ISO 8601 format, for example 2026-09-01T00:00:00Z. Leave empty to accept submissions without an upper time boundary. Ended leaderboards remain readable.")]
        private string endsAt = string.Empty;

        public string Key => key?.Trim() ?? string.Empty;
        public string SeasonId => string.IsNullOrWhiteSpace(seasonId) ? AllTimeSeasonId : seasonId.Trim();
        public LeaderboardSortOrder SortOrder => sortOrder;
        public bool KeepBest => keepBest;
        public int DecimalPlaces => decimalPlaces;
        public long MinimumScore => minimumScore;
        public long MaximumScore => maximumScore;
        public bool ServerOnly => serverOnly;
        public bool AllowGuests => allowGuests;

        /// <summary>
        /// Returns a detached language-to-title dictionary.
        /// </summary>
        public Dictionary<string, string> GetTitles()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (title == null)
                return result;

            foreach (LeaderboardLocalizedTitle localizedTitle in title)
            {
                if (localizedTitle == null ||
                    string.IsNullOrWhiteSpace(localizedTitle.Language) ||
                    string.IsNullOrWhiteSpace(localizedTitle.Value))
                {
                    continue;
                }

                result[localizedTitle.Language] = localizedTitle.Value;
            }

            return result;
        }

        /// <summary>
        /// Resolves the title using the requested language, English and then the first configured value.
        /// </summary>
        public string GetTitle(string language)
        {
            Dictionary<string, string> titles = GetTitles();

            if (!string.IsNullOrWhiteSpace(language) &&
                titles.TryGetValue(language.Trim(), out string localizedTitle))
            {
                return localizedTitle;
            }

            if (titles.TryGetValue("en", out string englishTitle))
                return englishTitle;

            foreach (string value in titles.Values)
                return value;

            return Key;
        }

        /// <summary>
        /// Parses and validates the optional UTC activity period.
        /// </summary>
        public bool TryGetPeriod(out DateTime? startsAtUtc, out DateTime? endsAtUtc, out string error)
        {
            startsAtUtc = null;
            endsAtUtc = null;
            error = string.Empty;

            if (!TryParseUtc(startsAt, out startsAtUtc))
            {
                error = $"Leaderboard '{Key}' has an invalid Starts At value: '{startsAt}'";
                return false;
            }

            if (!TryParseUtc(endsAt, out endsAtUtc))
            {
                error = $"Leaderboard '{Key}' has an invalid Ends At value: '{endsAt}'";
                return false;
            }

            if (startsAtUtc.HasValue && endsAtUtc.HasValue && startsAtUtc.Value >= endsAtUtc.Value)
            {
                error = $"Leaderboard '{Key}' must end after it starts";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates all inspector-owned settings.
        /// </summary>
        public bool TryValidate(out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(Key))
            {
                error = "Leaderboard key is required";
                return false;
            }

            if (Key.Length > MaxKeyLength)
            {
                error = $"Leaderboard key '{Key}' exceeds {MaxKeyLength} characters";
                return false;
            }

            if (SeasonId.Length > MaxSeasonIdLength)
            {
                error = $"Leaderboard '{Key}' season identifier exceeds {MaxSeasonIdLength} characters";
                return false;
            }

            if (!Enum.IsDefined(typeof(LeaderboardSortOrder), sortOrder))
            {
                error = $"Leaderboard '{Key}' has an invalid sort order";
                return false;
            }

            if (decimalPlaces < 0 || decimalPlaces > 18)
            {
                error = $"Leaderboard '{Key}' decimal places must be between 0 and 18";
                return false;
            }

            if (minimumScore > maximumScore)
            {
                error = $"Leaderboard '{Key}' minimum score exceeds its maximum score";
                return false;
            }

            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (title != null)
            {
                foreach (LeaderboardLocalizedTitle localizedTitle in title)
                {
                    if (localizedTitle == null || string.IsNullOrWhiteSpace(localizedTitle.Language))
                        continue;

                    if (!languages.Add(localizedTitle.Language))
                    {
                        error = $"Leaderboard '{Key}' contains duplicate language '{localizedTitle.Language}'";
                        return false;
                    }
                }
            }

            return TryGetPeriod(out _, out _, out error);
        }

        /// <summary>
        /// Returns the activity state at the supplied UTC time.
        /// </summary>
        public LeaderboardAvailability GetAvailability(DateTime utcNow)
        {
            if (!TryGetPeriod(out DateTime? startsAtUtc, out DateTime? endsAtUtc, out _))
                return LeaderboardAvailability.Ended;

            utcNow = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();

            if (startsAtUtc.HasValue && utcNow < startsAtUtc.Value)
                return LeaderboardAvailability.Upcoming;

            if (endsAtUtc.HasValue && utcNow >= endsAtUtc.Value)
                return LeaderboardAvailability.Ended;

            return LeaderboardAvailability.Active;
        }

        private static bool TryParseUtc(string value, out DateTime? utcValue)
        {
            utcValue = null;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (!DateTimeOffset.TryParse(
                    value.Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                return false;
            }

            utcValue = parsed.UtcDateTime;
            return true;
        }
    }
}
