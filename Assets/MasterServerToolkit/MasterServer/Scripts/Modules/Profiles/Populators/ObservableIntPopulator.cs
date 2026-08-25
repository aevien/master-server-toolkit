using MasterServerToolkit.Extensions;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableIntPopulator")]
    public class ObservableIntPopulator : ObservableBasePopulator<int>
    {
        [SerializeField, Tooltip("Send changes as deltas so concurrent additive updates can be merged safely")]
        private bool useDeltaUpdates;

        public override IObservableProperty Populate()
        {
            return new ObservableInt(key.ToUint16Hash(), defaultValue, useDeltaUpdates);
        }
    }
}
