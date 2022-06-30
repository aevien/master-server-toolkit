using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    [Obsolete]
    public class OldEventsChannel
    {
        /// <summary>
        /// Delegate used for event handlers
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        public delegate void EventHandler(object arg1, object arg2);

        /// <summary>
        /// Use exception catching
        /// </summary>
        private readonly bool _catchExceptions;

        /// <summary>
        /// Last event id
        /// </summary>
        private int _eventId = 0;

        /// <summary>
        /// List of event handlers
        /// </summary>
        private readonly Dictionary<string, List<EventHandler>> _handlers;

        /// <summary>
        /// Create new instance of events channel
        /// </summary>
        /// <param name="name"></param>
        /// <param name="catchExceptions"></param>
        public OldEventsChannel(string name, bool catchExceptions = false)
        {
            _catchExceptions = catchExceptions;
            _handlers = new Dictionary<string, List<EventHandler>>();
            Name = name;
        }

        /// <summary>
        /// Name of the events channel instance
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Raises an event, so that listeners can react to it.
        /// Fire and forget. Returns true if something has subscribed to the event
        /// </summary>
        /// <param name="eventName"></param>
        public bool Fire(string eventName)
        {
            return Fire(eventName, null, null);
        }

        /// <summary>
        /// Raises an event, so that listeners can react to it.
        /// Fire and forget. Returns true if something has subscribed to the event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="arg1"></param>
        /// <returns>True if there was at least one listener</returns>
        public bool Fire(string eventName, object arg1 = null)
        {
            return Fire(eventName, arg1, null);
        }

        /// <summary>
        /// Raises an event, so that listeners can react to it.
        /// Fire and forget. Returns true if something has subscribed to the event
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <returns>True if there was at least one listener</returns>
        public bool Fire(string eventName, object arg1 = null, object arg2 = null)
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
                        eventHandler.Invoke(arg1, arg2);
                        continue;
                    }

                    try
                    {
                        eventHandler.Invoke(arg1, arg2);
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
        /// Fires an event which has to be finished
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns>Promise, which must be finished at some point</returns>
        public Promise FireWithPromise(string eventName)
        {
            return FireWithPromise(eventName, null);
        }

        /// <summary>
        /// Fires an event which has to be finished
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="arg2"></param>
        /// <returns>Promise, which must be finished at some point</returns>
        public Promise FireWithPromise(string eventName, object arg2 = null)
        {
            var lastingEvent = new Promise(_eventId++);

            Fire(eventName, lastingEvent, arg2);

            return lastingEvent;
        }


        /// <summary>
        /// Same as Subscribe, except doesn't unsubscribe automatically.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="handler"></param>
        public void SubscribeManual(string eventName, EventHandler handler)
        {
            _handlers.TryGetValue(eventName, out List<EventHandler> list);

            if (list == null)
            {
                list = new List<EventHandler>();
                _handlers.Add(eventName, list);
            }

            list.Add(handler);
        }

        /// <summary>
        /// Registers into a queue to wait for event to happen.
        /// Unsubscribes automatically, when scene unloads
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="handler"></param>
        public void Subscribe(string eventName, EventHandler handler)
        {
            SubscribeManual(eventName, handler);

            // Cleanup when scene unloads
            void action(Scene scene)
            {
                Unsubscribe(eventName, handler);
                SceneManager.sceneUnloaded -= action;
            }

            SceneManager.sceneUnloaded += action;
        }

        public void Destroy()
        {
            _handlers.Clear();
        }

        public void Unsubscribe(string eventName, EventHandler listener)
        {
            _handlers.TryGetValue(eventName, out List<EventHandler> list);

            if (list == null)
            {
                return;
            }

            list.Remove(listener);
        }

        public class Promise
        {
            private bool _isFinished;

            public Promise(int eventId)
            {
                EventId = eventId;
            }

            public object Result { get; private set; }
            public int EventId { get; private set; }
            private event Action<Promise> OnDone;

            /// <summary>
            /// Subscribe to event
            /// If this event is finished, callback will be called instantly
            /// </summary>
            /// <param name="action">Called when event has finished, or instantly, if it's finished at the time of subscription</param>
            public void Subscribe(Action<Promise> action)
            {
                if (_isFinished)
                {
                    action.Invoke(this);
                    return;
                }

                OnDone += action;
            }

            /// <summary>
            /// Notifies all listeners that this event has finished
            /// </summary>
            /// <param name="result"></param>
            public void Finish(object result = null)
            {
                if (_isFinished)
                {
                    return;
                }

                _isFinished = true;

                Result = result;

                if (OnDone != null)
                {
                    OnDone.Invoke(this);
                }

                // Remove listeners
                OnDone = null;
            }
        }
    }
}