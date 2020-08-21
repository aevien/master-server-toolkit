using Newtonsoft.Json;

namespace MasterServerToolkit.MasterServer
{
    public class MstIpInfo
    {
        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("ip_decimal")]
        public decimal IpDecimal { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("country_eu")]
        public bool CountryEu { get; set; }

        [JsonProperty("country_iso")]
        public string CountryIso { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }

        [JsonProperty("asn")]
        public string Asn { get; set; }

        [JsonProperty("asn_org")]
        public string AsnOrg { get; set; }

        [JsonProperty("user_agent")]
        public UserAgent UserAgent { get; set; }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Ip", Ip);
            options.Add("IpDecimal", IpDecimal);
            options.Add("Country", Country);
            options.Add("CountryEu", CountryEu);
            options.Add("CountryIso", CountryIso);
            options.Add("City", City);
            options.Add("Latitude", Latitude);
            options.Add("Longitude", Longitude);
            options.Add("Asn", Asn);
            options.Add("AsnOrg", AsnOrg);
            options.Add("UserAgent", UserAgent?.ToString());

            return options.ToReadableString();
        }
    }

    public class UserAgent
    {
        [JsonProperty("product")]
        public string Product { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("raw_value")]
        public string RawValue { get; set; }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Product", Product);
            options.Add("Version", Version);
            options.Add("Comment", Comment);
            options.Add("RawValue", RawValue);

            return options.ToReadableString();
        }
    }
}
