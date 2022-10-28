using MasterServerToolkit.MasterServer;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.Examples.BasicProfile
{
    [CreateAssetMenu(fileName = "StoreOffersDatabase", menuName = MstConstants.CreateMenu + "Examples/BasicProfile/Create store offers database")]
    public class StoreOffersDatabase : ScriptableObject
    {
        [SerializeField]
        private StoreOffer[] offers;

        public StoreOffer[] Offers => offers;

        public StoreOffer GetOffer(string id)
        {
            return offers.ToList().Find(i => i.id == id);
        }

        public bool TryGetOffer(string id, out StoreOffer storeOffer)
        {
            storeOffer = GetOffer(id);
            return storeOffer != null;
        }
    }
}