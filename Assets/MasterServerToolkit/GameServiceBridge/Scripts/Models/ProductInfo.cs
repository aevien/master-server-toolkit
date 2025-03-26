using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Represents information about a product, which can be used for different game services.
    /// </summary>
    public class ProductInfo : SerializablePacket
    {
        /// <summary>
        /// Gets or sets the unique identifier of the product.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the title or name of the product.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the description of the product.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the URL of the product's image, stored in an MstJson object.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the formatted price string (e.g., "10 USD").
        /// </summary>
        public string Price { get; set; }

        /// <summary>
        /// Gets or sets the price value of the product.
        /// </summary>
        public int PriceValue { get; set; }

        /// <summary>
        /// Gets or sets the currency code for the product's price (e.g., USD, EUR).
        /// </summary>
        public string PriceCurrencyCode { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public MstJson PriceCurrencyImage { get; set; } = MstJson.NullObject;

        /// <summary>
        /// 
        /// </summary>
        public MstJson Extra { get; set; } = MstJson.NullObject;

        /// <summary>
        /// Gets or sets the platform associated with the product, such as VKPlay, YandexGames, or VK.
        /// </summary>
        public GameServiceId Platform { get; set; }

        /// <summary>
        /// Retrieves the URL of the currency icon, depending on the specified size.
        /// </summary>
        /// <param name="size">The size of the icon ("small", "medium"). Defaults to "small".</param>
        /// <returns>The URL of the currency icon.</returns>
        public string GetPriceCurrencyImage(string size = "small")
        {
            if (!PriceCurrencyImage.IsNull && PriceCurrencyImage.HasField(size))
            {
                return PriceCurrencyImage[size].StringValue;
            }
            else
            {
                return string.Empty;
            }
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField("id", Id);
            json.AddField("title", Title);
            json.AddField("description", Description);
            json.AddField("imageUrl", ImageUrl);
            json.AddField("price", Price);
            json.AddField("priceValue", PriceValue);
            json.AddField("priceCurrencyCode", PriceCurrencyCode);
            json.AddField("priceCurrencyImage", PriceCurrencyImage);
            json.AddField("platform", Platform.ToString());
            json.AddField("extra", Extra);

            return json;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Title = reader.ReadString();
            Description = reader.ReadString();
            ImageUrl = reader.ReadString();
            PriceValue = reader.ReadInt32();
            PriceCurrencyCode = reader.ReadString();
            Platform = reader.ReadEnum<GameServiceId>();
            Price = reader.ReadString();
            Extra = reader.ReadJson();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Title);
            writer.Write(Description);
            writer.Write(ImageUrl);
            writer.Write(PriceValue);
            writer.Write(PriceCurrencyCode);
            writer.Write(Platform);
            writer.Write(Price);
            writer.Write(Extra);
        }
    }
}