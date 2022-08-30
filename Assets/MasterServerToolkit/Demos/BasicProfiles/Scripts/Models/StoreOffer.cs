using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicProfile
{
    [Serializable]
    public class StoreOffer
    {
        public string id;
        public string name;
        public Sprite iconSprite;
        public int price;
        public string currency;
    }
}