using MasterServerToolkit.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableListStringPopulator")]
    public class ObservableListStringPopulator : ObservableBaseListPopulator<string>
    {
        public override IObservableProperty Populate()
        {
            return new ObservableListString(key.ToUint16Hash(), new List<string>(defaultValue));
        }
    }
}