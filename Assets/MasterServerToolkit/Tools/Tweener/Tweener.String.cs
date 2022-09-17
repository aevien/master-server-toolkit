using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public delegate void StringTweenCallback(string newValue);

    public partial class Tweener
    {
        public static TweenerActionInfo Tween(string to, float time, StringTweenCallback callback)
        {
            float currentTime = 0f;
            int lastLength = 0;
            int newLength = 0;

            return Start(() =>
            {
                if (string.IsNullOrEmpty(to))
                {
                    callback?.Invoke(to);
                    return true;
                }

                if (currentTime / time < 1f)
                {
                    currentTime += Time.deltaTime;
                    newLength = (int)(to.Length * (currentTime / time));

                    if (lastLength != newLength)
                    {
                        callback?.Invoke(to.Substring(0, newLength - 1));
                        lastLength = newLength;
                    }

                    return false;
                }
                else
                {
                    callback.Invoke(to);
                    return true;
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="time"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        //public static TweenerActionInfo DecodeTween(this string value, float time, UnityAction<string> callback)
        //{
        //    return Tweener.Start(DecodeTweenAction(value, time, callback));
        //}

        //private static IEnumerator DecodeTweenAction(string value, float time, UnityAction<string> callback)
        //{
        //    float curTime = 0f;
        //    float[] times = new float[value.Length];
        //    float changeInterval = 1f / 20f;

        //    for (int i = 0; i < value.Length; i++)
        //    {
        //        times[i] = Random.Range(0f, time);
        //    }

        //    while (curTime < time)
        //    {
        //        yield return new WaitForSeconds(changeInterval);
        //        curTime += Time.deltaTime + changeInterval;

        //        string newString = "";

        //        for (int i = 0; i < value.Length; i++)
        //        {
        //            if (times[i] > curTime)
        //            {
        //                newString = newString + $"<color=red>{Mst.Helper.CreateRandomAlphanumericString(1)}</color>";
        //            }
        //            else
        //            {
        //                newString = newString + value[i];
        //            }
        //        }

        //        callback?.Invoke(newString);
        //    }

        //    callback?.Invoke(value);
        //}
    }
}