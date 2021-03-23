using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    public class MstTimer : DynamicSingleton<MstTimer>
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
        /// Done handler delegate
        /// </summary>
        /// <param name="isSuccessful"></param>
        public delegate void TimerActionCompleteHandler(bool isSuccessful);

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

        protected void Awake()
        {
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
        /// Waits while condition is false
        /// If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="completeCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitUntil(Func<bool> condition, TimerActionCompleteHandler completeCallback, float timeoutSeconds)
        {
            if (Instance)
                Instance.StartCoroutine(WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds, true));
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
            if (Instance)
                Instance.StartCoroutine(WaitWhileTrueCoroutine(condition, completeCallback, timeoutSeconds));
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
            if (Instance)
                Instance.StartCoroutine(Instance.StartWaitingForSeconds(time, callback));
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
        /// <param name="callback"></param>
        public static void WaitForEndOfFrame(Action callback)
        {
            if (Instance)
                Instance.StartCoroutine(Instance.StartWaitingForEndOfFrame(callback));
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
            if (Instance)
                Instance.AddToMainThread(action);
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

        /// <summary>
        /// 
        /// </summary>
        private void OnApplicationQuit()
        {
            OnApplicationQuitEvent?.Invoke();
        }
    }
}