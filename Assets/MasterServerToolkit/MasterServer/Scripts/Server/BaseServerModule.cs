using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class BaseServerModule : MonoBehaviour, IBaseServerModule
    {
        private static Dictionary<Type, GameObject> instances;

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;

        [Header("Base Module Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        /// <summary>
        /// Returns a list of module types this module depends on
        /// </summary>
        public List<Type> Dependencies { get; private set; } = new List<Type>();

        /// <summary>
        /// Returns a list of module types this module depends on
        /// </summary>
        public List<Type> OptionalDependencies { get; private set; } = new List<Type>();

        /// <summary>
        /// Server, which initialized this module.
        /// Will be null, until the module is initialized
        /// </summary>
        public ServerBehaviour Server { get; set; }

        /// <summary>
        /// Called by master server, when module should be started
        /// </summary>
        public abstract void Initialize(IServer server);

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        /// <summary>
        /// Adds a dependency to list. Should be called in Awake or Start methods of module
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddDependency<T>() where T : class, IBaseServerModule
        {
            Dependencies.Add(typeof(T));
        }

        /// <summary>
        /// Adds an optional dependency to list. Should be called in Awake or Start methods of module
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddOptionalDependency<T>() where T : class, IBaseServerModule
        {
            OptionalDependencies.Add(typeof(T));
        }

        /// <summary>
        /// Returns true, if module should be destroyed
        /// </summary>
        /// <returns></returns>
        protected bool DestroyIfExists()
        {
            if (instances == null)
            {
                instances = new Dictionary<Type, GameObject>();
            }

            if (instances.ContainsKey(GetType()))
            {
                if (instances[GetType()] != null)
                {
                    // Module hasn't been destroyed
                    Destroy(gameObject);
                    return true;
                }

                // Remove an old module, which has been destroyed previously
                // (probably automatically when changing a scene)
                instances.Remove(GetType());
            }

            instances.Add(GetType(), gameObject);
            return false;
        }
    }
}