using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Dispatches actions to Unity main thread.
    /// 
    /// Why context capture is not done in constructor:
    /// Mst services can be created during static initialization, when Unity
    /// has not attached SynchronizationContext yet (or current thread is not
    /// the real Unity main thread). In that phase SynchronizationContext.Current
    /// can be null or wrong.
    /// 
    /// Because of that we capture lazily in Initialize()/RunInMainThread() and
    /// queue actions until valid context is available.
    /// </summary>
    public class MstThread
    {
        private readonly ConcurrentQueue<Action> pendingActions = new ConcurrentQueue<Action>();
        private SynchronizationContext mainThreadContext;
        private int mainThreadId = -1;

        public bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

        internal void Initialize()
        {
            TryCaptureContext();
            FlushPendingActions();
        }

        /// <summary>
        /// Runs action on Unity main thread.
        /// If main-thread context is not available yet, action is queued and
        /// will be executed once context is captured later.
        /// </summary>
        public void RunInMainThread(Action action)
        {
            if (action == null)
                return;

            if (!TryCaptureContext())
            {
                pendingActions.Enqueue(action);
                return;
            }

            if (IsMainThread)
            {
                SafeInvoke(action);
                return;
            }

            mainThreadContext.Post(_ => SafeInvoke(action), null);
        }

        /// <summary>
        /// Runs action in a dedicated background thread.
        /// Use this for non-Unity logic that should not block main thread.
        /// </summary>
        public Thread RunInSeparateThread(Action action)
        {
            if (action == null)
                return null;

            var thread = new Thread(() => SafeInvoke(action))
            {
                IsBackground = true,
                Name = "MstThreadWorker"
            };

            thread.Start();
            return thread;
        }

        private bool TryCaptureContext()
        {
            if (mainThreadContext != null)
                return true;

            var context = SynchronizationContext.Current;
            if (context == null)
                return false;

            mainThreadContext = context;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            return true;
        }

        private void FlushPendingActions()
        {
            while (pendingActions.TryDequeue(out var action))
            {
                RunInMainThread(action);
            }
        }

        private static void SafeInvoke(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
