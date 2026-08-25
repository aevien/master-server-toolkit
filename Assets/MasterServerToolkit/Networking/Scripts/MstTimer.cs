using MasterServerToolkit.Utils;
using MasterServerToolkit.Logging;
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public delegate void TimerActionCompleteHandler(bool isSuccessful);
    public delegate void TickActionHandler(long currentTick);

    /// <summary>
    /// Pink wait handler delegate
    /// </summary>
    public delegate void WaitPingCallback(int time);

    public class MstTimer : SingletonBehaviour<MstTimer>
    {
        private static readonly object tickHandlersSync = new();
        private static TickActionHandler onTickEvent;
        private static TickActionHandler[] tickHandlerSnapshot = Array.Empty<TickActionHandler>();

        private readonly WaitForSecondsRealtime waitForTick = new(1f);
        private readonly WaitForEndOfFrame waitForEndOfFrame = new();

        /// <summary>
        /// Current count of one-second realtime ticks. The counter is not affected by Time.timeScale.
        /// </summary>
        public static long CurrentTick { get; protected set; }

        /// <summary>
        /// Event, which is invoked every second. An exception from one subscriber does not stop
        /// the remaining subscribers or future timer ticks.
        /// </summary>
        public static event TickActionHandler OnTickEvent
        {
            add
            {
                if (value == null)
                    return;

                lock (tickHandlersSync)
                {
                    onTickEvent += value;
                    UpdateTickHandlerSnapshot();
                }
            }
            remove
            {
                if (value == null)
                    return;

                lock (tickHandlersSync)
                {
                    onTickEvent -= value;
                    UpdateTickHandlerSnapshot();
                }
            }
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorPlayModeState()
        {
            CurrentTick = 0;
            ClearTickHandlers();
        }
#endif

        protected override void Awake()
        {
            base.Awake();

            // Framework requires applications to run in background
            Application.runInBackground = true;
        }

        protected virtual void Start()
        {
            // Start timer
            StartCoroutine(StartTickTimer());
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            CurrentTick = 0;
            ClearTickHandlers();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="callback"></param>
        public static Coroutine WaitPing(string address, WaitPingCallback callback, float timeout = 5f)
        {

#if !UNITY_WEBGL
            if (TryGetOrCreate(out var instance))
                return instance.StartCoroutine(instance.WaitPingCoroutine(address, callback, timeout));
#else
            Logs.Warn("You cannot use Ping in WebGL. Ping time will always be zero");
            callback?.Invoke(0);
#endif
            return null;
        }

#if !UNITY_WEBGL
        private IEnumerator WaitPingCoroutine(string address, WaitPingCallback callback, float timeout)
        {
            float startTime = Time.realtimeSinceStartup;
            var ping = new Ping(address);

            while (!ping.isDone)
            {
                if (Time.realtimeSinceStartup - startTime > timeout)
                {
                    break;
                }

                yield return null;
            }

            if (ping.isDone)
                callback?.Invoke(ping.time);
            else
                callback?.Invoke((int)((Time.realtimeSinceStartup - startTime) * 1000));
        }
#endif

        /// <summary>
        /// Waits while condition is false
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static Coroutine WaitUntil(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            if (TryGetOrCreate(out var instance))
                return instance.StartCoroutine(instance.WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds, true));

            return null;
        }

        /// <summary>
        /// Waits while condition is true
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static Coroutine WaitWhile(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            if (TryGetOrCreate(out var instance))
                return instance.StartCoroutine(instance.WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds));

            return null;
        }

        /// <summary>
        /// Stops a coroutine on the active MST timer if it still exists.
        /// This does not create a new timer during shutdown.
        /// </summary>
        /// <param name="coroutine">Coroutine handle to stop.</param>
        /// <returns>True if a timer existed and StopCoroutine was called.</returns>
        public static bool TryStopCoroutine(Coroutine coroutine)
        {
            if (coroutine == null)
                return false;

            var instance = _instance;

            if (instance == null)
                return false;

            instance.StopCoroutine(coroutine);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="reverseCondition"></param>
        /// <returns></returns>
        private IEnumerator WaitWhileTrueCoroutine(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds, bool reverseCondition = false)
        {
            while ((timeoutSeconds > 0) && (condition != null && condition.Invoke() == !reverseCondition))
            {
                timeoutSeconds -= Time.unscaledDeltaTime;
                yield return null;
            }

            completeCallback?.Invoke(timeoutSeconds > 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        public static Coroutine WaitForSeconds(float time, Action callback)
        {
            if (TryGetOrCreate(out var instance))
                return instance.StartCoroutine(instance.StartWaitingForSeconds(time, callback));

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator StartWaitingForSeconds(float time, Action callback)
        {
            yield return new WaitForSeconds(time);
            callback?.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        public static Coroutine WaitForRealtimeSeconds(float time, Action callback)
        {
            if (TryGetOrCreate(out var instance))
                return instance.StartCoroutine(instance.StartWaitingForRealtimeSeconds(time, callback));

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator StartWaitingForRealtimeSeconds(float time, Action callback)
        {
            yield return new WaitForSecondsRealtime(time);
            callback?.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        public static Coroutine WaitForEndOfFrame(Action callback)
        {
            if (TryGetOrCreate(out var instance))
                return instance.StartCoroutine(instance.StartWaitingForEndOfFrame(callback));

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator StartWaitingForEndOfFrame(Action callback)
        {
            yield return waitForEndOfFrame;
            callback?.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartTickTimer()
        {
            while (true)
            {
                yield return waitForTick;
                CurrentTick++;
                InvokeTickHandlers(CurrentTick);
            }
        }

        private static void InvokeTickHandlers(long currentTick)
        {
            TickActionHandler[] handlers = Volatile.Read(ref tickHandlerSnapshot);

            foreach (TickActionHandler handler in handlers)
            {
                try
                {
                    handler.Invoke(currentTick);
                }
                catch (Exception exception)
                {
                    LogTickHandlerException(handler, currentTick, exception);
                }
            }
        }

        private static void LogTickHandlerException(TickActionHandler handler, long currentTick, Exception exception)
        {
            try
            {
                string declaringType = handler.Method.DeclaringType?.FullName ?? "UnknownType";
                Logs.Error($"MstTimer tick subscriber '{declaringType}.{handler.Method.Name}' failed at tick {currentTick}: {exception}");
            }
            catch
            {
                // Timer processing must survive even when logging is unavailable during shutdown.
            }
        }

        private static void ClearTickHandlers()
        {
            lock (tickHandlersSync)
            {
                onTickEvent = null;
                UpdateTickHandlerSnapshot();
            }
        }

        private static void UpdateTickHandlerSnapshot()
        {
            if (onTickEvent == null)
            {
                Volatile.Write(ref tickHandlerSnapshot, Array.Empty<TickActionHandler>());
                return;
            }

            Delegate[] invocationList = onTickEvent.GetInvocationList();
            var handlers = new TickActionHandler[invocationList.Length];

            for (int i = 0; i < invocationList.Length; i++)
                handlers[i] = (TickActionHandler)invocationList[i];

            Volatile.Write(ref tickHandlerSnapshot, handlers);
        }
    }
}
