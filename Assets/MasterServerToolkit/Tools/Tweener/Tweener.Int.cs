using System.Collections;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public delegate void IntTweenCallback(int newValue);

    public partial class Tweener
    {
        public static TweenerActionInfo Tween(int from, int to, float time, IntTweenCallback callback)
        {
            return Start(TweenAction(from, to, time, callback));
        }

        private static IEnumerator TweenAction(int from, int to, float time, IntTweenCallback callback)
        {
            if (from == to)
            {
                callback?.Invoke(to);
                yield break;
            }

            bool negative = to < from;
            float curTime = 0f;
            int dif = Mathf.Abs(from - to);

            while (curTime < time)
            {
                yield return new WaitForEndOfFrame();

                curTime += Time.deltaTime;

                if (negative)
                    callback?.Invoke(from - (int)(dif * (curTime / time)));
                else
                    callback?.Invoke(from + (int)(dif * (curTime / time)));
            }

            callback?.Invoke(to);
        }
    }
}
