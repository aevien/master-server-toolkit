using MasterServerToolkit.Extensions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableLongPopulator")]
    public class ObservableLongPopulator : ObservableBasePopulator<long>
    {
        public override IObservableProperty Populate()
        {
            return new ObservableLong(key.ToUint16Hash(), defaultValue);
        }
    }
}