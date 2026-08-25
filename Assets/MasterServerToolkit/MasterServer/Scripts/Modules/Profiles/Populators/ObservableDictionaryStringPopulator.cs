using MasterServerToolkit.Extensions;
using System.Collections.Concurrent;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [CreateAssetMenu(menuName = MstConstants.CreateMenu + "Profile/ObservableDictionaryStringPopulator")]
    public class ObservableDictionaryStringPopulator : ObservableBasePopulator<DictionaryKeyValue<string, string>[]>
    {
        public override IObservableProperty Populate()
        {
            var dict = new ConcurrentDictionary<string, string>();

            foreach (var value in defaultValue)
            {
                dict.TryAdd(value.key, value.value);
            }

            return new ObservableDictionaryString(key.ToUint16Hash(), dict);
        }
    }
}