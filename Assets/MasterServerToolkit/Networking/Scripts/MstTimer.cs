using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Done handler delegate
    /// </summary>
    /// <param name="isSuccessful"></param>
    public delegate void TimerActionCompleteHandler(bool isSuccessful);

    /// <summary>
    /// Pink wait handler delegate
    /// </summary>
    public delegate void WaitPingCallback(int time);

    public class MstTimer : DynamicSingletonBehaviour<MstTimer>
    {
        /// <summary>
        /// List of main thread actions
        /// </summary>
        private List<Action> _mainThreadActions;

        /// <summary>
        /// Main thread lockobject
        /// </summary>
        private readonly object _mainThreadLock = new object();

        /// <summary>
        /// Current tick of scaled time
        /// </summary>
        public long CurrentTick { get; protected set; }

        /// <summary>
        /// Event, which is invoked every second
        /// </summary>
        public event Action<long> OnTickEvent;

        /// <summary>
        /// Invokes when application shuts down
        /// </summary>
        public event Action OnApplicationQuitEvent;

        protected override void Awake()
        {
            base.Awake();

            // Framework requires applications to run in background
            Application.runInBackground = true;

            // Create list of main thread actions
            _mainThreadActions = new List<Action>();

            // Start timer
            StartCoroutine(StartTicker());
        }

        private void Update()
        {
            if (_mainThreadActions.Count > 0)
            {
                lock (_mainThreadLock)
                {
                    foreach (var actions in _mainThreadActions)
                    {
                        actions.Invoke();
                    }

                    _mainThreadActions.Clear();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnApplicationQuit()
        {
            OnApplicationQuitEvent?.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="callback"></param>
        public static void WaitPing(string address, WaitPingCallback callback, float timeout = 5f)
        {
            if (Singleton)
                Singleton.StartCoroutine(WaitPingCoroutine(address, callback, timeout));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private static IEnumerator WaitPingCoroutine(string address, WaitPingCallback callback, float timeout)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime.AddSeconds(timeout);
            var ping = new Ping(address);

            while (!ping.isDone)
            {
                if (endTime <= DateTime.Now)
                {
                    break;
                }

                yield return null;
            }

            bool success = ping.isDone;

            if (success)
                callback?.Invoke(ping.time);
            else
                callback?.Invoke((int)(DateTime.Now - startTime).TotalMilliseconds);
        }

        /// <summary>
        /// Waits while condition is false
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitUntil(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            if (Singleton)
                Singleton.StartCoroutine(WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds, true));
        }

        /// <summary>
        /// Waits while condition is true
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitWhile(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            if (Singleton)
                Singleton.StartCoroutine(WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="reverseCondition"></param>
        /// <returns></returns>
        private static IEnumerator WaitWhileTrueCoroutine(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds, bool reverseCondition = false)
        {
            while ((timeoutSeconds > 0) && (condition.Invoke() == !reverseCondition))
            {
                timeoutSeconds -= Time.deltaTime;
                yield return null;
            }

            completeCallback.Invoke(timeoutSeconds > 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        public static void WaitForSeconds(float time, Action callback)
        {
            if (Singleton)
                Singleton.StartCoroutine(Singleton.StartWaitingForSeconds(time, callback));
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
            callback.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        public static void WaitForRealtimeSeconds(float time, Action callback)
        {
            if (Singleton)
                Singleton.StartCoroutine(Singleton.StartWaitingForRealtimeSeconds(time, callback));
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
            callback.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        public static void WaitForEndOfFrame(Action callback)
        {
            if (Singleton)
                Singleton.StartCoroutine(Singleton.StartWaitingForEndOfFrame(callback));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator StartWaitingForEndOfFrame(Action callback)
        {
            yield return new WaitForEndOfFrame();
            callback.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        public static void RunInMainThread(Action action)
        {
            if (Singleton)
                Singleton.AddToMainThread(action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        private void AddToMainThread(Action action)
        {
            lock (_mainThreadLock)
            {
                _mainThreadActions.Add(action);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartTicker()
        {
            CurrentTick = 0;

            while (true)
            {
                yield return new WaitForSecondsRealtime(1);

                CurrentTick++;

                try
                {
                    OnTickEvent?.Invoke(CurrentTick);
                }
                catch (Exception e)
                {
                    Logs.Error(e);
                }
            }
        }
    }
}