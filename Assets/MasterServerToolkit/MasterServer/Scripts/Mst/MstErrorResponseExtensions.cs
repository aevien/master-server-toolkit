using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Creates consistent structured error responses for MST requests.
    /// </summary>
    public static class MstErrorResponseExtensions
    {
        public static void RespondError(
            this IIncomingMessage message,
            ResponseStatus status,
            string code,
            MstProperties properties = null)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (status == ResponseStatus.Success)
                throw new ArgumentException("Structured errors cannot use the Success status", nameof(status));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Error code cannot be empty", nameof(code));

            properties = properties == null
                ? new MstProperties()
                : new MstProperties(properties);

            properties.Set(MstErrorPropertyKeys.CODE, code);
            message.Respond(properties.ToBytes(), status);
        }
    }
}
