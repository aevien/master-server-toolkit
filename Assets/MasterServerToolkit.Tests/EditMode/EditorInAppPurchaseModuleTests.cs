using MasterServerToolkit.GameService;
using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Tests.EditMode
{
    [TestFixture]
    public class EditorInAppPurchaseModuleTests
    {
        private const string PendingPurchasesKeyPrefix = "editorPendingPurchases:";
        private const string ProductId = "editor-product";

        private readonly List<GameObject> serviceObjects = new();
        private readonly List<string> playerIds = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject serviceObject in serviceObjects)
            {
                if (serviceObject != null)
                    UnityEngine.Object.DestroyImmediate(serviceObject);
            }

            foreach (string playerId in playerIds)
                PlayerPrefs.DeleteKey(GetPendingPurchasesKey(playerId));

            PlayerPrefs.Save();
            serviceObjects.Clear();
            playerIds.Clear();
        }

        [Test]
        public void Purchase_RegisteredProduct_PersistsReceiptBeforeCallback()
        {
            string playerId = CreatePlayerId();
            EditorInAppPurchaseModule module = CreateModule(playerId);
            bool persistedBeforeCallback = false;
            PurchasesInfo result = null;

            module.Purchase(ProductId, purchase =>
            {
                result = purchase;
                string stored = PlayerPrefs.GetString(
                    GetPendingPurchasesKey(playerId), string.Empty);
                persistedBeforeCallback = MstJson.IsJson(stored) &&
                    new MstJson(stored).Count == 1;
            });

            Assert.That(result, Is.Not.Null);
            Assert.That(persistedBeforeCallback, Is.True);
            Assert.That(ReadSingleToken(result), Is.Not.Empty);
        }

        [Test]
        public void Purchase_UnknownProduct_ReturnsNullWithoutPersisting()
        {
            string playerId = CreatePlayerId();
            EditorInAppPurchaseModule module = CreateModule(playerId);
            PurchasesInfo result = new PurchasesInfo();

            module.Purchase("unknown-product", purchase => result = purchase);

            Assert.That(result, Is.Null);
            Assert.That(PlayerPrefs.HasKey(GetPendingPurchasesKey(playerId)), Is.False);
        }

        [Test]
        public void ProcessPurchase_RemovesOnlyMatchingPendingReceipt()
        {
            string playerId = CreatePlayerId();
            EditorInAppPurchaseModule module = CreateModule(playerId);
            PurchasesInfo first = Purchase(module);
            PurchasesInfo second = Purchase(module);
            string firstToken = ReadSingleToken(first);
            string secondToken = ReadSingleToken(second);

            module.ProcessPurchase(firstToken);
            PurchasesInfo pending = GetPurchases(module);

            Assert.That(pending.data["data"].Count, Is.EqualTo(1));
            Assert.That(ReadToken(pending.data["data"][0]), Is.EqualTo(secondToken));
        }

        [Test]
        public void GetPurchases_IsolatesPendingReceiptsByPlayerId()
        {
            EditorInAppPurchaseModule firstPlayer = CreateModule(CreatePlayerId());
            EditorInAppPurchaseModule secondPlayer = CreateModule(CreatePlayerId());

            Purchase(firstPlayer);

            Assert.That(GetPurchases(firstPlayer).data["data"].Count, Is.EqualTo(1));
            Assert.That(GetPurchases(secondPlayer).data["data"].Count, Is.Zero);
        }

        [Test]
        public void CorruptedPendingJson_IsIgnoredAndReplacedByNextPurchase()
        {
            string playerId = CreatePlayerId();
            EditorInAppPurchaseModule module = CreateModule(playerId);
            PlayerPrefs.SetString(GetPendingPurchasesKey(playerId), "{invalid-json");
            PlayerPrefs.Save();

            Assert.That(GetPurchases(module).data["data"].Count, Is.Zero);

            PurchasesInfo purchase = Purchase(module);
            string stored = PlayerPrefs.GetString(
                GetPendingPurchasesKey(playerId), string.Empty);

            Assert.That(purchase, Is.Not.Null);
            Assert.That(MstJson.IsJson(stored), Is.True);
            Assert.That(new MstJson(stored).Count, Is.EqualTo(1));
        }

        private EditorInAppPurchaseModule CreateModule(string playerId)
        {
            var serviceObject = new GameObject($"EditorService-{playerId}");
            serviceObjects.Add(serviceObject);
            playerIds.Add(playerId);

            var service = serviceObject.AddComponent<EditorService>();
            service.Logger = Mst.Create.Logger(nameof(EditorInAppPurchaseModuleTests));

            var settings = new EditorSdkSettings
            {
                startAsGuest = false,
                userId = playerId,
                authenticatedDisplayName = "Editor Test Player",
                inAppPurchaseProducts = new[]
                {
                    new EditorInAppPurchaseProduct { id = ProductId }
                }
            };

            service.OnBeforeInit(settings.ToJson());
            ((EditorPlayerModule)service.Player).OnInit(service);

            var module = (EditorInAppPurchaseModule)service.IAP;
            module.OnInit(service);
            return module;
        }

        private string CreatePlayerId() => $"editor-iap-test-{Guid.NewGuid():N}";

        private static PurchasesInfo Purchase(EditorInAppPurchaseModule module)
        {
            PurchasesInfo result = null;
            module.Purchase(ProductId, purchase => result = purchase);
            return result;
        }

        private static PurchasesInfo GetPurchases(EditorInAppPurchaseModule module)
        {
            PurchasesInfo result = null;
            module.GetPurchases(purchases => result = purchases);
            return result;
        }

        private static string ReadSingleToken(PurchasesInfo purchase) =>
            ReadToken(purchase.data["data"]);

        private static string ReadToken(MstJson receipt) =>
            receipt["token"].StringValue;

        private static string GetPendingPurchasesKey(string playerId) =>
            $"{PendingPurchasesKeyPrefix}{playerId}";
    }
}
