using MasterServerToolkit.Json;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MasterServerToolkit.GameService
{
    public class YandexGamesInAppPurchaseModule : BaseInAppPurchaseModule
    {
        [DllImport("__Internal")]
        private static extern void Gb_Yg_Purchase(string productId, string payload);
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetProducts();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_GetPurchases();
        [DllImport("__Internal")]
        private static extern void Gb_Yg_ConsumePurchase(string purchaseId);

        public override void OnBeforeInit(IService service)
        {
            IsSupported = true;
            base.OnBeforeInit(service);
        }

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            IsReady = true;
        }

        public override void GetProducts(ProductsHandler callback)
        {
            base.GetProducts(callback);
            Gb_Yg_GetProducts();
        }

        public override void Purchase(string productId, PurchaseHandler callback)
        {
            Purchase(productId, string.Empty, callback);
        }

        public override void Purchase(string productId, string payload, PurchaseHandler callback)
        {
            base.Purchase(productId, payload, callback);
            Gb_Yg_Purchase(productId, payload ?? string.Empty);
        }

        public override void GetPurchases(PurchaseHandler callback)
        {
            base.GetPurchases(callback);
            Gb_Yg_GetPurchases();
        }

        public override void ProcessPurchase(string purchaseId)
        {
            Gb_Yg_ConsumePurchase(purchaseId);
        }

        #region WEB_CALLBACK

        protected void Yg_OnPurchaseResult(string json)
        {
            var result = new MstJson(json);

            if (result.HasField(YandexGamesKeys.Error))
            {
                NotifyOnPurchase(null);
                return;
            }

            var data = new PurchasesInfo()
            {
                serviceId = GameServiceId.YandexGames,
                data = result
            };

            NotifyOnPurchase(data);
        }

        protected void Yg_OnGetProducts(string json)
        {
            var result = new MstJson(json);

            Logger.Info(result);

            if (result.HasField(YandexGamesKeys.Error))
            {
                NotifyOnGetProducts(Products);
                return;
            }

            List<ProductInfo> products = new();

            foreach (var productJson in result)
            {
                var productInfo = new ProductInfo()
                {
                    Id = productJson[YandexGamesKeys.Id].StringValue,
                    Title = productJson[YandexGamesKeys.Title].StringValue,
                    Description = productJson[YandexGamesKeys.Description].StringValue,
                    ImageUrl = productJson[YandexGamesKeys.ImageUrl].StringValue,
                    Price = productJson[YandexGamesKeys.Price].StringValue,
                    PriceValue = 0,
                    PriceCurrencyCode = productJson[YandexGamesKeys.PriceCurrencyCode].StringValue,
                    PriceCurrencyImage = productJson[YandexGamesKeys.PriceCurrencyImage],
                    Extra = productJson.HasField(YandexGamesKeys.Extra) ? productJson[YandexGamesKeys.Extra] : MstJson.CreateNull()
                };

                if (int.TryParse(productJson[YandexGamesKeys.PriceValue].StringValue, out int price))
                    productInfo.PriceValue = price;

                products.Add(productInfo);
            }

            Products = products;
            NotifyOnGetProducts(Products);
        }

        protected void Yg_OnGetPurchases(string json)
        {
            var result = new MstJson(json);

            if (result.HasField(YandexGamesKeys.Error))
            {
                NotifyOnGetPurchases(null);
                return;
            }

            var data = new PurchasesInfo()
            {
                serviceId = GameServiceId.YandexGames,
                data = result
            };

            NotifyOnGetPurchases(data);
        }

        protected void Yg_OnConsumePurchaseResult(string json)
        {
            var result = new MstJson(json);

            if (result.HasField(YandexGamesKeys.Error))
            {
                Logger.Warn($"Yandex purchase consume failed. Error: {result[YandexGamesKeys.Error].StringValue}");
                return;
            }

            Logger.Info("Yandex purchase consumed.");
        }

        #endregion
    }
}
