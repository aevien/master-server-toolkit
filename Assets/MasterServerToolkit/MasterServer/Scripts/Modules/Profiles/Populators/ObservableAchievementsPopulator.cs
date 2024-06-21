using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableAchievementsPopulator")]
    public class ObservableAchievementsPopulator : ObservableBasePopulator<List<AchievementData>>
    {
        public List<AchievementData> Achievements => defaultValue;

        protected override void OnValidate()
        {
            key = "achievements";
            base.OnValidate();
        }

        public override IObservableProperty Populate()
        {
            var achievements = new ObservableAchievements(ProfilePropertyOpCodes.achievements);

            foreach (var achievement in defaultValue)
            {
                achievements.UpdateProgress(achievement.id, 0, achievement.value);
            }

            return achievements;
        }
    }
}