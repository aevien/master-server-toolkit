using System;

namespace MasterServerToolkit.Networking
{
    public delegate void IncommingMessageHandler(IIncomingMessage message);
    public delegate void ResponseCallback(ResponseStatus status, IIncomingMessage response);

    /// <summary>
    /// Represents connection peer
    /// </summary>
    public interface IPeer : IDisposable, IMsgDispatcher
    {
        /// <summary>
        /// Unique peer id
        /// </summary>
        int Id { get; }

        /// <summary>
        /// True, if connection is stil valid
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Invoked when peer disconnects
        /// </summary>
        event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Invoked when peer receives a message
        /// </summary>
        event Action<IIncomingMessage> OnMessageReceivedEvent;

        /// <summary>
        /// Force disconnect
        /// </summary>
        /// <param name="reason"></param>
        void Disconnect(string reason);

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        void SendMessage(IOutgoingMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Stores a property into peer
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        void SetProperty(int id, object data);

        /// <summary>
        /// Retrieves a property from the peer
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        object GetProperty(int id);

        /// <summary>
        /// Retrieves a property from the peer, and if it's not found,
        /// retrieves a default value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        object GetProperty(int id, object defaultValue);

        /// <summary>
        /// Adds an extension to this peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extension"></param>
        T AddExtension<T>(T extension) where T : IPeerExtension;

        /// <summary>
        /// Retrieves an extension of this peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetExtension<T>() where T : IPeerExtension;

        /// <summary>
        /// Check if peer has <see cref="IPeerExtension"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool HasExtension<T>();
    }
}