using MasterServerToolkit.Json;
using System.Collections;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public interface IGameService
    {
        GameServiceId Id { get; }
        string PlayerId { get; }
        MstJson Products { get; }
        MstJson Purchases { get; }
        IEnumerator Authenticate(UnityAction<bool> callback);
        IEnumerator GetProducts(UnityAction<bool> callback);
        IEnumerator GetProductPurchases(UnityAction<bool> callback);
    }
}