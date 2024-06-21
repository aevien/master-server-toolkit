using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Data/Achievement")]
    public class AchievementData : ScriptableObject
    {
        public string id = "achievementId";
        public int value = 0;

        public bool IsMet(ObservableProfile profile)
        {
            if (profile.TryGet(ProfilePropertyOpCodes.achievements, out ObservableDictStringInt property))
            {
                return property[id] >= value;
            }

            return false;
        }
    }
}