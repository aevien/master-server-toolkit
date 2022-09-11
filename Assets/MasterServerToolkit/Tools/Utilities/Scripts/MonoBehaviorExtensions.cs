using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public static class MonoBehaviorExtensions
    {
        public static SafeCoroutine<string> StartSafeCoroutine(this MonoBehaviour obj, IEnumerator coroutine)
        {
            var coroutineObject = new SafeCoroutine<string>();
            coroutineObject.WaitCoroutine = obj.StartCoroutine(coroutineObject.InternalRoutine(coroutine));
            return coroutineObject;
        }

        public static SafeCoroutine<T> StartSafeCoroutine<T>(this MonoBehaviour obj, IEnumerator coroutine)
        {
            var coroutineObject = new SafeCoroutine<T>();
            coroutineObject.WaitCoroutine = obj.StartCoroutine(coroutineObject.InternalRoutine(coroutine));
            return coroutineObject;
        }

        public static IEnumerator WaitOrException(this MonoBehaviour obj, IEnumerator coroutine)
        {
            var safeCoroutine = obj.StartSafeCoroutine(coroutine);
            yield return safeCoroutine.WaitCoroutine;

            if (safeCoroutine.Exception != null)
                throw safeCoroutine.Exception;
        }
    }
}