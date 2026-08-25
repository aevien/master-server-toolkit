namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Legacy name kept for existing integrations. Use <see cref="IMessageDispatcher" />
    /// for new code unless the connected <see cref="IPeer" /> must be exposed.
    /// </summary>
    public interface IMsgDispatcher : IMessageDispatcher
    {
        IPeer Peer { get; }
    }
}
