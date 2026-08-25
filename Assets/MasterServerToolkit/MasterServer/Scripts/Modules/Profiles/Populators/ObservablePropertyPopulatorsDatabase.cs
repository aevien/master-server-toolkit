using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/Populators Database")]
    public class ObservablePropertyPopulatorsDatabase : ObjectsDatabase<ObservableBasePopulator>
    {
        [ContextMenu("Populate")]
        private void Populate()
        {
            FindObjects();
        }
    } 
}
