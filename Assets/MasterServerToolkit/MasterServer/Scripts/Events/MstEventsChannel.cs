using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    public class MstEventsChannel
    {
        public delegate void EventHandler(EventMessage message);

        /// <summary>
        /// Use exception catching
        /// </summary>
        private readonly bool _catchExceptions;

        /// <summary>
        /// List of event handlers
        /// </summary>
        private readonly Dictionary<string, List<EventHandler>> _handlers;

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
            _handlers = new Dictionary<string, List<EventHandler>>();
            Name = "default";
        }

        /// <summary>
        /// Create new instance of events channel
        /// </summary>
        /// <param name="name"></param>
        public MstEventsChannel(string name)
        {
            _catchExceptions = true;
            _handlers = new Dictionary<string, List<EventHandler>>();
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
            _handlers = new Dictionary<string, List<EventHandler>>();
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
            if (_handlers.TryGetValue(eventName, out List<EventHandler> eventHandlersList))
            {
                if (eventHandlersList == null || eventHandlersList.Count == 0)
                {
                    return false;
                }

                foreach (var eventHandler in eventHandlersList)
                {
                    if (!_catchExceptions)
                    {
                        eventHandler.Invoke(message);
                        continue;
                    }

                    try
                    {
                        eventHandler.Invoke(message);
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
        public void AddEventListener(string eventName, EventHandler handler, bool autoUnsubscribe = true)
        {
            if (_handlers.TryGetValue(eventName, out List<EventHandler> handlersList))
            {
                handlersList.Add(handler);
            }
            else
            {
                handlersList = new List<EventHandler>
                {
                    handler
                };

                _handlers.Add(eventName, handlersList);
            }

            if (autoUnsubscribe)
            {
                // Cleanup when scene unloads
                void action(Scene scene)
                {
                    RemoveEventListener(eventName, handler);
                    SceneManager.sceneUnloaded -= action;
                }

                SceneManager.sceneUnloaded += action;
            }
        }

        /// <summary>
        /// Remove handler of given event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="handler"></param>
        public void RemoveEventListener(string eventName, EventHandler handler)
        {
            if (_handlers.TryGetValue(eventName, out List<EventHandler> handlersList))
            {
                handlersList.Remove(handler);
            }
        }

        /// <summary>
        /// Remove all handlers of given event
        /// </summary>
        /// <param name="eventName"></param>
        public void RemoveAllEventListeners(string eventName)
        {
            if (_handlers.TryGetValue(eventName, out List<EventHandler> handlersList))
            {
                handlersList.Clear();
            }
        }

        /// <summary>
        /// Remove all handlers
        /// </summary>
        /// <param name="eventName"></param>
        public void RemoveAllEventListeners()
        {
            _handlers.Clear();
        }
    }
}