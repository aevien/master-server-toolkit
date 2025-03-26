using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Achievements/AchievementsDatabase")]
    public class AchievementsDatabase : ScriptableObject
    {
        [SerializeField]
        protected AchievementData[] achievements;
        public IEnumerable<AchievementData> Achievements => achievements;
    }
}