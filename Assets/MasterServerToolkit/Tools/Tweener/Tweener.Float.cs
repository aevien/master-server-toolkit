using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public delegate void FloatTweenCallback(float newValue);

    public partial class Tweener
    {
        public static TweenerActionInfo Tween(float from, float to, float time, FloatTweenCallback callback)
        {
            return Start(TweenAction(from, to, time, callback));
        }

        private static IEnumerator TweenAction(float from, float to, float time, FloatTweenCallback callback)
        {
            if (from == to)
            {
                callback?.Invoke(to);
                yield break;
            }

            bool negative = to < from;
            float curTime = 0f;
            float dif = Mathf.Abs(from - to);

            while (curTime < time)
            {
                yield return new WaitForEndOfFrame();

                curTime += Time.deltaTime;

                if (negative)
                    callback?.Invoke(from - (dif * (curTime / time)));
                else
                    callback?.Invoke(from + (dif * (curTime / time)));
            }

            callback?.Invoke(to);
        }
    }
}