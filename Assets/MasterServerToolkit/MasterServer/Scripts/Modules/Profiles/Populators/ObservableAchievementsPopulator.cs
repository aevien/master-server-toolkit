using MasterServerToolkit.Extensions;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableAchievementsPopulator")]
    public class ObservableAchievementsPopulator : ObservableBasePopulator<ObservableAchievements>
    {
        [SerializeField]
        private HelpBox hpInfo = new HelpBox()
        {
            Text = "achievements key word is reserved for this populator. Use ProfilePropertyOpCodes to get this key for you property in your code.",
            Type = HelpBoxType.Warning
        };

        private void Reset()
        {
            key = "achievements";
        }

        protected override void OnValidate()
        {
            key = "achievements";
        }

        public override IObservableProperty Populate()
        {
            return new ObservableAchievements(key.ToUint16Hash());
        }
    }
}
