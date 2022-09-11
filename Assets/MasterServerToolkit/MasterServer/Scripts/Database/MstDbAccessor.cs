using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class MstDbAccessor
    {
        /// <summary>
        /// List of the db/api accessors
        /// </summary>
        private Dictionary<Type, IDatabaseAccessor> _accessors;

        public MstDbAccessor()
        {
            _accessors = new Dictionary<Type, IDatabaseAccessor>();
        }

        /// <summary>
        /// Adds a database accessor to the list of available accessors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="access"></param>
        public void AddAccessor(IDatabaseAccessor access)
        {
            if (_accessors.ContainsKey(access.GetType()))
            {
                Logs.Warn($"Database accessor of type {access.GetType()} was overwriten");

                _accessors[access.GetType()].Dispose();
                _accessors.Remove(access.GetType());
            }

            _accessors[access.GetType()] = access;
        }

        /// <summary>
        /// Retrieves a database accessor from a list of available accessors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetAccessor<T>() where T : class, IDatabaseAccessor
        {
            _accessors.TryGetValue(typeof(T), out IDatabaseAccessor accessor);

            if (accessor == null)
                accessor = _accessors.Values.FirstOrDefault(m => m is T);

            return accessor as T;
        }
    }
}