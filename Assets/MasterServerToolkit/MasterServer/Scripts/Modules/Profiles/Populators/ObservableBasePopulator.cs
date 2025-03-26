using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBasePopulator : ScriptableObject
    {
        [SerializeField]
        protected string key = "property";

        public string Key => key;
        public abstract IObservableProperty Populate();
    }

    public abstract class ObservableBasePopulator<T> : ObservableBasePopulator
    {
        [SerializeField]
        protected T defaultValue;

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(key))
            {
                key = name;
            }
        }
    }

    [Serializable]
    public struct DictionaryKeyValue<TKey, TValue>
    {
        public TKey key;
        public TValue value;
    }
}