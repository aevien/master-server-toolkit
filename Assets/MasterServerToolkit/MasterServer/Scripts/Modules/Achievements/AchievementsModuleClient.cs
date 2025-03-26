using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModuleClient : MstBaseClient
    {
        public event Action<string> OnAchievementUnlocked;

        public IEnumerable<AchievementProgressInfo> Progresses
        {
            get
            {
                if (Mst.Client.Profiles.HasProfile
                    && Mst.Client.Profiles.Current
                    .TryGet(ProfilePropertyOpCodes.achievements, out ObservableAchievements property))
                {
                    return property.Value;
                }
                else
                {
                    return new List<AchievementProgressInfo>();
                }
            }
        }

        public AchievementsModuleClient(IClientSocket connection) : base(connection)
        {
            RegisterMessageHandler(MstOpCodes.ClientAchievementUnlocked, OnUnlocked);
        }

        private void OnUnlocked(IIncomingMessage message)
        {
            OnAchievementUnlocked?.Invoke(message.AsString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        public void UpdateProgress(string key, int progress)
        {
            UpdateProgress(key, progress, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        /// <param name="connetion"></param>
        public void UpdateProgress(string key, int progress, IClientSocket connetion)
        {
            var data = new UpdateAchievementProgressPacket()
            {
                key = key,
                progress = progress,
                userId = ""
            };

            connetion.SendMessage(MstOpCodes.ClientUpdateAchievementProgress, data, (status, response) =>
            {
                if (status == ResponseStatus.Success)
                {
                    OnAchievementUnlocked?.Invoke(key);
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsUnlocked(string key)
        {
            var progress = Progresses.ToList().Find(p => p.key == key);

            if (progress == null)
            {
                return false;
            }
            else
            {
                return progress.IsUnlocked;
            }
        }
    }
}