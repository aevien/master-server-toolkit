using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// This is an object which gets spawned into game once.
    /// It's main purpose is to call update methods
    /// </summary>
    public class MstUpdateRunner : DynamicSingleton<MstUpdateRunner>
    {
        /// <summary>
        /// List of <see cref="IUpdatable"/>
        /// </summary>
        private List<IUpdatable> _runnables = new List<IUpdatable>();

        private void Update()
        {
            for (int i = 0; i < _runnables.Count; i++)
            {
                IUpdatable runnable = _runnables[i];
                runnable.Update();
            }
        }

        /// <summary>
        /// Adds <see cref="IUpdatable"/> to list of updates that are running in main Unity thread
        /// </summary>
        /// <param name="updatable"></param>
        public void Add(IUpdatable updatable)
        {
            if (!_runnables.Contains(updatable))
            {
                _runnables.Add(updatable);
            }
        }

        /// <summary>
        /// Removes <see cref="IUpdatable"/> from list of updates that are running in main Unity thread
        /// </summary>
        /// <param name="updatable"></param>
        public void Remove(IUpdatable updatable)
        {
            _runnables.Remove(updatable);
        }
    }
}