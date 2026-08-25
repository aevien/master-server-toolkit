namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Represents a callback that receives review prompt availability information.
    /// </summary>
    /// <param name="info">The review prompt availability information.</param>
    public delegate void ReviewAvailabilityHandler(ReviewAvailabilityInfo info);

    /// <summary>
    /// Represents a callback that receives the result of a review prompt request.
    /// </summary>
    /// <param name="info">The review prompt result information.</param>
    public delegate void ReviewHandler(ReviewResultInfo info);

    /// <summary>
    /// Represents a callback that receives shortcut prompt availability information.
    /// </summary>
    /// <param name="info">The shortcut prompt availability information.</param>
    public delegate void ShortcutAvailabilityHandler(ShortcutAvailabilityInfo info);

    /// <summary>
    /// Represents a callback that receives the result of a shortcut prompt request.
    /// </summary>
    /// <param name="info">The shortcut prompt result information.</param>
    public delegate void ShortcutPromptHandler(ShortcutPromptInfo info);

    /// <summary>
    /// Provides information about whether a review prompt can be shown.
    /// </summary>
    public class ReviewAvailabilityInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the review prompt can be shown.
        /// </summary>
        public bool CanReview { get; set; }

        /// <summary>
        /// Gets or sets the platform-specific reason when the review prompt cannot be shown.
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Provides information about the result of a review prompt request.
    /// </summary>
    public class ReviewResultInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the player sent review feedback.
        /// </summary>
        public bool FeedbackSent { get; set; }

        /// <summary>
        /// Gets or sets the platform-specific error message.
        /// </summary>
        public string Error { get; set; }
    }

    /// <summary>
    /// Provides information about whether a shortcut prompt can be shown.
    /// </summary>
    public class ShortcutAvailabilityInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the shortcut prompt can be shown.
        /// </summary>
        public bool CanShow { get; set; }

        /// <summary>
        /// Gets or sets the platform-specific reason when the shortcut prompt cannot be shown.
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Provides information about the result of a shortcut prompt request.
    /// </summary>
    public class ShortcutPromptInfo
    {
        /// <summary>
        /// Gets or sets the platform-specific prompt outcome.
        /// </summary>
        public string Outcome { get; set; }

        /// <summary>
        /// Gets or sets the platform-specific error message.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets a value indicating whether the player accepted the shortcut prompt.
        /// </summary>
        public bool Accepted => Outcome == "accepted";
    }

    /// <summary>
    /// Defines the contract for platform sharing, review, and shortcut features.
    /// </summary>
    public interface IShareModule : IServiceModule
    {
        /// <summary>
        /// Checks whether the platform allows showing a review prompt.
        /// </summary>
        /// <param name="callback">The callback that receives review availability information.</param>
        void CanReview(ReviewAvailabilityHandler callback);

        /// <summary>
        /// Prompts the player to rate and review the game on the platform's store.
        /// </summary>
        void Review();

        /// <summary>
        /// Prompts the player to rate and review the game on the platform's store.
        /// </summary>
        /// <param name="callback">The callback that receives the review prompt result.</param>
        void Review(ReviewHandler callback);

        /// <summary>
        /// Checks whether the platform allows showing a shortcut installation prompt.
        /// </summary>
        /// <param name="callback">The callback that receives shortcut prompt availability information.</param>
        void CanShowShortcutPrompt(ShortcutAvailabilityHandler callback);

        /// <summary>
        /// Shows the platform shortcut installation prompt.
        /// </summary>
        /// <param name="callback">The callback that receives the shortcut prompt result.</param>
        void ShowShortcutPrompt(ShortcutPromptHandler callback);
    }
}
