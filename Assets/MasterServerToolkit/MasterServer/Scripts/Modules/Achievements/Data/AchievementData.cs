using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Achievements/AchievementData")]
    public class AchievementData : ScriptableObject
    {
        [Tooltip("Stable achievement identifier stored in profile progress. Empty values are replaced with the asset name; do not change it after release.")]
        public string key;
        [Tooltip("Localization key or display text used as the achievement title. Empty values are replaced with the asset name.")]
        public string title;
        [TextArea(3, 10), Tooltip("Localization key or display text describing the achievement requirement.")]
        public string description;
        [TextArea(3, 10), Tooltip("Localization key or display text shown when the achievement unlocks.")]
        public string result = "Wow! You've got an achievement!";
        [Tooltip("Progress value required to unlock the achievement. Updates at or above this value complete it; 0 or a negative value makes the first accepted update meet the threshold.")]
        public int requiredProgress;
        [Tooltip("Optional icon displayed by achievement UI and unlock notifications.")]
        public Sprite icon;
        [Tooltip("Hides the achievement from normal client lists until game UI chooses to reveal it. This does not change progress authority.")]
        public bool hidden = false;
        [Tooltip("Designer-defined ordering or difficulty rank. MST stores the value but does not assign reward policy from it.")]
        public int rank = 0;
        [Tooltip("Achievements that must be unlocked before this achievement may progress. Keep the dependency graph acyclic.")]
        public AchievementData[] dependencies;
        [Tooltip("Generic key/value parameters consumed by project-specific achievement logic. MST does not interpret their parameter strings.")]
        public AchievementExtraData[] extraParameters;
        [Tooltip("Generic commands/parameters consumed after unlock by project-specific reward logic. Keep handlers idempotent because pending rewards may retry.")]
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
            [Tooltip("Identifier used by project-specific code to find this parameter or result command.")]
            public string key = "parametersKey";
            [Tooltip("Project-defined parameter payload. MST preserves this string but does not parse its delimiter or schema.")]
            public string parameters = "your;key;parameters;here";

            public override string ToString()
            {
                return $"{key}({parameters})";
            }
        }
    }
}
