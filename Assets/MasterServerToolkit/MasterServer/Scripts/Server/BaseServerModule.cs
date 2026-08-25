using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class BaseServerModule : MonoBehaviour, IBaseServerModule, IServerRunModule
    {
        #region INSPECTOR
        [Header("Base Module Settings"), SerializeField, Tooltip("Minimum severity written by this server module. The owning server component has a separate Log Level setting.")]
        protected LogLevel logLevel = LogLevel.Info; 
        #endregion

        private static Dictionary<Type, GameObject> instances;

        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// 
        /// </summary>
        public string Id {  get; private set; }

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

        /// <inheritdoc />
        public virtual void StartServerRun(CancellationToken runCancellationToken) { }

        /// <inheritdoc />
        public virtual Task StopServerRunAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            Id = GetType().Name.FromCamelcase().Replace(" ", "_").ToLower();
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

        /// <summary>
        /// Resolves a configured server permission key to its numeric permission level.
        /// </summary>
        /// <param name="key">Permission key configured on the server.</param>
        /// <param name="permissionLevel">Resolved permission level when the key exists.</param>
        /// <returns><c>true</c> when the key exists; otherwise, <c>false</c>.</returns>
        protected bool TryGetPermissionLevel(string key, out int permissionLevel)
        {
            permissionLevel = MstPermissionLevels.Default;
            return Server != null && Server.TryGetPermissionLevel(key, out permissionLevel);
        }

        public virtual MstJson Info()
        {
            MstJson json = MstJson.CreateObject();

            try
            {
                json.AddField("id", GetType().Name.FromCamelcase().Replace(" ", "_").ToLower());
                json.AddField("name", GetType().Name.FromCamelcase());
                json.AddField("description", GetType().Name);
            }
            catch (Exception e)
            {
                json.AddField("error", e.ToString());
            }

            return json;
        }

        public virtual MstJson Details()
        {
            MstJson json = Info();

            try
            {
                if (Dependencies.Count > 0)
                {
                    var dependenciesArray = MstJson.CreateArray();

                    for (int i = 0; i < Dependencies.Count; i++)
                    {
                        dependenciesArray.Add(Dependencies[i].Name);
                    }

                    json.AddField("dependencies", dependenciesArray);
                }

                if (OptionalDependencies.Count > 0)
                {
                    var optionalDependenciesArray = MstJson.CreateArray();

                    for (int i = 0; i < OptionalDependencies.Count; i++)
                    {
                        optionalDependenciesArray.Add(OptionalDependencies[i].Name);
                    }

                    json.AddField("optionalDependencies", optionalDependenciesArray);
                }

                json.AddField("properties", MstJson.CreateObject());
            }
            catch (Exception e)
            {
                json.AddField("error", e.ToString());
            }

            return json;
        }
    }
}
