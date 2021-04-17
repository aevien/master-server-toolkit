using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class MstDbAccessor
    {
        /// <summary>
        /// List of the db/api accessors
        /// </summary>
        private Dictionary<Type, object> _accessors = new Dictionary<Type, object>();

        /// <summary>
        /// Adds a database accessor to the list of available accessors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="access"></param>
        public void SetAccessor<T>(object access)
        {
            if (_accessors.ContainsKey(typeof(T)))
            {
                Logs.Warn($"Database accessor of type {typeof(T)} was overwriten");
            }

            _accessors[typeof(T)] = access;
        }

        /// <summary>
        /// Retrieves a database accessor from a list of available accessors
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetAccessor<T>() where T : class
        {
            _accessors.TryGetValue(typeof(T), out object result);
            return result as T;
        }
    }
}