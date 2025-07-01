using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Achievements/AchievementData")]
    public class AchievementData : ScriptableObject
    {
        public string key;
        public string title;
        [TextArea(3, 10)]
        public string description;
        [TextArea(3, 10)]
        public string result = "Wow! You've got an achievement!";
        public int requiredProgress;
        public Sprite icon;
        public AchievementExtraData[] extraParameters;
        public AchievementExtraData[] resultCommands;

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
            foreach(var data in extraParameters)
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
            foreach (var data in resultCommands)
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