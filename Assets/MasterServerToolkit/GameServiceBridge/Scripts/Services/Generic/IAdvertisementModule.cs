namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Specifies the playback status of a rewarded video advertisement.
    /// </summary>
    public enum RewardedVideoStatus
    {
        /// <summary>
        /// The rewarded video advertisement has opened and started playing.
        /// </summary>
        Opened,

        /// <summary>
        /// The player has watched enough of the video to receive the reward.
        /// </summary>
        Rewarded,

        /// <summary>
        /// The rewarded video advertisement has closed.
        /// </summary>
        Closed,

        /// <summary>
        /// An error occurred during rewarded video playback.
        /// </summary>
        Error
    }

    /// <summary>
    /// Specifies the playback status of a full-screen video advertisement.
    /// </summary>
    public enum FullScreenVideoStatus
    {
        /// <summary>
        /// The full-screen video advertisement has opened and started playing.
        /// </summary>
        Opened,

        /// <summary>
        /// The full-screen video advertisement has closed.
        /// </summary>
        Closed,

        /// <summary>
        /// An error occurred during full-screen video playback.
        /// </summary>
        Error
    }

    /// <summary>
    /// Specifies the display status of a sticky or banner advertisement.
    /// </summary>
    public enum BannerAdvertisementStatus
    {
        /// <summary>
        /// The banner advertisement is visible.
        /// </summary>
        Shown,

        /// <summary>
        /// The banner advertisement is hidden.
        /// </summary>
        Hidden,

        /// <summary>
        /// An error occurred while showing or hiding the banner advertisement.
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents a callback that receives rewarded video advertisement status changes.
    /// </summary>
    /// <param name="status">The current status of rewarded video playback.</param>
    public delegate void RewardedVideoHandler(RewardedVideoStatus status);

    /// <summary>
    /// Represents a callback that receives full-screen video advertisement status changes.
    /// </summary>
    /// <param name="status">The current status of full-screen video playback.</param>
    public delegate void FullScreenVideoHandler(FullScreenVideoStatus status);

    /// <summary>
    /// Represents a callback that receives banner advertisement status changes.
    /// </summary>
    /// <param name="status">The current status of the banner advertisement.</param>
    public delegate void BannerAdvertisementHandler(BannerAdvertisementStatus status);

    /// <summary>
    /// Defines the contract for advertisement features exposed by the current game service.
    /// </summary>
    public interface IAdvertisementModule : IServiceModule
    {
        /// <summary>
        /// Gets a value indicating whether any advertisement is currently visible or playing.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Gets a value indicating whether a full-screen video advertisement is ready to be shown.
        /// </summary>
        bool IsFullScreenVideoReady { get; }

        /// <summary>
        /// Gets a value indicating whether a banner advertisement is currently visible.
        /// </summary>
        bool IsBannerVisible { get; }

        /// <summary>
        /// Displays a full-screen video advertisement to the player.
        /// </summary>
        /// <param name="callback">The callback that receives full-screen video status updates.</param>
        void ShowFullScreenVideo(FullScreenVideoHandler callback);

        /// <summary>
        /// Displays a rewarded video advertisement that can grant rewards upon completion.
        /// </summary>
        /// <param name="callback">The callback that receives rewarded video status updates.</param>
        void ShowRewardedVideo(RewardedVideoHandler callback);

        /// <summary>
        /// Displays a sticky or banner advertisement to the player.
        /// </summary>
        /// <param name="callback">The callback that receives banner advertisement status updates.</param>
        void ShowBanner(BannerAdvertisementHandler callback);

        /// <summary>
        /// Hides the currently visible sticky or banner advertisement.
        /// </summary>
        /// <param name="callback">The callback that receives banner advertisement status updates.</param>
        void HideBanner(BannerAdvertisementHandler callback);
    }
}
