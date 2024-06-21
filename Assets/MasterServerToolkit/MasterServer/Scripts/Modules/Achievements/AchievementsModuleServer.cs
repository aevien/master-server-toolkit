using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModuleServer : MstBaseClient
    {
        public AchievementsModuleServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <param name="maxValue"></param>
        /// <param name="userId"></param>
        public void UpdateProgress(string id, int value, int maxValue, string userId)
        {
            UpdateProgress(id, value, maxValue, userId, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <param name="maxValue"></param>
        /// <param name="userId"></param>
        /// <param name="connetion"></param>
        public void UpdateProgress(string id, int value, int maxValue, string userId, IClientSocket connetion)
        {
            if (Mst.Server.Profiles.TryGetById(userId, out var profile)
                && profile.TryGet(ProfilePropertyOpCodes.achievements,
                    out ObservableAchievements achievements))
            {
                if (!achievements.IsProgressMet(id))
                {
                    achievements.UpdateProgress(id, 1, maxValue);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool IsProgressMet(string id, string userId)
        {
            if (Mst.Server.Profiles.TryGetById(userId, out var profile)
                    && profile.TryGet(ProfilePropertyOpCodes.achievements,
                        out ObservableAchievements achievements))
            {
                return achievements.IsProgressMet(id);
            }
            else
            {
                return false;
            }
        }
    }
}