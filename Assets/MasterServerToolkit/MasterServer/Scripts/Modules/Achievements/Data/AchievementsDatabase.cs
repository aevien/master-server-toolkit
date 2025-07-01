using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Achievements/AchievementsDatabase")]
    public class AchievementsDatabase : ScriptableObject, IEnumerable<AchievementData>
    {
        [SerializeField]
        protected AchievementData[] achievements;

        public IEnumerator<AchievementData> GetEnumerator()
        {
            foreach (var achievement in achievements)
            {
                yield return achievement;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}