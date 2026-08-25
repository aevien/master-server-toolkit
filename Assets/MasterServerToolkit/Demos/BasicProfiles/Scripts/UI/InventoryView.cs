using MasterServerToolkit.Bridges;
using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Demos.BasicProfile
{
    public class InventoryView : UIView
    {
        #region INSPECTOR

        [Header("Inventory"), SerializeField]
        [Tooltip("Required ItemUI prefab instantiated once for every offer in the demo store.")]
        private ItemUI storeItemUIPrefab;
        [SerializeField]
        [Tooltip("Required RectTransform that receives instantiated store offer entries. Existing children are removed when the store is drawn.")]
        private RectTransform storeItemsContainer;
        [SerializeField]
        [Tooltip("Required ItemUI prefab instantiated for item stacks currently stored in the loaded profile.")]
        private ItemUI backpackItemUIPrefab;
        [SerializeField]
        [Tooltip("Required RectTransform that receives instantiated backpack entries. Existing children are removed when the profile inventory is drawn.")]
        private RectTransform backpackItemsContainer;
        [SerializeField]
        [Tooltip("Required Basic Profiles offer database used to build store entries and resolve inventory item IDs to names, icons, prices, and currencies.")]
        private StoreOffersDatabase storeOffers;

        [Header("Currencies"), SerializeField]
        [Tooltip("Required UIProperty that displays the loaded profile's bronze currency value.")]
        private UIProperty bronzeUIProperty;
        [SerializeField]
        [Tooltip("Required UIProperty that displays the loaded profile's silver currency value.")]
        private UIProperty silverUIProperty;
        [SerializeField]
        [Tooltip("Required UIProperty that displays the loaded profile's gold currency value.")]
        private UIProperty goldUIProperty;

        #endregion

        private readonly Dictionary<string, ItemUI> itemUis = new Dictionary<string, ItemUI>();

        protected  void Start()
        {
            DrawStoreOffers();
            Mst.Client.Profiles.OnProfileLoadedEvent += Profiles_OnProfileLoadedEvent;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mst.Client.Profiles.OnProfileLoadedEvent -= Profiles_OnProfileLoadedEvent;
        }

        private void Profiles_OnProfileLoadedEvent(ObservableProfile profile)
        {
            profile.OnPropertyUpdatedEvent += Profile_OnPropertyUpdatedEvent;

            if (profile.TryGet(ProfilePropertyOpCodes.items, out ObservableDictStringInt items))
            {
                DrawBackpackItems(items);

                items.OnAddEvent += Items_OnAddEvent;
                items.OnRemoveEvent += Items_OnRemoveEvent;
                items.OnSetEvent += Items_OnSetEvent;
            }

            foreach (var property in profile.Properties)
                Profile_OnPropertyUpdatedEvent(property.Key, property.Value);
        }

        private void Items_OnAddEvent(string newKey, int newValue)
        {
            if (!itemUis.TryGetValue(newKey, out var itemUI))
            {
                itemUI = Instantiate(backpackItemUIPrefab, backpackItemsContainer, false);
                itemUis.Add(newKey, itemUI);
            }

            if (storeOffers.TryGetOffer(newKey, out var storeOffer))
            {
                itemUI.Lable = $"{storeOffer.name} [{newValue}]";
                itemUI.SetButtonLable($"{storeOffer.price * 0.7f} {storeOffer.currency}");
                itemUI.Icon = storeOffer.iconSprite;
                itemUI.OnClick(() =>
                {
                    logger.Info($"Click on {storeOffer.name}");
                    SellItem(storeOffer);
                });

                itemUI.gameObject.SetActive(true);
            }
        }

        private void Items_OnRemoveEvent(string key, int removedValue)
        {
            if (itemUis.TryGetValue(key, out var itemUI))
            {
                itemUI.gameObject.SetActive(false);
            }
        }

        private void Items_OnSetEvent(string key, int oldValue, int newValue)
        {
            if (!itemUis.TryGetValue(key, out var itemUI))
            {
                itemUI = Instantiate(backpackItemUIPrefab, backpackItemsContainer, false);
                itemUis.Add(key, itemUI);
            }

            if (storeOffers.TryGetOffer(key, out var storeOffer))
            {
                itemUI.Lable = $"{storeOffer.name} [{newValue}]";
                itemUI.SetButtonLable($"{storeOffer.price * 0.7f} {storeOffer.currency}");
                itemUI.Icon = storeOffer.iconSprite;
                itemUI.OnClick(() =>
                {
                    logger.Info($"Click on {storeOffer.name}");
                    SellItem(storeOffer);
                });

                itemUI.gameObject.SetActive(true);
            }
        }

        private void Profile_OnPropertyUpdatedEvent(ushort key, IObservableProperty property)
        {
            if (key == ProfilePropertyOpCodes.bronze)
            {
                bronzeUIProperty.Lable = "Bronze";
                bronzeUIProperty.SetValue(property.As<ObservableInt>().Value);
            }
            else if (key == ProfilePropertyOpCodes.silver)
            {
                silverUIProperty.Lable = "Silver";
                silverUIProperty.SetValue(property.As<ObservableInt>().Value);
            }
            else if (key == ProfilePropertyOpCodes.gold)
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
                storeOfferUIInstance.SetButtonLable($"{storeOffer.price} {storeOffer.currency}");
                storeOfferUIInstance.Icon = storeOffer.iconSprite;
                storeOfferUIInstance.OnClick(() =>
                {
                    BuyItem(storeOffer);
                });
            }
        }

        private void DrawBackpackItems(IObservableProperty property)
        {
            itemUis.Clear();
            backpackItemsContainer.RemoveChildren();

            foreach (var itemKvp in property.As<ObservableDictStringInt>())
            {
                if (storeOffers.TryGetOffer(itemKvp.Key, out var storeOffer))
                {
                    var backpackItemUIInstance = Instantiate(backpackItemUIPrefab, backpackItemsContainer, false);
                    backpackItemUIInstance.Lable = $"{storeOffer.name} [{itemKvp.Value}]";
                    backpackItemUIInstance.SetButtonLable($"{storeOffer.price * 0.7f} {storeOffer.currency}");
                    backpackItemUIInstance.Icon = storeOffer.iconSprite;
                    backpackItemUIInstance.OnClick(() =>
                    {
                        logger.Info($"Click on {storeOffer.name}");
                        SellItem(storeOffer);
                    });

                    itemUis.Add(itemKvp.Key, backpackItemUIInstance);
                }
            }
        }

        private void BuyItem(StoreOffer storeOffer)
        {
            if (Mst.Connection.IsConnected)
            {
                var data = new BuySellItemPacket
                {
                    Id = storeOffer.id,
                    Price = storeOffer.price,
                    Currency = storeOffer.currency
                };

                Mst.Connection.SendMessage(MessageOpCodes.BuyDemoItem, data, (status, responce) =>
                {
                    if (status != Networking.ResponseStatus.Success)
                    {
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage($"An error occurred: {responce.AsString("Unhandled")}", null));
                        return;
                    }

                    logger.Info($"You bought {storeOffer.name}");
                });
            }
        }

        private void SellItem(StoreOffer storeOffer)
        {
            if (Mst.Connection.IsConnected)
            {
                var data = new BuySellItemPacket
                {
                    Id = storeOffer.id,
                    Price = (int)(storeOffer.price * 0.7f),
                    Currency = storeOffer.currency
                };

                Mst.Connection.SendMessage(MessageOpCodes.SellDemoItem, data, (status, responce) =>
                {
                    if (status != Networking.ResponseStatus.Success)
                    {
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage($"An error occurred: {responce.AsString("Unhandled")}", null));
                        return;
                    }

                    logger.Info($"You sold {storeOffer.name}");
                });
            }
        }
    }
}
