using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class BaseRegistry<TKey, TItem> : IEnumerable<TItem> where TItem : MonoBehaviour
    {
        private readonly Dictionary<TKey, TItem> items = new Dictionary<TKey, TItem>();

        public int Count => items.Count;

        public event Action<TItem> OnItemAddEvent;
        public event Action<TItem> OnItemRemoveEvent;
        public event Action OnChangeEvent;

        public bool Add(TKey key, TItem item)
        {
            if (!items.ContainsKey(key))
            {
                items.Add(key, item);
                OnItemAddEvent?.Invoke(item);
                OnChangeEvent?.Invoke();
                return true;
            }

            return false;
        }

        public bool Remove(TKey key)
        {
            var item = Get(key);

            if (items.Remove(key))
            {
                OnItemRemoveEvent?.Invoke(item);
                OnChangeEvent?.Invoke();
                return true;
            }

            return false;
        }

        public bool Has(TKey key)
        {
            return items.ContainsKey(key);
        }

        public TItem Get(TKey key)
        {
            if (Has(key))
            {
                return items[key];
            }
            else
            {
                return null;
            }
        }

        public bool TryGet(TKey key, out TItem item)
        {
            item = Get(key);
            return item != null;
        }

        public void Clear()
        {
            items.Clear();
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            foreach (var item in items.Values)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return items.GetEnumerator();
        }
    }
}