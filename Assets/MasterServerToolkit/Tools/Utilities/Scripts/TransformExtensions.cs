using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.Extensions
{
    public static class TransformExtensions
    {
        public static void RemoveChildren(this Transform parent, params Transform[] ignore)
        {
            var ignoreList = ignore.ToList();

            foreach (Transform t in parent)
            {
                if (!ignoreList.Contains(t))
                {
                    Object.Destroy(t.gameObject);
                }
            }
        }

        public static T GetChildComponentByName<T>(this Transform parent, string childName) where T : Component
        {
            return parent.GetComponentsInChildren<T>(true).Where(c => c.name == childName).FirstOrDefault();
        }

        public static T TryGetChildComponentByName<T>(this Transform parent, string childName, out T comp) where T : Component
        {
            comp = GetChildComponentByName<T>(parent, childName);
            return comp;
        }
    }
}