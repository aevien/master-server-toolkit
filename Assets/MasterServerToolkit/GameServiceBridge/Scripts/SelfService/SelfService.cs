using MasterServerToolkit.MasterServer;
using System.Collections;

namespace MasterServerToolkit.GameService
{
    public class SelfService : BaseGameService
    {
        public SelfService()
        {
            Id = GameServiceId.Self;
        }

        public override IEnumerator Authenticate(SuccessCallback callback)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerator GetProductPurchases(SuccessCallback callback)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerator GetProducts(SuccessCallback callback)
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerator Init()
        {
            throw new System.NotImplementedException();
        }
    }
}
