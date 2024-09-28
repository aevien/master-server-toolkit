using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public enum GameServiceId
    {
        Self, PlayWeb3, YandexGames
    }

    public interface IGameService
    {
        GameServiceId Id { get; }
        PlayerInfo Player { get; }
        IEnumerable<ProductInfo> Products { get; }
        MstJson Purchases { get; }
        bool IsAdSupported { get; }
        IEnumerator Init();
        IEnumerator Authenticate(SuccessCallback callback);
        IEnumerator GetProducts(SuccessCallback callback);
        IEnumerator GetProductPurchases(SuccessCallback callback);
        void SetString(string key, string value);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void SetBool(string key, bool value);
        IEnumerator GetString(string key, UnityAction<bool, string> callback);
        IEnumerator GetFloat(string key, UnityAction<bool, float> callback);
        IEnumerator GetInt(string key, UnityAction<bool, int> callback);
        IEnumerator GetBool(string key, UnityAction<bool, bool> callback);
        void OnReady(UnityAction callback);
    }
}