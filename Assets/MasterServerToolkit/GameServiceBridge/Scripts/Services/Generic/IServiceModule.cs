using MasterServerToolkit.Logging;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Defines the common contract for a game service module.
    /// </summary>
    /// <remarks>
    /// Implement this interface through <see cref="BaseServiceModule"/> when possible so modules share
    /// the same readiness, support, logging, and lifecycle behavior.
    /// </remarks>
    public interface IServiceModule
    {
        /// <summary>
        /// Gets the logger assigned to this module.
        /// </summary>
        Logger Logger { get; }

        /// <summary>
        /// Gets a value indicating whether the current service implementation supports this module.
        /// </summary>
        /// <remarks>
        /// The default module implementation returns <see langword="false"/>. Service-specific modules must
        /// explicitly set this value to <see langword="true"/> when the feature is available.
        /// </remarks>
        bool IsSupported { get; }

        /// <summary>
        /// Gets a value indicating whether this module has completed initialization and can be used.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the service instance that owns this module.
        /// </summary>
        IService Service { get; }

        /// <summary>
        /// Performs pre-initialization work before the service starts initializing modules.
        /// </summary>
        /// <param name="service">The service instance that owns this module.</param>
        void OnBeforeInit(IService service);

        /// <summary>
        /// Initializes the module.
        /// </summary>
        /// <param name="service">The service instance that owns this module.</param>
        void OnInit(IService service);

        /// <summary>
        /// Performs post-initialization work after all service modules have been initialized.
        /// </summary>
        /// <param name="service">The service instance that owns this module.</param>
        void OnAfterInit(IService service);

        /// <summary>
        /// Notifies the module that the owning service is ready.
        /// </summary>
        /// <param name="service">The service instance that owns this module.</param>
        void OnReady(IService service);
    } 
}
