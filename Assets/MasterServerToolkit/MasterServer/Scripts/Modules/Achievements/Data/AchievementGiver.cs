using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class AchievementGiver : MonoBehaviour
    {
        [Header("Data"), SerializeField, Tooltip("Achievement definitions this gameplay component may update. Leave empty when the derived component resolves achievements by another project-specific rule.")]
        protected AchievementData[] achievements;
    }
}
