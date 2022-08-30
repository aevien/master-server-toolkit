using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Utils
{
    public class TweenerBehaviour : MonoBehaviour { }

    public struct TweenerActionInfo
    {
        public int Id { get; set; }
        public Tweener Tweener { get; set; }
        public bool IsRunning => Tweener.IsRunning(Id);

        public TweenerActionInfo OnStart(TweenerActionInfoCallback callback)
        {
            Tweener.AddOnStartListener(Id, callback);
            return this;
        }

        public TweenerActionInfo OnComplete(TweenerActionInfoCallback callback)
        {
            Tweener.AddOnCompleteListener(Id, callback);
            return this;
        }
    }

    public delegate void TweenerActionInfoCallback(int id);

    public partial class Tweener
    {
        private static bool _wasCreated = false;
        private static TweenerBehaviour _tweenerInstance;
        private static Tweener _tweener;
        private static int _nextId = 0;
        private static readonly ConcurrentDictionary<int, Coroutine> coroutines = new ConcurrentDictionary<int, Coroutine>();
        private static readonly ConcurrentDictionary<int, TweenerActionInfoCallback> onStart = new ConcurrentDictionary<int, TweenerActionInfoCallback>();
        private static readonly ConcurrentDictionary<int, TweenerActionInfoCallback> onComplete = new ConcurrentDictionary<int, TweenerActionInfoCallback>();

        public static int NextId => _nextId++;
        public static int Count => coroutines.Values.Count;

        static Tweener()
        {
            _tweener = new Tweener();
        }

        private static bool TryGetOrCreate(out TweenerBehaviour tweenerInstance)
        {
            if (!_tweenerInstance && !_wasCreated)
            {
                var weenerInstanceObj = new GameObject("--MST_TWEENER");
                _tweenerInstance = weenerInstanceObj.AddComponent<TweenerBehaviour>();
                UnityEngine.Object.DontDestroyOnLoad(_tweenerInstance);
                _wasCreated = true;
            }

            tweenerInstance = _tweenerInstance;
            return _tweenerInstance != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Tweener Cancel(int id)
        {
            if (TryGetOrCreate(out TweenerBehaviour tweenerInstance)
                && coroutines.TryRemove(id, out var coroutine))
                tweenerInstance.StopCoroutine(coroutine);

            return _tweener;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static Tweener CancelAll()
        {
            if (TryGetOrCreate(out TweenerBehaviour tweenerInstance))
                tweenerInstance.StopAllCoroutines();

            coroutines.Clear();
            onStart.Clear();
            onComplete.Clear();

            return _tweener;
        }

        public static TweenerActionInfo Start(IEnumerator action)
        {
            return Start(NextId, action);
        }

        public static TweenerActionInfo Start(int id, IEnumerator action)
        {
            var info = new TweenerActionInfo()
            {
                Id = id,
                Tweener = _tweener
            };

            if (TryGetOrCreate(out TweenerBehaviour tweenerInstance))
                coroutines.TryAdd(id, tweenerInstance.StartCoroutine(CoroutineRunner(info.Id, tweenerInstance, action)));

            return info;
        }

        private static IEnumerator CoroutineRunner(int id, TweenerBehaviour tweenerInstance, IEnumerator action)
        {
            yield return new WaitForEndOfFrame();

            if (onStart.TryGetValue(id, out var onStartCallback) && onStartCallback != null)
                onStartCallback?.Invoke(id);

            yield return tweenerInstance.StartCoroutine(action);

            Cancel(id);

            if (onComplete.TryGetValue(id, out var onCompleteCallback) && onCompleteCallback != null)
                onCompleteCallback?.Invoke(id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static TweenerActionInfo DelayedCall(float time, UnityAction callback)
        {
            int id = NextId;
            return Start(id, _tweener.DelayCallCoroutine(id, time, callback));
        }

        private IEnumerator DelayCallCoroutine(int id, float time, UnityAction callback)
        {
            yield return new WaitForSeconds(time);
            coroutines.TryRemove(id, out _);
            callback?.Invoke();
        }

        public static void AddOnStartListener(int id, TweenerActionInfoCallback callback)
        {
            RemoveOnStartListener(id);
            onStart.TryAdd(id, callback);
        }

        public static void RemoveOnStartListener(int id)
        {
            onStart.TryRemove(id, out _);
        }

        public static void AddOnCompleteListener(int id, TweenerActionInfoCallback callback)
        {
            RemoveOnCompleteListener(id);
            onComplete.TryAdd(id, callback);
        }

        public static void RemoveOnCompleteListener(int id)
        {
            onComplete.TryRemove(id, out _);
        }

        public static bool IsRunning(int id)
        {
            return coroutines.ContainsKey(id);
        }
    }
}