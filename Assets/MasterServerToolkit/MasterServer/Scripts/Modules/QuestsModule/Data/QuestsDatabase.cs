using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Quests/QuestsDatabase")]
    public class QuestsDatabase : ScriptableObject
    {
        [SerializeField]
        private QuestData[] quests;
        public IEnumerable<QuestData> Quests => quests;
    }
}