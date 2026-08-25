namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Minimal contract for sending already built network messages.
    /// Payload-specific overloads belong to <see cref="MessageDispatcherExtensions" />.
    /// </summary>
    public interface IMessageDispatcher
    {
        void SendMessage(IOutgoingMessage message);
        void SendMessage(IOutgoingMessage message, DeliveryMethod method);
        int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback);
        int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs);
    }
}
