using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class QuestProgressInfo : SerializablePacket
    {
        public string id;
        public string key;
        public string userId;
        public int progress;
        public int required;
        public DateTime startTime;
        public DateTime endTime;
        public DateTime completeTime;
        public QuestStatus status;
        public string parentQuestKey;
        public string childrenQuestsKeys;

        public QuestProgressInfo() { }

        public QuestProgressInfo(IQuestInfo questInfo)
        {
            id = questInfo.Id;
            key = questInfo.Key;
            userId = questInfo.UserId;
            progress = questInfo.Progress;
            required = questInfo.Required;
            startTime = questInfo.StartTime;
            completeTime = questInfo.CompleteTime;
            status = questInfo.Status;
            parentQuestKey = questInfo.ParentQuestKey;
            childrenQuestsKeys = questInfo.ChildrenQuestsKeys;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            throw new System.NotImplementedException();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}
