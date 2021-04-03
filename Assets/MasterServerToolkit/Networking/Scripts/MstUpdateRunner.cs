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
        private List<IUpdatable> _runnables;

        public event Action OnApplicationQuitEvent;

        protected void Awake()
        {
            _runnables = new List<IUpdatable>();
        }

        private void Update()
        {
            for (int i = 0; i < _runnables.Count; i++)
            {
                IUpdatable runnable = _runnables[i];
                runnable.Update();
            }
        }

        public void Add(IUpdatable updatable)
        {
            if (!_runnables.Contains(updatable))
            {
                _runnables.Add(updatable);
            }
        }

        public void Remove(IUpdatable updatable)
        {
            _runnables.Remove(updatable);
        }

        private void OnApplicationQuit()
        {
            OnApplicationQuitEvent?.Invoke();
        }
    }
}