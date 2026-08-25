using MasterServerToolkit.Json;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Contains normalized launch referrer data provided by the active game service.
    /// </summary>
    public class ReferrerInfo
    {
        /// <summary>
        /// Gets or sets the referrer type. Empty value means that referrer data is absent.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the service-specific promotion identifier.
        /// </summary>
        public string PromoId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional launch intent.
        /// </summary>
        public string Intent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional in-app product identifier.
        /// </summary>
        public string InappId { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this instance contains referrer data.
        /// </summary>
        public bool HasData => !string.IsNullOrWhiteSpace(Type);

        /// <summary>
        /// Creates a <see cref="ReferrerInfo"/> instance from JSON data.
        /// </summary>
        /// <param name="json">The source JSON object.</param>
        /// <returns>A normalized referrer object.</returns>
        public static ReferrerInfo FromJson(MstJson json)
        {
            if (json == null || json.IsNull || !json.IsObject)
                return new ReferrerInfo();

            string type = GetString(json, "type");

            if (string.IsNullOrWhiteSpace(type))
                return new ReferrerInfo();

            return new ReferrerInfo
            {
                Type = type,
                PromoId = GetString(json, "promoId"),
                Intent = GetString(json, "intent"),
                InappId = GetString(json, "inappId")
            };
        }

        /// <summary>
        /// Converts this referrer object to JSON.
        /// </summary>
        /// <returns>A JSON representation of this referrer.</returns>
        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.SetField("type", Type);
            json.SetField("promoId", PromoId);
            json.SetField("intent", Intent);
            json.SetField("inappId", InappId);
            return json;
        }

        private static string GetString(MstJson json, string fieldName)
        {
            MstJson field = json.GetField(fieldName);
            return field == null || field.IsNull ? string.Empty : field.StringValue ?? string.Empty;
        }
    }
}
