using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBasePopulator : ScriptableObject
    {
        public abstract IObservableProperty Populate();
    }

    public abstract class ObservableBasePopulator<T> : ObservableBasePopulator
    {
        [SerializeField]
        protected string key = "property";
        [SerializeField]
        protected T defaultValue;

        protected virtual void OnValidate() { }
    }

    public abstract class ObservableBaseListPopulator<T> : ObservableBasePopulator<List<T>> { }
    public abstract class ObservableBaseDictionaryPopulator<TKey, TValue> : ObservableBasePopulator<ConcurrentDictionary<TKey, TValue>> { }
}