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
        [Header("Base info"), SerializeField]
        private string key;
        [SerializeField]
        private string title = "Your title here";
        [SerializeField, TextArea(3, 10)]
        private string description = "Your quest description here";
        [SerializeField]
        private int requiredProgress;
        [SerializeField]
        private Sprite icon;

        [Header("Messages"), SerializeField, TextArea(3, 10)]
        private string startMessage = "Enter the message that will be displayed before you take the quest";
        [SerializeField, TextArea(3, 10)]
        private string activeMessage = "Enter the message that will be displayed during the quest";
        [SerializeField, TextArea(3, 10)]
        private string completedMessage = "Enter the message that will be displayed after completing the quest";
        [SerializeField, TextArea(3, 10)]
        private string cancelMessage = "Enter the message that will be displayed if you cancel the quest";
        [SerializeField, TextArea(3, 10)]
        private string expireMessage = "Enter the message that will be displayed if the quest completion time will be expired";

        [Header("Settings"), SerializeField]
        private bool isOneTime = false;
        [SerializeField, Range(0, 43200), Tooltip("Time in minutes to complete quest. Leave it 0 if time is unlimited or give it some time to complete the quest. Max time is 43200 = 30 days")]
        private int timeToComplete;

        [Header("Quests"), SerializeField]
        private QuestData parentQuest;
        [SerializeField]
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
