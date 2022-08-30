using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public delegate void TimerActionCompleteHandler(bool isSuccessful);
    public delegate void TickActionHandler(long currentTick);

    /// <summary>
    /// Pink wait handler delegate
    /// </summary>
    public delegate void WaitPingCallback(int time);

    public class MstTimer : DynamicSingletonBehaviour<MstTimer>
    {
        /// <summary>
        /// List of main thread actions
        /// </summary>
        private Queue<Action> _mainThreadActions;

        /// <summary>
        /// Current tick of scaled time
        /// </summary>
        public long CurrentTick { get; protected set; }

        /// <summary>
        /// Event, which is invoked every second
        /// </summary>
        public event TickActionHandler OnTickEvent;

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
            _mainThreadActions = new Queue<Action>();
        }

        protected virtual void Start()
        {
            // Start timer
            StartCoroutine(StartTicker());
        }

        private void Update()
        {
            lock (_mainThreadActions)
            {
                while (_mainThreadActions.Count > 0)
                {
                    _mainThreadActions.Dequeue()?.Invoke();
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
        public void WaitPing(string address, WaitPingCallback callback, float timeout = 5f)
        {
            if (_instance == null) return;

#if !UNITY_WEBGL
            StartCoroutine(WaitPingCoroutine(address, callback, timeout));
#else
            logger.Warn("You cannot use Ping in WebGL. Ping time will always be zero");
            callback?.Invoke(0);
#endif
        }

#if !UNITY_WEBGL
        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator WaitPingCoroutine(string address, WaitPingCallback callback, float timeout)
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
#endif

        /// <summary>
        /// Waits while condition is false
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public void WaitUntil(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            StartCoroutine(WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds, true));
        }

        /// <summary>
        /// Waits while condition is true
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public void WaitWhile(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            StartCoroutine(WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds));
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
        public void WaitForSeconds(float time, Action callback)
        {
            StartCoroutine(StartWaitingForSeconds(time, callback));
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
        public void WaitForRealtimeSeconds(float time, Action callback)
        {
            StartCoroutine(StartWaitingForRealtimeSeconds(time, callback));
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
        public void WaitForEndOfFrame(Action callback)
        {
            StartCoroutine(StartWaitingForEndOfFrame(callback));
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
        public void RunInMainThread(Action action)
        {
            AddToMainThread(action);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        private void AddToMainThread(Action action)
        {
            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(action);
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