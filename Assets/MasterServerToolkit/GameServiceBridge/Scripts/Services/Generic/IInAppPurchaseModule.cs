using System.Collections.Generic;

namespace MasterServerToolkit.GameService
{

    /// <summary>
    /// Represents a callback that receives the available in-app purchase products.
    /// </summary>
    /// <param name="products">The available products.</param>
    public delegate void ProductsHandler(IEnumerable<ProductInfo> products);

    /// <summary>
    /// Represents a callback that receives purchase operation results.
    /// </summary>
    /// <param name="purchase">The purchase information, or <see langword="null"/> when the operation failed or is unsupported.</param>
    public delegate void PurchaseHandler(PurchasesInfo purchase);

    /// <summary>
    /// Defines the contract for in-app purchase features exposed by the current game service.
    /// </summary>
    public interface IInAppPurchaseModule : IServiceModule
    {
        /// <summary>
        /// Gets the current collection of available products.
        /// </summary>
        IEnumerable<ProductInfo> Products { get; }

        /// <summary>
        /// Retrieves the list of available products from the platform.
        /// </summary>
        /// <param name="callback">The callback that receives the available products.</param>
        void GetProducts(ProductsHandler callback);

        /// <summary>
        /// Initiates a purchase transaction for the specified product.
        /// </summary>
        /// <param name="productId">The unique identifier of the product to purchase.</param>
        /// <param name="callback">The callback that receives the purchase result.</param>
        void Purchase(string productId, PurchaseHandler callback);

        /// <summary>
        /// Initiates a purchase transaction for the specified product with an additional developer payload.
        /// </summary>
        /// <param name="productId">The unique identifier of the product to purchase.</param>
        /// <param name="payload">The developer payload to associate with the purchase.</param>
        /// <param name="callback">The callback that receives the purchase result.</param>
        void Purchase(string productId, string payload, PurchaseHandler callback);

        /// <summary>
        /// Retrieves the completed purchases for the current player.
        /// </summary>
        /// <param name="callback">The callback that receives the completed purchases.</param>
        void GetPurchases(PurchaseHandler callback);

        /// <summary>
        /// Acknowledges or consumes a completed purchase transaction.
        /// </summary>
        /// <param name="purchaseId">The unique identifier of the purchase transaction to process.</param>
        void ProcessPurchase(string purchaseId);
    }
}
