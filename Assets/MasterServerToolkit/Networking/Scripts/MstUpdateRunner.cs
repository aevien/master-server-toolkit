using MasterServerToolkit.Utils;
using System.Collections.Generic;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// This is an object which gets spawned into game once.
    /// It's main purpose is to call update methods
    /// </summary>
    public class MstUpdateRunner : DynamicSingletonBehaviour<MstUpdateRunner>
    {
        /// <summary>
        /// List of <see cref="IUpdatable"/>
        /// </summary>
        private readonly List<IUpdatable> _updatebles = new List<IUpdatable>();

        /// <summary>
        /// 
        /// </summary>
        public int Count => _updatebles.Count;

        private void Update()
        {
            for (int i = 0; i < _updatebles.Count; i++)
            {
                IUpdatable runnable = _updatebles[i];

                if (runnable != null)
                    runnable.DoUpdate();
                else
                    _updatebles.RemoveAt(i);
            }
        }

        /// <summary>
        /// Adds <see cref="IUpdatable"/> to list of updates that are running in main Unity thread
        /// </summary>
        /// <param name="updatable"></param>
        public static void Add(IUpdatable updatable)
        {
            if (TryGetOrCreate(out var instance))
            {
                if (!instance._updatebles.Contains(updatable))
                    instance._updatebles.Add(updatable);
            }
        }

        /// <summary>
        /// Removes <see cref="IUpdatable"/> from list of updates that are running in main Unity thread
        /// </summary>
        /// <param name="updatable"></param>
        public static void Remove(IUpdatable updatable)
        {
            if (TryGetOrCreate(out var instance))
                instance._updatebles.Remove(updatable);
        }
    }
}