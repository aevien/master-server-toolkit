using MasterServerToolkit.Json;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Defines the contract for sending analytics events through the current game service.
    /// </summary>
    public interface IAnalyticsModule : IServiceModule
    {
        /// <summary>
        /// Sends a custom analytics event to the current platform.
        /// </summary>
        /// <param name="eventData">The JSON object that contains event data and parameters.</param>
        /// <param name="singleton">
        /// <see langword="true"/> to send this event only once per session; otherwise, <see langword="false"/>.
        /// </param>
        void AnalyticsEvent(MstJson eventData, bool singleton = false);

        /// <summary>
        /// Sends a custom analytics event to the current platform.
        /// </summary>
        /// <param name="eventData">The serialized event data.</param>
        /// <param name="singleton">
        /// <see langword="true"/> to send this event only once per session; otherwise, <see langword="false"/>.
        /// </param>
        void AnalyticsEvent(string eventData, bool singleton = false);
    }
}
