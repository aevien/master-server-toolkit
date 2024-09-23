using MasterServerToolkit.Json;
using System.Collections;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public enum GameServiceId
    {
        Self, PlayWeb3, YandexGames
    }

    public abstract class BaseGameService : IGameService
    {
        public GameServiceId Id { get; protected set; }
        public string PlayerId { get; protected set; }
        public MstJson Products { get; protected set; } = MstJson.EmptyArray;
        public MstJson Purchases { get; protected set; } = MstJson.EmptyArray;

        public abstract IEnumerator Authenticate(UnityAction<bool> callback);
        public abstract IEnumerator GetProducts(UnityAction<bool> callback);
        public abstract IEnumerator GetProductPurchases(UnityAction<bool> callback);
    }
}