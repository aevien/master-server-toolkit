using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public class TweenerActionInfo
    {
        public int Id { get; set; }
        public Tweener Tweener { get; set; }
        public bool IsRunning
        {
            get
            {
                return Tweener.IsRunning(Id);
            }
        }

        public void Cancel()
        {
            Tweener.Cancel(this);
        }

        public TweenerActionInfo OnComplete(TweenerActionInfoCallback callback)
        {
            Tweener.AddOnCompleteListener(Id, callback);
            return this;
        }
    }

    public delegate void TweenerActionInfoCallback(int id);

    public partial class Tweener : IUpdatable
    {
        private static int _id = 0;
        private static Tweener _tweener;
        private static readonly ConcurrentDictionary<int, Func<bool>> actions = new ConcurrentDictionary<int, Func<bool>>();
        private static readonly ConcurrentDictionary<int, TweenerActionInfoCallback> onCompleted = new ConcurrentDictionary<int, TweenerActionInfoCallback>();

        public static int NextId => _id++;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RunOnStart()
        {
            _id = 0;
            actions.Clear();
            onCompleted.Clear();
        }
#endif

        public void DoUpdate()
        {
            foreach (var actionKvp in actions)
                ProcessAction(actionKvp);
        }

        static Tweener()
        {
            _tweener = new Tweener();
        }

        private void Init()
        {
            MstUpdateRunner.Add(this);
        }

        private void ProcessAction(KeyValuePair<int, Func<bool>> actionKvp)
        {
            if (actionKvp.Value?.Invoke() == true)
            {
                if (actions.TryRemove(actionKvp.Key, out _))
                {
                    Debug.Log($"Tween action {actionKvp.Key} has completed");
                }

                if (onCompleted.TryRemove(actionKvp.Key, out var onComplete))
                    onComplete?.Invoke(actionKvp.Key);


            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actionInfo"></param>
        /// <returns></returns>
        public static bool IsRunning(TweenerActionInfo actionInfo)
        {
            if (actionInfo == null) return false;
            return IsRunning(actionInfo.Id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool IsRunning(int id)
        {
            return actions.ContainsKey(id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actionInfo"></param>
        /// <param name="callback"></param>
        public static void AddOnCompleteListener(TweenerActionInfo actionInfo, TweenerActionInfoCallback callback)
        {
            if (actionInfo == null) return;
            onCompleted.TryAdd(actionInfo.Id, callback);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="callback"></param>
        public static void AddOnCompleteListener(int id, TweenerActionInfoCallback callback)
        {
            onCompleted.TryAdd(id, callback);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actionInfo"></param>
        /// <returns></returns>
        public static Tweener Cancel(TweenerActionInfo actionInfo)
        {
            if (actionInfo == null) return _tweener;
            return Cancel(actionInfo.Id);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Tweener Cancel(int id)
        {
            if (actions.TryRemove(id, out _))
            {
                Debug.Log($"Tween action {id} has canceled");
            }

            return _tweener;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static TweenerActionInfo Start(Func<bool> action)
        {
            _tweener.Init();

            var info = new TweenerActionInfo()
            {
                Id = NextId,
                Tweener = _tweener
            };

            if (actions.TryAdd(info.Id, action))
            {
                Debug.Log($"Tween action {info.Id} has started");
            }


            return info;
        }
    }
}