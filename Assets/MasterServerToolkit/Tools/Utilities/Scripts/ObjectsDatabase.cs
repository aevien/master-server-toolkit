using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MasterServerToolkit.Utils
{
    public abstract class ObjectsDatabase<ObjectType> : ScriptableObject, IEnumerable<ObjectType> where ObjectType : Object
    {
        [SerializeField]
        [Tooltip("Project folders searched by AssetDatabase.FindAssets when the derived database rebuilds its index. Paths are relative to the project, for example Assets/GameData. An empty list resets to Assets/.")]
        protected string[] searchPaths = new string[] { "Assets/" };
        [SerializeField]
        [Tooltip("Alphabetical asset-name order applied when the derived database rebuilds the serialized index.")]
        protected SortOrder sortOrder = SortOrder.Ascending;
        [SerializeField]
        [Tooltip("Serialized asset index consumed by database lookups and enumeration. It is populated by derived database refresh logic; manual edits may be overwritten.")]
        protected List<ObjectType> objects;

        protected enum SortOrder
        {
            Ascending,
            Descending
        }

        protected virtual void FindObjects()
        {
#if UNITY_EDITOR
            objects.Clear();

            string type = SearchType();
            var guids = AssetDatabase.FindAssets(type, searchPaths);

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ObjectType>(assetPath);

                if (item != null)
                {
                    objects.Add(item);
                }
            }

            switch (sortOrder)
            {
                case SortOrder.Ascending:
                    objects = objects.OrderBy(i => i.name).ToList();
                    break;
                case SortOrder.Descending:
                    objects = objects.OrderByDescending(i => i.name).ToList();
                    break;
            }
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
            return $"t:{typeof(ObjectType).Name}";
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
