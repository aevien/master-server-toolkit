using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
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
        public MstJson ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the price value of the product.
        /// </summary>
        public int PriceValue { get; set; }

        /// <summary>
        /// Gets or sets the currency code for the product's price (e.g., USD, EUR).
        /// </summary>
        public string PriceCurrencyCode { get; set; }

        /// <summary>
        /// Gets or sets the platform associated with the product, such as VKPlay, YandexGames, or VK.
        /// </summary>
        public GameServiceId Platform { get; set; }

        /// <summary>
        /// Gets or sets the formatted price string (e.g., "10 USD").
        /// </summary>
        public string PriceFormat { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public MstProperties ExtraProperties { get; set; } = new MstProperties();

        /// <summary>
        /// Retrieves the URL of the currency icon, depending on the specified size.
        /// </summary>
        /// <param name="size">The size of the icon ("small", "medium"). Defaults to "small".</param>
        /// <returns>The URL of the currency icon.</returns>
        public string GetPriceCurrencyImage(string size = "small")
        {
            if (ImageUrl.IsNull)
            {
                if (ImageUrl.HasField(size))
                {
                    return ImageUrl[size].StringValue;
                }
                else
                {
                    return ImageUrl.StringValue;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField(nameof(Id), Id);
            json.AddField(nameof(Title), Title);
            json.AddField(nameof(Description), Description);
            json.AddField(nameof(ImageUrl), ImageUrl);
            json.AddField(nameof(PriceValue), PriceValue);
            json.AddField(nameof(PriceCurrencyCode), PriceCurrencyCode);
            json.AddField(nameof(Platform), Platform.ToString());
            json.AddField(nameof(PriceFormat), PriceFormat.ToString());
            json.AddField(nameof(ExtraProperties), ExtraProperties.ToJson());

            return json;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Title = reader.ReadString();
            Description = reader.ReadString();
            ImageUrl = reader.ReadJson();
            PriceValue = reader.ReadInt32();
            PriceCurrencyCode = reader.ReadString();
            Platform = reader.ReadEnum<GameServiceId>();
            PriceFormat = reader.ReadString();
            ExtraProperties = new MstProperties(reader.ReadDictionary());
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
            writer.Write(PriceFormat);
            writer.Write(ExtraProperties.ToDictionary());
        }
    }
}