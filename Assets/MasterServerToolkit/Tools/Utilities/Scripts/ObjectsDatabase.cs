using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public abstract class ObjectsDatabase<ObjectType> : ScriptableObject, IEnumerable<ObjectType> where ObjectType : Object
    {
        [SerializeField]
        protected string[] searchPaths = new string[] { "Assets/" };
        [SerializeField]
        protected List<ObjectType> objects;

        protected virtual void FindObjects()
        {
#if UNITY_EDITOR
            objects.Clear();

            var guids = AssetDatabase.FindAssets(SearchType(), searchPaths);

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ObjectType>(assetPath);

                if (item != null)
                {
                    objects.Add(item);
                }
            }

            objects = objects.OrderBy(i => i.name).OrderBy(i => i.GetType().Name).ToList();
#endif
        }

        protected virtual void OnValidate()
        {
            if (searchPaths.Length == 0)
            {
                searchPaths = new string[] { "Assets/" };
            }

            for (int i = 0; i < searchPaths.Length; i++)
            {
                searchPaths[i] = searchPaths[i].Trim().Replace("\\", "/");
            }
        }

        protected virtual string SearchType()
        {
            return "t:prefab";
        }

        public T GetItemByName<T>(string itemName) where T : ObjectType
        {
            return objects.FirstOrDefault(i => i.name == itemName) as T;
        }

        public bool TryGetItemByName<T>(string itemName, out T item) where T : ObjectType
        {
            item = GetItemByName<T>(itemName);
            return item != null;
        }

        public IEnumerator<ObjectType> GetEnumerator()
        {
            foreach (ObjectType obj in objects)
            {
                yield return obj;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
