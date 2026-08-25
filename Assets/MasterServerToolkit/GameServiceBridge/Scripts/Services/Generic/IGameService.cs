using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.GameService
{

    /// <summary>
    /// Main interface for game service integration providing authentication, data storage,
    /// advertising, in-app purchases, leaderboards and analytics functionality
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Gets the unique identifier of the current game service platform
        /// </summary>
        GameServiceId Id { get; }

        /// <summary>
        /// Gets the application identifier assigned by the game service platform
        /// </summary>
        string AppId { get; }

        /// <summary>
        /// Gets the current language code of the player's locale (e.g., "en", "ru", "es")
        /// </summary>
        string Lang { get; }

        /// <summary>
        /// Gets the type of device the game is running on (e.g., "desktop", "mobile", "tablet")
        /// </summary>
        string DeviceType { get; }

        /// <summary>
        /// Gets additional platform-specific payload data in JSON format
        /// </summary>
        MstJson Payload { get; }

        /// <summary>
        /// Gets normalized launch referrer data provided by the active service.
        /// </summary>
        ReferrerInfo Referrer { get; }

        /// <summary>
        /// Gets a value indicating whether the game is running on a mobile device
        /// </summary>
        bool IsMobile { get; }

        /// <summary>
        /// Gets a value indicating whether the game service has been initialized and is ready to use
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Initializes the game service with default configuration
        /// </summary>
        void Init();

        /// <summary>
        /// Initializes the game service with custom configuration options
        /// </summary>
        /// <param name="options">JSON object containing initialization parameters specific to the platform</param>
        void Init(MstJson options);

        /// <summary>
        /// Notifies the platform that the game has finished loading and is ready to play
        /// </summary>
        void GameLoaded();

        /// <summary>
        /// Notifies the platform that the game has finished loading with additional options
        /// </summary>
        /// <param name="options">JSON object containing game loading parameters or metrics</param>
        void GameLoaded(MstJson options);

        /// <summary>
        /// Notifies the platform that the game session has started
        /// </summary>
        void GameStart();

        /// <summary>
        /// Notifies the platform that the game session has started with additional parameters
        /// </summary>
        /// <param name="options">JSON object containing game start parameters or level information</param>
        void GameStart(MstJson options);

        /// <summary>
        /// Notifies the platform that the game session has ended
        /// </summary>
        void GameStop();

        /// <summary>
        /// Notifies the platform that the game session has ended with additional parameters
        /// </summary>
        /// <param name="options">JSON object containing game stop parameters or final statistics</param>
        void GameStop(MstJson options);
    }
}
