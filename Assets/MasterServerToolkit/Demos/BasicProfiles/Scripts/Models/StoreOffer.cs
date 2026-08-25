using System;
using UnityEngine;

namespace MasterServerToolkit.Demos.BasicProfile
{
    [Serializable]
    public class StoreOffer
    {
        [Tooltip("Unique item identifier stored as the key in the demo profile inventory. IDs must be non-empty and unique within the offers database.")]
        public string id;
        [Tooltip("Player-facing item name displayed by the demo store and backpack UI. This sample does not localize the value.")]
        public string name;
        [Tooltip("Optional icon displayed for this offer in the store and backpack item prefabs.")]
        public Sprite iconSprite;
        [Tooltip("Whole-number purchase price. Use a positive value; this demo performs no Inspector validation and sells the item back for 70 percent of this value.")]
        public int price;
        [Tooltip("Profile currency property used to pay for the item, for example bronze, silver, or gold. The value must match a currency property created by the demo profiles module.")]
        public string currency;
    }
}
