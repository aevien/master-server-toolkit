using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MasterServerToolkit.Utils
{
    public static class ListSortUtility
    {
        /// <summary>
        /// Sorts items by property name (case-insensitive) and direction ("asc"/"desc").
        /// If property is not found, returns the original list (no sorting).
        /// </summary>
        public static List<T> SortByProperty<T>(IEnumerable<T> source, string propertyName, string direction)
        {
            if (source == null)
                return new List<T>();

            var list = source.ToList();

            if (string.IsNullOrWhiteSpace(propertyName))
                return list;

            /// <summary>
            /// Finds public instance property ignoring case.
            /// </summary>
            PropertyInfo prop = typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (prop == null)
                return list;

            bool desc = IsDescending(direction);

            /// <summary>
            /// Stable sort: keep original order for equal keys.
            /// </summary>
            var indexed = list.Select((item, index) => new IndexedItem<T>(item, index));

            IOrderedEnumerable<IndexedItem<T>> ordered;

            if (desc)
            {
                ordered = indexed
                    .OrderByDescending(x => prop.GetValue(x.Item, null), Comparer<object>.Create(NullSafeCompare))
                    .ThenBy(x => x.Index);
            }
            else
            {
                ordered = indexed
                    .OrderBy(x => prop.GetValue(x.Item, null), Comparer<object>.Create(NullSafeCompare))
                    .ThenBy(x => x.Index);
            }

            return ordered.Select(x => x.Item).ToList();
        }

        /// <summary>
        /// Detects descending direction.
        /// </summary>
        private static bool IsDescending(string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return false;

            string dir = direction.Trim();

            return dir.Equals("desc", StringComparison.OrdinalIgnoreCase)
                || dir.Equals("descending", StringComparison.OrdinalIgnoreCase)
                || dir.Equals("-1", StringComparison.OrdinalIgnoreCase)
                || dir.Equals("-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Null-safe comparison that supports IComparable values and falls back to string comparison.
        /// </summary>
        private static int NullSafeCompare(object a, object b)
        {
            if (ReferenceEquals(a, b))
                return 0;

            if (a == null)
                return -1;

            if (b == null)
                return 1;

            if (a is IComparable comparableA)
            {
                try
                {
                    return comparableA.CompareTo(b);
                }
                catch
                {
                    /// <summary>
                    /// Types are not directly comparable, will fallback to string comparison.
                    /// </summary>
                }
            }

            return string.CompareOrdinal(a.ToString(), b.ToString());
        }

        /// <summary>
        /// Helper structure for stable sorting.
        /// </summary>
        private readonly struct IndexedItem<TItem>
        {
            public TItem Item { get; }
            public int Index { get; }

            public IndexedItem(TItem item, int index)
            {
                Item = item;
                Index = index;
            }
        }
    }
}
