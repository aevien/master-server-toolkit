using MasterServerToolkit.MasterServer;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    /// <inheritdoc />
    public abstract class BaseServiceModule : MonoBehaviour, IServiceModule
    {
        /// <inheritdoc />
        public IService Service { get; private set; }

        /// <inheritdoc />
        public virtual bool IsSupported { get; protected set; }

        /// <inheritdoc />
        public bool IsReady { get; protected set; }

        /// <inheritdoc />
        public Logging.Logger Logger { get; protected set; }

        /// <inheritdoc />
        public virtual void OnBeforeInit(IService service)
        {
            Logger = Mst.Create.Logger(GetType().Name);
            Logger.LogLevel = service.Logger.LogLevel;
            Service = service;
        }

        /// <inheritdoc />
        public virtual void OnInit(IService service) => Logger.Debug($"Init module");

        /// <inheritdoc />
        public virtual void OnAfterInit(IService service) { }

        /// <inheritdoc />
        public void OnReady(IService service) => Logger.Debug($"Module ready");
    }
}
