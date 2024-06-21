using MasterServerToolkit.Extensions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableStringPopulator")]
    public class ObservableStringPopulator : ObservableBasePopulator<string>
    {
        public override IObservableProperty Populate()
        {
            return new ObservableString(key.ToUint16Hash(), defaultValue);
        }
    }
}
