using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MasterServerToolkit.GameService
{
    [Serializable]
    public class EditorSdkSettings
    {
        [FormerlySerializedAs("isGuest")]
        [Tooltip("Starts the Editor service with a persistent guest platform identity and removes the saved MST authentication token before the game authentication workflow begins. User Id and Authenticated Display Name are applied only after Player.Authenticate() succeeds.")]
        public bool startAsGuest = true;
        [Tooltip("Controls whether the Editor service offers interactive platform authentication. Disable it with Start As Guest enabled to test the MST username/password login and registration flow used by guest-only desktop services.")]
        public bool interactiveAuthenticationSupported = true;
        [Tooltip("Non-empty platform identifier used after Editor authentication, or immediately when Start As Guest is disabled. Changing it simulates another platform account.")]
        public string userId = "6f34aae2-55eb-4a27-806f-f4b8d5ddb2f8";
        [FormerlySerializedAs("userDisplayName")]
        [Tooltip("Platform display name used with User Id after Editor authentication. Leave empty to simulate a platform that does not provide a name and test the game's generated-name fallback.")]
        public string authenticatedDisplayName = "Jack Richer";
        [Tooltip("ISO language code returned by the Editor service, for example en, ru, or tr.")]
        public string lang = "en";
        [Tooltip("Products exposed by the simulated Editor in-app-purchase module.")]
        public EditorInAppPurchaseProduct[] inAppPurchaseProducts = new EditorInAppPurchaseProduct[0];

        public MstJson ToJson()
        {
            var products = MstJson.CreateArray();

            foreach (var p in inAppPurchaseProducts)
            {
                products.Add(p.ToJson());
            }

            var json = MstJson.CreateObject();
            json.AddField(nameof(startAsGuest), startAsGuest);
            json.AddField(nameof(interactiveAuthenticationSupported), interactiveAuthenticationSupported);
            json.AddField(nameof(inAppPurchaseProducts), products);
            json.AddField(nameof(lang), lang);
            json.AddField(nameof(userId), userId);
            json.AddField(nameof(authenticatedDisplayName), authenticatedDisplayName);
            return json;
        }
    }

    [Serializable]
    public class EditorInAppPurchaseProduct
    {
        [Tooltip("Stable product identifier passed to purchase and acknowledgement calls.")]
        public string id = "editor_pack";
        [Tooltip("Product title returned by the simulated platform catalogue.")]
        public string title = "Editor Pack";
        [Tooltip("Product description returned by the simulated platform catalogue.")]
        public string description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua";
        [Tooltip("HTTP(S) image URL returned by the simulated platform catalogue.")]
        public string imgUrl = "https://i.pravatar.cc/300";
        [Tooltip("Human-readable platform price, including currency, shown by store UI.")]
        public string price = "100 USD";
        [Tooltip("Numeric price in the smallest unit expected by the test catalogue. This value is not charged by a real platform.")]
        public int priceValue = 100;
        [Tooltip("ISO 4217 currency code associated with Price Value, for example usd.")]
        public string priceCurrencyCode = "usd";

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.AddField(nameof(id), id);
            json.AddField(nameof(title), title);
            json.AddField(nameof(description), description);
            json.AddField(nameof(imgUrl), imgUrl);
            json.AddField(nameof(price), price);
            json.AddField(nameof(priceValue), priceValue);
            json.AddField(nameof(priceCurrencyCode), priceCurrencyCode);
            return json;
        }
    }

    public class EditorService : BaseService
    {
        public override long ServerTime { get => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

        public override void OnBeforeInit(MstJson options)
        {
            Id = GameServiceId.Editor;
            AppId = $"{Application.identifier}";
            Lang = options[nameof(EditorSdkSettings.lang)].StringValue;

            if (options[nameof(EditorSdkSettings.startAsGuest)].BoolValue)
                Mst.Client.Auth.ClearAuthToken();

            Player = AddModule<EditorPlayerModule>();
            IAP = AddModule<EditorInAppPurchaseModule>();
            Leaderboards = AddModule<EditorLeaderboardsModule>();
            Ad = AddModule<EditorAdvertisementModule>();
            Analytics = AddModule<EditorAnalyticsModule>();
            Storage = AddModule<EditorStorageModule>();
            Share = AddModule<EditorShareModule>();

            base.OnBeforeInit(options);
        }
    }
}
