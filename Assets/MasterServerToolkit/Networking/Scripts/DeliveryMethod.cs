namespace MasterServerToolkit.Networking
{
    public enum DeliveryMethod : byte
    {
        Unreliable = 0,
        // There is no guarantee of delivery or ordering, but allowing fragmented messages
        // with up to 32 fragments per message.
        UnreliableFragmented = 1,
        // There is no guarantee of delivery and all unordered messages will be dropped.
        // Example: VoIP.
        UnreliableSequenced = 2,
        // Each message is guaranteed to be delivered but not guaranteed to be in order.
        Reliable = 3,
        // Each message is guaranteed to be delivered, also allowing fragmented messages
        // with up to 32 fragments per message.
        ReliableFragmented = 4,
        // Each message is guaranteed to be delivered and in order.
        ReliableSequenced = 5,
        // An unreliable message. Only the last message in the send buffer is sent. Only
        // the most recent message in the receive buffer will be delivered.
        StateUpdate = 6,
        // A reliable message. Note: Only the last message in the send buffer is sent. Only
        // the most recent message in the receive buffer will be delivered.
        ReliableStateUpdate = 7,
        // A reliable message that will be re-sent with a high frequency until it is acknowledged.
        AllCostDelivery = 8,
        // There is garantee of ordering, no guarantee of delivery, but allowing fragmented
        // messages with up to 32 fragments per message.
        UnreliableFragmentedSequenced = 9,
        // Each message is guaranteed to be delivered in order, also allowing fragmented
        // messages with up to 32 fragments per message.
        ReliableFragmentedSequenced = 10
    }
}