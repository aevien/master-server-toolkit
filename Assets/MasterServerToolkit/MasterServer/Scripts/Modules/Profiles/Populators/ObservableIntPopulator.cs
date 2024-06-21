using MasterServerToolkit.Extensions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableIntPopulator")]
    public class ObservableIntPopulator : ObservableBasePopulator<int>
    {
        public override IObservableProperty Populate()
        {
            return new ObservableInt(key.ToUint16Hash(), defaultValue);
        }
    }
}