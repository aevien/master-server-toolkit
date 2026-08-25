using MasterServerToolkit.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Delegate type for event listeners.
    /// </summary>
    public delegate void EventHandler(EventPayload message);

    /// <summary>
    /// Disposable subscription token; detaches bound handler on Dispose.
    /// </summary>
    internal sealed class SubscriptionToken : IDisposable
    {
        /// <summary>
        /// Reference to the channel to unsubscribe from.
        /// </summary>
        private readonly MstEventsChannel _channel;

        /// <summary>
        /// Name of the event this token is bound to.
        /// </summary>
        private readonly string _eventName;

        /// <summary>
        /// Bound delegate instance.
        /// </summary>
        private readonly EventHandler _handler;

        /// <summary>
        /// Disposal guard.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Constructs a new subscription token.
        /// </summary>
        /// <param name="channel">Target channel.</param>
        /// <param name="eventName">Event name.</param>
        /// <param name="handler">Bound handler.</param>
        public SubscriptionToken(MstEventsChannel channel, string eventName, EventHandler handler)
        {
            _channel = channel;
            _eventName = eventName;
            _handler = handler;
        }

        /// <summary>
        /// Unsubscribes the handler from the channel event.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _channel.RemoveListener(_eventName, _handler);
        }
    }

    /// <summary>
    /// Events channel with main-thread enforcement, fast dispatch, once-listeners,
    /// IDisposable tokens, and safe invocation for Unity 2022.3+.
    /// </summary>
    public class MstEventsChannel
    {
        /// <summary>
        /// Listener entry with optional one-shot semantics.
        /// </summary>
        private sealed class Listener
        {
            /// <summary>
            /// Delegate to invoke.
            /// </summary>
            public EventHandler Callback { get; private set; }

            /// <summary>
            /// If true, handler will be removed after first successful invocation.
            /// </summary>
            public bool Once { get; private set; }

            /// <summary>
            /// Creates a new listener entry.
            /// </summary>
            /// <param name="callback">Delegate to invoke.</param>
            /// <param name="once">One-shot listener flag.</param>
            public Listener(EventHandler callback, bool once)
            {
                Callback = callback;
                Once = once;
            }
        }

        /// <summary>
        /// Whether to catch and log exceptions thrown by listeners.
        /// </summary>
        private readonly bool _catchExceptions;

        /// <summary>
        /// Whether to enforce main-thread-only access for all public APIs.
        /// </summary>
        private readonly bool _enforceMainThread;

        /// <summary>
        /// Managed thread ID captured at construction time (Unity main thread).
        /// </summary>
        private readonly int _mainThreadId;

        /// <summary>
        /// Unity SynchronizationContext captured at construction time (may be null if not created on main thread).
        /// </summary>
        private readonly SynchronizationContext _mainThreadContext;

        /// <summary>
        /// Map: event name -> list of listeners.
        /// </summary>
        private readonly Dictionary<string, List<Listener>> _handlers;

        /// <summary>
        /// Name of this events channel instance.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Indicates if posting to the Unity main thread is available.
        /// </summary>
        public bool CanPostToMainThread => _mainThreadContext != null;

        /// <summary>
        /// Creates a new events channel named "default", with exception catching and main-thread enforcement enabled.
        /// </summary>
        public MstEventsChannel() : this("default", catchExceptions: true, enforceMainThread: true) { }

        /// <summary>
        /// Creates a new events channel with a custom name, exception catching and main-thread enforcement enabled.
        /// </summary>
        /// <param name="name">Channel name.</param>
        public MstEventsChannel(string name) : this(name, catchExceptions: true, enforceMainThread: true) { }

        /// <summary>
        /// Creates a new events channel with custom name and behaviors.
        /// </summary>
        /// <param name="name">Channel name.</param>
        /// <param name="catchExceptions">Catch and log listener exceptions.</param>
        /// <param name="enforceMainThread">Throw if called not from Unity main thread.</param>
        public MstEventsChannel(string name, bool catchExceptions, bool enforceMainThread)
        {
            _catchExceptions = catchExceptions;
            _enforceMainThread = enforceMainThread;

            // Capture Unity main thread context and ID at construction time.
            _mainThreadContext = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            _handlers = new Dictionary<string, List<Listener>>(StringComparer.Ordinal);
            Name = string.IsNullOrWhiteSpace(name) ? "default" : name;
        }

        /// <summary>
        /// Invokes an event without payload and returns the number of listeners invoked.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        public int Invoke(string eventName)
        {
            EnsureMainThread(nameof(Invoke));
            return InvokeInternal(eventName, new EventPayload());
        }

        /// <summary>
        /// Invokes an event with a raw payload object and returns the number of listeners invoked.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="payload">Payload object.</param>
        public int Invoke(string eventName, object payload)
        {
            EnsureMainThread(nameof(Invoke));
            return InvokeInternal(eventName, new EventPayload(payload));
        }

        /// <summary>
        /// Invokes an event with a prepared message and returns the number of listeners invoked.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="message">Event message.</param>
        public int Invoke(string eventName, EventPayload message)
        {
            EnsureMainThread(nameof(Invoke));
            return InvokeInternal(eventName, message);
        }

        /// <summary>
        /// Posts an invocation to the Unity main thread via SynchronizationContext.
        /// Use when triggering from background threads.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="message">Event message.</param>
        public void PostInvoke(string eventName, EventPayload message)
        {
            if (_mainThreadContext == null)
                throw new InvalidOperationException($"[MstEventsChannel:{Name}] Unity SynchronizationContext is not available. Create the channel on the main thread.");

            _mainThreadContext.Post(_ => Invoke(eventName, message), null);
        }

        /// <summary>
        /// Subscribes a persistent handler; returns an IDisposable token to unsubscribe.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="handler">Handler delegate.</param>
        public IDisposable AddListener(string eventName, EventHandler handler)
        {
            EnsureMainThread(nameof(AddListener));
            return AddListenerInternal(eventName, handler, once: false);
        }

        /// <summary>
        /// Subscribes a one-shot handler that auto-unsubscribes after the first invocation.
        /// Returns an IDisposable token which you can Dispose to cancel before it fires.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="handler">Handler delegate.</param>
        public IDisposable AddListenerOnce(string eventName, EventHandler handler)
        {
            EnsureMainThread(nameof(AddListenerOnce));
            return AddListenerInternal(eventName, handler, once: true);
        }

        /// <summary>
        /// Removes a specific handler from an event.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="handler">Handler delegate to remove.</param>
        public void RemoveListener(string eventName, EventHandler handler)
        {
            EnsureMainThread(nameof(RemoveListener));
            if (string.IsNullOrEmpty(eventName) || handler == null) return;

            if (_handlers.TryGetValue(eventName, out var list))
            {
                list.RemoveAll(l => l == null || l.Callback == handler);
                if (list.Count == 0)
                    _handlers.Remove(eventName);
            }
        }

        /// <summary>
        /// Removes all handlers for a specific event.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        public void RemoveAllListeners(string eventName)
        {
            EnsureMainThread(nameof(RemoveAllListeners));
            if (string.IsNullOrEmpty(eventName)) return;

            _handlers.Remove(eventName);
        }

        /// <summary>
        /// Removes all handlers for all events.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        public void RemoveAllListeners()
        {
            EnsureMainThread(nameof(RemoveAllListeners));
            _handlers.Clear();
        }

        /// <summary>
        /// Checks if an event currently has at least one listener.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        public bool HasListeners(string eventName)
        {
            EnsureMainThread(nameof(HasListeners));
            return _handlers.TryGetValue(eventName, out var list) && list.Count > 0;
        }

        /// <summary>
        /// Returns the number of registered listeners for an event.
        /// Must be called from the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        public int GetListenerCount(string eventName)
        {
            EnsureMainThread(nameof(GetListenerCount));
            return _handlers.TryGetValue(eventName, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// Internal helper to register a listener with optional once-flag.
        /// Returns an IDisposable subscription token.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="handler">Handler delegate.</param>
        /// <param name="once">One-shot flag.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDisposable AddListenerInternal(string eventName, EventHandler handler, bool once)
        {
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentException("Event name cannot be null or empty.", nameof(eventName));

            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var entry = new Listener(handler, once);

            if (!_handlers.TryGetValue(eventName, out var list))
            {
                list = new List<Listener>(4);
                _handlers[eventName] = list;
            }

            list.Add(entry);

            return new SubscriptionToken(this, eventName, handler);
        }

        /// <summary>
        /// Core invoke logic working on a snapshot; assumes main-thread already validated.
        /// </summary>
        /// <param name="eventName">Event name.</param>
        /// <param name="message">Event message.</param>
        private int InvokeInternal(string eventName, EventPayload message)
        {
            if (string.IsNullOrEmpty(eventName))
                return 0;

            if (!_handlers.TryGetValue(eventName, out var list) || list.Count == 0)
                return 0;

            // Snapshot to allow safe modifications (subscribe/unsubscribe) inside callbacks.
            var snapshot = new List<Listener>(list);

            int invoked = 0;
            bool anyOnce = false;

            for (int i = 0; i < snapshot.Count; i++)
            {
                var l = snapshot[i];
                if (l?.Callback == null) continue;

                if (!_catchExceptions)
                {
                    l.Callback(message);
                    invoked++;
                }
                else
                {
                    try
                    {
                        l.Callback(message);
                        invoked++;
                    }
                    catch (Exception e)
                    {
                        Logs.Error(e);
                    }
                }

                if (l.Once) anyOnce = true;
            }

            if (anyOnce && _handlers.TryGetValue(eventName, out var current))
            {
                current.RemoveAll(static l => l == null || l.Once);
                if (current.Count == 0)
                    _handlers.Remove(eventName);
            }

            return invoked;
        }

        /// <summary>
        /// Ensures that the current call is executed on the Unity main thread if enforcement is enabled.
        /// </summary>
        /// <param name="apiName">API name for diagnostics.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureMainThread(string apiName)
        {
            if (!_enforceMainThread) return;

            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException(
                    $"[MstEventsChannel:{Name}] '{apiName}' must be called from the Unity main thread. " +
                    "If you are calling from a background thread, use 'PostInvoke(...)'.");
        }
    }
}
