using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.Text;
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

        /// <summary>
        /// 
        /// </summary>
        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
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

        public virtual MstJson JsonInfo()
        {
            MstJson json = new MstJson();

            try
            {
                json.AddField("name", GetType().Name);
                json.AddField("description", GetType().Name);

                if (Dependencies.Count > 0)
                {
                    var dependenciesArray = MstJson.EmptyArray;

                    for (int i = 0; i < Dependencies.Count; i++)
                    {
                        dependenciesArray.Add(Dependencies[i].Name);
                    }

                    json.AddField("dependencies", dependenciesArray);
                }

                if (OptionalDependencies.Count > 0)
                {
                    var optionalDependenciesArray = MstJson.EmptyArray;

                    for (int i = 0; i < OptionalDependencies.Count; i++)
                    {
                        optionalDependenciesArray.Add(OptionalDependencies[i].Name);
                    }

                    json.AddField("optionalDependencies", optionalDependenciesArray);
                }
            }
            catch (Exception e)
            {
                json.AddField("error", e.ToString());
            }

            return json;
        }

        /// <summary>
        /// Gets base module info
        /// </summary>
        /// <returns></returns>
        public virtual MstProperties Info()
        {
            MstProperties info = new MstProperties();
            info.Set("Description", GetType().Name);

            if (Dependencies.Count > 0)
            {
                StringBuilder dep = new StringBuilder();

                for (int i = 0; i < Dependencies.Count; i++)
                {
                    dep.Append(Dependencies[i].Name + (Dependencies.Count == i + 1 ? "" : ", "));
                }

                info.Add("Dependencies", dep.ToString());
            }

            if (OptionalDependencies.Count > 0)
            {
                StringBuilder dep = new StringBuilder();

                for (int i = 0; i < OptionalDependencies.Count; i++)
                {
                    dep.Append(OptionalDependencies[i].Name + (OptionalDependencies.Count == i + 1 ? "" : ", "));
                }

                info.Add("Optional Dependencies", dep.ToString());
            }

            return info;
        }
    }
}