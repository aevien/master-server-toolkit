using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementsModuleClient : MstBaseClient
    {
        public event Action<string> OnAchievementObtained;

        public AchievementsModuleClient(IClientSocket connection) : base(connection)
        {
            RegisterMessageHandler(MstOpCodes.ClientAchievementProgressIsMet, OnProgressIsMet);
        }

        private void OnProgressIsMet(IIncomingMessage message)
        {
            OnAchievementObtained?.Invoke(message.AsString());
        }

        public void UpdateProgress(string id, int value)
        {
            UpdateProgress(id, value, Connection);
        }

        public void UpdateProgress(string id, int value, IClientSocket connetion)
        {
            var data = new UpdateAchievementProgressPacket()
            {
                id = id,
                value = value,
                username = ""
            };

            connetion.SendMessage(MstOpCodes.ClientUpdateAchievementProgress, data);
        }

        public void CheckProgress(string id)
        {

        }
    }
}