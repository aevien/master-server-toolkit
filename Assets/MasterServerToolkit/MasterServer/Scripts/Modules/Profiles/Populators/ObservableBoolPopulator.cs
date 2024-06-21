using MasterServerToolkit.Extensions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableBoolPopulator")]
    public class ObservableBoolPopulator : ObservableBasePopulator<bool>
    {
        public override IObservableProperty Populate()
        {
            return new ObservableBool(key.ToUint16Hash(), defaultValue);
        }
    }
}
