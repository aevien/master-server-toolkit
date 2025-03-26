using MasterServerToolkit.Utils;
using System.Collections.Generic;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// This is an object which gets spawned into game once.
    /// Its main purpose is to call update methods
    /// </summary>
    public class MstUpdateRunner : SingletonBehaviour<MstUpdateRunner>
    {
        /// <summary>
        /// Set of <see cref="IUpdatable"/>
        /// </summary>
        private readonly List<IUpdatable> updatebles = new List<IUpdatable>();

        /// <summary>
        /// Number of updatables
        /// </summary>
        public int Count => updatebles.Count;

        private void Update()
        {
            if (updatebles.Count == 0)
                return;

            for (int i = 0; i < updatebles.Count; i++)
            {
                var updatable = updatebles[i];

                if (updatable == null)
                {
                    updatebles.RemoveAt(i);
                    i--;
                    continue;
                }

                updatable.DoUpdate();
            }
        }


        /// <summary>
        /// Adds <see cref="IUpdatable"/> to the set of updates that are running in main Unity thread
        /// </summary>
        /// <param name="updatable"></param>
        public static void Add(IUpdatable updatable)
        {
            if (updatable == null)
            {
                return;
            }

            if (TryGetOrCreate(out var instance))
            {
                instance.updatebles.Add(updatable);
            }
        }

        /// <summary>
        /// Removes <see cref="IUpdatable"/> from the set of updates that are running in main Unity thread
        /// </summary>
        /// <param name="updatable"></param>
        public static void Remove(IUpdatable updatable)
        {
            if (TryGetOrCreate(out var instance))
            {
                instance.updatebles.Remove(updatable);
            }
        }
    }
}
