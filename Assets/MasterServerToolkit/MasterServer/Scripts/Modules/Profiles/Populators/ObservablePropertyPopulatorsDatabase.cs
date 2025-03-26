using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/Populators Database")]
    public class ObservablePropertyPopulatorsDatabase : ScriptableObject
    {
        [SerializeField]
        private ObservableBasePopulator[] populators;
        public ObservableBasePopulator[] Populators => populators;
    } 
}
