using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Quests/QuestsDatabase")]
    public class QuestsDatabase : ObjectsDatabase<QuestData>
    {
        [ContextMenu("Populate")]
        private void Populate()
        {
            FindObjects();
        }

        protected override string SearchType()
        {
            return "t:QuestData";
        }
    }
}