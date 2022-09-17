using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MasterServerToolkit.Utils
{
    public class GenericPool<T> where T : MonoBehaviour
    {
        private readonly Stack<T> _freeObjects;
        private readonly T _prefab;

        public GenericPool(T prefab, bool dontUseOriginal = false)
        {
            if (prefab == null)
            {
                throw new NullReferenceException("Generic pool received a null as a prefab");
            }

            prefab.gameObject.SetActive(false);
            _prefab = prefab;
            _freeObjects = new Stack<T>();

            if (!dontUseOriginal)
            {
                Store(prefab);
            }
        }

        private T InstantiateNew()
        {
            return Object.Instantiate(_prefab);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="active"></param>
        /// <returns></returns>
        public T Get(bool active = true)
        {
            if (!_freeObjects.TryPop(out T obj))
                obj = InstantiateNew();

            obj.gameObject.SetActive(active);

            return obj;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        public void Store(T obj)
        {
            if (obj != null)
            {
                obj.gameObject.SetActive(false);
                _freeObjects.Push(obj);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Cleanup()
        {
            int count = _freeObjects.Count;

            for (int i = 0; i < count; i++)
            {
                if (_freeObjects.TryPop(out T obj))
                    Object.Destroy(obj.gameObject);
            }

            _freeObjects.Clear();
        }
    }
}