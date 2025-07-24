using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Achievements/AchievementsDatabase")]
    public class AchievementsDatabase : ObjectsDatabase<AchievementData>
    {
        [ContextMenu("Populate")]
        private void Populate()
        {
            FindObjects();
        }

        protected override string SearchType()
        {
            return "t:AchievementData";
        }
    }
}