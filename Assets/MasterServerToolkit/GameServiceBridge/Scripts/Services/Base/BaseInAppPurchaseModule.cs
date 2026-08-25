using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.GameService
{
    public class BaseInAppPurchaseModule : BaseServiceModule, IInAppPurchaseModule
    {
        public IEnumerable<ProductInfo> Products { get; protected set; } = Enumerable.Empty<ProductInfo>();
        public override bool IsSupported { get; protected set; }

        protected ProductsHandler getProductsHandler;
        protected PurchaseHandler purchaseHandler;
        protected PurchaseHandler getPurchasesHandler;

        public virtual void GetProducts(ProductsHandler callback)
        {
            if (IsSupported)
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
            if (IsSupported)
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
            if (IsSupported)
            {
                getPurchasesHandler = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void ProcessPurchase(string purchaseId) { }

        protected void NotifyOnGetProducts(IEnumerable<ProductInfo> products)
        {
            getProductsHandler?.Invoke(products);
            getProductsHandler = null;
        }

        protected void NotifyOnPurchase(PurchasesInfo purchase)
        {
            purchaseHandler?.Invoke(purchase);
            purchaseHandler = null;
        }

        protected void NotifyOnGetPurchases(PurchasesInfo purchases)
        {
            getPurchasesHandler?.Invoke(purchases);
            getPurchasesHandler = null;
        }
    }
}
