using System.Collections;
using UnityEngine.Events;

namespace MasterServerToolkit.GameService
{
    public class SelfService : BaseGameService
    {
        public SelfService()
        {
            Id = GameServiceId.Self;
        }

        public override IEnumerator Authenticate(UnityAction<bool> callback)
        {
            yield break;
        }

        public override IEnumerator GetProducts(UnityAction<bool> callback)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerator GetProductPurchases(UnityAction<bool> callback)
        {
            throw new System.NotImplementedException();
        }
    }
}
