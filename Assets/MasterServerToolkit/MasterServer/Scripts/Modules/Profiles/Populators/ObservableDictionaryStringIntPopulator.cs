using MasterServerToolkit.Extensions;
using System.Collections.Concurrent;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableDictionaryStringIntPopulator")]
    public class ObservableDictionaryStringIntPopulator : ObservableBasePopulator<DictionaryKeyValue<string, int>[]>
    {
        public override IObservableProperty Populate()
        {
            var dict = new ConcurrentDictionary<string, int>();

            foreach(var value in defaultValue)
            {
                dict.TryAdd(value.key, value.value);
            }

            return new ObservableDictStringInt(key.ToUint16Hash(), dict);
        }
    }
}
