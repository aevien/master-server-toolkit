using MasterServerToolkit.Extensions;
using MasterServerToolkit.Games;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public class InventoryView : UIView
    {
        #region INSPECTOR

        [Header("Inventory"), SerializeField]
        private ItemUI storeItemUIPrefab;
        [SerializeField]
        private RectTransform storeItemsContainer;
        [SerializeField]
        private ItemUI backpackItemUIPrefab;
        [SerializeField]
        private RectTransform backpackItemsContainer;
        [SerializeField]
        private StoreOffersDatabase storeOffers;

        [Header("Currencies"), SerializeField]
        private UIProperty bronzeUIProperty;
        [SerializeField]
        private UIProperty silverUIProperty;
        [SerializeField]
        private UIProperty goldUIProperty;

        #endregion

        private ProfileLoaderBehaviour profileLoader;

        protected override void Start()
        {
            base.Start();

            DrawStoreOffers();

            profileLoader = FindObjectOfType<ProfileLoaderBehaviour>();
            profileLoader.OnProfileLoadedEvent.AddListener(OnProfileLoadedEventHandler);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profileLoader && profileLoader.Profile != null)
                profileLoader.Profile.OnPropertyUpdatedEvent -= Profile_OnPropertyUpdatedEvent;
        }

        private void OnProfileLoadedEventHandler()
        {
            profileLoader.Profile.OnPropertyUpdatedEvent += Profile_OnPropertyUpdatedEvent;

            foreach (var property in profileLoader.Profile.Properties)
                Profile_OnPropertyUpdatedEvent(property.Key, property.Value);
        }

        private void Profile_OnPropertyUpdatedEvent(ushort key, IObservableProperty property)
        {
            if (key == ProfilePropertyKeys.items)
                DrawBackpackItems(property.As<ObservableDictStringInt>().Value);
            else if (key == ProfilePropertyKeys.bronze)
            {
                bronzeUIProperty.Lable = "Bronze";
                bronzeUIProperty.SetValue(property.As<ObservableInt>().Value);
            }
            else if (key == ProfilePropertyKeys.silver)
            {
                silverUIProperty.Lable = "Silver";
                silverUIProperty.SetValue(property.As<ObservableInt>().Value);
            }
            else if (key == ProfilePropertyKeys.gold)
            {
                goldUIProperty.Lable = "Gold";
                goldUIProperty.SetValue(property.As<ObservableInt>().Value);
            }
        }

        private void DrawStoreOffers()
        {
            storeItemsContainer.RemoveChildren();

            foreach (var storeOffer in storeOffers.Offers)
            {
                var storeOfferUIInstance = Instantiate(storeItemUIPrefab, storeItemsContainer, false);
                storeOfferUIInstance.Lable = storeOffer.name;
                storeOfferUIInstance.SetButtonLable($"Buy for {storeOffer.price} {storeOffer.currency}");
                storeOfferUIInstance.Icon = storeOffer.iconSprite;
                storeOfferUIInstance.OnClick(() =>
                {
                    logger.Info($"Click on {storeOffer.name}");

                    BuyItem(storeOffer);
                });
            }
        }

        private void DrawBackpackItems(Dictionary<string, int> value)
        {
            backpackItemsContainer.RemoveChildren();

            foreach (var itemKvp in value)
            {
                if (storeOffers.TryGetOffer(itemKvp.Key, out var storeOffer))
                {
                    var backpackItemUIInstance = Instantiate(backpackItemUIPrefab, backpackItemsContainer, false);
                    backpackItemUIInstance.Lable = $"{storeOffer.name} [{itemKvp.Value}]";
                    backpackItemUIInstance.SetButtonLable($"Sell for {storeOffer.price * 0.7f} {storeOffer.currency}");
                    backpackItemUIInstance.Icon = storeOffer.iconSprite;
                    backpackItemUIInstance.OnClick(() =>
                    {
                        logger.Info($"Click on {storeOffer.name}");
                        SellItem(storeOffer);
                    });
                }
            }
        }

        private void BuyItem(StoreOffer storeOffer)
        {
            var data = new BuySellItemPacket
            {
                Id = storeOffer.id,
                Price = storeOffer.price,
                Currency = storeOffer.currency
            };

            Mst.Client.Connection.SendMessage(MessageOpCodes.BuyDemoItem, data, (status, responce) =>
            {
                if (status != Networking.ResponseStatus.Success)
                {
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage($"An error occurred: {responce.AsString("Unhandled")}", null));
                    return;
                }

                logger.Info($"You bought {storeOffer.name}");
            });
        }

        private void SellItem(StoreOffer storeOffer)
        {
            var data = new BuySellItemPacket
            {
                Id = storeOffer.id,
                Price = (int)(storeOffer.price * 0.7f),
                Currency = storeOffer.currency
            };

            Mst.Client.Connection.SendMessage(MessageOpCodes.SellDemoItem, data, (status, responce) =>
            {
                if (status != Networking.ResponseStatus.Success)
                {
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage($"An error occurred: {responce.AsString("Unhandled")}", null));
                    return;
                }

                logger.Info($"You sold {storeOffer.name}");
            });
        }
    }
}
