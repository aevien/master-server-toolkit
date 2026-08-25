namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Convenience overloads that build messages and pass them to <see cref="IMessageDispatcher" />.
    /// Keep the dispatcher contract small by adding payload-specific helpers here.
    /// </summary>
    public static class MessageDispatcherExtensions
    {
        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode));
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, ResponseCallback responseCallback)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode), responseCallback);
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, ISerializablePacket packet)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, packet));
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, ISerializablePacket packet, DeliveryMethod method)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, packet), method);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, packet), responseCallback);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, packet), responseCallback, timeoutSecs);
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, byte[] data)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data));
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, byte[] data, DeliveryMethod method)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data), method);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, byte[] data, ResponseCallback responseCallback)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback, timeoutSecs);
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, string data)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data));
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, string data, DeliveryMethod method)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data), method);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, string data, ResponseCallback responseCallback)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, string data, ResponseCallback responseCallback, int timeoutSecs)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback, timeoutSecs);
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, int data)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data));
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, int data, DeliveryMethod method)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data), method);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, int data, ResponseCallback responseCallback)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, int data, ResponseCallback responseCallback, int timeoutSecs)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback, timeoutSecs);
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, bool data)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data));
        }

        public static void SendMessage(this IMessageDispatcher dispatcher, ushort opCode, bool data, DeliveryMethod method)
        {
            dispatcher.SendMessage(MessageHelper.Create(opCode, data), method);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, bool data, ResponseCallback responseCallback)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback);
        }

        public static int SendMessage(this IMessageDispatcher dispatcher, ushort opCode, bool data, ResponseCallback responseCallback, int timeoutSecs)
        {
            return dispatcher.SendMessage(MessageHelper.Create(opCode, data), responseCallback, timeoutSecs);
        }
    }
}
