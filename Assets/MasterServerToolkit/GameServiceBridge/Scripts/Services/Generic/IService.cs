using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Callback delegate for handling game pause/resume events
    /// </summary>
    /// <param name="paused">True if game is paused, false if resumed</param>
    public delegate void PauseHandler(bool paused);

    /// <summary>
    /// Callback delegate for service readiness changes.
    /// </summary>
    /// <param name="isReady"><see langword="true"/> when the service is ready; otherwise, <see langword="false"/>.</param>
    public delegate void ReadyHandler(bool isReady);

    /// <summary>
    /// Callback delegate for platform account-selection dialog state changes.
    /// </summary>
    /// <param name="opened"><see langword="true"/> when the dialog opens; <see langword="false"/> when it closes.</param>
    public delegate void AccountSelectionDialogHandler(bool opened);

    public interface IService
    {
        /// <summary>
        /// Gets or sets the logger instance for debugging and error reporting
        /// </summary>
        Logger Logger { get; set; }

        /// <summary>
        /// Service id
        /// </summary>
        GameServiceId Id { get; }

        /// <summary>
        /// Service app id
        /// </summary>
        string AppId { get; }

        /// <summary>
        /// Service lang
        /// </summary>
        string Lang { get; }

        /// <summary>
        /// Server time in milliseconds
        /// </summary>
        long ServerTime { get; }

        /// <summary>
        /// Current device type
        /// </summary>
        ServiceDeviceType Device { get; }

        /// <summary>
        /// Some payload data taken from service
        /// </summary>
        MstJson Payload { get; }

        /// <summary>
        /// Gets normalized launch referrer data provided by the active service.
        /// </summary>
        ReferrerInfo Referrer { get; }

        /// <summary>
        /// Gets service-provided remote configuration flags loaded during service initialization.
        /// </summary>
        MstJson RemoteFlags { get; }

        /// <summary>
        /// Check if service is ready
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Custom parameters list
        /// </summary>
        MstJson Options { get; }

        /// <summary>
        /// 
        /// </summary>
        float WaitForReadyTime { get; set; }

        /// <summary>
        /// Player module
        /// </summary>
        IPlayerModule Player { get; }

        /// <summary>
        /// In app purchase module
        /// </summary>
        IInAppPurchaseModule IAP { get; }

        /// <summary>
        /// Leaderboards module
        /// </summary>
        ILeaderboardsModule Leaderboards { get; }

        /// <summary>
        /// Advertisement module
        /// </summary>
        IAdvertisementModule Ad { get; }

        /// <summary>
        /// Analytics and metrics module
        /// </summary>
        IAnalyticsModule Analytics { get; }

        /// <summary>
        /// Storage module
        /// </summary>
        IStorageModule Storage { get; }

        /// <summary>
        /// Shareing module
        /// </summary>
        IShareModule Share { get; }

        /// <summary>
        /// Event fired when the game service initialization is complete and ready to use
        /// </summary>
        event ReadyHandler OnReadyEvent;

        /// <summary>
        /// Event fired when the game is paused or resumed by the platform
        /// </summary>
        event PauseHandler OnPauseEvent;

        /// <summary>
        /// Occurs when the platform account-selection dialog opens or closes.
        /// </summary>
        event AccountSelectionDialogHandler OnAccountSelectionDialogEvent;

        /// <summary>
        /// 
        /// </summary>
        void OnBeforeInit();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        void OnBeforeInit(MstJson options);

        /// <summary>
        /// Init service
        /// </summary>
        void OnInit();

        /// <summary>
        /// 
        /// </summary>
        void OnAfterInit();

        /// <summary>
        /// 
        /// </summary>
        void OnReady();

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game is ready to interact with the player. 
        /// For these purposes, call this method.
        /// </summary>
        void GameLoaded();

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game is ready to interact with the player. 
        /// For these purposes, call this method.
        /// </summary>
        /// <param name="options"></param>
        void GameLoaded(MstJson options);

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has started. For these purposes, call this method.
        /// </summary>
        void GameStart();

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has started. For these purposes, call this method.
        /// </summary>
        /// <param name="options"></param>
        void GameStart(MstJson options);

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has stopped. For these purposes, call this method.
        /// </summary>
        void GameStop();

        /// <summary>
        /// Sometimes the game portal where the game is launched requires an explicit 
        /// indication that the game has stopped. For these purposes, call this method.
        /// </summary>
        /// <param name="options"></param>
        void GameStop(MstJson options);
    }
}
