using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
        #region PURCHASES

        public bool IsInAppPurchaseSupported { get; protected set; }
        public IEnumerable<ProductInfo> Products { get; protected set; } = Enumerable.Empty<ProductInfo>();
        public PurchasesInfo Purchases { get; protected set; }

        public virtual void GetProducts(ProductsHandler callback)
        {
            if (IsInAppPurchaseSupported)
            {
                getProductsHandler = callback;
            }
            else
            {
                callback?.Invoke(Products);
            }
        }

        public virtual void Purchase(string productId, PurchaseHandler callback)
        {
            Purchase(productId, string.Empty, callback);
        }

        public virtual void Purchase(string productId, string payload, PurchaseHandler callback)
        {
            if (IsInAppPurchaseSupported)
            {
                purchaseHandler = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void GetPurchases(PurchaseHandler callback)
        {
            if (IsInAppPurchaseSupported)
            {
                getPurchasesHandler = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void ProcessPurchase(string purchaseId) { }

        #endregion
    }
}