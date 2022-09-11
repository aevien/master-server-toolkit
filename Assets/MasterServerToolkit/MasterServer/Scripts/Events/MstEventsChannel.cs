using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    public delegate void EventHandler(EventMessage message);

    public class MstEventsChannel
    {
        /// <summary>
        /// Use exception catching
        /// </summary>
        private readonly bool _catchExceptions;

        /// <summary>
        /// List of event handlers
        /// </summary>
        private readonly Dictionary<string, UnityEvent<EventMessage>> _handlers;

        /// <summary>
        /// Name of the events channel instance
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Create new instance of events channel
        /// </summary>
        public MstEventsChannel()
        {
            _catchExceptions = true;
            _handlers = new Dictionary<string, UnityEvent<EventMessage>>();
            Name = "default";
        }

        /// <summary>
        /// Create new instance of events channel
        /// </summary>
        /// <param name="name"></param>
        public MstEventsChannel(string name)
        {
            _catchExceptions = true;
            _handlers = new Dictionary<string, UnityEvent<EventMessage>>();
            Name = name;
        }

        /// <summary>
        /// Create new instance of events channel
        /// </summary>
        /// <param name="name"></param>
        /// <param name="catchExceptions"></param>
        public MstEventsChannel(string name, bool catchExceptions = false)
        {
            _catchExceptions = catchExceptions;
            _handlers = new Dictionary<string, UnityEvent<EventMessage>>();
            Name = name;
        }

        /// <summary>
        /// Invoke event without data
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public bool Invoke(string eventName)
        {
            return Invoke(eventName, new EventMessage());
        }

        /// <summary>
        /// Invoke event with data
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool Invoke(string eventName, object data)
        {
            return Invoke(eventName, new EventMessage(data));
        }

        /// <summary>
        /// Invoke event with data
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool Invoke(string eventName, EventMessage message)
        {
            if (_handlers.TryGetValue(eventName, out UnityEvent<EventMessage> eventHandler))
            {
                if (!_catchExceptions)
                {
                    eventHandler?.Invoke(message);
                }
                else
                {
                    try
                    {
                        eventHandler?.Invoke(message);
                    }
                    catch (Exception e)
                    {
                        Logs.Error(e);
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Subscribe to event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="handler"></param>
        /// <param name="autoUnsubscribe">If true, handler will unsubscribe when scene unloads</param>
        public void AddListener(string eventName, UnityAction<EventMessage> handler, bool autoUnsubscribe = true)
        {
            if (!_handlers.TryGetValue(eventName, out UnityEvent<EventMessage> handlersList))
                _handlers[eventName] = new UnityEvent<EventMessage>();

            _handlers[eventName].AddListener(handler);
        }

        /// <summary>
        /// Remove handler of given event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="handler"></param>
        public void RemoveListener(string eventName, UnityAction<EventMessage> handler)
        {
            if (_handlers.TryGetValue(eventName, out UnityEvent<EventMessage> handlersList))
                handlersList.RemoveListener(handler);
        }

        /// <summary>
        /// Remove all handlers of given event
        /// </summary>
        /// <param name="eventName"></param>
        public void RemoveAllListeners(string eventName)
        {
            if (_handlers.TryGetValue(eventName, out UnityEvent<EventMessage> handlersList))
                handlersList.RemoveAllListeners();
        }

        /// <summary>
        /// Remove all handlers
        /// </summary>
        /// <param name="eventName"></param>
        public void RemoveAllListeners()
        {
            foreach (UnityEvent<EventMessage> handler in _handlers.Values)
                handler.RemoveAllListeners();
        }
    }
}