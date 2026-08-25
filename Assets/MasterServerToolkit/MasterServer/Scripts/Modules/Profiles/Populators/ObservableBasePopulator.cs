using System;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ObservableBasePopulator : ScriptableObject
    {
        [SerializeField, Tooltip("Stable profile property key used for serialization, synchronization and persistence. Keys must be unique in the populators database and must not change after player data has been stored.")]
        protected string key = "property";

        public string Key => key;
        public abstract IObservableProperty Populate();
    }

    public abstract class ObservableBasePopulator<T> : ObservableBasePopulator
    {
        [SerializeField, Tooltip("Value assigned when this property is first created for a profile. Existing persisted values are restored instead of receiving this default again.")]
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
        [Tooltip("Initial dictionary entry key. Keys should be unique within the populator's default-value list.")]
        public TKey key;
        [Tooltip("Initial value assigned to this dictionary key when a new profile property is created.")]
        public TValue value;
    }
}
