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
    }
}