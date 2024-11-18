using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public enum GameServiceId
    {
        Self, PlayWeb3, YandexGames, Itch
    }

    public interface IGameService
    {
        GameServiceId Id { get; }
        bool IsMobile { get; }

        PlayerInfo Player { get; }

        bool IsInAppPurchaseSupported { get; }
        IEnumerable<ProductInfo> Products { get; }
        MstJson Purchases { get; }
        bool IsAdSupported { get; }
        void GetProducts(SuccessCallback callback);
        void Purchase(string productId, SuccessCallback callback);
        void GetProductPurchases(SuccessCallback callback);

        void Init();
        void Authenticate(SuccessCallback callback);

        void SetString(string key, string value);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void SetBool(string key, bool value);
        void GetString(string key, UnityAction<bool, string> callback);
        void GetFloat(string key, UnityAction<bool, float> callback);
        void GetInt(string key, UnityAction<bool, int> callback);
        void GetBool(string key, UnityAction<bool, bool> callback);

        void OnReady(UnityAction callback);
    }
}