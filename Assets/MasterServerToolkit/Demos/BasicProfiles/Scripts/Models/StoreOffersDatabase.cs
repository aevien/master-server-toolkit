using MasterServerToolkit.MasterServer;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.Demos.BasicProfile
{
    [CreateAssetMenu(fileName = "StoreOffersDatabase", menuName = MstConstants.CreateMenu + "Examples/BasicProfile/Create store offers database")]
    public class StoreOffersDatabase : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Offers displayed by the Basic Profiles store. Keep item IDs unique; an empty array produces an empty store without changing the player's profile.")]
        private StoreOffer[] offers;

        public StoreOffer[] Offers => offers;

        public StoreOffer GetOffer(string id)
        {
            return offers.Where(i => i.id == id).FirstOrDefault();
        }

        public bool TryGetOffer(string id, out StoreOffer storeOffer)
        {
            storeOffer = GetOffer(id);
            return storeOffer != null;
        }
    }
}
