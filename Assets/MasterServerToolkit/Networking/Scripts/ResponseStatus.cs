namespace MasterServerToolkit.Networking
{
    public enum ResponseStatus : ushort
    {
        Default,
        Success,
        Timeout,
        Error,
        Unauthorized,
        Invalid,
        Failed,
        NotConnected,
        NotHandled,
        NotFound
    }
}