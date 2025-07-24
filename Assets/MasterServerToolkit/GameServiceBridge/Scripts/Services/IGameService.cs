using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.GameService
{
    public enum GameServiceId
    {
        Self, PlayWeb3, YandexGames, Itch, VKPlay
    }

    public enum RewardedVideoStatus
    {
        Opened, Rewarded, Closed, Error
    }

    public enum FullScreenVideoStatus
    {
        Opened, Closed, Error
    }

    public delegate void PlayerInfoHandler(PlayerInfo info);
    public delegate void PlayerDataHandler(MstJson data);

    public delegate void PauseHandler(bool paused);

    public delegate void RewardedVideoHandler(RewardedVideoStatus status);
    public delegate void FullScreenVideoHandler(FullScreenVideoStatus status);

    public delegate void ProductsHandler(IEnumerable<ProductInfo> products);
    public delegate void PurchaseHandler(PurchasesInfo purchase);

    public delegate void LeaderboardInfoHandler(LeaderboardInfo leaderboardInfo);
    public delegate void LeaderboardEntriesHandler(LeaderboardEntries leaderboardEntries);
    public delegate void LeaderboardPlayerInfoHandler(LeaderboardPlayerInfo leaderboardPlayerInfo);

    public interface IGameService
    {
        GameServiceId Id { get; }
        string AppId { get; }
        string Lang { get; }
        string DeviceType { get; }
        MstJson Payload { get; }
        bool IsMobile { get; }
        bool IsReady { get; }
        Logger Logger { get; set; }

        event Action OnReadyEvent;
        event PlayerInfoHandler OnPlayerInfoEvent;
        event PlayerDataHandler OnPlayerDataEvent;
        event PauseHandler OnPauseEvent;

        void Init();
        void Init(MstJson options);
        void GameLoaded();
        void GameLoaded(MstJson options);
        void GameStart();
        void GameStart(MstJson options);
        void GameStop();
        void GameStop(MstJson options);

        PlayerInfo Player { get; }
        void Authenticate(SuccessCallback callback);
        void LoadPlayerData(PlayerDataHandler callback);
        void SavePlayerData(string key, string value, SuccessCallback callback = null);
        void SavePlayerData(string key, int value, bool saveAsStats = false, SuccessCallback callback = null);
        void SavePlayerData(MstJson data, bool saveAsStats = false, SuccessCallback callback = null);

        void AnalyticsEvent(MstJson eventData, bool singleton = false);
        void AnalyticsEvent(string eventData, bool singleton = false);

        bool IsInAppPurchaseSupported { get; }
        IEnumerable<ProductInfo> Products { get; }
        void GetProducts(ProductsHandler callback);
        void Purchase(string productId, PurchaseHandler callback);
        void Purchase(string productId, string payload, PurchaseHandler callback);
        void GetPurchases(PurchaseHandler callback);
        void ProcessPurchase(string purchaseId);

        bool IsAdVisible { get; }
        bool IsAdSupported { get; }
        bool IsAdFullScreenVideoReady { get; }
        void ShowFullScreenVideo(FullScreenVideoHandler callback);
        void ShowRewardedVideo(RewardedVideoHandler callback);

        LeaderboardInfo LeaderboardDescription { get; }
        LeaderboardEntries LeaderboardEntries { get; }
        LeaderboardPlayerInfo LeaderboardPlayerEntry { get; }
        bool IsLeaderboardSupported { get; }
        void SetLeaderboardScore(string name, int score, MstJson extra);
        void GetLeaderboardInfo(string name, LeaderboardInfoHandler callback);
        void GetLeaderboardEntries(string name, MstJson options, LeaderboardEntriesHandler callback);
        void GetLeaderboardPlayerInfo(string name, LeaderboardPlayerInfoHandler callback);

        void ReviewGame();
        void SetString(string key, string value);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        string GetString(string key, string defaultValue = "");
        float GetFloat(string key, float defaultValue = 0f);
        int GetInt(string key, int defaultValue = 0);
    }
}