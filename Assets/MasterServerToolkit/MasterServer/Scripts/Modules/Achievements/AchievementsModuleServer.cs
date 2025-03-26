using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModuleServer : MstBaseClient
    {
        private readonly List<UpdateAchievementProgressPacket> achievementsToUpdate = new List<UpdateAchievementProgressPacket>();
        private Coroutine sendUpdatesCoroutine;

        public float UpdatesInterval { get; set; } = 1f;

        public AchievementsModuleServer(IClientSocket connection) : base(connection) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        /// <param name="userId"></param>
        public void UpdateProgress(string key, int progress, string userId)
        {
            UpdateProgress(key, progress, userId, Connection);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        /// <param name="userId"></param>
        /// <param name="connection"></param>
        public void UpdateProgress(string key, int progress, string userId, IClientSocket connection)
        {
            var data = achievementsToUpdate.Find(d => d.key == key && d.userId == userId);

            if (data != null)
            {
                data.progress += progress;
            }
            else
            {
                data = new UpdateAchievementProgressPacket()
                {
                    key = key,
                    userId = userId,
                    progress = progress,
                };

                achievementsToUpdate.Add(data);
            }

            if (sendUpdatesCoroutine != null)
            {
                return;
            }

            sendUpdatesCoroutine = MstTimer.Instance.StartCoroutine(KeepSendingUpdates(connection));
        }

        private IEnumerator KeepSendingUpdates(IClientSocket connection)
        {
            while (true)
            {
                yield return new WaitForSeconds(UpdatesInterval);

                if (achievementsToUpdate.Count == 0)
                {
                    continue;
                }

                var data = achievementsToUpdate.ToBytes();
                connection.SendMessage(MstOpCodes.ServerUpdateAchievementProgress, data);
                achievementsToUpdate.Clear();
            }
        }
    }
}