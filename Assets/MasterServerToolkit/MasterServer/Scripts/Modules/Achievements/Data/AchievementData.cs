using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Achievements/AchievementData")]
    public class AchievementData : ScriptableObject
    {
        [SerializeField]
        protected string key;
        [SerializeField]
        protected string title;
        [SerializeField, TextArea(3, 10)]
        protected string description;
        [SerializeField, TextArea(3, 10)]
        protected string result = "Wow! You've got an achievement!";
        [SerializeField]
        protected int requiredProgress;
        [SerializeField]
        protected Sprite icon;
        [SerializeField]
        protected AchievementExtraData[] extraParameters;
        [SerializeField]
        protected AchievementExtraData[] resultCommands;

        public string Title => title;
        public string Description => description;
        public string Result => result;
        public string Key => key;
        public int RequiredProgress => requiredProgress;
        public Sprite Icon => icon;
        public AchievementExtraData[] ResultCommands => resultCommands;
        public AchievementExtraData[] ExtraParameters => extraParameters;

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(key))
            {
                key = name;
            }

            if (string.IsNullOrEmpty(title))
            {
                title = name;
            }

            if (string.IsNullOrEmpty(description))
            {
                description = $"{name}_desc";
            }

            if (string.IsNullOrEmpty(result))
            {
                result = $"{name}_result";
            }
        }

        public AchievementExtraData GetExtraParametersByKey(string key)
        {
            foreach(var data in ExtraParameters)
            {
                if(data.key == key)
                {
                    return data;
                }
            }

            return null;
        }

        public bool TryGetExtraParametersByKey(string key, out AchievementExtraData data)
        {
            data = GetExtraParametersByKey(key);
            return data != null;
        }

        public AchievementExtraData GetResultCommandsByKey(string key)
        {
            foreach (var data in ResultCommands)
            {
                if (data.key == key)
                {
                    return data;
                }
            }

            return null;
        }

        public bool TryGetResultCommandsByKey(string key, out AchievementExtraData data)
        {
            data = GetResultCommandsByKey(key);
            return data != null;
        }

        [Serializable]
        public class AchievementExtraData
        {
            public string key = "parametersKey";
            public string parameters = "your;key;parameters;here";

            public override string ToString()
            {
                return $"{key}({parameters})";
            }
        }
    }
}