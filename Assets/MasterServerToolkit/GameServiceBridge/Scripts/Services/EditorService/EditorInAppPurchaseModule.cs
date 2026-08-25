using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public class EditorInAppPurchaseModule : BaseInAppPurchaseModule
    {
        private const string PendingPurchasesKeyPrefix = "editorPendingPurchases:";
        private const string DataField = "data";
        private const string TokenField = "token";
        private const string ProductField = "product";
        private const string IdField = "id";

        public override void OnInit(IService service)
        {
            base.OnInit(service);
            CreateEditorProducts();
            IsSupported = true;
            IsReady = true;
        }

        private void CreateEditorProducts()
        {
            var editorProducts = Service.Options.GetField(nameof(EditorSdkSettings.inAppPurchaseProducts));
            List<ProductInfo> products = new();

            if (editorProducts.IsArray)
            {
                for (int i = 0; i < editorProducts.Count; i++)
                {
                    var productJson = editorProducts[i];

                    products.Add(new ProductInfo
                    {
                        Id = productJson[nameof(EditorInAppPurchaseProduct.id)].StringValue,
                        Title = productJson[nameof(EditorInAppPurchaseProduct.title)].StringValue,
                        Description = productJson[nameof(EditorInAppPurchaseProduct.description)].StringValue,
                        ImageUrl = productJson[nameof(EditorInAppPurchaseProduct.imgUrl)].StringValue,
                        Price = productJson[nameof(EditorInAppPurchaseProduct.price)].StringValue,
                        PriceValue = productJson[nameof(EditorInAppPurchaseProduct.priceValue)].IntValue,
                        PriceCurrencyCode = productJson[nameof(EditorInAppPurchaseProduct.priceCurrencyCode)].StringValue,
                        Platform = GameServiceId.Editor
                    });
                }
            }

            Products = products;
        }

        public override void GetProducts(ProductsHandler callback)
        {
            callback?.Invoke(Products);
        }

        public override void GetPurchases(PurchaseHandler callback)
        {
            callback?.Invoke(CreatePurchasesInfo(LoadPendingPurchases()));
        }

        public override void Purchase(string productId, string payload, PurchaseHandler callback)
        {
            if (!HasProduct(productId))
            {
                Logger.Warn($"Editor purchase rejected because product '{productId}' is not registered");
                callback?.Invoke(null);
                return;
            }

            if (!TryGetPendingPurchasesKey(out _))
            {
                callback?.Invoke(null);
                return;
            }

            MstJson receipt = CreateReceipt(productId, Mst.Helper.CreateGuidStringN());
            MstJson pendingPurchases = LoadPendingPurchases();
            pendingPurchases.Add(receipt);

            if (!SavePendingPurchases(pendingPurchases))
            {
                callback?.Invoke(null);
                return;
            }

            callback?.Invoke(CreatePurchasesInfo(receipt));
        }

        public override void ProcessPurchase(string purchaseId)
        {
            if (string.IsNullOrWhiteSpace(purchaseId) ||
                !TryGetPendingPurchasesKey(out _))
            {
                return;
            }

            MstJson pendingPurchases = LoadPendingPurchases();
            MstJson remainingPurchases = MstJson.CreateArray();
            bool removed = false;

            foreach (MstJson receipt in pendingPurchases)
            {
                if (TryReadReceipt(receipt, out _, out string token) &&
                    string.Equals(token, purchaseId, StringComparison.Ordinal))
                {
                    removed = true;
                    continue;
                }

                remainingPurchases.Add(receipt);
            }

            if (removed)
                SavePendingPurchases(remainingPurchases);
        }

        private bool HasProduct(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return false;

            foreach (ProductInfo product in Products)
            {
                if (product != null &&
                    string.Equals(product.Id, productId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private MstJson LoadPendingPurchases()
        {
            MstJson pendingPurchases = MstJson.CreateArray();

            if (!TryGetPendingPurchasesKey(out string key))
                return pendingPurchases;

            try
            {
                string rawData = PlayerPrefs.GetString(key, string.Empty);

                if (string.IsNullOrWhiteSpace(rawData))
                    return pendingPurchases;

                if (!MstJson.IsJson(rawData))
                {
                    Logger.Warn($"Ignored invalid Editor pending purchases for player '{Service.Player.Id}'");
                    return pendingPurchases;
                }

                var storedPurchases = new MstJson(rawData);

                if (!storedPurchases.IsArray)
                {
                    Logger.Warn($"Ignored invalid Editor pending purchases container for player '{Service.Player.Id}'");
                    return pendingPurchases;
                }

                foreach (MstJson receipt in storedPurchases)
                {
                    if (TryReadReceipt(receipt, out _, out _))
                    {
                        pendingPurchases.Add(receipt);
                    }
                    else
                    {
                        Logger.Warn($"Ignored invalid Editor pending purchase for player '{Service.Player.Id}'");
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to load Editor pending purchases for player '{Service.Player.Id}': {exception}");
            }

            return pendingPurchases;
        }

        private bool SavePendingPurchases(MstJson pendingPurchases)
        {
            if (!TryGetPendingPurchasesKey(out string key))
                return false;

            try
            {
                PlayerPrefs.SetString(key, pendingPurchases.ToString());
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to save Editor pending purchases for player '{Service.Player.Id}': {exception}");
                return false;
            }
        }

        private bool TryGetPendingPurchasesKey(out string key)
        {
            string playerId = Service?.Player?.Id;

            if (string.IsNullOrWhiteSpace(playerId))
            {
                key = string.Empty;
                Logger.Error("Editor purchases require a non-empty player ID");
                return false;
            }

            key = $"{PendingPurchasesKeyPrefix}{playerId}";
            return true;
        }

        private static PurchasesInfo CreatePurchasesInfo(MstJson data)
        {
            var envelope = MstJson.CreateObject();
            envelope.AddField(DataField, data);

            return new PurchasesInfo
            {
                serviceId = GameServiceId.Editor,
                data = envelope
            };
        }

        private static MstJson CreateReceipt(string productId, string token)
        {
            var product = MstJson.CreateObject();
            product.AddField(IdField, productId);

            var receipt = MstJson.CreateObject();
            receipt.AddField(TokenField, token);
            receipt.AddField(ProductField, product);
            return receipt;
        }

        private static bool TryReadReceipt(MstJson receipt, out string productId,
            out string token)
        {
            productId = string.Empty;
            token = string.Empty;

            if (receipt == null || !receipt.IsObject ||
                !receipt.HasField(TokenField) ||
                !receipt.HasField(ProductField))
            {
                return false;
            }

            MstJson product = receipt[ProductField];

            if (product == null || !product.IsObject || !product.HasField(IdField))
                return false;

            productId = product[IdField].StringValue;
            token = receipt[TokenField].StringValue;
            return !string.IsNullOrWhiteSpace(productId) &&
                !string.IsNullOrWhiteSpace(token);
        }
    }
}
