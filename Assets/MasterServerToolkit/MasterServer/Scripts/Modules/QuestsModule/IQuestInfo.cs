using System;

namespace MasterServerToolkit.MasterServer
{
    public enum QuestStatus { Inactive, Active, Completed, Canceled, Expired }

    public interface IQuestInfo
    {
        string Id { get; set; }
        string Key { get; set; }
        string UserId { get; set; }
        int Progress { get; set; }
        int Required { get; set; }
        DateTime StartTime { get; set; }
        DateTime ExpireTime { get; set; }
        DateTime CompleteTime { get; set; }
        QuestStatus Status { get; set; }
        string ParentQuestKey { get; set; }
        string ChildrenQuestsKeys { get; set; }
        bool TryToComplete(int progress);
    }
}