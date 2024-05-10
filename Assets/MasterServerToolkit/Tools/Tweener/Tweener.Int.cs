using UnityEngine;

namespace MasterServerToolkit.Utils
{
    public delegate void IntTweenCallback(int newValue);

    public partial class Tweener
    {
        public static TweenerActionInfo Int(int from, int to, float time, IntTweenCallback callback)
        {
            float currentTime = 0f;
            float difference = Mathf.Abs(from - to);
            bool negative = to < from;

            return Start(() =>
            {
                if (from == to)
                {
                    callback?.Invoke(to);
                    return true;
                }

                if (currentTime / time < 1f)
                {
                    currentTime += Time.deltaTime;

                    if (negative)
                        callback?.Invoke(from - (int)(difference * (currentTime / time)));
                    else
                        callback?.Invoke(from + (int)(difference * (currentTime / time)));

                    return false;
                }
                else
                {
                    callback.Invoke(to);
                    return true;
                }
            });
        }
    }
}
