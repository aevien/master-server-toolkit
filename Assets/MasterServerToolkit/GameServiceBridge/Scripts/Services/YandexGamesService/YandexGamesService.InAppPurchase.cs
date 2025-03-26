using System.Runtime.InteropServices;

namespace MasterServerToolkit.GameService
{
    public partial class YandexGamesService : BaseGameService
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
        [DllImport("__Internal")]
        private static extern void Gb_Yg_Purchase(string productId, string payload);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetProducts();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetPurchases();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ConsumePurchase(string purchaseId);
#endif

        public override void GetProducts(ProductsHandler callback)
        {
            base.GetProducts(callback);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_GetProducts();
#endif
        }

        public override void Purchase(string productId, PurchaseHandler callback)
        {
            string payload = string.Empty;
            base.Purchase(productId, payload, callback);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_Purchase(productId, payload);
#endif
        }

        public override void GetPurchases(PurchaseHandler callback)
        {
            base.GetPurchases(callback);

#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_GetPurchases();
#endif
        }

        public override void ProcessPurchase(string purchaseId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_STANDALONE
            Gb_Yg_ConsumePurchase(purchaseId);
#endif
        }
    }
}