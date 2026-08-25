using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public enum QuestState
    {
        NotStarted,
        Active,
        Completed,
        Canceled,
        Expired
    }

    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Quests/New Quest")]
    public class QuestData : ScriptableObject
    {
        [Header("Base info"), SerializeField, Tooltip("Stable quest identifier stored in progress data. It is generated from a GUID when empty; do not change it after release.")]
        private string key;
        [SerializeField, Tooltip("Localization key or display text used as the quest title.")]
        private string title = "Your title here";
        [SerializeField, TextArea(3, 10), Tooltip("Localization key or display text describing the quest objective and conditions.")]
        private string description = "Your quest description here";
        [SerializeField, Tooltip("Progress value required for completion. Objective code defines what one progress unit means; use a positive value for normal quests.")]
        private int requiredProgress;
        [SerializeField, Tooltip("Optional quest icon displayed by client UI. Leave None when the game supplies an icon elsewhere.")]
        private Sprite icon;

        [Header("Messages"), SerializeField, TextArea(3, 10), Tooltip("Localization key or display text shown when the quest becomes available or is accepted.")]
        private string startMessage = "Enter the message that will be displayed before you take the quest";
        [SerializeField, TextArea(3, 10), Tooltip("Localization key or display text shown while the quest is active.")]
        private string activeMessage = "Enter the message that will be displayed during the quest";
        [SerializeField, TextArea(3, 10), Tooltip("Localization key or display text shown after the quest completes.")]
        private string completedMessage = "Enter the message that will be displayed after completing the quest";
        [SerializeField, TextArea(3, 10), Tooltip("Localization key or display text shown when the quest is canceled.")]
        private string cancelMessage = "Enter the message that will be displayed if you cancel the quest";
        [SerializeField, TextArea(3, 10), Tooltip("Localization key or display text shown when the quest time limit expires.")]
        private string expireMessage = "Enter the message that will be displayed if the quest completion time will be expired";

        [Header("Settings"), SerializeField, Tooltip("Prevents the quest from being started again after it has completed once. Persistence policy belongs to the game profile implementation.")]
        private bool isOneTime = false;
        [SerializeField, Range(0, 43200), Tooltip("Completion limit in minutes. 0 means unlimited; maximum Inspector value is 43,200 minutes (30 days).")]
        private int timeToComplete;

        [Header("Quests"), SerializeField, Tooltip("Optional prerequisite/parent quest. The game decides whether completion or another parent state unlocks this quest.")]
        private QuestData parentQuest;
        [SerializeField, Tooltip("Quests related or unlocked after this quest. Keep relationships acyclic and implement transition policy in game code.")]
        private QuestData[] childrenQuests;

        public string Key => key;
        public string Title => title;
        public string Description => description;
        public int RequiredProgress => requiredProgress;
        public Sprite Icon => icon;
        public string StartMessage => startMessage;
        public string ActiveMessage => activeMessage;
        public string CompletedMessage => completedMessage;
        public string CancelMessage => cancelMessage;
        public string ExpireMessage => expireMessage;
        public bool IsOneTime => isOneTime;
        public int TimeToComplete => timeToComplete;
        public QuestData ParentQuest => parentQuest;
        public QuestData[] ChildrenQuests => childrenQuests;

#if UNITY_EDITOR
        private void Reset()
        {
            if (string.IsNullOrEmpty(key))
            {
                key = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(key))
            {
                key = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
